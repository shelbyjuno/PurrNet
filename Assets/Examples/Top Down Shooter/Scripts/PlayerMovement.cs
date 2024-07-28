using System;
using PurrNet;
using PurrNet.Logging;
using UnityEngine;

public class PlayerMovement : NetworkIdentity
{
    [SerializeField] private float moveSpeed;

    private CharacterController _controller;

    private void Awake()
    {
        if(!TryGetComponent(out _controller))
            PurrLogger.LogError($"Failed to get component '{nameof(CharacterController)}' on '{name}'.", this);
    }

    protected override void OnSpawned(bool asServer)
    {
        enabled = isOwner;
        Debug.Log($"Is owner: {isOwner} | AsServer: {asServer}");
    }

    private void Update()
    {
        if (!_controller)
            return;
        
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector3 move = new Vector3(input.x, 0, input.y);
        _controller.Move(move * (moveSpeed * Time.deltaTime));
    }
}
