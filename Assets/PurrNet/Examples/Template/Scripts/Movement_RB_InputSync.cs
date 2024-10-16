using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet.Examples.Template
{
    public class Movement_RB_InputSync : NetworkBehaviour, ITick
    {
        [Header("Settings")]
        [SerializeField] private float moveForce = 5f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float jumpForce = 20f;
        [SerializeField] private float visualRotationSpeed = 10f;
        
        [Space(10)]
        [Header("Ground check")]
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private float groundCheckRadius = 0.5f;
        [SerializeField] private LayerMask groundMask;
        
        private Rigidbody _rigidbody;
        
        //Client variable
        private Vector2 _lastInput;
        private readonly SyncVar<Quaternion> _targetRotation = new();
        
        //Server variable
        private Vector2 _serverInput;
        
        private void Awake()
        {
            if (!TryGetComponent(out _rigidbody))
                PurrLogger.LogError($"Movement_RB_InputSync could not get rigidbody!", this);
        }

        private void Update()
        {
            if (isOwner && Input.GetKeyDown(KeyCode.Space))
                Jump();
        }

        private void HandleLocalRotation()
        {
            var rotationVector = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
            if (rotationVector == Vector3.zero)
                return;
            var targetRotation = Quaternion.LookRotation(rotationVector, Vector3.up);
            _rigidbody.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * visualRotationSpeed);
        }

        public void OnTick(float delta)
        {
            if (isServer)
                ServerTick();

            if (isOwner)
            {
                OwnerTick();
                HandleLocalRotation();
            }
            else
            {
                var normalized = _targetRotation.value.normalized;
                _rigidbody.rotation = Quaternion.Slerp(transform.rotation, normalized, delta * visualRotationSpeed);
            }
        }

        private void ServerTick()
        {
            if (_serverInput.magnitude > 1)
                _serverInput.Normalize();
            var force = new Vector3(_serverInput.x, 0, _serverInput.y);
            _rigidbody.AddForce(force * moveForce);

#if UNITY_6000_0_OR_NEWER
            Vector3 velocity = _rigidbody.linearVelocity;
#else
            Vector3 velocity = _rigidbody.velocity;
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

            var lookVector = new Vector3(velocity.x, 0, velocity.z);
            if(lookVector != Vector3.zero)
                _targetRotation.value = Quaternion.LookRotation(lookVector.normalized, Vector3.up);
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

        [ServerRPC]
        private void Jump()
        {
            if(IsGrounded())
                _rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private static Collider[] _groundCheckResults = new Collider[30];
        private bool IsGrounded()
        {
            var position = transform.position - Vector3.up * groundCheckDistance;
            var count = Physics.OverlapSphereNonAlloc(position, groundCheckRadius, _groundCheckResults, groundMask);
            for (int i = 0; i < count; i++)
            {
                if (_groundCheckResults[i].gameObject != gameObject)
                    return true;
            }

            return false;
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position - Vector3.up * groundCheckDistance, groundCheckRadius);
        }
    }
}