using System;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public class NetworkIdentity : MonoBehaviour
    {
        public int prefabId { get; private set; } = -1;
        
        public int id { get; private set; } = -1;

        public bool isSpawned => id != -1;
        
        private bool _lastEnabledState;
        
        internal event Action<NetworkIdentity> onRemoved;
        internal event Action<NetworkIdentity, bool> onEnabledChanged;
        internal event Action<NetworkIdentity, bool> onActivatedChanged;
        
        private GameObjectEvents _events;

        void InternalAwake()
        {
            Hasher.PrepareType(GetType());
            _lastEnabledState = enabled;

            if (!gameObject.TryGetComponent(out _events))
            {
                _events = gameObject.AddComponent<GameObjectEvents>();
                _events.InternalAwake();
                _events.hideFlags = HideFlags.HideInInspector;
                _events.onActivatedChanged += OnActivated;
                _events.Register(this);
            }
        }

        private void OnActivated(bool active)
        {
            if (_ignoreNextActivation)
            {
                _ignoreNextActivation = false;
                return;
            }
            
            onActivatedChanged?.Invoke(this, active);
        }

        private void OnEnable()
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

        private void OnDisable()
        {
            UpdateEnabledState();
        }

        internal void SetIdentity(int pid, int identityId)
        {
            prefabId = pid;
            id = identityId;
            
            InternalAwake();
        }

        private bool _ignoreNextDestroy;
        
        public void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        protected virtual void OnDestroy()
        {
            _events.Unregister(this);
            
            if (_ignoreNextDestroy)
            {
                _ignoreNextDestroy = false;
                return;
            }
            
            if (ApplicationContext.isQuitting)
                return;
            
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
