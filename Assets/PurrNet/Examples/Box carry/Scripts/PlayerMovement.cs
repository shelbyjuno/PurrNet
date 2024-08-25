using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Examples.BoxCarry
{
    public class PlayerMovement : NetworkIdentity
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float acceleration = 4f;
        [SerializeField] private float jumpForce = 1.5f;
        [SerializeField] private float gravity = 9.81f;

        private CharacterController _controller;
        private float rotationSpeed;
        private float _verticalVelocity;
        private Vector3 currentMove;

        private void Awake()
        {
            if (!TryGetComponent(out _controller))
                PurrLogger.LogError($"Failed to get component '{nameof(CharacterController)}' on '{name}'.", this);
        }

        protected override void OnSpawned(bool asServer)
        {
            enabled = isOwner;
        }

        private void Update()
        {
            if (!_controller)
                return;

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 targetMove = new Vector3(input.x, 0, input.y).normalized;

            currentMove = Vector3.Lerp(currentMove, targetMove, acceleration * Time.deltaTime);

            if (_controller.isGrounded)
                _verticalVelocity = -gravity * Time.deltaTime;
            else
                _verticalVelocity -= gravity * Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Space) && _controller.isGrounded)
                _verticalVelocity = jumpForce;

            currentMove.y = _verticalVelocity;

            _controller.Move(currentMove * (moveSpeed * Time.deltaTime));

            if (input != Vector2.zero)
            {
                float targetAngle = Mathf.Atan2(currentMove.x, currentMove.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationSpeed, 0.1f);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
        }
    }
}