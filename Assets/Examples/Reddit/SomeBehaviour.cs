using PurrNet;
using UnityEngine;

public class SomeBehaviour : NetworkBehaviour
{
    [SerializeField] private SomeNode _prefab;
    [SerializeField] SyncList<SomeNode> _list = new ();
    
    protected override void OnSpawned(bool asServer)
    {
        if (IsController(_list.ownerAuth))
        {
            var instance = Instantiate(_prefab);
            _list.Add(instance);
        }
    }
}
