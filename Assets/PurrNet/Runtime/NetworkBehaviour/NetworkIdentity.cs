using System;
using PurrNet.Modules;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    [DefaultExecutionOrder(-1000)]
    public class NetworkIdentity : MonoBehaviour
    {
        /// <summary>
        /// The prefab id of this object;
        /// Is -1 if scene object.
        /// </summary>
        public int prefabId { get; private set; } = -1;
        
        /// <summary>
        /// Network id of this object.
        /// </summary>
        public int id { get; private set; } = -1;
        
        /// <summary>
        /// Scene id of this object.
        /// </summary>
        public SceneID sceneId { get; private set; }

        internal PlayerID? internalOwner;
        
        /// <summary>
        /// Returns the owner of this object.
        /// It will return the owner of the closest parent object.
        /// If you can, cache this value for performance.
        /// </summary>
        public PlayerID? owner
        {
            get
            {
                if (internalOwner.HasValue)
                    return internalOwner;

                GetComponents(HierarchyScene.CACHE);
                
                for (int i = 0; i < HierarchyScene.CACHE.Count; i++)
                {
                    var sibling = HierarchyScene.CACHE[i];
                    
                    if (!sibling.propagateOwner) continue;
                    
                    if (sibling.internalOwner.HasValue)
                        return sibling.internalOwner;
                }

                var parentTrs = transform.parent;
                
                if (!parentTrs)
                    return null;

                var parent = GetParentThatPropagatesOwner();
                return parent ? parent.owner : null;
            }
        }
        
        private NetworkIdentity GetParentThatPropagatesOwner()
        {
            var parentTrs = transform.parent;
                
            if (!parentTrs)
                return null;

            var parent = parentTrs.GetComponentInParent<NetworkIdentity>(true);
            return parent.propagateOwner ? parent : parent.GetParentThatPropagatesOwner();
        }
        
        public NetworkManager networkManager { get; private set; }

        /// <summary>
        /// True if the owner is propagated to children automatically.
        /// False if the owner is only set to this identity.
        /// </summary>
        [Header("Ownership Settings")]
        [Tooltip("True if the owner is propagated to children automatically.\nFalse if the owner is only set to this identity.")]
        [SerializeField] private bool propagateOwner = true;

        /// <summary>
        /// True if this object is spawned and has valid id.
        /// </summary>
        public bool isSpawned => id != -1;

        internal event Action<NetworkIdentity> onRemoved;
        internal event Action<NetworkIdentity, bool> onEnabledChanged;
        internal event Action<NetworkIdentity, bool> onActivatedChanged;
        
        private bool _lastEnabledState;
        private GameObjectEvents _events;
        private GameObject _gameObject;

        protected virtual void OnSpawned(bool asServer) { }
        
        protected virtual void OnDespawned(bool asServer) { }
        
        protected virtual void OnSpawned() { }
        
        protected virtual void OnDespawned() { }

        private void OnActivated(bool active)
        {
            if (_ignoreNextActivation)
            {
                _ignoreNextActivation = false;
                return;
            }
            
            onActivatedChanged?.Invoke(this, active);
        }

        public virtual void OnEnable()
        {
            UpdateEnabledState();
        }

        internal void UpdateEnabledState()
        {
            if (_lastEnabledState != enabled)
            {
                if (_ignoreNextEnable)
                     _ignoreNextEnable = false;
                else onEnabledChanged?.Invoke(this, enabled);

                _lastEnabledState = enabled;
            }
        }

        public virtual void OnDisable()
        {
            UpdateEnabledState();
        }

        internal void SetIdentity(NetworkManager manager, SceneID scene, int pid, int identityId, bool asServer)
        {
            Hasher.PrepareType(GetType());

            sceneId = scene;
            prefabId = pid;
            id = identityId;
            
            internalOwner = null;
            _lastEnabledState = enabled;
            _gameObject = gameObject;

            if (!_gameObject.TryGetComponent(out _events))
            {
                _events = _gameObject.AddComponent<GameObjectEvents>();
                _events.InternalAwake();
                _events.hideFlags = HideFlags.HideInInspector;
                _events.onActivatedChanged += OnActivated;
                _events.Register(this);
            }
            
            networkManager = manager;

            if (manager.isHost)
            {
                if (asServer)
                {
                    OnSpawned(true);
                    OnSpawned(false);
                    OnSpawned();
                }
            }
            else
            {
                OnSpawned(asServer);
                OnSpawned();
            }
        }

        private bool _ignoreNextDestroy;
        
        public void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        public void GiveOwnership(PlayerID player)
        {
            if (networkManager.TryGetModule(networkManager.isServer, out GlobalOwnershipModule module))
                module.GiveOwnership(this, player);
        }
        
        public void RemoveOwnership()
        {
            if (networkManager.TryGetModule(networkManager.isServer, out GlobalOwnershipModule module))
                module.RemoveOwnership(this);
        }
        
        protected virtual void OnDestroy()
        {
            if (_events)
                _events.Unregister(this);
            
            if (_ignoreNextDestroy)
            {
                _ignoreNextDestroy = false;
                return;
            }
            
            if (ApplicationContext.isQuitting)
                return;

            if (isSpawned)
            {
                if (networkManager.isHost)
                {
                    OnDespawned(true);
                    OnDespawned(false);
                    OnDespawned();
                }
                else
                {
                    OnDespawned(networkManager.isServer);
                    OnDespawned();
                }
            }
            
            onRemoved?.Invoke(this);
        }
        
        private bool _ignoreNextActivation;
        private bool _ignoreNextEnable;

        internal void IgnoreNextActivationCallback()
        {
            _ignoreNextActivation = true;
        }
        
        internal void IgnoreNextEnableCallback()
        {
            _ignoreNextEnable = true;
        }
    }
}
