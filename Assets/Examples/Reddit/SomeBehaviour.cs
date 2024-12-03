using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packing;
using UnityEngine;
using Channel = PurrNet.Transports.Channel;

public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (asServer)
            return;

        RequestOwnership();
        DoSomething3(default, default);
    }
    
    [ServerRpc]
    private async void RequestOwnership(RPCInfo info = default)
    {
        try
        {
            Debug.Log("RequestOwnership");
        
            await UniTask.Delay(1000);
            // await DoSomething(info.sender);
        
            Debug.Log("RequestOwnership done ====");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    
    public Task DoSomething3(PlayerID target, RPCInfo info = default(RPCInfo))
    {
        RPCSignature signature = RPCSignature.Make(RPCType.ServerRPC, Channel.ReliableOrdered, runLocally: false, requireOwnership: true, bufferLast: false, requireServer: false, excludeOwner: false, "DoSomething2", isStatic: false, 5f, excludeSender: false);
        RpcRequest request;
        Task nextId = GetNextId(RPCType.ServerRPC, signature.targetPlayer, 5f, out request);
        BitPacker bitPacker = RPCModule.AllocStream(reading: false);
        Packer<uint>.Write(bitPacker, request.id);
        Packer<PlayerID>.Write(bitPacker, target);
        RPCPacket packet = RPCModule.BuildRawRPC(base.id, base.sceneId, 1, bitPacker);
        SendRPC(packet, signature);
        RPCModule.FreeStream(bitPacker);
        if (signature.runLocally)
        {
            return DoSomething(target, info);
        }
        return nextId;
    }
    
    [ServerRpc]
    private async Task DoSomething2(PlayerID target, RPCInfo info = default)
    {
        Debug.Log("Doing something");

        await UniTask.Yield();
        
        Debug.Log("Done");
    }
    
    private async Task DoSomething(PlayerID target, RPCInfo info = default)
    {
        Debug.Log("Doing something");

        await UniTask.Yield();
        
        Debug.Log("Done");
    }
}
