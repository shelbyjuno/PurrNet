
using System;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet.Examples.Template
{
    public class Movement_RB_InputSync : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float moveForce = 5f;
        [SerializeField] private float maxSpeed = 5f;
        
        private Rigidbody _rigidbody;
        
        //Client variable
        private Vector2 _lastInput;

        //Server variable
        private Vector2 _serverInput;
        
        private void Awake()
        {
            if (!TryGetComponent(out _rigidbody))
                PurrLogger.LogError($"Movement_RB_InputSync could not get rigidbody!", this);
        }

        protected override void OnSpawned(bool asServer)
        {
            if (isOwner || isServer)
            {
                networkManager.GetModule<TickManager>(isServer).onTick += OnTick;
            }
            
            _rigidbody.isKinematic = !isServer;
            Debug.Log($"Kinematic: {_rigidbody.isKinematic}");
        }

        protected override void OnDespawned()
        {
            if(networkManager.TryGetModule(out TickManager tickManager, isServer))
                tickManager.onTick -= OnTick;
        }

        private void OnTick()
        {
            if (isOwner)
                OwnerTick();

            if (isServer)
                ServerTick();
        }

        
        private void ServerTick()
        {
            if(_serverInput.magnitude > 1)
                _serverInput.Normalize();
            var force = new Vector3(_serverInput.x, 0, _serverInput.y);
            _rigidbody.AddForce(force * moveForce);

            Vector3 velocity;
            
#if UNITY_6000_0_OR_NEWER
            velocity = _rigidbody.linearVelocity;
#else
            velocity = _rigidbody.velocity;
#endif
            
            var magnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            if (magnitude > maxSpeed)
            {
                var clamped = velocity.normalized * maxSpeed;
                 
#if UNITY_6000_0_OR_NEWER
                _rigidbody.linearVelocity = new Vector3(clamped.x, velocity.y, clamped.z);
#else
                _rigidbody.velocity = new Vector3(clamped.x, velocity.y, clamped.z);
#endif
            }
        }

        private void OwnerTick()
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (input == _lastInput)
                return;

            _lastInput = input;
            SendInput(input);
        }

        [ServerRPC]
        private void SendInput(Vector2 input)
        {
            _serverInput = input;
        }
    }
}