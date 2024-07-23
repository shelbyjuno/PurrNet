using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public class IdentitiesCollection
    {
        readonly Dictionary<int, NetworkIdentity> _identities = new ();
        
        private int _nextId;
        
        public IEnumerable<NetworkIdentity> collection => _identities.Values;

        public bool TryGetIdentity(int id, out NetworkIdentity identity)
        {
            return _identities.TryGetValue(id, out identity);
        }
        
        public void RegisterIdentity(NetworkIdentity identity)
        {
            _identities.Add(identity.id, identity);
        }
        
        public bool UnregisterIdentity(NetworkIdentity identity)
        {
            return _identities.Remove(identity.id);
        }

        public int GetNextId()
        {
            return _nextId++;
        }
        
        public int PeekNextId()
        {
            return _nextId;
        }

        public void DestroyAll()
        {
            foreach (var identity in _identities.Values)
            {
                if (identity && identity.gameObject)
                    Object.Destroy(identity.gameObject);
            }
            
            _identities.Clear();
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
