using JetBrains.Annotations;
using PurrNet;
using PurrNet.Packets;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity someRef;

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ObserversRPCTest(Time.time, someRef);
                SomeClass.Test("WAZZAAp");
            }
        }
    }

    [ObserversRPC(bufferLast: true)]
    private void ObserversRPCTest<T>(T data, NetworkIdentity someNetRef, RPCInfo info = default)
    {
        Debug.Log("Observers: " + data + " " + info.sender);
        
        if (someNetRef)
            Debug.Log(someNetRef.name);
        else
            Debug.Log("No ref");
    }

    [TargetRPC(bufferLast: true)]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}

class SomeClass
{
    [ObserversRPC(runLocally: true)]
    public static void Test(string someVal)
    {
        if (NetworkManager.main.isClient)
            ServerTest(someVal + someVal);
    }
    
    [ServerRPC]
    public static void ServerTest(string someVal)
    {
        Debug.Log("Test from static " + someVal);
    }
}
