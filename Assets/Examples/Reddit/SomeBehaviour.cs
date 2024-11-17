using System.IO;
using System.Threading.Tasks;
using PurrNet;
using UnityEngine;

struct SomeData
{
    public string data;
}

public class SomeBehaviour : NetworkIdentity
{
    protected override async void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var assetPath = new DirectoryInfo(".").Name;
            Debug.Log("Sending: " + assetPath);
            var res = await CalculateSomething(new SomeData { data = assetPath });
            Debug.Log("Result: " + res);
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(SomeData data)
    {
        return Task.FromResult(data.data.Contains("password"));
    }
}
