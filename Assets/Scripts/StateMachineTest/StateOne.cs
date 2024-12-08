using PurrNet.StateMachine;
using UnityEngine;

public class StateOne : StateNode
{
    [SerializeField] private int forTwo;

    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);

        if (Input.GetKeyDown(KeyCode.X) && isController)
        {
            machine.Next(forTwo);
        }
    }
}
