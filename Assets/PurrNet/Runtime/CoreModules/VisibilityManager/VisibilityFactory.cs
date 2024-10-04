using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public class VisibilityFactory : INetworkModule, IFixedUpdate
    {
        private readonly NetworkManager _manager;
        private readonly ScenesModule _scenes;
        private readonly HierarchyModule _hierarchy;
        private readonly ScenePlayersModule _players;
        private readonly PlayersManager _playersManager;

        private readonly Dictionary<SceneID, VisibilityManager> _sceneToVisibilityManager = new ();
        private readonly List<VisibilityManager> _visibilityManagers = new ();

        public VisibilityFactory(NetworkManager manager, PlayersManager playersManager, ScenesModule scenes, HierarchyModule hierarchy, ScenePlayersModule players)
        {
            _manager = manager;
            _playersManager = playersManager;
            _scenes = scenes;
            _hierarchy = hierarchy;
            _players = players;
        }

        public void Enable(bool asServer)
        {
            var scenes = _scenes.scenes;
            var sceneCount = scenes.Count;
            
            for (var i = 0; i < sceneCount; i++)
                OnSceneLoaded(scenes[i], asServer);
            
            _scenes.onPreSceneLoaded += OnSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
        }

        public void Disable(bool asServer)
        {
            for (var i = 0; i < _visibilityManagers.Count; i++)
                _visibilityManagers[i].Disable(asServer);
            
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(SceneID scene, bool asserver)
        {
            if (!_hierarchy.TryGetHierarchy(scene, out var hierarchy))
            {
                PurrLogger.LogError("Hierarchy not found for scene " + scene);
                return;
            }
            
            if (!_sceneToVisibilityManager.ContainsKey(scene))
            {
                var visibility = new VisibilityManager(_manager, _playersManager, hierarchy, _players, scene);
                
                _visibilityManagers.Add(visibility);
                _sceneToVisibilityManager.Add(scene, visibility);
                
                visibility.Enable(asserver);
            }
        }

        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            if (_sceneToVisibilityManager.TryGetValue(scene, out var hierarchy))
            {
                hierarchy.Disable(asserver);
                
                _visibilityManagers.Remove(hierarchy);
                _sceneToVisibilityManager.Remove(scene);
            }
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < _visibilityManagers.Count; i++)
                _visibilityManagers[i].FixedUpdate();
        }
    }
}
