using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Examples.Sumo
{
    public class Movement_RB_InputSync : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float moveForce = 5f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float jumpForce = 20f;
        [SerializeField] private float visualRotationSpeed = 10f;

        [Space(10)] 
        [Header("Collision")] 
        [SerializeField] private float playerCollisionForce = 10;
        
        [Space(10)]
        [Header("Ground check")]
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private float groundCheckRadius = 0.5f;
        [SerializeField] private LayerMask groundMask;
        
        private Rigidbody _rigidbody;
        private float _originalDrag;
        
        //Client variable
        private Vector2 _lastInput;
        private readonly SyncVar<Quaternion> _targetRotation = new();
        
        //Server variable
        private Vector2 _serverInput;
        
        private void Awake()
        {
            if (!TryGetComponent(out _rigidbody))
                PurrLogger.LogError($"Movement_RB_InputSync could not get rigidbody!", this);

            if (_rigidbody)
            {
#if UNITY_6000_0_OR_NEWER
                _originalDrag = _rigidbody.linearDamping;
#else
                _originalDrag = _rigidbody.drag;
#endif
            }
        }

        private void Update()
        {
            if (isOwner && Input.GetKeyDown(KeyCode.Space))
                Jump();
        }

        private void HandleLocalRotation(float delta)
        {
            var rotationVector = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
            if (rotationVector == Vector3.zero)
                return;
            var targetRotation = Quaternion.LookRotation(rotationVector, Vector3.up);
            _rigidbody.rotation = Quaternion.Slerp(transform.rotation, targetRotation, delta * visualRotationSpeed);
        }

        protected override void OnTick(float delta, bool asServer)
        {
            if (isOwner)
            {
                HandleLocalRotation(delta);
            }
            else
            {
                _rigidbody.rotation = Quaternion.Slerp(transform.rotation, _targetRotation.value.normalized, delta * visualRotationSpeed);
            }
            
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

            Vector3 velocity;
            
#if UNITY_6000_0_OR_NEWER
            velocity = _rigidbody.linearVelocity;
#else
            velocity = _rigidbody.velocity;
#endif
            
            var magnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            if (magnitude < maxSpeed)
                _rigidbody.AddForce(force * moveForce);

            var lookVector = new Vector3(velocity.x, 0, velocity.z);
            if(lookVector != Vector3.zero)
                _targetRotation.value = Quaternion.LookRotation(lookVector.normalized, Vector3.up);

            DragHandling();
        }

        private void OwnerTick()
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (input == _lastInput)
                return;

            _lastInput = input;
            SendInput(input);
        }

        [ServerRPC(channel: Channel.Unreliable)]
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

        private void DragHandling()
        {
            if (IsGrounded())
            {
#if UNITY_6000_0_OR_NEWER
                _rigidbody.linearDamping = _originalDrag;
#else
                _rigidbody.drag = _originalDrag;
#endif
                return;
            }
            
#if UNITY_6000_0_OR_NEWER
            _rigidbody.linearDamping = 0;
#else
                _rigidbody.drag = 0;
#endif
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.transform.TryGetComponent(out Movement_RB_InputSync otherPlayer))
            {
                var direction = (transform.position - other.transform.position).normalized;
                _rigidbody.AddForce(direction * playerCollisionForce, ForceMode.Impulse);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position - Vector3.up * groundCheckDistance, groundCheckRadius);
        }

        public void ResetPosition(Vector3 position)
        {
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.angularVelocity = Vector3.zero;

#if UNITY_6000_0_OR_NEWER
                _rigidbody.linearVelocity = Vector3.zero;
#else
                _rigidbody.velocity = Vector3.zero;
#endif
            }

            _rigidbody.position = position;
        }
    }
}