using System;
using PurrNet.StateMachine;
using UnityEngine;

public class StateOne : StateNode
{
    public override void Enter(bool asServer)
    {
        base.Enter(asServer);
        
        Debug.Log($"Entering state one");
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.X))
            machine.Next();
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
        
        Debug.Log($"Exiting state one");
    }
}
