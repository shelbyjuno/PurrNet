using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet
{
    public class IdentitiesCollection
    {
        readonly bool _asServer;

        readonly Dictionary<NetworkID, NetworkIdentity> _identities = new ();
        
        private ushort _nextId;
        
        public ICollection<NetworkIdentity> collection => _identities.Values;

        public IdentitiesCollection(bool asServer)
        {
            _asServer = asServer;
        }

        public bool TryGetIdentity(NetworkID id, out NetworkIdentity identity)
        {
            return _identities.TryGetValue(id, out identity);
        }
        
        public bool TryGetIdentity(NetworkID? id, out NetworkIdentity identity)
        {
            identity = null;
            return id.HasValue && TryGetIdentity(id.Value, out identity);
        }
        
        public bool TryRegisterIdentity(NetworkIdentity identity)
        {
            return identity.id.HasValue && _identities.TryAdd(identity.id.Value, identity);
        }
        
        public void RegisterIdentity(NetworkIdentity identity)
        {
            if (identity.id.HasValue)
            {
                if (!_identities.TryAdd(identity.id.Value, identity))
                    PurrLogger.LogError($"Identity with id {identity.id} already exists.");
            }
        }
        
        public void RegisterIdentity(NetworkIdentity identity, NetworkID id)
        {
            _identities.TryAdd(id, identity);
        }
        
        public bool UnregisterIdentity(NetworkIdentity identity)
        {
            return identity.id.HasValue && _identities.Remove(identity.id.Value);
        }
        
        public bool UnregisterIdentity(NetworkID id)
        {
            return _identities.Remove(id);
        }

        public void SkipIds(ushort count)
        {
            _nextId += count;
        }
        
        public ushort GetNextId()
        {
            return _nextId++;
        }
        
        public ushort PeekNextId()
        {
            return _nextId;
        }

        public void DestroyAllNonSceneObjects()
        {
            foreach (var identity in _identities.Values)
            {
                identity.TriggerDespawnEvent(_asServer);
                
                if (identity && identity.gameObject && !identity.isSceneObject)
                {
                    identity.IgnoreNextDestroyCallback();
                    Object.Destroy(identity.gameObject);
                }
            }
            
            _identities.Clear();
        }

        public bool HasIdentity(NetworkID nid)
        {
            return _identities.ContainsKey(nid);
        }
    }
}
