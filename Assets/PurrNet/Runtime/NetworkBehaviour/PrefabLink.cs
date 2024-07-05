using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public sealed class PrefabLink : NetworkIdentity
    {
        [SerializeField] private string _guid;

        static bool _muteAutoSpawn;
        
        public string prefabGuid => _guid;

        internal static void IgnoreNextAutoSpawnAttempt()
        {
            _muteAutoSpawn = true;
        }

        void Awake()
        {
            if (_muteAutoSpawn)
            {
                _muteAutoSpawn = false;
                return;
            }
            
            var networkManager = NetworkManager.main;
            
            if (!networkManager)
            {
                gameObject.SetActive(false);
                return;
            }
            
            bool anyConnected = networkManager.isClient || networkManager.isServer;
            
            if (!anyConnected)
            {
                gameObject.SetActive(false);
                return;
            }

            var prefab = networkManager.GetPrefabFromGuid(_guid);

            if (!prefab)
            {
                Debug.LogError($"Failed to find prefab id for '{gameObject.name}'. Refresh the prefab list in the NetworkManager.");
                return;
            }

            var spawnModule = networkManager.GetModule<HierarchyModule>(networkManager.isServer);
            spawnModule.Spawn(gameObject);
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
