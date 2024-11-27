using System.Collections.Generic;
using PurrNet;
using UnityEngine;

public class SomeNode : NetworkIdentity
{
    [SerializeField] SyncList<SomeNode> _list = new (true);

    readonly List<SomeNode> _queued = new ();

    public void CreateMore(SomeNode prefab, PlayerID? player)
    {
        var instance = Instantiate(prefab);
        instance.GiveOwnership(player);
        _queued.Add(instance);
    }

    protected override void OnSpawned(bool asServer)
    {
        if (!asServer && isOwner)
        {
            Debug.Log($"I'm the owner! adding {_queued.Count} to list", this);
            foreach (var node in _queued)
                _list.Add(node);
            _queued.Clear();
        }
    }
}
