using System.Collections.Generic;
using PurrNet.Modules;

namespace PurrNet
{
    public class VisibilityFactory : INetworkModule
    {
        private readonly NetworkManager _manager;
        private readonly ScenesModule _scenes;
        private readonly bool _asServer;

        private readonly Dictionary<SceneID, VisibilityManager> _sceneToVisiblityManager = new ();
        private readonly List<VisibilityManager> _visiblityManagers = new ();

        public VisibilityFactory(NetworkManager manager, ScenesModule scenes, bool asServer)
        {
            _asServer = asServer;
            _manager = manager;
            _scenes = scenes;
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
            for (var i = 0; i < _visiblityManagers.Count; i++)
                _visiblityManagers[i].Disable(asServer);
            
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(SceneID scene, bool asserver)
        {
            if (!_sceneToVisiblityManager.ContainsKey(scene))
            {
                var hierarchy = new VisibilityManager(scene);
                
                _visiblityManagers.Add(hierarchy);
                _sceneToVisiblityManager.Add(scene, hierarchy);
                
                hierarchy.Enable(asserver);
            }
        }

        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            if (_sceneToVisiblityManager.TryGetValue(scene, out var hierarchy))
            {
                hierarchy.Disable(asserver);
                
                _visiblityManagers.Remove(hierarchy);
                _sceneToVisiblityManager.Remove(scene);
            }
        }
    }
}
