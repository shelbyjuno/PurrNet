using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class HierarchyModule : INetworkModule, IPreFixedUpdate
    {
        private readonly NetworkManager _manager;
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _players;
        private readonly ScenesModule _scenes;
        private readonly ScenePlayersModule _scenePlayers;
        
        private readonly List<HierarchyScene> _hierarchies = new ();
        private readonly Dictionary<SceneID, HierarchyScene> _sceneToHierarchy = new ();
        
        public event Action<NetworkIdentity> onIdentityRemoved;
        public event Action<NetworkIdentity> onIdentityAdded;

        private HierarchyModule _serverModule;
        private bool _asServer;
        
        public HierarchyModule(NetworkManager manager, ScenesModule scenes, PlayersManager players,
            ScenePlayersModule scenePlayers, NetworkPrefabs prefabs)
        {
            _scenes = scenes;
            _manager = manager;
            _players = players;
            _scenePlayers = scenePlayers;
            _prefabs = prefabs;
        }
        
        public bool IsSceneReady(SceneID sceneID)
        {
            if (!_sceneToHierarchy.TryGetValue(sceneID, out var hierarchy))
                return false;
            
            return hierarchy.IsSceneReady();
        }

        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            var scenes = _scenes.scenes;
            var sceneCount = scenes.Count;
            
            for (var i = 0; i < sceneCount; i++)
                OnSceneLoaded(scenes[i], asServer);
            
            _scenes.onPreSceneLoaded += OnSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
        }

        public void Disable(bool asServer)
        {
            for (var i = 0; i < _hierarchies.Count; i++)
                _hierarchies[i].Disable(asServer);
            
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            if (_sceneToHierarchy.TryGetValue(scene, out var hierarchy))
            {
                hierarchy.onIdentityAdded -= TriggerOnEntityAdded;
                hierarchy.onIdentityRemoved -= TriggerOnEntityRemoved;
                
                hierarchy.Disable(asserver);
                _hierarchies.Remove(hierarchy);
                _sceneToHierarchy.Remove(scene);
            }
        }

        private void OnSceneLoaded(SceneID scene, bool asserver)
        {
            if (!_sceneToHierarchy.ContainsKey(scene))
            {
                var hierarchy = new HierarchyScene(scene, _scenes, _manager, _players, _scenePlayers, _prefabs);
                
                hierarchy.onIdentityAdded += TriggerOnEntityAdded;
                hierarchy.onIdentityRemoved += TriggerOnEntityRemoved;
                
                _hierarchies.Add(hierarchy);
                _sceneToHierarchy.Add(scene, hierarchy);
                
                hierarchy.Enable(asserver);
            }
        }

        private void TriggerOnEntityAdded(NetworkIdentity obj) => onIdentityAdded?.Invoke(obj);

        private void TriggerOnEntityRemoved(NetworkIdentity obj) => onIdentityRemoved?.Invoke(obj);

        public void PreFixedUpdate()
        {
            for (var i = 0; i < _hierarchies.Count; i++)
                _hierarchies[i].PreFixedUpdate();
        }

        internal void TriggerOnSpawnedEventForClient()
        {
            foreach (var hierarchy in _sceneToHierarchy.Values)
                hierarchy.TriggerSpawnEventOnClient();
        }
        
        public bool TryGetIdentity(SceneID sceneID, NetworkID id, out NetworkIdentity identity)
        {
            if (!_asServer && _manager.isServer)
            {
                _serverModule ??= _manager.GetModule<HierarchyModule>(true);
                return _serverModule.TryGetIdentity(sceneID, id, out identity);
            } 
            
            if (!_sceneToHierarchy.TryGetValue(sceneID, out var hierarchy))
            {
                PurrLogger.LogError($"Failed to find hierarchy for scene '{sceneID}'.");
                identity = null;
                return false;
            }
            
            return hierarchy.TryGetIdentity(id, out identity);
        }
        
        internal void AutoSpawn(GameObject gameObject)
        {
            if (!_scenes.TryGetSceneID(gameObject.scene, out var sceneID))
            {
                PurrLogger.LogError($"Failed to find scene id for '{gameObject.scene.name}'.");
                return;
            }
            
            if (!_sceneToHierarchy.TryGetValue(sceneID, out var hierarchy))
            {
                PurrLogger.LogError($"Failed to find hierarchy for scene '{sceneID}'.");
                return;
            }

            if (!hierarchy.IsSceneReady())
            {
                Debug.LogError($"Scene '{sceneID}' is not ready.");
                return;
            }
            
            hierarchy.Spawn(gameObject);
        }

        public void Spawn(GameObject gameObject)
        {
            if (!_scenes.TryGetSceneID(gameObject.scene, out var sceneID))
            {
                PurrLogger.LogError($"Failed to find scene id for '{gameObject.scene.name}'.");
                return;
            }
            
            if (!_sceneToHierarchy.TryGetValue(sceneID, out var hierarchy))
            {
                PurrLogger.LogError($"Failed to find hierarchy for scene '{sceneID}'.");
                return;
            }
            
            hierarchy.Spawn(gameObject);
        }

        public string GetActionsAsString(SceneID sceneId)
        {
            return _sceneToHierarchy.TryGetValue(sceneId, out var hierarchy) ? hierarchy.GetActionsAsString() : string.Empty;
        }
    }
}
