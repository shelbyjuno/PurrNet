using System;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public enum AuthorityLevel
    {
        Server,
        Owner,
        Everyone
    }

    public class NetworkIdentity : MonoBehaviour
    {
        // [SerializeField] private AuthorityLevel _spawnAuthorityLevel = AuthorityLevel.Server;

        public int prefabId { get; private set; } = -1;
        
        public int id { get; private set; } = -1;

        public bool isSpawned => id != -1;
        
        internal event Action<NetworkIdentity> onRemoved;

        protected virtual void Awake()
        {
            Hasher.PrepareType(GetType());
        }
        
        internal void SetIdentity(int pid, int identityId)
        {
            prefabId = pid;
            id = identityId;
        }

        private bool _ignoreNextDestroy;
        
        public void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        protected virtual void OnDestroy()
        {
            if (_ignoreNextDestroy)
            {
                _ignoreNextDestroy = false;
                return;
            }
            
            if (ApplicationContext.isQuitting)
                return;
            
            onRemoved?.Invoke(this);
        }
    }
}
