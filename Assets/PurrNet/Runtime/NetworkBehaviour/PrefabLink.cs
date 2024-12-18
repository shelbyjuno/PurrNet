namespace PurrNet
{
    public sealed class PrefabLink : NetworkIdentity
    {
        
        public int poolWarmupCount => 0;

        public string prefabGuid => null;

        internal static void StartIgnoreAutoSpawn()
        {
        }
        
        internal static void StopIgnoreAutoSpawn()
        {
        }

        /*void Awake()
        {
            if (isSceneObject)
                return;
            
            if (_muteAutoSpawn)
                return;

            if (isSpawned)
                return;
            
            var manager = NetworkManager.main;
            
            if (!manager)
                return;

            if (!manager.TryGetModule<ScenesModule>(manager.isServer, out var scenesModule))
                return;
            
            if (!scenesModule.TryGetSceneID(gameObject.scene, out var currentSceneId))
                return;
            
            bool anyConnected = manager.isClient || manager.isServer;

            if (!anyConnected)
                return;

            var prefab = manager.prefabProvider.GetPrefabFromGuid(_guid);

            if (!prefab)
            {
                Debug.LogError($"Failed to find prefab id for '{gameObject.name}'. Refresh the prefab list in the NetworkManager.");
                return;
            }

            if (!manager.TryGetModule<HierarchyFactory>(manager.isServer, out var hierarchyFactory))
                return;

            if (hierarchyFactory.TryGetHierarchy(currentSceneId, out var hierarchy) && hierarchy.areSceneObjectsReady)
                hierarchy.Spawn(gameObject);
        }*/

        public void AutoSpawn()
        {
            // Awake();
        }

        internal bool SetGUID(string guid)
        {
            return false;
        }

        public bool MatchesGUID(string guid)
        {
            return false;
        }
    }
}
