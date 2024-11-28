using PurrNet;
using UnityEngine;

public class SomeNode : NetworkIdentity
{
    [SerializeField] SyncList<SomeNode> _list = new (true);

    protected override void OnSpawned()
    {
        Debug.Log("N OnSpawned " + owner, this);
    }

    protected override void OnSpawned(bool asServer)
    {
        Debug.Log("N Spawned "  + owner, this);
    }

    public void CreateMore(SomeNode prefab, PlayerID? player)
    {
        var instance = Instantiate(prefab);
        instance.GiveOwnership(player);
        instance.QueueOnSpawned(() =>
        {
            _list.Add(instance);
        });
    }
}
