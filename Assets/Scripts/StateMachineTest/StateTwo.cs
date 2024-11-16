using System;
using PurrNet.StateMachine;
using UnityEngine;

public class StateTwo : StateNode
{
    public override void Enter(bool asServer)
    {
        base.Enter(asServer);
        
        Debug.Log($"Entering state two");
    }

    private void Update()
    {
        
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
        
        Debug.Log($"Exiting state two");
    }
}
