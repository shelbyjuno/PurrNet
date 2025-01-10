using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;

public class StateOne : StateNode
{
    [SerializeField] private SyncList<GameObject> list;
    [SerializeField] private int forTwo;

    [SerializeField] private GameObject prefab;

    protected override void OnSpawned(bool asServer)
    {
        if (asServer)
        {
            var thing = Instantiate(prefab);
            list.Add(thing);
        }
    }

    [ContextMenu("Resync")]
    void Resync()
    {
        list.SetDirty(0);
    }

    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);

        if (Input.GetKeyDown(KeyCode.X) && isController)
        {
            machine.Next(forTwo);
        }
    }
}
