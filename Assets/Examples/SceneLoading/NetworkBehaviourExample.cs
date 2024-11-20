using System;
using System.Collections;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PurrNet;
using PurrNet.Examples;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using PurrNet.Utils;
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

    // private ReturnableRpcsInModules _returnableRpcsInModules = new();
    
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            //StartCoroutine(CoolRPC_Coroutines());
            /*var res = await CoolRPCTest("fefes");
            Debug.Log("CoolRPCTest result: " + res);

            var pong = PingPongTest("Pong");
            Debug.Log("PingPongTest result: " + await pong);
            
            await SomeDelay(1);
            Debug.Log("SomeDelay Done");*/
            if (localPlayer.HasValue)
            {
                Debug.Log("Sending Targeted RPC to " + localPlayer.Value);
                SetColor_Target(localPlayer.Value, Color.red);
            }
            else Debug.Log("No local player");
            
            // _ = CoolRPCTest2();

            // this will be sent to the server as per usual
            // Test("Test 3");
        }
    }
    
    [TargetRpc(requireServer: false)]
    private void SetColor_Target([UsedImplicitly] PlayerID player, Color color)
    {
        Debug.Log("SetColor_Target: " + color);
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
    Task<bool> CoolRPCTest(string fuck)
    {
        return Task.FromResult(Random.Range(0, 2) == 0);
    }
    
    [ServerRpc(requireOwnership: false)]
    static Task<T> PingPongTest<T>(T ping)
    {
        return Task.FromResult(ping);
    }
    
    [ServerRpc(requireOwnership: false)]
    static Task SomeDelay(float secToWait)
    {
        return Task.Delay(Mathf.RoundToInt(secToWait * 1000));
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
