using System;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class NetworkTransform : NetworkIdentity
    {
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;

        private bool _isResettingParent;
        private bool _isFirstTransform = true;

        Interpolated<Vector3> _position;
        Interpolated<Quaternion> _rotation;
        Interpolated<Vector3> _scale;

        private Transform _trs;

        private void Awake()
        {
            _trs = transform;

            ValidateParent();

            _position = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, _trs.position);
            _rotation = new Interpolated<Quaternion>(Quaternion.Lerp, Time.fixedDeltaTime, _trs.rotation);
            _scale = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, _trs.localScale);
        }

        protected override void OnSpawned()
        {
            _isFirstTransform = true;
        }

        protected override void OnOwnerConnected(PlayerID ownerId, bool asServer)
        {
            if (asServer)
                SendLatestTranform(ownerId, _trs.position, _trs.rotation, _trs.localScale);
        }

        private void FixedUpdate()
        {
            if (isOwner)
            {
                SendTransform(_trs.position, _trs.rotation, _trs.localScale);
            }
        }
        
        private void Update()
        {
            if (!isOwner)
                ApplyLerpedPosition();
        }

        private void ApplyLerpedPosition()
        {
            _trs.SetPositionAndRotation(
                _position.Advance(Time.deltaTime),
                _rotation.Advance(Time.deltaTime)
            );

            _trs.localScale = _scale.Advance(Time.deltaTime);
        }
        
        [ServerRPC(Channel.UnreliableSequenced)]
        private void SendTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            ReceiveTransform(position, rotation, scale);
        }
        
        [ObserversRPC(Channel.UnreliableSequenced, excludeOwner: true)]
        private void ReceiveTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (_isFirstTransform)
            {
                _isFirstTransform = false;
                _position.Teleport(position);
                _rotation.Teleport(rotation);
                _scale.Teleport(scale);

                ApplyLerpedPosition();
            }
            else
            {
                _position.Add(position);
                _rotation.Add(rotation);
                _scale.Add(scale);
            }
        }

        [TargetRPC]
        private void SendLatestTranform(PlayerID player, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _position.Teleport(position);
            _rotation.Teleport(rotation);
            _scale.Teleport(scale);

            ApplyLerpedPosition();
        }

        void OnTransformParentChanged()
        {
            if (ApplicationContext.isQuitting)
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
}
