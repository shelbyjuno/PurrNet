using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    internal struct PendingOperation
    {
        public int buildIndex;
        public int idToAssign;
        public LoadSceneMode mode;
    }
    
    public class ScenesModule : INetworkModule, IFixedUpdate
    {
        private readonly NetworkManager _networkManager;
        private readonly PlayersManager _players;
        
        private readonly SceneHistory _history;
        private bool _asServer;
        
        private readonly List<Scene?> _scenes = new ();
        private readonly List<PendingOperation> _pendingOperations = new ();
        
        private readonly Queue<SceneAction> _actionsQueue = new ();

        public ScenesModule(NetworkManager manager, PlayersManager players)
        {
            _networkManager = manager;
            _players = players;
            
            _history = new SceneHistory();
        }

        public void Enable(bool asServer)
        {
            var nmScene = _networkManager.gameObject.scene;
            _scenes.Add(nmScene);

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

                if (operation.buildIndex == scene.buildIndex && operation.mode == mode)
                {
                    int difference = operation.idToAssign - _scenes.Count;
                    if (difference > 100)
                    {
                        PurrLogger.LogError($"Scene ID difference is too big: {difference}; ignoring operation.");
                        _pendingOperations.RemoveAt(i);
                        break;
                    }
                    
                    while (operation.idToAssign >= _scenes.Count)
                        _scenes.Add(null);
                    
                    _scenes[operation.idToAssign] = scene;
                    _pendingOperations.RemoveAt(i);
                    break;
                }
            }
        }
        
        private bool IsScenePending(int sceneId)
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
                    
                    SceneManager.LoadSceneAsync(loadAction.buildIndex, loadAction.parameters);

                    _pendingOperations.Add(new PendingOperation
                    {
                        buildIndex = loadAction.buildIndex,
                        mode = loadAction.parameters.loadSceneMode,
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

                    if (idx < 0 || idx >= _scenes.Count || !_scenes[idx].HasValue)
                    {
                        PurrLogger.LogError($"Couldn't find scene with index {idx} to unload");
                        break;
                    }

                    SceneManager.UnloadSceneAsync(_scenes[idx].Value);
                    _scenes[idx] = null;
                    _actionsQueue.Dequeue();
                    break;
                }
                case SceneActionType.SetActive:
                {
                    var idx = action.unloadSceneAction.sceneID;
                    
                    // if the scene is pending, don't do anything for now
                    if (IsScenePending(idx)) break;

                    if (idx < 0 || idx >= _scenes.Count || !_scenes[idx].HasValue)
                    {
                        PurrLogger.LogError($"Couldn't find scene with index {idx} to set as active");
                        break;
                    }

                    SceneManager.SetActiveScene(_scenes[idx].Value);
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

        private int ReserverSceneID()
        {
            int id = _scenes.Count;
            _scenes.Add(null);
            return id;
        }
        
        private int GetSceneID(Scene scene)
        {
            int targetHandle = scene.handle;
            
            for (int i = 0; i < _scenes.Count; i++)
            {
                var value = _scenes[i];
                
                if (!value.HasValue) continue;
                
                if (value.Value.handle == targetHandle)
                {
                    return i;
                }
            }
            
            return -1;
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

            int idToAssign = ReserverSceneID();
            
            _history.AddLoadAction(new LoadSceneAction { buildIndex = sceneIndex, sceneID = idToAssign, parameters = parameters});
            
            var op = SceneManager.LoadSceneAsync(sceneIndex, parameters);
            
            _pendingOperations.Add(new PendingOperation
            {
                buildIndex = sceneIndex,
                mode = parameters.loadSceneMode,
                idToAssign = idToAssign
            });
            
            return op;
        }
        
        public void UnloadSceneAsync(string sceneName, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            var idx = SceneNameToBuildIndex(sceneName);
            
            if (idx == -1)
            {
                PurrLogger.LogError($"Scene {sceneName} not found in build settings");
                return;
            }
            
            UnloadSceneAsync(idx, options);
        }
        
        public void UnloadSceneAsync(Scene scene, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            var sceneIndex = GetSceneID(scene);
            
            if (sceneIndex == -1)
            {
                PurrLogger.LogError($"Scene {scene.name} not found in scenes list");
                return;
            }
            
            UnloadSceneAsync(sceneIndex, options);
        }
        
        public void UnloadSceneAsync(int sceneIndex, UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("Only server can unload scenes; for now at least ;)");
                return;
            }
            _history.AddUnloadAction(new UnloadSceneAction { sceneID = sceneIndex, options = options});
            SceneManager.UnloadSceneAsync(sceneIndex, options);
        }
        
        public void SetActiveScene(Scene scene)
        {
            var sceneIndex = GetSceneID(scene);
            
            if (sceneIndex == -1)
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
            bool isAnythingConnected = _networkManager.isServer || _networkManager.isClient;
            
            if (isAnythingConnected) 
                return;
            
            for (int i = 0; i < _scenes.Count; i++)
            {
                if (_scenes[i].HasValue)
                {
                    // Don't unload the scene where the network manager is
                    if (_networkManager.gameObject.scene.handle == _scenes[i].Value.handle)
                        continue;
                    
                    SceneManager.UnloadSceneAsync(_scenes[i].Value);
                }
            }
        }
    }
}
