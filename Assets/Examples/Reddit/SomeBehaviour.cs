using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using PurrNet;
using UnityEngine;

public class SomeBehaviour : NetworkIdentity
{
    protected override async void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var assetPath = new DirectoryInfo(".").Name;
            Debug.Log("Sending: " + assetPath);
            var res = await CalculateSomething(assetPath);
            Debug.Log("Result: " + res);
            
            Debug.Log("Sending: " + 123);
            res = await CalculateSomething(123);
            
            Debug.Log("Result: " + res);
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    UniTask<string> CalculateSomething(string data)
    {
        return UniTask.FromResult($"From server: {data}");
    }
    
    [ServerRpc(requireOwnership: false)]
    UniTask<string> CalculateSomething<T>(T data)
    {
        return UniTask.FromResult($"From server: {data}");
    }
        
    [ServerRpc(requireOwnership: false)]
    Task<string> CalculateSomething2<T>(T data)
    {
        return Task.FromResult($"From server: {data}");
    }
}
