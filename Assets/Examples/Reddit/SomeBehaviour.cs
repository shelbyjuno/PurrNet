using System;
using System.IO;
using System.Threading.Tasks;
using PurrNet;
using UnityEngine;

[Serializable]
public class MyModule<A> : NetworkModule
{
    [SerializeField] private A _toAddGeneric;
    [SerializeField] private string _toAdd;
    
    [ServerRpc(requireOwnership: false)]
    public async Task<string> ExampleRPC<T, Y>(T somethingToPrint, Y two)
    {
        Debug.Log($"Received {somethingToPrint} and {two} {typeof(Y).Name}");
        
        await Task.Delay(1000);

        return $"{somethingToPrint} + {two} + {_toAdd} + {_toAddGeneric}";
    }
}

public class SomeBehaviour : NetworkIdentity
{
    [SerializeField] MyModule<int> _moduleA;
    [SerializeField] MyModule<double> _moduleB;
    [SerializeField] SyncVar<double> _moduleBfes;
    
    protected override async void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var assetPath = new DirectoryInfo(".").Name;
            
            var result = await _moduleA.ExampleRPC(assetPath, 69f);
            
            Debug.Log($"Result A: {result}");
            
            result = await _moduleB.ExampleRPC(assetPath, 69f);
            
            Debug.Log($"Result B: {result}");
        }
    }
}
