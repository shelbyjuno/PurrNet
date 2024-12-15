using PurrNet.Modules;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class PrefabLink : NetworkIdentity
    {
        [SerializeField, PurrReadOnly] private string _guid;

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

        private bool _autoSpawnCalled;

        void Awake()
        {
            DoAutoSpawn();
        }

        private void Start()
        {
            if (!_autoSpawnCalled)
                DoAutoSpawn();
        }

        private void DoAutoSpawn()
        {
            if (_muteAutoSpawn)
                return;

            if (isSpawned)
                return;
            
            var manager = NetworkManager.main;
            
            if (!manager)
                return;

            if (manager.TryGetModule<ScenesModule>(manager.isServer, out var scenesModule))
            {
                if (!scenesModule.TryGetSceneID(gameObject.scene, out _))
                    return;
            }
            
            bool anyConnected = manager.isClient || manager.isServer;

            if (!anyConnected)
            {
                return;
            }

            var prefab = manager.prefabProvider.GetPrefabFromGuid(_guid);

            if (!prefab)
            {
                Debug.LogError($"Failed to find prefab id for '{gameObject.name}'. Refresh the prefab list in the NetworkManager.");
                return;
            }

            if (!manager.TryGetModule<HierarchyModule>(manager.isServer, out var spawnModule))
                return;
                    
            spawnModule.AutoSpawn(gameObject);
            _autoSpawnCalledFrame = Time.frameCount;
            _autoSpawnCalled = true;
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
