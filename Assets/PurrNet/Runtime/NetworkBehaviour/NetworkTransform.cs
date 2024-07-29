using System;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace PurrNet
{
    [Flags]
    public enum NetworkPermissions
    {
        None,
        Server = 1,
        Owner = 2,
        Everyone = 4
    }
    
    public enum NetworkTarget
    {
        None,
        Server,
        Owner
    }
    
    [Serializable]
    public struct PropertySyncSettings
    {
        public bool sync;
        public int interpolationTicks;
    }
    
    public sealed class NetworkTransform : NetworkIdentity
    {
        [FormerlySerializedAs("_syncParentFrom")]
        [Header("Permission Settings")]
        [SerializeField] private bool _syncParent = true;
        [SerializeField] private NetworkPermissions _syncParentPermissions = NetworkPermissions.None;
        [SerializeField] private NetworkTarget _syncTransformFrom = NetworkTarget.Server;
        
        [Header("Sync Settings")]
        [SerializeField] private PropertySyncSettings _transformPosition = new() { sync = true, interpolationTicks = 1 };
        [SerializeField] private PropertySyncSettings _transformRotation =  new() { sync = true, interpolationTicks = 1 };
        [SerializeField] private PropertySyncSettings _transformScale = new() { sync = false, interpolationTicks = 1 };
        
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
        
        public bool HasParentSyncAuthority(bool asServer)
        {
            if (_syncParentPermissions.HasFlag(NetworkPermissions.Everyone))
                return true;

            if (_syncParentPermissions.HasFlag(NetworkPermissions.Server) && asServer)
                return true;

            return _syncParentPermissions.HasFlag(NetworkPermissions.Owner) && owner == localPlayer;
        }
        
        public bool HasParentSyncAuthority(PlayerID playerId)
        {
            if (_syncParentPermissions.HasFlag(NetworkPermissions.Everyone))
                return true;

            return _syncParentPermissions.HasFlag(NetworkPermissions.Owner) && owner == playerId;
        }

        internal void StartIgnoreParentChanged()
        {
            _isResettingParent = true;
        }

        internal void StopIgnoreParentChanged()
        {
            _isResettingParent = false;
        }

        public bool ShouldSyncParent()
        {
            return _syncParent;
        }
    }
}
