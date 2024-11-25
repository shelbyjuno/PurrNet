using System;
using PurrNet.StateMachine;
using UnityEngine;

public class StateOne : StateNode
{
    [SerializeField] private int forTwo;
    
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
