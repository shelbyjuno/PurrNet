using System;
using JetBrains.Annotations;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public sealed class NetworkTransform : NetworkIdentity
    {
        [SerializeField] private bool clientAuth = true;
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [SerializeField] private bool syncScale = true;
        
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;

        private bool _isResettingParent;
        private bool _isFirstTransform = true;

        Interpolated<Vector3> _position;
        Interpolated<Quaternion> _rotation;
        Interpolated<Vector3> _scale;

        private Transform _trs;
        private Rigidbody _rb;

        private bool _isController => hasConnectedOwner ? isOwner && clientAuth || !clientAuth && isServer : isServer;

        private void Awake()
        {
            _trs = transform;
            _rb = GetComponent<Rigidbody>();

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
                SendLatestTransform(ownerId);
        }

        private void FixedUpdate()
        {
            if (_isController)
            {
                if (isServer)
                {
                    if (_isController)
                        ReceiveTransformServerAuth(GetCurrentTransformData());
                    else
                        ReceiveTransform(GetCurrentTransformData());
                }
                else SendTransform(GetCurrentTransformData());
            }
        }
        
        private void Update()
        {
            if (!_isController)
            {
                ApplyLerpedPosition();
            }
        }

        private void ApplyLerpedPosition()
        {
            if (syncPosition)
                _trs.position = _position.Advance(Time.deltaTime);
            
            if (syncRotation)
                _trs.rotation = _rotation.Advance(Time.deltaTime);
            
            if (syncScale)
                _trs.localScale = _scale.Advance(Time.deltaTime);
        }

        private TransformData GetCurrentTransformData()
        {
            return new TransformData
            {
                Position = syncPosition ? _trs.position : null,
                Rotation = syncRotation ? _trs.rotation : null,
                Scale = syncScale ? _trs.localScale : null
            };
        }
        
        [ServerRPC(Channel.UnreliableSequenced)]
        private void SendTransform(TransformData data)
        {
            if (_isController)
                ReceiveTransformServerAuth(data);
            else
                ReceiveTransform(data);
        }
        
        [ObserversRPC(Channel.UnreliableSequenced, excludeOwner: true)]
        private void ReceiveTransform(TransformData data)
        {
            ReceiveTransform_Internal(data);
        }
        
        [ObserversRPC(Channel.UnreliableSequenced)]
        private void ReceiveTransformServerAuth(TransformData data)
        {
            ReceiveTransform_Internal(data);
        }
        
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
            if (data.Position.HasValue)
            {
                if (teleport) _position.Teleport(data.Position.Value);
                else _position.Add(data.Position.Value);
            }

            if (data.Rotation.HasValue)
            {
                if (teleport) _rotation.Teleport(data.Rotation.Value);
                else _rotation.Add(data.Rotation.Value);
            }

            if (data.Scale.HasValue)
            {
                if (teleport) _scale.Teleport(data.Scale.Value);
                else _scale.Add(data.Scale.Value);
            }
        }

        [TargetRPC]
        private void SendLatestTransform([UsedImplicitly] PlayerID player)
        {
            ApplyTransformData(GetCurrentTransformData(), true);
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

    [Serializable]
    public struct TransformData
    {
        public Vector3? Position;
        public Quaternion? Rotation;
        public Vector3? Scale;
    }
}