using System.Collections.Generic;
using PurrNet.Logging;
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
        public List<PlayerID> players;

        public SceneState(Scene scene, PurrSceneSettings settings)
        {
            this.scene = scene;
            this.settings = settings;
            players = new List<PlayerID>();
        }
    }

    public struct PurrSceneSettings
    {
        public LoadSceneMode mode;
        public LocalPhysicsMode physicsMode;
        public bool isPublic;
        public HashSet<PlayerID> whiteList;
        public HashSet<PlayerID> blackList;
        
        public bool CanJoin(PlayerID player)
        {
            if (whiteList != null && whiteList.Contains(player)) return true;
            if (blackList != null && blackList.Contains(player)) return false;

            return isPublic;
        }
    }
    
    public delegate void OnPlayerSceneDelegate(PlayerID player, SceneID scene, bool asServer);
    
    public class ScenesModule : INetworkModule, IFixedUpdate
    {
        private readonly NetworkManager _networkManager;
        private readonly PlayersManager _players;
        
        private readonly SceneHistory _history;
        private bool _asServer;
        
        private readonly List<PendingOperation> _pendingOperations = new ();
        private readonly Queue<SceneAction> _actionsQueue = new ();

        private readonly Dictionary<SceneID, SceneState> _scenes = new ();
        private readonly Dictionary<Scene, SceneID> _idToScene = new ();

        public event OnPlayerSceneDelegate onPlayerJoinedScene;
        public event OnPlayerSceneDelegate onPlayerLeftScene;

        private ushort _nextSceneID;
        
        private SceneID GetNextID() => new(_nextSceneID++);

        public ScenesModule(NetworkManager manager, PlayersManager players)
        {
            _networkManager = manager;
            _players = players;
            
            _history = new SceneHistory();
        }
        
        private void AddScene(Scene scene, PurrSceneSettings settings, SceneID id)
        {
            _scenes.Add(id, new SceneState(scene, settings));
            _idToScene.Add(scene, id);
        }
        
        private void RemoveScene(Scene scene)
        {
            if (!_idToScene.TryGetValue(scene, out var id))
                return;
            
            _scenes.Remove(id);
            _idToScene.Remove(scene);
        }

        public void Enable(bool asServer)
        {
            var nmScene = _networkManager.gameObject.scene;
            
            AddScene(nmScene, new PurrSceneSettings
            {
                mode = LoadSceneMode.Single,
                isPublic = true,
                blackList = null,
                whiteList = null,
                physicsMode = LocalPhysicsMode.None
            }, GetNextID());
            
            _asServer = asServer;

            if (!asServer)
            {
                _players.Subscribe<SceneActionsBatch>(OnSceneActionsBatch);
            }
            else
            {
                _players.onPlayerJoined += OnPlayerJoined;
            }
            
            SceneManager.sceneLoaded += SceneManagerOnsceneLoaded;
        }

        private void OnPlayerJoined(PlayerID player, bool asserver)
        {
            if (!asserver)
                return;
            
            _players.Send(player, _history.GetFullHistory());
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
                    
                    // if the scene is pending, don't do anything for now
                    if (IsScenePending(idx)) break;

                    if (_scenes.TryGetValue(idx, out var sceneState))
                    {
                        PurrLogger.LogError($"Couldn't find scene with index {idx} to unload");
                        break;
                    }

                    SceneManager.UnloadSceneAsync(sceneState.scene);
                    RemoveScene(sceneState.scene);
                    _actionsQueue.Dequeue();
                    break;
                }
                case SceneActionType.SetActive:
                {
                    var idx = action.unloadSceneAction.sceneID;
                    
                    // if the scene is pending, don't do anything for now
                    if (IsScenePending(idx)) break;

                    if (_scenes.TryGetValue(idx, out var sceneState))
                    {
                        PurrLogger.LogError($"Couldn't find scene with index {idx} to set as active");
                        break;
                    }

                    SceneManager.SetActiveScene(sceneState.scene);
                    _actionsQueue.Dequeue();
                    break;
                }
            }
        }

        private void OnSceneActionsBatch(PlayerID player, SceneActionsBatch data, bool asserver)
        {
            if (_networkManager.isHost && !asserver)
                return;

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
                isPublic = true,
                blackList = null,
                whiteList = null
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
        
        public void UnloadSceneAsync(Scene scene, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            if (!_idToScene.TryGetValue(scene, out var sceneIndex))
            {
                PurrLogger.LogError($"Scene {scene.name} not found in scenes list");
                return;
            }
            
            _history.AddUnloadAction(new UnloadSceneAction { sceneID = sceneIndex, options = options});
            SceneManager.UnloadSceneAsync(scene, options);
            RemoveScene(scene);
        }
        
        public void SetActiveScene(Scene scene)
        {
            if (!_idToScene.TryGetValue(scene, out var sceneIndex))
            {
                PurrLogger.LogError($"Scene {scene.name} not found in scenes list");
                return;
            }
            
            _history.AddSetActiveAction(new SetActiveSceneAction { sceneID = sceneIndex });
            SceneManager.SetActiveScene(scene);
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
            {
                _players.Unsubscribe<SceneActionsBatch>(OnSceneActionsBatch);
            }
            
            SceneManager.sceneLoaded -= SceneManagerOnsceneLoaded;

            DoCleanup();
        }

        public void FixedUpdate()
        {
            HandleNextSceneAction();
            
            if (_history.hasUnflushedActions)
            {
                _players.SendToAll(_history.GetDelta());
                _history.Flush();
            }
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
    }
}
