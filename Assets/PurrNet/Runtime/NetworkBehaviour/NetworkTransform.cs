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

        private void Awake()
        {
            ValidateParent();

            var trs = transform;

            _position = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, trs.position);
            _rotation = new Interpolated<Quaternion>(Quaternion.Lerp, Time.fixedDeltaTime, trs.rotation);
            _scale = new Interpolated<Vector3>(Vector3.Lerp, Time.fixedDeltaTime, trs.localScale);
        }

        protected override void OnSpawned()
        {
            _isFirstTransform = true;
        }
        
        private void FixedUpdate()
        {
            if (!isSpawned)
                return;
            
            if (isOwner)
            {
                var trs = transform;
                SendTransform(trs.position, trs.rotation, trs.localScale);
            }
        }
        
        private void Update()
        {
            if (!isSpawned)
                return;
            
            if (!isOwner)
            {
                var trs = transform;
                trs.position = _position.Advance(Time.deltaTime);
                trs.rotation = _rotation.Advance(Time.deltaTime);
                trs.localScale = _scale.Advance(Time.deltaTime);
            }
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
            }
            else
            {
                _position.Add(position);
                _rotation.Add(rotation);
                _scale.Add(scale);
            }
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
