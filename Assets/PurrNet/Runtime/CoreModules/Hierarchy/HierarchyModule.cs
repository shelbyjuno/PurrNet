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
        private readonly IPrefabProvider _prefabs;
        private readonly PlayersManager _players;
        private readonly ScenesModule _scenes;
        private readonly ScenePlayersModule _scenePlayers;
        private VisibilityFactory _visibilityFactory;
        
        private readonly List<HierarchyScene> _hierarchies = new ();
        private readonly Dictionary<SceneID, HierarchyScene> _sceneToHierarchy = new ();

        internal event Action<SceneID> onBeforeSpawnTrigger;
        public event Action<NetworkIdentity> onIdentityRemoved;
        public event Action<NetworkIdentity> onIdentityAdded;

        private HierarchyModule _serverModule;
        private bool _asServer;
        
        public HierarchyModule(NetworkManager manager, ScenesModule scenes, PlayersManager players,
            ScenePlayersModule scenePlayers, IPrefabProvider prefabs)
        {
            _scenes = scenes;
            _manager = manager;
            _players = players;
            _scenePlayers = scenePlayers;
            _prefabs = prefabs;
        }
        
        public void SetVisibilityFactory(VisibilityFactory factory)
        {
            _visibilityFactory = factory;
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
                _visibilityFactory.OnSceneUnloaded(scene, asserver);
                hierarchy.onIdentityAdded -= TriggerOnEntityAdded;
                hierarchy.onIdentityRemoved -= TriggerOnEntityRemoved;
                hierarchy.onBeforeSpawnTrigger -= TriggerOnBeforeSpawnTrigger;
                hierarchy.Disable(asserver);
                
                _hierarchies.Remove(hierarchy);
                _sceneToHierarchy.Remove(scene);
            }
        }

        private void OnSceneLoaded(SceneID scene, bool asserver)
        {
            if (!_sceneToHierarchy.ContainsKey(scene))
            {
                var hierarchy = new HierarchyScene(asserver, scene, _scenes, _manager, _players, _scenePlayers, _prefabs);
                
                hierarchy.onIdentityAdded += TriggerOnEntityAdded;
                hierarchy.onIdentityRemoved += TriggerOnEntityRemoved;
                hierarchy.onBeforeSpawnTrigger += TriggerOnBeforeSpawnTrigger;
                
                _hierarchies.Add(hierarchy);
                _sceneToHierarchy.Add(scene, hierarchy);

                if (!_visibilityFactory.OnSceneLoaded(scene, asserver, out var vmanager))
                     PurrLogger.LogError("Failed to create visibility manager for scene " + scene);
                else hierarchy.SetVisibilityManager(_visibilityFactory, vmanager);
                
                hierarchy.Enable(asserver);
            }
        }

        private void TriggerOnBeforeSpawnTrigger(SceneID id)
        {
            onBeforeSpawnTrigger?.Invoke(id);
        }

        private void TriggerOnEntityAdded(NetworkIdentity obj) => onIdentityAdded?.Invoke(obj);

        private void TriggerOnEntityRemoved(NetworkIdentity obj) => onIdentityRemoved?.Invoke(obj);

        public void PreFixedUpdate()
        {
            for (var i = 0; i < _hierarchies.Count; i++)
                _hierarchies[i].PreFixedUpdate();
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
            
            return hierarchy.TryGetIdentity(id, out identity) && identity;
        }
        
        internal bool TryGetHierarchy(SceneID sceneID, out HierarchyScene hierarchy)
        {
            return _sceneToHierarchy.TryGetValue(sceneID, out hierarchy);
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

            string name = gameObject.name;

            hierarchy.Spawn(ref gameObject);

            if (gameObject == null)
            {
                PurrLogger.LogError($"Failed to spawn '{name}'.");
                return;
            }
            
            if (!_manager.isClientOnly || !_players.localPlayerId.HasValue)
                return;
            
            var identity = gameObject.GetComponent<NetworkIdentity>();
            
            if (identity && identity.isSpawned && identity.ShouldClientGiveOwnershipOnSpawn())
                identity.SetPendingOwnershipRequest(_players.localPlayerId.Value);
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
            
            hierarchy.Spawn(ref gameObject);
        }

        public string GetActionsAsString(SceneID sceneId)
        {
            return _sceneToHierarchy.TryGetValue(sceneId, out var hierarchy) ? hierarchy.GetActionsAsString() : string.Empty;
        }
    }
}
