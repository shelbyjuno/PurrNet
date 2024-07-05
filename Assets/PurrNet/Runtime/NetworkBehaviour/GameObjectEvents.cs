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
        
        static readonly List<GameObjectEvents> _children = new ();

        private void UpdateEnabled(bool updateChildren = true)
        {
            var go = gameObject;
            var enabledState = go.activeSelf;
            
            if (_lastActive != enabledState)
            {
                if (updateChildren)
                {
                    go.GetComponentsInChildren(true, _children);

                    for (var i = 0; i < _children.Count; i++)
                    {
                        var child = _children[i];
                        if (!child.gameObject.activeSelf && child.gameObject != go)
                            child.UpdateEnabled(false);
                    }
                }
                
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
