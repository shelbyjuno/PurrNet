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
        Debug.Log("Targeted: " + newValue + " from test child");
        _value = newValue;
    }

/*
    [ServerRPC(runLocally: true)]
    public static void LetServerKnow()
    {
        Debug.Log("SERVER " + typeof(T).FullName);
    }
*/
}

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity someRef;

    private readonly SyncVar<int> _testChild = new (69);

    private void Awake()
    {
        _testChild.SetParent(this, 0);

        //SyncVar<int>.LetServerKnow();
    }

    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            Debug.Log(_testChild.index, _testChild.parent);
        }
    }

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _testChild.value = Random.Range(0, 100);
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