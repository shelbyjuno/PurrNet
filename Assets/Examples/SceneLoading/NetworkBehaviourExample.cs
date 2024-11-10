using System;
using System.Collections;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
struct TestStruct
{
    public int a;
    public int b;
}

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private Transform someRef;

    [SerializeField]
    private SyncVar<int> _testChild2 = new (70);

    [SerializeField] private bool _keepChanging;
    
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            //StartCoroutine(CoolRPC_Coroutines());
            CoolRPCTestNoReturnValue();
            /*_ = CoolRPCTest();
            _ = CoolRPCTest2();*/

            // this will be sent to the server as per usual
            // Test("Test 3");
        }
    }
    
    public Task<bool> CoolRPCTestNoReturnValue3()
    {
        var task = GetNextId<bool>(networkManager.localClientConnection, 5, out var request);
        
        NetworkStream networkStream = RPCModule.AllocStream(reading: false);

        networkStream.Serialize<uint>(ref request.id);
        
        RPCPacket packet = RPCModule.BuildRawRPC(base.id, base.sceneId, 0, networkStream);
        SendRPC(packet, RPCSignature.Make(RPCType.ServerRPC, Channel.ReliableOrdered, runLocally: false, requireOwnership: false, bufferLast: false, requireServer: false, excludeOwner: false, "CoolRPCTestNoReturnValue", isStatic: false, 5f));
        RPCModule.FreeStream(networkStream);

        return task;
    }

    
    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _testChild2.value = Random.Range(0, 100);
                ObserversRPCTest(Time.time, someRef);
            }
        }
    }
    
    /*[ServerRpc(requireOwnership: false)]
    IEnumerator CoolRPC_Coroutines()
    {
        Debug.Log("CoolRPC_Coroutines");
        yield return new WaitForSeconds(1);
        Debug.Log("CoolRPC_Coroutines Done");
    }*/
    
    [ServerRpc(requireOwnership: false)]
    async void CoolRPCTestNoReturnValue()
    {
        await Task.Delay(1000);
        Debug.Log("CoolRPCTestNoReturnValue");
    }

    [ServerRpc(requireOwnership: false)]
    Task<bool> CoolRPCTest()
    {
        Debug.Log("CoolRPCTest");
        return Task.FromResult(Random.Range(0, 2) == 0);
    }
    
    [ServerRpc(requireOwnership: false)]
    async Task<bool> CoolRPCTest2()
    {
        Debug.Log("Waiting for 1 second");
        await Task.Delay(1000);
        Debug.Log("Done waiting");

        return Random.Range(0, 2) == 0;
    }
    
    [ObserversRpc(bufferLast: true), UsedImplicitly]
    private static void ObserversRPCTest<T>(T data, Transform someNetRef, RPCInfo info = default)
    {
        Debug.Log("Observers: " + data + " " + info.sender);
        
        if (someNetRef)
            Debug.Log(someNetRef.name);
        else
            Debug.Log("No ref");
    }

    private void FixedUpdate()
    {
        if (_keepChanging)
            _testChild2.value = Random.Range(0, 100);
    }

    [ObserversRpc(requireServer: false, bufferLast: true)]
    private void Test(string test)
    {
        Debug.Log(test);
    }

    [TargetRpc(bufferLast: true)]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}
