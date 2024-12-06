using PurrNet;
using UnityEngine;

public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (asServer)
            return;

        FuckYou("Hello, World!");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
            ObsDoSomething();
    }

    [ObserversRpc(requireServer:false)]
    private void ObsDoSomething(RPCInfo info = default)
    {
        Debug.Log($"Called by {info.sender}, I am {localPlayer}");
    }
    
    [ObserversRpc(runLocally:true)]
    private void FuckYou(string itsMyStringNow) {
        Debug.Log(itsMyStringNow);
    }
}
