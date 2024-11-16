using PurrNet.StateMachine;
using UnityEngine;

public class StateTwo : StateNode<int>
{
    public override void Enter(int data, bool asServer)
    {
        base.Enter(data, asServer);
        
        Debug.Log($"Entering state two | Data: {data}");
    }

   
    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);
        
        if(Input.GetKeyDown(KeyCode.X))
            machine.Next();
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
    }
}
