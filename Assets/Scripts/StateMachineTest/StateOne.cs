using System;
using PurrNet.StateMachine;
using UnityEngine;

public class StateOne : StateNode
{
    [SerializeField] private int forTwo;

    private void Awake()
    {
        machine.onStateChanged += OnReceivedNewData;
    }

    private void OnReceivedNewData()
    {
        Debug.Log($"state changed");
    }

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);
        
    }

    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);

        if (Input.GetKeyDown(KeyCode.X))
        {
            machine.Next(forTwo);
        }
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
        
    }
}
