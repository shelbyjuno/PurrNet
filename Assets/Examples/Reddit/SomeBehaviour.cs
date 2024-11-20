using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.Utils;
using UnityEngine;

public struct SomeNetworkedData
{
    public int data;
    public object random;
}

public struct SomeDataB
{
    public int data;
}
public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            using var stream = BitStreamPool.Get();
            
            var data = new SomeNetworkedData
            {
                data = 69,
                random = 69.42f
            };
            
            Packer<SomeNetworkedData>.Write(stream, data);
            
            stream.ResetPositionAndMode(true);
            SomeNetworkedData result = default;
            
            Packer<SomeNetworkedData>.Read(stream, ref result);
            
            PurrLogger.Log($"Read: {result.data}", this);
            PurrLogger.Log($"Read: {(float)result.random}", this);
        }
        else
        {
            RPC_SyncShuffleToClients((sbyte)69, new int[] { 1, 2, 3, 4, 5 });
        }
    }
    
    [ObserversRpc(bufferLast:true)]
    private void RPC_SyncShuffleToClients<T>(T test, int[] shuffledIndices)
    {
        Debug.Log(test + " "  + Hasher.GetStableHashU32<int[]>());
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(SomeBehaviour reference, SomeNetworkedData networkedData, List<int> test, List<float> rt, SomeDataB rf)
    {
        PurrLogger.Log($"CalculateSomething: {networkedData.data}", this);
        return Task.FromResult(networkedData.data == 69);
    }
}
