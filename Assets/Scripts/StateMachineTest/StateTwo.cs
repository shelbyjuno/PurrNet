using PurrNet.StateMachine;
using UnityEngine;

public class StateTwo : StateNode<int>
{
    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);
        
        if(Input.GetKeyDown(KeyCode.X) && isController)
            machine.Next();
    }
}
