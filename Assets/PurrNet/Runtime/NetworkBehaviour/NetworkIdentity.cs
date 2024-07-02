using System;
using PurrNet.Modules;
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
        [SerializeField] private AuthorityLevel _spawnAuthorityLevel = AuthorityLevel.Server;

        public int prefabId { get; private set; } = -1;

        public int id { get; private set; } = -1;
        
        public bool isSpawned => id != -1;

        public bool isValid => id != -1;
        
        internal event Action<NetworkIdentity> onDestroy;
        internal event Action<NetworkIdentity> onRemoved;

        private PurrEventsListener _events;
        
        private void Reset()
        {
            Debug.Log("Todo");
        }

        protected virtual void Awake()
        {
            Hasher.PrepareType(GetType());
            
            if (!gameObject.TryGetComponent(out _events))
            {
                _events = gameObject.AddComponent<PurrEventsListener>();
                _events.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            }

            _events.onDestroy += OnDestroyedGameObject;
        }
        
        public bool HasSpawningAuthority(PlayerID playerId)
        {
            switch (_spawnAuthorityLevel)
            {
                case AuthorityLevel.Server:
                    return true;
                case AuthorityLevel.Owner:
                    return true; // TODO: Ownership
                case AuthorityLevel.Everyone:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public bool HasSpawningAuthority(bool asServer)
        {
            switch (_spawnAuthorityLevel)
            {
                case AuthorityLevel.Server: 
                    return asServer;
                case AuthorityLevel.Owner:
                    return false; // TODO: Ownership
                case AuthorityLevel.Everyone:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal SpawnPrefabMessage GetSpawnMessage()
        {
            var trs = transform;
            
            return new SpawnPrefabMessage
            {
                prefabId = prefabId,
                rootIdentityId = id,
                position = trs.position,
                rotation = trs.rotation,
                scale = trs.localScale
            };
        }
        
        internal void SetIdentity(int pid, int identityId)
        {
            prefabId = pid;
            id = identityId;
        }
        
        private bool _isGameObjectDestroyed;

        private void OnDestroyedGameObject()
        {
            _isGameObjectDestroyed = true;
            onDestroy?.Invoke(this);
        }
        
        protected virtual void OnDestroy()
        {
            if (_isGameObjectDestroyed)
                return;
            
            onRemoved?.Invoke(this);
        }
    }
}
