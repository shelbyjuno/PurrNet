using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Transports;
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
    
    public class SpawnManager : INetworkModule, IConnectionStateListener
    {
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _playersManager;
        private readonly PlayersBroadcaster _broadcaster;
        internal static readonly List<NetworkIdentity> _identitiesCache = new ();
        
        private bool _asServer;
        private int _nextIdentity;
        
        private readonly List<NetworkIdentity> _spawnedObjects = new ();
        private readonly List<SpawnPrefabMessage> _prefabsCache = new ();
        
        public SpawnManager(PlayersManager players, PlayersBroadcaster broadcaster, NetworkPrefabs prefabs)
        {
            _playersManager = players;
            _broadcaster = broadcaster;
            _prefabs = prefabs;
        }

        public void Enable(bool asServer)
        {
            _asServer = asServer;

            if (!asServer)
            {
                _broadcaster.Subscribe<SpawnPrefabMessage>(ClientSpawnEvent);
                _broadcaster.Subscribe<SpawnPrefabsSnapshot>(ClientBatchedSpawnEvent);
            }
            else
            {
                _playersManager.onPlayerJoined += PlayerJoined;
            }
        }

        private void PlayerJoined(PlayerID player, bool asserver)
        {
            var snapshot = new SpawnPrefabsSnapshot
            {
                prefabs = _prefabsCache
            };

            for (var i = 0; i < _spawnedObjects.Count; i++)
            {
                var spawnedObject = _spawnedObjects[i];
                _prefabsCache.Add(spawnedObject.GetSpawnMessage());
            }

            _broadcaster.Send(player, snapshot);
        }
        
        private void ClientBatchedSpawnEvent(PlayerID player, SpawnPrefabsSnapshot data, bool asserver)
        {
            for (var i = 0; i < data.prefabs.Count; i++)
                ClientSpawnEvent(player, data.prefabs[i], asserver);
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

        public void OnConnectionState(ConnectionState state, bool asServer)
        {
            if (state == ConnectionState.Disconnecting)
            {
                for (var i = 0; i < _spawnedObjects.Count; i++)
                {
                    if (_spawnedObjects[i])
                        Object.Destroy(_spawnedObjects[i].gameObject);
                }
                
                _spawnedObjects.Clear();
            }
        }
    }
}
