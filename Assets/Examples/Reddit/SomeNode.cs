using PurrNet;
using UnityEngine;

public class SomeNode : NetworkIdentity
{
    [SerializeField] SyncList<SomeNode> _list = new (true);

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
