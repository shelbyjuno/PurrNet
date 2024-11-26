using System.Collections.Generic;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;

public class SomeNodeWithState : StateNode
{
    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);

        if (Input.GetKeyDown(KeyCode.X))
        {
        }
    }
}
