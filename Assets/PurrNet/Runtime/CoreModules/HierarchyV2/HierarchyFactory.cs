using System.Collections.Generic;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public class HierarchyFactory : INetworkModule, IPreFixedUpdate, IFixedUpdate
    {
        readonly ScenesModule _scenes;
        
        readonly ScenePlayersModule _scenePlayersModule;
        
        readonly Dictionary<SceneID, HierarchyV2> _hierarchies = new ();
        
        readonly List<HierarchyV2> _rawScenes = new ();
        
        readonly IPrefabProvider _prefabs;
        
        public HierarchyFactory(IPrefabProvider prefabs, ScenesModule scenes, ScenePlayersModule scenePlayersModule)
        {
            _scenes = scenes;
            _scenePlayersModule = scenePlayersModule;
            _prefabs = prefabs;
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
            
            var hierarchy = new HierarchyV2(scene, sceneState.scene, _scenePlayersModule, _prefabs, asServer);
            hierarchy.Enable();
            
            _rawScenes.Add(hierarchy);
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
            
            _rawScenes.Remove(hierarchy);
            _hierarchies.Remove(scene);
        }


        public void PreFixedUpdate()
        {
            for (var i = 0; i < _rawScenes.Count; i++)
                _rawScenes[i].PreNetworkMessages();
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < _rawScenes.Count; i++)
                _rawScenes[i].PostNetworkMessages();
        }

        public bool TryGetHierarchy(SceneID sceneId, out HierarchyV2 o)
        {
            return _hierarchies.TryGetValue(sceneId, out o);
        }
    }
}
