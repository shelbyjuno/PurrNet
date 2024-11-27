using PurrNet;
using UnityEngine;

public class SomeBehaviour : NetworkBehaviour
{
    [SerializeField] private SomeNode _prefab;
    [SerializeField] SyncList<SomeNode> _list = new ();
    
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var instance = Instantiate(_prefab);
            instance.GiveOwnership(localPlayer);

            if (IsController(_list.ownerAuth))
                _list.Add(instance);
            
            instance.CreateMore(_prefab, localPlayer);
            instance.CreateMore(_prefab, localPlayer);
            instance.CreateMore(_prefab, localPlayer);
        }
    }
}
