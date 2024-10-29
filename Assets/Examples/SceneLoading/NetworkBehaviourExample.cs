using System;
using JetBrains.Annotations;
using PurrNet;
using PurrNet.Logging;
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
    [SerializeField] private NetworkIdentity someRef;

    [SerializeField]
    private SyncVar<int> _testChild2 = new (70);

    [SerializeField] private bool _keepChanging;
    
    static bool _sentOnce;

    protected override void OnSpawned(bool asServer)
    {
        if (!asServer && !_sentOnce)
        {
            // this will be sent to the server as per usual
            // Test("Test 3");
            
            _sentOnce = true;
        }
    }
    
    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _testChild2.value = Random.Range(0, 100);
                // ObserversRPCTest(Time.time, someRef);
            }
        }
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

    [ObserversRpc(bufferLast: true), UsedImplicitly]
    private static void ObserversRPCTest<T>(T data, NetworkIdentity someNetRef, RPCInfo info = default)
    {
        Debug.Log("Observers: " + data + " " + info.sender);
        
        if (someNetRef)
            Debug.Log(someNetRef.name);
        else
            Debug.Log("No ref");
    }

    [TargetRpc(bufferLast: true)]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}
