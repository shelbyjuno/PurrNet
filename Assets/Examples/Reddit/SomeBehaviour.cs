using System;
using System.Threading.Tasks;
using PurrNet;
using UnityEngine;

public class SomeBehaviour : NetworkIdentity
{
    /*protected override void OnSpawned(bool asServer)
    {
        if (asServer)
            return;

        RequestOwnership();
    }
    
    [ServerRpc]
    private async void RequestOwnership(RPCInfo info = default)
    {
        try
        {
            Debug.Log("RequestOwnership");
        
            await Task.Delay(1000);
            await DoSomething(info.sender);
        
            Debug.Log("RequestOwnership done ====");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    
    [TargetRpc]
    private async Task DoSomething(PlayerID target, RPCInfo info = default)
    {
        Debug.Log("Doing something");

        await Task.Yield();
        
        Debug.Log("Done");
    }*/
}
