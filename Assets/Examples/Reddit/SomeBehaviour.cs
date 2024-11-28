using System.Threading.Tasks;
using PurrNet;
using UnityEngine;
using UnityEngine.Events;

public class SomeBehaviour : NetworkBehaviour
{
    [SerializeField] private SomeNode _prefab;
    public SyncVar<SomeNode> fesfes { get; } = new();
    [SerializeField] SyncList<SomeNode> _list = new ();
    [SerializeField] UnityEvent<int> _evemt = new ();
    
    [ServerRpc(requireOwnership: false)]
    public void SetReady(RPCInfo info = default)
    {
        Debug.Log("SetReady " + owner, this);
    }

    protected override void OnSpawned()
    {
        Debug.Log("OnSpawned " + owner, this);
    }

    protected override void OnSpawned(bool asServer)
    {
        Debug.Log("Spawned " + owner, this);
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
    
    [ObserversRpc]
    private static void OnEvent_STATIC_NON_GEn()
    {
        Debug.Log("Static event");
    }
    
    [ObserversRpc]
    protected static void OnEvent_STATIC<T>()
    {
        Debug.Log("Static event");
    }
    
    [ObserversRpc]
    protected void OnEvent<T, G>()
    {
    }
    
    [ObserversRpc]
    private void OnEvent2<H>() where H : class
    {
    }
}
