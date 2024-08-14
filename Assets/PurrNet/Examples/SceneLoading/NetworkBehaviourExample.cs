using JetBrains.Annotations;
using PurrNet;
using UnityEngine;

public class TestChild : NetworkModule
{
    [ServerRPC]
    public void Test(string message)
    {
        Debug.Log("Targeted: " + message + " from test child");
    }
}

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity someRef;

    private readonly TestChild _testChild = new ();
    private readonly TestChild _testChild2 = new ();

    private void Awake()
    {
        _testChild.SetParent(this, 0);
        _testChild2.SetParent(this, 1);
    }

    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            Debug.Log(_testChild.index, _testChild.parent);
            Debug.Log(_testChild2.index, _testChild2.parent);
            
            _testChild2.Test("Hello");
        }
    }

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ObserversRPCTest(Time.time, someRef);
            }
        }
    }

    [ObserversRPC(bufferLast: true)]
    private static void ObserversRPCTest<T>(T data, NetworkIdentity someNetRef, RPCInfo info = default)
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