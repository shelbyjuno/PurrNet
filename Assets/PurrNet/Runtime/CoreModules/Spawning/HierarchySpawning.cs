namespace PurrNet.Modules
{
    public class HierarchySpawning : INetworkModule
    {
        private readonly HierarchyScene _hierarchyScene;
        private readonly VisibilityManager _visibilityManager;
        
        internal HierarchySpawning(HierarchyScene hierarchyScene, VisibilityManager visibilityManager)
        {
            _hierarchyScene = hierarchyScene;
            _visibilityManager = visibilityManager;
        }
        
        public void Enable(bool asServer)
        {
        }

        public void Disable(bool asServer)
        {
        }
    }
}