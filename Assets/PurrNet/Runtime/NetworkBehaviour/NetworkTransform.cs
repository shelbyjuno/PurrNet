using System;
using JetBrains.Annotations;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class NetworkTransform : NetworkIdentity
    {
        [SerializeField, PurrLock] private bool _clientAuth = true;
        [SerializeField, PurrLock] private bool _syncPosition = true;
        [SerializeField, PurrLock] private bool _syncRotation = true;
        [SerializeField, PurrLock] private bool _syncScale = true;
        
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;

        private bool _isResettingParent;
        private bool _isFirstTransform = true;

        Interpolated<Vector3> _position;
        Interpolated<Quaternion> _rotation;
        Interpolated<Vector3> _scale;

        private Transform _trs;
        private Rigidbody _rb;
        
        private bool _prevWasController;

        private bool isController => hasConnectedOwner ? (isOwner && _clientAuth) || (!_clientAuth && isServer) : isServer;

        private void Awake()
        {
            _trs = transform;
            _rb = GetComponent<Rigidbody>();

            ValidateParent();

            if (_syncPosition)
                _position = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, _trs.position);
            
            if (_syncRotation)
                _rotation = new Interpolated<Quaternion>(Quaternion.Lerp, Time.fixedDeltaTime, _trs.rotation);
            
            if (_syncScale)
                _scale = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, _trs.localScale);
        }

        protected override void OnSpawned()
        {
            _isFirstTransform = true;
        }

        protected override void OnOwnerConnected(PlayerID ownerId, bool asServer)
        {
            if (asServer)
                SendLatestTransform(ownerId, GetCurrentTransformData());
        }

        private void FixedUpdate()
        {
            if (isController)
            {
                if (isServer)
                     SendToAll(GetCurrentTransformData());
                else SendTransformToServer(GetCurrentTransformData());
            }
            else if (_rb) _rb.Sleep();
        }
        
        private void Update()
        {
            if (!isController)
            {
                ApplyLerpedPosition();
            }
            
            if (_prevWasController != isController)
            {
                if (isController && _rb)
                    _rb.WakeUp();
                
                _prevWasController = isController;
            }
        }

        private void ApplyLerpedPosition()
        {
            if (_syncPosition)
                _trs.position = _position.Advance(Time.deltaTime);
            
            if (_syncRotation)
                _trs.rotation = _rotation.Advance(Time.deltaTime);
            
            if (_syncScale)
                _trs.localScale = _scale.Advance(Time.deltaTime);
        }

        private TransformData GetCurrentTransformData()
        {
            return new TransformData(_trs.position, _trs.rotation, _trs.localScale);
        }
        
        [ServerRPC(Channel.UnreliableSequenced, requireOwnership: true)]
        private void SendTransformToServer(TransformData data)
        {
            // If clientAuth is disabled, the client can't send transform data to the server
            if (!_clientAuth) return;
            
            // Apply the transform data to the server
            ReceiveTransform_Internal(data);
            
            // Send the transform data to others expect the owner
            SendToOthers(data);
        }
        
        [ObserversRPC(Channel.UnreliableSequenced, excludeOwner: true)]
        private void SendToOthers(TransformData data) => ReceiveTransform_Internal(data);

        [ObserversRPC(Channel.UnreliableSequenced)]
        private void SendToAll(TransformData data) => ReceiveTransform_Internal(data);

        private void ReceiveTransform_Internal(TransformData data)
        {
            if (_isFirstTransform)
            {
                _isFirstTransform = false;
                ApplyTransformData(data, true);
                ApplyLerpedPosition();
            }
            else
            {
                ApplyTransformData(data, false);
            }
        }

        private void ApplyTransformData(TransformData data, bool teleport)
        {
            if (_syncPosition)
            {
                if (teleport) _position.Teleport(data.position);
                else _position.Add(data.position);
            }

            if (_syncRotation)
            {
                if (teleport) _rotation.Teleport(data.rotation);
                else _rotation.Add(data.rotation);
            }

            if (_syncScale)
            {
                if (teleport) _scale.Teleport(data.scale);
                else _scale.Add(data.scale);
            }
        }

        [TargetRPC]
        private void SendLatestTransform([UsedImplicitly] PlayerID player, TransformData data)
        {
            ApplyTransformData(data, true);
            ApplyLerpedPosition();
        }

        void OnTransformParentChanged()
        {
            if (ApplicationContext.isQuitting)
                return;
            
            if (!_trs)
                return;
            
            if (!_isResettingParent && _lastValidParent != _trs.parent)
                onParentChanged?.Invoke(this);
        }

        internal void ValidateParent()
        {
            _lastValidParent = _trs.parent;
        }
        
        internal void ResetToLastValidParent()
        {
            StartIgnoreParentChanged();
            _trs.SetParent(_lastValidParent, true);
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

    public struct TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        
        public TransformData(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}