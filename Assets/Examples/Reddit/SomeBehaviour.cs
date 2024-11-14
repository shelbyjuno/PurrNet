using System.IO;
using System.Threading.Tasks;
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
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(string data)
    {
        return Task.FromResult(data.Contains("password"));
    }
}
