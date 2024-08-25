using JetBrains.Annotations;
using PurrNet;
using PurrNet.Logging;
using UnityEngine;

public class SyncVar<T> : NetworkModule where T : struct
{
    private T _value;
    
    public T value
    {
        get => _value;
        set
        {
            if (!isServer)
            {
                PurrLogger.LogError("Only server can change the value of SyncVar.");
                return;
            }
            
            if (value.Equals(_value))
                return;

            _value = value;
            Sync();
        }
    }
    
    public SyncVar(T initialValue = default)
    {
        _value = initialValue;
    }

    private void Sync()
    {
        SendValue(_value);
    }
    
    [ObserversRPC]
    private void SendValue(T newValue)
    {
        _value = newValue;
    }
}

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity someRef;

    private SyncVar<int> _testChild;
    
    private SyncVar<int> _testChild2 = new (70);

    protected override void OnPreModulesInitialize()
    {
        _testChild = new SyncVar<int>(69);
    }

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _testChild.value = Random.Range(0, 100);
                _testChild2.value = Random.Range(0, 100);
                // ObserversRPCTest(Time.time, someRef);
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