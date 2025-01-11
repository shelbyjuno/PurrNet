using System.Threading.Tasks;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;

public class SomeNode : NetworkIdentity
{
    [SerializeField] SyncList<SomeNode> _list = new (true);

    protected override void OnSpawned(bool asServer)
    {
        if (asServer)
            TickManager.TestServerRpc();
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
