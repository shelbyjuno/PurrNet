using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class HierarchyModule : INetworkModule, IFixedUpdate, IUpdate
    {
        private readonly NetworkManager _manager;
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _players;
        private readonly ScenesModule _scenes;
        private readonly ScenePlayersModule _scenePlayers;
        
        private readonly List<HierarchyScene> _hierarchies = new ();
        private readonly Dictionary<SceneID, HierarchyScene> _sceneToHierarchy = new ();

        public HierarchyModule(NetworkManager manager, ScenesModule scenes, PlayersManager players,
            ScenePlayersModule scenePlayers, NetworkPrefabs prefabs)
        {
            _scenes = scenes;
            _manager = manager;
            _players = players;
            _scenePlayers = scenePlayers;
            _prefabs = prefabs;
        }

        public void Enable(bool asServer)
        {
            _scenes.onSceneLoaded += OnSceneLoaded;
        }

        public void Disable(bool asServer)
        {
            _scenes.onSceneLoaded -= OnSceneLoaded;
        }
        
        private void OnSceneLoaded(SceneID scene, bool asserver)
        {
            if (!_sceneToHierarchy.ContainsKey(scene))
            {
                var hierarchy = new HierarchyScene(scene, _manager, _players, _scenePlayers, _prefabs);
                
                _hierarchies.Add(hierarchy);
                _sceneToHierarchy.Add(scene, hierarchy);
                
                hierarchy.Enable(asserver);
            }
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < _hierarchies.Count; i++)
            {
                var hierarchy = _hierarchies[i];
                hierarchy.FixedUpdate();
            }
        }

        public void Update()
        {
            for (var i = 0; i < _hierarchies.Count; i++)
            {
                var hierarchy = _hierarchies[i];
                hierarchy.Update();
            }
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
    }
}
