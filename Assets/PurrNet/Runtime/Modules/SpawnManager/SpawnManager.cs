using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    internal partial struct SpawnPrefabMessage : IAutoNetworkedData
    {
        public int prefabId { get; set; }
        public int prefabOffset { get; set; }
        public int rootIdentityId { get; set; }
        public int childrenCount { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 scale { get; set; }
    }
    
    internal partial struct SpawnPrefabsSnapshot : IAutoNetworkedData
    {
        public List<SpawnPrefabMessage> prefabs { get; set; }
    }
    
    public class SpawnManager : INetworkModule
    {
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersBroadcaster _broadcaster;
        internal static readonly List<NetworkIdentity> _identitiesCache = new ();
        
        private bool _asServer;
        private int _nextIdentity;
        
        private readonly List<NetworkIdentity> _spawnedObjects = new ();
        
        public SpawnManager(PlayersBroadcaster broadcaster, NetworkPrefabs prefabs)
        {
            _broadcaster = broadcaster;
            _prefabs = prefabs;
        }

        public void Enable(bool asServer)
        {
            _asServer = asServer;

            if (!asServer)
            {
                _broadcaster.Subscribe<SpawnPrefabMessage>(ClientSpawnEvent, false);
            }
        }

        private void ClientSpawnEvent(PlayerID player, SpawnPrefabMessage data, bool asserver)
        {
            if (!_prefabs.TryGetPrefab(data.prefabId, out var prefab))
                PurrLogger.Throw<InvalidOperationException>($"The specified prefab id '{data.prefabId}' is not a network prefab.");
            
            var instance = Object.Instantiate(prefab, data.position, data.rotation);
            instance.transform.localScale = data.scale;
            
            if (!instance.TryGetComponent<NetworkIdentity>(out _))
                instance.AddComponent<NetworkIdentity>();
            
            instance.GetComponentsInChildren(true, _identitiesCache);

            var identitiesCount = _identitiesCache.Count;
            int firstIdentity = data.rootIdentityId;

            if (identitiesCount != data.childrenCount)
                PurrLogger.Throw<InvalidOperationException>($"The specified prefab '{prefab.name}' has a different amount of NetworkIdentities than the one sent by the server.");
            
            for (int i = 0; i < identitiesCount; i++)
                _identitiesCache[i].SetIdentity(data.prefabId, i, firstIdentity + i);
            
            var rootIdentity = _identitiesCache[0];
            _spawnedObjects.Add(rootIdentity);
        }

        public void Disable(bool asServer) { }
        
        private int AllocateNewIdentity()
        {
            return _nextIdentity++;
        }
        
        public void Spawn(GameObject prefab, GameObject instance)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can spawn objects.");

            if (!_prefabs.TryGetPrefabId(prefab, out var prefabId))
                PurrLogger.Throw<InvalidOperationException>($"The specified object '{prefab.name}' is not a network prefab.");
            
            // Check if the object already has a NetworkIdentity if not add one
            if (!instance.TryGetComponent<NetworkIdentity>(out _))
                instance.AddComponent<NetworkIdentity>();
            
            // Get all NetworkIdentities in the object
            instance.GetComponentsInChildren(true, _identitiesCache);

            var identitiesCount = _identitiesCache.Count;
            for (int i = 0; i < identitiesCount; i++)
            {
                var allocatedIdentity = AllocateNewIdentity();
                _identitiesCache[i].SetIdentity(prefabId, i, allocatedIdentity);
            }
            
            var rootIdentity = _identitiesCache[0];
            
            _spawnedObjects.Add(rootIdentity);
            
            // Send the spawn message to all clients
            _broadcaster.SendToAll(rootIdentity.GetSpawnMessage(identitiesCount));
        }
    }
}
