using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public class IdentitiesCollection
    {
        readonly Dictionary<NetworkID, NetworkIdentity> _identities = new ();
        
        private ushort _nextId;
        
        public IEnumerable<NetworkIdentity> collection => _identities.Values;

        public bool TryGetIdentity(NetworkID id, out NetworkIdentity identity)
        {
            return _identities.TryGetValue(id, out identity);
        }
        
        public bool TryGetIdentity(NetworkID? id, out NetworkIdentity identity)
        {
            identity = null;
            return id.HasValue && TryGetIdentity(id.Value, out identity);
        }
        
        public void RegisterIdentity(NetworkIdentity identity)
        {
            if (identity.id.HasValue)
                _identities.Add(identity.id.Value, identity);
        }
        
        public bool UnregisterIdentity(NetworkIdentity identity)
        {
            return identity.id.HasValue && _identities.Remove(identity.id.Value);
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
                if (identity && identity.gameObject && identity.prefabId != -1)
                    Object.Destroy(identity.gameObject);
            }
            
            _identities.Clear();
        }
    }
}
