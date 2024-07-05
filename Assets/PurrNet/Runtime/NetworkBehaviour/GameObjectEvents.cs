using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    internal class GameObjectEvents : MonoBehaviour
    {
        private bool _lastActive;
        
        internal event Action<bool> onActivatedChanged;
        
        readonly List<NetworkIdentity> _siblings = new ();
        
        public void Register(NetworkIdentity identity)
        {
            _siblings.Add(identity);
        }
        
        public void Unregister(NetworkIdentity identity)
        {
            _siblings.Remove(identity);
        }
        
        internal void InternalAwake()
        {
            _lastActive = gameObject.activeSelf;
        }

        private void UpdateEnabled()
        {
            var enabledState = gameObject.activeSelf;
            
            if (_lastActive != enabledState)
            {
                onActivatedChanged?.Invoke(enabledState);
                _lastActive = enabledState;
            }

            for (var i = 0; i < _siblings.Count; i++)
            {
                var sibling = _siblings[i];
                sibling.UpdateEnabledState();
            }
        }

        private void OnEnable()
        {
            UpdateEnabled();
        }

        private void OnDisable()
        {
            UpdateEnabled();
        }
    }
}
