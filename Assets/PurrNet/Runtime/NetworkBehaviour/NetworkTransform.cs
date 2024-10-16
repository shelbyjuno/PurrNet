using System;
using JetBrains.Annotations;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace PurrNet
{
    [Flags]
    [Serializable]
    public enum TransformSyncMode : byte
    {
        None,
        Position = 1,
        Rotation = 2,
        Scale = 4
    }
    
    public sealed class NetworkTransform : NetworkIdentity, ITick
    {
        [SerializeField, PurrLock] private TransformSyncMode _syncSettings = 
            TransformSyncMode.Position | TransformSyncMode.Rotation | TransformSyncMode.Scale;
        
        [FormerlySerializedAs("_clientAuth")]
        
        [Tooltip("If true, the client can send transform data to the server. If false, the client can't send transform data to the server.")]
        [SerializeField, PurrLock] private bool _ownerAuth = true;
        
        [Tooltip("The interval in ticks to send the transform data. 0 means send every tick.")]
        [SerializeField, Min(0), PurrLock] private int _sendIntervalInTicks;

        public bool syncPosition => _syncSettings.HasFlag(TransformSyncMode.Position);
        
        public bool syncRotation => _syncSettings.HasFlag(TransformSyncMode.Rotation);
        
        public bool syncScale => _syncSettings.HasFlag(TransformSyncMode.Scale);
        
        public bool ownerAuth => _ownerAuth;

        public int sendIntervalInTicks
        {
            get => _sendIntervalInTicks;
            set => _sendIntervalInTicks = value;
        }
        
        Transform _lastValidParent;
        
        internal event Action<NetworkTransform> onParentChanged;

        private bool _isResettingParent;
        private bool _isFirstTransform = true;

        Interpolated<Vector3> _position;
        Interpolated<Quaternion> _rotation;
        Interpolated<Vector3> _scale;

        private Transform _trs;
        private Rigidbody _rb;
        private CharacterController _controller;
        
        private bool _prevWasController;

        private new bool isController => hasConnectedOwner ? (isOwner && _ownerAuth) || (!_ownerAuth && isServer) : isServer;

        private void Awake()
        {
            _trs = transform;
            _rb = GetComponent<Rigidbody>();
            _controller = GetComponent<CharacterController>();

            ValidateParent();
            
            float sendDelta = (_sendIntervalInTicks + 1) * Time.fixedDeltaTime;

            if (syncPosition)
                _position = new Interpolated<Vector3>(Vector3.Lerp, sendDelta, _trs.position);
            
            if (syncRotation)
                _rotation = new Interpolated<Quaternion>(Quaternion.Lerp, sendDelta, _trs.rotation);
            
            if (syncScale)
                _scale = new Interpolated<Vector3>(Vector3.Lerp, sendDelta, _trs.localScale);
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
        
        private int _ticksSinceLastSend;

        public void OnTick(float delta)
        {
            if (isController)
            {
                if (_ticksSinceLastSend >= _sendIntervalInTicks)
                {
                    _ticksSinceLastSend = 0;
                    
                    if (isServer)
                        SendToAll(GetCurrentTransformData());
                    else SendTransformToServer(GetCurrentTransformData());
                }
                else
                {
                    _ticksSinceLastSend++;
                }
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
            bool disableController = _controller && _controller.enabled;
            
            if (disableController)
                _controller.enabled = false;
            
            if (syncPosition)
                _trs.position = _position.Advance(Time.deltaTime);
            
            if (syncRotation)
                _trs.rotation = _rotation.Advance(Time.deltaTime);
            
            if (syncScale)
                _trs.localScale = _scale.Advance(Time.deltaTime);
            
            if (disableController)
                _controller.enabled = true;
        }

        private TransformData GetCurrentTransformData()
        {
            return new TransformData(_trs.position, _trs.rotation, _trs.localScale);
        }
        
        [ServerRPC(Channel.UnreliableSequenced, requireOwnership: true)]
        private void SendTransformToServer(TransformData data)
        {
            // If clientAuth is disabled, the client can't send transform data to the server
            if (!_ownerAuth) return;
            
            // Apply the transform data to the server
            ReceiveTransform_Internal(data);
            
            // Send the transform data to others expect the owner
            SendToOthers(data);
        }
        
        [ObserversRPC(Channel.UnreliableSequenced, excludeOwner: true)]
        private void SendToOthers(TransformData data)
        {
            if (isHost) return;
            
            ReceiveTransform_Internal(data);
        }

        [ObserversRPC(Channel.UnreliableSequenced)]
        private void SendToAll(TransformData data)
        {
            if (isHost) return;

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
            if (syncPosition)
            {
                if (teleport) _position.Teleport(data.position);
                else _position.Add(data.position);
            }

            if (syncRotation)
            {
                if (teleport) _rotation.Teleport(data.rotation);
                else _rotation.Add(data.rotation);
            }

            if (syncScale)
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