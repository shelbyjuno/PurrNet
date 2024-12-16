using PurrNet.Modules;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class PrefabLink : NetworkIdentity
    {
        [SerializeField, PurrReadOnly] private string _guid;
        [SerializeField, PurrLock] private bool _usePooling;
        [SerializeField, PurrLock] private int _poolWarmupCount = 1;
        
        public bool usePooling => _usePooling;
        
        public int poolWarmupCount => _poolWarmupCount;

        static bool _muteAutoSpawn;
        
        public string prefabGuid => _guid;

        internal static void StartIgnoreAutoSpawn()
        {
            _muteAutoSpawn = true;
        }
        
        internal static void StopIgnoreAutoSpawn()
        {
            _muteAutoSpawn = false;
        }

        void Awake()
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
                hierarchy.Spawn(this);
        }
        
        public void AutoSpawn()
        {
            DoAutoSpawn();
        }

        internal bool SetGUID(string guid)
        {
            if (guid == _guid)
                return false;
            _guid = guid;
            return true;
        }

        public bool MatchesGUID(string guid)
        {
            return guid == _guid;
        }
    }
}
