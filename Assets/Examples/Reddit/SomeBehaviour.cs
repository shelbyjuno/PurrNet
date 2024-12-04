using PurrNet;
using PurrNet.Logging;
using UnityEngine;

public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (asServer)
            return;

        FuckYou("Hello, World!");
    }
    
    [ObserversRpc(runLocally:true)]
    private void FuckYou(string itsMyStringNow) {
        Debug.Log(itsMyStringNow);
    }
}
