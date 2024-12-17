using System.Collections.Generic;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public class HierarchyFactory : INetworkModule, IPreFixedUpdate, IFixedUpdate
    {
        readonly ScenesModule _scenes;
        
        readonly NetworkManager _manager;

        readonly ScenePlayersModule _scenePlayersModule;
        
        readonly Dictionary<SceneID, HierarchyV2> _hierarchies = new ();
        
        readonly List<HierarchyV2> _rawHierarchies = new ();
        
        readonly IPrefabProvider _prefabs;
        
        readonly PlayersManager _playersManager;
        
        public HierarchyFactory(NetworkManager manager, ScenesModule scenes, ScenePlayersModule scenePlayersModule, PlayersManager playersManager)
        {
            _manager = manager;
            _scenes = scenes;
            _scenePlayersModule = scenePlayersModule;
            _prefabs = manager.prefabProvider;
            _playersManager = playersManager;
        }
        
        public void Enable(bool asServer)
        {
            var scenes = _scenes.sceneStates;

            foreach (var (id, sceneState) in scenes)
            {
                if (sceneState.scene.isLoaded)
                    OnPreSceneLoaded(id, asServer);
            }

            _scenes.onPreSceneLoaded += OnPreSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
        }

        public void Disable(bool asServer)
        {
            for (var i = 0; i < _rawHierarchies.Count; i++)
                _rawHierarchies[i].Disable();

            _scenes.onPreSceneLoaded -= OnPreSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
        }

        private void OnPreSceneLoaded(SceneID scene, bool asServer)
        {
            if (_hierarchies.ContainsKey(scene))
            {
                PurrLogger.LogError($"Hierarchy module for scene {scene} already exists; trying to create another one?");
                return;
            }
            
            if (!_scenes.TryGetSceneState(scene, out var sceneState))
            {
                PurrLogger.LogError($"Scene {scene} doesn't exist; trying to create hierarchy module for it?");
                return;
            }
            
            var hierarchy = new HierarchyV2(_manager, scene, sceneState.scene, _scenePlayersModule, _playersManager, asServer);
            hierarchy.Enable();
            
            _rawHierarchies.Add(hierarchy);
            _hierarchies.Add(scene, hierarchy);
        }
        
        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            if (!_hierarchies.TryGetValue(scene, out var hierarchy))
            {
                PurrLogger.LogError($"Hierarchy module for scene {scene} doesn't exist; trying to unload it?");
                return;
            }
            
            hierarchy.Disable();
            
            _rawHierarchies.Remove(hierarchy);
            _hierarchies.Remove(scene);
        }


        public void PreFixedUpdate()
        {
            for (var i = 0; i < _rawHierarchies.Count; i++)
                _rawHierarchies[i].PreNetworkMessages();
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < _rawHierarchies.Count; i++)
                _rawHierarchies[i].PostNetworkMessages();
        }

        public bool TryGetHierarchy(SceneID sceneId, out HierarchyV2 o)
        {
            return _hierarchies.TryGetValue(sceneId, out o);
        }
    }
}
