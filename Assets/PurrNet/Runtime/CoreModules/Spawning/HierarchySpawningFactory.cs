using System.Collections.Generic;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public class HierarchySpawningFactory : INetworkModule
    {
        private readonly HierarchyModule _hierarchy;
        private readonly VisibilityFactory _visibilityFactory;
        
        private readonly Dictionary<SceneID, HierarchySpawning> _hierarchies = new ();
        private readonly bool _asServer;
        
        public HierarchySpawningFactory(
            HierarchyModule hierarchy,
            VisibilityFactory visibilityFactory, bool asServer)
        {
            _asServer = asServer;
            _hierarchy = hierarchy;
            _visibilityFactory = visibilityFactory;
        }
        
        public void Enable(bool asServer)
        {
            foreach (var (scene, manager) in _visibilityFactory.sceneToVisibilityManager)
                OnSceneVisiblityAdded(scene, manager);
            
            _visibilityFactory.onVisibilityManagerAdded += OnSceneVisiblityAdded;
            _visibilityFactory.onVisibilityManagerRemoved += OnSceneVisiblityRemoved;
        }

        public void Disable(bool asServer)
        {
            _visibilityFactory.onVisibilityManagerAdded -= OnSceneVisiblityAdded;
            _visibilityFactory.onVisibilityManagerRemoved -= OnSceneVisiblityRemoved;
        }
        
        private void OnSceneVisiblityAdded(SceneID scene, VisibilityManager manager)
        {
            if (!_hierarchy.TryGetHierarchy(scene, out var hierarchy))
            {
                PurrLogger.LogError("Hierarchy not found for scene " + scene);
                return;
            }
            
            var spawning = new HierarchySpawning(hierarchy, manager);
            spawning.Enable(_asServer);
            _hierarchies.Add(scene, spawning);
        }
        
        private void OnSceneVisiblityRemoved(SceneID scene, VisibilityManager manager)
        {
            if (_hierarchies.Remove(scene, out var spawning))
                spawning.Disable(_asServer);
        }
    }
}
