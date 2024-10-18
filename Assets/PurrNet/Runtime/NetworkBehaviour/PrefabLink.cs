using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class PrefabLink : NetworkIdentity
    {
        [SerializeField, PurrReadOnly] private string _guid;

        static bool _muteAutoSpawn;
        
        public string prefabGuid => _guid;

        internal static void IgnoreNextAutoSpawnAttempt()
        {
            _muteAutoSpawn = true;
        }

        void Start()
        {
            if (_muteAutoSpawn)
            {
                _muteAutoSpawn = false;
                return;
            }

            if (isSpawned)
                return;
            
            var manager = NetworkManager.main;
            
            if (!manager)
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

            var spawnModule = manager.GetModule<HierarchyModule>(manager.isServer);
            spawnModule.AutoSpawn(gameObject);
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
