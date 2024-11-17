using System.IO;
using System.Threading.Tasks;
using PurrNet;
using UnityEngine;

public struct SomeData
{
    public int data;
}

public class SomeBehaviour : NetworkIdentity
{
    SyncTimer _timer = new ();
    [SerializeField] SyncVar<int> _countDownTime = new ();
    
    protected override async void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var assetPath = new DirectoryInfo(".").Name;
            Debug.Log("Sending: " + assetPath);
            var res = await CalculateSomething(new SomeData { data = 59 });
            Debug.Log("Result: " + res);
        }
        
        _countDownTime.value = 5;
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(SomeData data)
    {
        return Task.FromResult(data.data == 69);
    }
}
