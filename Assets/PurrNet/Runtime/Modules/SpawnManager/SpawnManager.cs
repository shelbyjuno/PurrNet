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
    
    internal partial struct DespawnIdentityMessage : IAutoNetworkedData
    {
        public int identityId { get; set; }
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
        
        private readonly List<NetworkIdentity> _spawnedObjects = new ();
        private readonly List<SpawnPrefabMessage> _prefabsCache = new ();
        
        private bool _asServer;
        private int _nextIdentity;
        
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
                _broadcaster.Subscribe<SpawnPrefabsSnapshot>(ClientBatchedSpawnEvent);
                _broadcaster.Subscribe<SpawnPrefabMessage>(ClientSpawnEvent);
                _broadcaster.Subscribe<DespawnIdentityMessage>(ClientDespawnEvent);
            }
            else
            {
                _playersManager.onPrePlayerJoined += PlayerJoined;
            }
        }

        private void ClientDespawnEvent(PlayerID player, DespawnIdentityMessage data, bool asserver)
        {
            for (var i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i].identity == data.identityId)
                {
                    Object.Destroy(_spawnedObjects[i].gameObject);
                    _spawnedObjects.RemoveAt(i);
                    break;
                }
            }
        }

        private void PlayerJoined(PlayerID player, bool asserver)
        {
            if (_spawnedObjects.Count == 0)
                return;

            _prefabsCache.Clear();
            
            for (var i = 0; i < _spawnedObjects.Count; i++)
            {
                var spawnedObject = _spawnedObjects[i];
                _prefabsCache.Add(spawnedObject.GetSpawnMessage());
            }
                        
            var snapshot = new SpawnPrefabsSnapshot
            {
                prefabs = _prefabsCache
            };

            _broadcaster.Send(player, snapshot);
        }
        
        private void ClientBatchedSpawnEvent(PlayerID player, SpawnPrefabsSnapshot data, bool asserver)
        {
            for (var i = 0; i < data.prefabs.Count; i++)
                CreateInstanceFromSpawnMessage(data.prefabs[i]);
        }

        private void ClientSpawnEvent(PlayerID player, SpawnPrefabMessage data, bool asserver)
        {
            CreateInstanceFromSpawnMessage(data);
        }

        private void CreateInstanceFromSpawnMessage(SpawnPrefabMessage data)
        {
            if (!_prefabs.TryGetPrefab(data.prefabId, out var prefab))
            {
                PurrLogger.Throw<InvalidOperationException>(
                    $"The specified prefab id '{data.prefabId}' is not a network prefab.");
            }

            var instance = Object.Instantiate(prefab, data.position, data.rotation);
            instance.transform.localScale = data.scale;

            if (!instance.TryGetComponent<NetworkIdentity>(out _))
                instance.AddComponent<NetworkIdentity>();

            instance.GetComponentsInChildren(true, _identitiesCache);

            var identitiesCount = _identitiesCache.Count;
            int firstIdentity = data.rootIdentityId;

            if (identitiesCount != data.childrenCount)
            {
                PurrLogger.Throw<InvalidOperationException>(
                    $"The specified prefab '{prefab.name}' has a different amount of NetworkIdentities than the one sent by the server.");
            }

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
            
            instance.name += "_Server";

            var identitiesCount = _identitiesCache.Count;
            for (int i = 0; i < identitiesCount; i++)
            {
                var allocatedIdentity = AllocateNewIdentity();
                _identitiesCache[i].SetIdentity(prefabId, i, allocatedIdentity);
            }
            
            var rootIdentity = _identitiesCache[0];
            rootIdentity.onDestroy += OnSpawnedObjectGotDestroyed;
            
            _spawnedObjects.Add(rootIdentity);
            
            // Send the spawn message to all clients
            _broadcaster.SendToAll(rootIdentity.GetSpawnMessage(identitiesCount));
        }

        public void Despawn(GameObject instance)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can despawn objects.");
            
            if (!instance.TryGetComponent<NetworkIdentity>(out var identity))
                PurrLogger.Throw<InvalidOperationException>("The specified object does not have a NetworkIdentity component.");
            
            Despawn(identity);
        }
        
        public void Despawn(NetworkIdentity identity)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can despawn objects.");

            if (!identity.isValid)
                PurrLogger.Throw<InvalidOperationException>("The specified object isn't spawn, can't despawn it.");
            
            identity.onDestroy -= OnSpawnedObjectGotDestroyed;
            
            Object.Destroy(identity.gameObject);

            _spawnedObjects.Remove(identity);
            
            _broadcaster.SendToAll(new DespawnIdentityMessage
            {
                identityId = identity.identity
            });
        }

        private void OnSpawnedObjectGotDestroyed(NetworkIdentity ni)
        {
            Despawn(ni);
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
