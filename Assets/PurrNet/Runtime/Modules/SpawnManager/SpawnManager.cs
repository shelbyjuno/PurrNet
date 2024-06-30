using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using UnityEngine;

namespace PurrNet.Modules
{
    internal partial struct SpawnPrefabMessage : IAutoNetworkedData
    {
        public int prefabId { get; set; }
        
        public List<int> identities { get; set; }
    }
    
    public class SpawnManager : INetworkModule
    {
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersBroadcaster _broadcaster;
        private readonly List<NetworkIdentity> _identitiesCache = new ();
        
        private bool _asServer;
        private int _nextIdentity;
        
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
            Debug.Log($"Client received spawn event from {player} for prefab {data.prefabId} with {data.identities.Count} identities.");
        }

        public void Disable(bool asServer) { }
        
        private int AllocateNewIdentity()
        {
            return _nextIdentity++;
        }
        
        private readonly List<int> _tempIdentities = new ();

        public void Spawn(GameObject gameObject)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can spawn objects.");

            if (!_prefabs.TryGetPrefabId(gameObject, out var prefabId))
                PurrLogger.Throw<InvalidOperationException>($"The specified object '{gameObject.name}' is not a network prefab.");

            _tempIdentities.Clear();
            
            // Check if the object already has a NetworkIdentity if not add one
            if (!gameObject.TryGetComponent<NetworkIdentity>(out _))
                gameObject.AddComponent<NetworkIdentity>();
            
            // Get all NetworkIdentities in the object
            gameObject.GetComponentsInChildren(true, _identitiesCache);
            
            for (int i = 0; i < _identitiesCache.Count; i++)
            {
                var identity = _identitiesCache[i];
                var allocatedIdentity = AllocateNewIdentity();
                
                identity.SetIdentity(allocatedIdentity);
                _tempIdentities.Add(allocatedIdentity);
            }
            
            _broadcaster.SendToAll(new SpawnPrefabMessage
            {
                prefabId = prefabId,
                identities = _tempIdentities
            });
        }
    }
}
