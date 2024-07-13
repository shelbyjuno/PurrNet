using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    internal struct PendingOperation
    {
        public int buildIndex;
        public SceneID idToAssign;
        public PurrSceneSettings settings;
    }

    internal struct SceneState
    {
        public Scene scene;
        public PurrSceneSettings settings;

        public SceneState(Scene scene, PurrSceneSettings settings)
        {
            this.scene = scene;
            this.settings = settings;
        }
    }

    public partial struct PurrSceneSettings : IAutoNetworkedData
    {
        public LoadSceneMode mode;
        public LocalPhysicsMode physicsMode;
        public bool isPublic;
    }
    
    public delegate void OnSceneActionEvent(SceneID scene, bool asServer);
    public delegate void OnSceneVisibilityEvent(SceneID scene, bool isVisible, bool asServer);
    
    public class ScenesModule : INetworkModule, IFixedUpdate, ICleanup
    {
        private readonly NetworkManager _networkManager;
        private readonly PlayersManager _players;
        
        private readonly SceneHistory _history;
        private bool _asServer;
        
        private readonly List<PendingOperation> _pendingOperations = new ();
        private readonly Queue<SceneAction> _actionsQueue = new ();

        private readonly Dictionary<SceneID, SceneState> _scenes = new ();
        private readonly Dictionary<Scene, SceneID> _idToScene = new ();
        private readonly List<SceneID> _rawScenes = new ();
        
        internal event OnSceneActionEvent onPreSceneLoaded;
        public event OnSceneActionEvent onSceneLoaded;
        
        public event OnSceneActionEvent onSceneUnloaded;
        public event OnSceneVisibilityEvent onSceneVisibilityChanged;

        private ushort _nextSceneID;
        private ScenePlayersModule _scenePlayers;
        
        public IReadOnlyList<SceneID> scenes => _rawScenes;
        
        private SceneID GetNextID() => new(_nextSceneID++);

        public ScenesModule(NetworkManager manager, PlayersManager players)
        {
            _networkManager = manager;
            _players = players;
            _history = new SceneHistory();
        }
        
        internal void SetScenePlayers(ScenePlayersModule scenePlayersModule)
        {
            _scenePlayers = scenePlayersModule;
        }
        
        internal bool TryGetSceneState(SceneID sceneID, out SceneState state)
        {
            return _scenes.TryGetValue(sceneID, out state);
        }
        
        private void AddScene(Scene scene, PurrSceneSettings settings, SceneID id)
        {
            _scenes.Add(id, new SceneState(scene, settings));
            _idToScene.Add(scene, id);
            _rawScenes.Add(id);
            
            onPreSceneLoaded?.Invoke(id, _asServer);
            onSceneLoaded?.Invoke(id, _asServer);
        }
        
        public void UpdateSceneVisibility(SceneID scene, bool isPublic)
        {
            if (_asServer)
            {
                PurrLogger.LogError("Only clients can change scene visibility; for now at least ;)");
                return;
            }
            
            if (!_scenes.TryGetValue(scene, out var state))
            {
                PurrLogger.LogError($"Scene with ID {scene} not found");
                return;
            }

            state.settings.isPublic = isPublic;
            _scenes[scene] = state;
            
            onSceneVisibilityChanged?.Invoke(scene, isPublic, _asServer);
        }
        
        private readonly List<SceneID> _scenesToTriggerUnloadEvent = new();
        
        private void RemoveScene(Scene scene)
        {
            if (!_idToScene.TryGetValue(scene, out var id))
                return;
            
            _scenes.Remove(id);
            _idToScene.Remove(scene);
            _rawScenes.Remove(id);
            _scenesToTriggerUnloadEvent.Add(id);
        }

        public void Enable(bool asServer)
        {
            _asServer = asServer;

            var nmScene = _networkManager.gameObject.scene;
            
            AddScene(nmScene, new PurrSceneSettings
            {
                mode = LoadSceneMode.Single,
                isPublic = true,
                physicsMode = LocalPhysicsMode.None
            }, GetNextID());

            if (!asServer)
            {
                _players.Subscribe<SceneActionsBatch>(OnSceneActionsBatch);
            }
            else
            {
                _scenePlayers.onPlayerJoinedScene += OnPlayerJoinedScene;
            }
            
            SceneManager.sceneLoaded += SceneManagerOnsceneLoaded;
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (!asserver)
                return;
            
            var history = _history.GetFullHistory();
            
            _playerFilteredActions.Clear();
            
            // send all actions for the scene
            FilterActionsForPlayerBySceneID(player, scene, history.actions, _playerFilteredActions);

            if (_playerFilteredActions.Count > 0)
                _players.Send(player, new SceneActionsBatch { actions = _playerFilteredActions });
        }

        private void SceneManagerOnsceneLoaded(Scene scene, LoadSceneMode mode)
        {
            for (int i = 0; i < _pendingOperations.Count; i++)
            {
                var operation = _pendingOperations[i];

                if (operation.buildIndex == scene.buildIndex && operation.settings.mode == mode)
                {
                    AddScene(scene, operation.settings, operation.idToAssign);
                    _pendingOperations.RemoveAt(i);
                    break;
                }
            }
        }
        
        private bool IsScenePending(SceneID sceneId)
        {
            for (int i = 0; i < _pendingOperations.Count; i++)
            {
                if (_pendingOperations[i].idToAssign == sceneId)
                    return true;
            }
            
            return false;
        }

        private void HandleNextSceneAction()
        {
            if (_actionsQueue.Count == 0) return;
            
            var action = _actionsQueue.Peek();
            switch (action.type)
            {
                case SceneActionType.Load:
                {
                    if (_networkManager.isHost && !_asServer)
                    {
                        onPreSceneLoaded?.Invoke(action.loadSceneAction.sceneID, false);
                        onSceneLoaded?.Invoke(action.loadSceneAction.sceneID, false);
                        _actionsQueue.Dequeue();
                        break;
                    }
                    
                    var loadAction = action.loadSceneAction;

                    if (loadAction.buildIndex < 0 || loadAction.buildIndex >= SceneManager.sceneCountInBuildSettings)
                    {
                        PurrLogger.LogError($"Invalid build index {loadAction.buildIndex} to load");
                        break;
                    }
                    
                    SceneManager.LoadSceneAsync(loadAction.buildIndex, loadAction.GetLoadSceneParameters());

                    _pendingOperations.Add(new PendingOperation
                    {
                        buildIndex = loadAction.buildIndex,
                        settings = loadAction.parameters,
                        idToAssign = loadAction.sceneID
                    });
                    
                    _actionsQueue.Dequeue();
                    break;
                }
                case SceneActionType.Unload:
                {
                    var idx = action.unloadSceneAction.sceneID;
                    
                    if (_networkManager.isHost && !_asServer)
                    {
                        _scenesToTriggerUnloadEvent.Add(idx);
                        _actionsQueue.Dequeue();
                        break;
                    } 
                    
                    // if the scene is pending, don't do anything for now
                    if (IsScenePending(idx)) break;

                    if (!_scenes.TryGetValue(idx, out var sceneState))
                    {
                        PurrLogger.LogError($"Couldn't find scene with index {idx} to unload");
                        break;
                    }

                    SceneManager.UnloadSceneAsync(sceneState.scene, action.unloadSceneAction.options);
                    RemoveScene(sceneState.scene);
                    _actionsQueue.Dequeue();
                    break;
                }
            }
        }

        private void OnSceneActionsBatch(PlayerID player, SceneActionsBatch data, bool asserver)
        {
            for (var i = 0; i < data.actions.Count; i++)
                _actionsQueue.Enqueue(data.actions[i]);
            
            HandleNextSceneAction();
        }
        
        private static int SceneNameToBuildIndex(string name)
        {
            var bidxCount = SceneManager.sceneCountInBuildSettings;
            
            for (int i = 0; i < bidxCount; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                
                if (sceneName == name)
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        public AsyncOperation LoadSceneAsync(int sceneIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            var parameters = new LoadSceneParameters(mode);
            return LoadSceneAsync(sceneIndex, parameters);
        }

        public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            var idx = SceneNameToBuildIndex(sceneName);
            
            if (idx == -1)
            {
                PurrLogger.LogError($"Scene {sceneName} not found in build settings");
                return null;
            }
            
            var parameters = new LoadSceneParameters(mode);
            return LoadSceneAsync(idx, parameters);
        }

        public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters)
        {
            var idx = SceneNameToBuildIndex(sceneName);
            
            if (idx == -1)
            {
                PurrLogger.LogError($"Scene {sceneName} not found in build settings");
                return null;
            }
            
            return LoadSceneAsync(idx, parameters);
        }

        public AsyncOperation LoadSceneAsync(int sceneIndex, LoadSceneParameters parameters)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("Only server can load scenes; for now at least ;)");
                return null;
            }
            
            return LoadSceneAsync(sceneIndex, new PurrSceneSettings
            {
                mode = parameters.loadSceneMode,
                physicsMode = parameters.localPhysicsMode,
                isPublic = true
            });
        }
        
        public AsyncOperation LoadSceneAsync(int sceneIndex, PurrSceneSettings settings)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("Only server can load scenes; for now at least ;)");
                return null;
            }

            var idToAssign = GetNextID();
            var parameters = new LoadSceneParameters(settings.mode, settings.physicsMode);
            
            _history.AddLoadAction(new LoadSceneAction
            {
                buildIndex = sceneIndex, 
                sceneID = idToAssign, 
                parameters = settings
            });
            
            var op = SceneManager.LoadSceneAsync(sceneIndex, parameters);
            
            _pendingOperations.Add(new PendingOperation
            {
                buildIndex = sceneIndex,
                settings = settings,
                idToAssign = idToAssign
            });
            
            return op;
        }
        
        public void UnloadSceneAsync(string sceneName, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            
            if (!scene.IsValid())
            {
                PurrLogger.LogError($"Scene with name '{sceneName}' not found");
                return;
            }
            
            UnloadSceneAsync(scene, options);
        }

        public void UnloadSceneAsync(int buildIndex, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            var scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            
            if (!scene.IsValid())
            {
                PurrLogger.LogError($"Scene with build index {buildIndex} not found");
                return;
            }
            
            UnloadSceneAsync(scene, options);
        }
        
        public void UnloadSceneAsync(Scene scene, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("Only server can unload scenes; for now at least ;)");
                return;
            }
            
            if (_networkManager.gameObject.scene == scene)
            {
                PurrLogger.LogError("Can't unload the network manager scene");
                return;
            }
            
            if (!_idToScene.TryGetValue(scene, out var sceneIndex))
            {
                PurrLogger.LogError($"Scene {scene.name} not found in scenes list");
                return;
            }
            
            _history.AddUnloadAction(new UnloadSceneAction { sceneID = sceneIndex, options = options});
            SceneManager.UnloadSceneAsync(scene, options);
            RemoveScene(scene);
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
            {
                _players.Unsubscribe<SceneActionsBatch>(OnSceneActionsBatch);
            }
            else
            {
                _scenePlayers.onPlayerJoinedScene -= OnPlayerJoinedScene;
            }
            
            SceneManager.sceneLoaded -= SceneManagerOnsceneLoaded;

            DoCleanup();
        }
        
        static readonly List<SceneAction> _playerFilteredActions = new();

        private void FilterActionsForPlayer(PlayerID player, IReadOnlyList<SceneAction> actions, ICollection<SceneAction> destination)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                        
                var target = action.type switch
                {
                    SceneActionType.Load => action.loadSceneAction.sceneID,
                    SceneActionType.Unload => action.unloadSceneAction.sceneID,
                    _ => default
                };

                if (_scenePlayers.IsPlayerInScene(player, target))
                    destination.Add(action);
            }
        }
        
        private void FilterActionsForPlayerBySceneID(PlayerID player, SceneID id, IReadOnlyList<SceneAction> actions, ICollection<SceneAction> destination)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                        
                var target = action.type switch
                {
                    SceneActionType.Load => action.loadSceneAction.sceneID,
                    SceneActionType.Unload => action.unloadSceneAction.sceneID,
                    _ => default
                };
                
                if (target != id)
                    continue;

                if (_scenePlayers.IsPlayerInScene(player, target))
                    destination.Add(action);
            }
        }

        public void FixedUpdate()
        {
            HandleNextSceneAction();

            if (_history.hasUnflushedActions)
                FlushActions();

            if (_scenesToTriggerUnloadEvent.Count > 0)
            {
                for (var i = 0; i < _scenesToTriggerUnloadEvent.Count; i++)
                    onSceneUnloaded?.Invoke(_scenesToTriggerUnloadEvent[i], _asServer);
                _scenesToTriggerUnloadEvent.Clear();
            }
        }

        private void FlushActions()
        {
            var delta = _history.GetDelta();

            for (var i = 0; i < _players.players.Count; i++)
            {
                var player = _players.players[i];

                _playerFilteredActions.Clear();

                FilterActionsForPlayer(player, delta.actions, _playerFilteredActions);

                if (_playerFilteredActions.Count > 0)
                {
                    _players.Send(player, new SceneActionsBatch { actions = _playerFilteredActions });
                }
            }

            _history.Flush();
        }

        private void DoCleanup()
        {
            if (ApplicationContext.isQuitting) return;
            
            bool isAnythingConnected = _networkManager.isServer || _networkManager.isClient;
            
            if (isAnythingConnected) 
                return;

            foreach (var (id, scene) in _scenes)
            {
                if (id == default) 
                    continue;
                
                SceneManager.UnloadSceneAsync(scene.scene);
            }
        }

        private readonly List<AsyncOperation> _pendingUnloads = new();

        public bool Cleanup()
        {
            if (!_networkManager.isOffline)
                return true;

            if (_pendingOperations.Count > 0)
                return false;

            if (_scenes.Count > 0)
            {
                _pendingUnloads.Clear();

                foreach (var (id, scene) in _scenes)
                {
                    if (id == default)
                        continue;

                    _pendingUnloads.Add(SceneManager.UnloadSceneAsync(scene.scene));
                }

                _scenes.Clear();
            }
            
            if (_pendingUnloads.Count > 0)
            {
                for (int i = 0; i < _pendingUnloads.Count; i++)
                {
                    if (!_pendingUnloads[i].isDone)
                        return false;
                }
            }

            return true;
        }

        public bool TryGetSceneID(Scene scene, out SceneID o)
        {
            if (!_idToScene.TryGetValue(scene, out var id))
            {
                o = default;
                return false;
            }

            o = id;
            return true;
        }
    }
}
