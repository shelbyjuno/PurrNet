using System;
using UnityEngine;

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
    
    public class NetworkTransform : NetworkIdentity
    {
        [Header("Permission Settings")]
        [SerializeField] private NetworkPermissions _syncParentFrom = NetworkPermissions.None;
        [SerializeField] private NetworkTarget _syncTransformFrom = NetworkTarget.Server;
        
        [Header("Sync Settings")]
        [SerializeField] private PropertySyncSettings _transformPosition = new() { sync = true, interpolationTicks = 1 };
        [SerializeField] private PropertySyncSettings _transformRotation =  new() { sync = true, interpolationTicks = 1 };
        [SerializeField] private PropertySyncSettings _transformScale = new() { sync = false, interpolationTicks = 1 };
        
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;
        private bool _isResettingParent;
        
        public int parentId
        {
            get
            {
                var parent = transform.parent;
                
                if (!parent)
                    return -1;
                
                var parentIdentity = parent.GetComponent<NetworkIdentity>();
                return parentIdentity ? parentIdentity.id : -1;
            }
        }

        protected virtual void Awake()
        {
            ValidateParent();
        }

        protected virtual void OnTransformParentChanged()
        {
            if (!_isResettingParent)
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
            if (_syncParentFrom.HasFlag(NetworkPermissions.Everyone))
                return true;
            
            return _syncParentFrom.HasFlag(NetworkPermissions.Server) && asServer;
        }
        
        public bool HasParentSyncAuthority(PlayerID playerId)
        {
            if (_syncParentFrom.HasFlag(NetworkPermissions.Everyone))
                return true;
            
            /*if (_syncParentFrom.HasFlag(NetworkPermissions.Owner) && playersManagerLocalPlayerId == PlayerID.LocalPlayer)
                return true;*/

            return false;
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
