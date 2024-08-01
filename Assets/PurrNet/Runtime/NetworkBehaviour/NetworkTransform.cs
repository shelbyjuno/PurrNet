using System;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class NetworkTransform : NetworkIdentity
    {
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;
        private bool _isResettingParent;
        
        void Awake()
        {
            ValidateParent();
        }

        void OnTransformParentChanged()
        {
            if (ApplicationContext.isQuitting)
                return;
            
            if (!_isResettingParent && _lastValidParent != transform.parent)
                onParentChanged?.Invoke(this);
        }

        internal void ValidateParent()
        {
            _lastValidParent = transform.parent;
        }
        
        internal void ResetToLastValidParent()
        {
            StartIgnoreParentChanged();
            transform.SetParent(_lastValidParent, true);
            StopIgnoreParentChanged();
        }

        internal void StartIgnoreParentChanged()
        {
            _isResettingParent = true;
        }

        internal void StopIgnoreParentChanged()
        {
            _isResettingParent = false;
        }
    }
}
