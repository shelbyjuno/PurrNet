using System;
using JetBrains.Annotations;
using PurrNet;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        Hasher.PrepareType<int>();
        Hasher.PrepareType<uint>();
        Hasher.PrepareType<double>();
        Hasher.PrepareType<string>();
        Hasher.PrepareType<float>();
        
        if (asServer)
            ObserversRPCTest(Time.time);
    }

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ObserversRPCTest(Time.time);
            }
        }
    }


    [ObserversRPC(bufferLast: true)]
    private void ObserversRPCTest<T>(T data, RPCInfo info = default)
    {
        Debug.Log("Observers: " + data + " " + typeof(T).Name + " " + info.sender);
    }
    
    [ServerRPC(Channel.Unreliable)]
    private void ServerRPCMethodGeneric<T>(T data, RPCInfo info = default)
    {
        SendToTarget(info.sender, data);
    }

    
    [TargetRPC(bufferLast: true)]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}
