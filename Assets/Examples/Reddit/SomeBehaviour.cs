using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;

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
            /*List<int> test = new List<int> { 1, 2, 3, 4, 5 };
            
            Packer<List<int>>.Write(stream, test);
            
            stream.ResetPositionAndMode(true);
            test.Clear();
            test.Add(6);
            Packer<List<int>>.Read(stream, ref test);
            
            PurrLogger.Log($"Test: {test.Count}", this);*/
            Packer<SomeBehaviour>.Write(stream, this);
            PurrLogger.Log($"Stream size: {stream.length}", this);

            SomeBehaviour data = null;
            stream.ResetPositionAndMode(true);
            
            Packer<SomeBehaviour>.Read(stream, ref data);
            PurrLogger.Log($"Data: {data == null}", data);
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(SomeBehaviour reference, SomeNetworkedData networkedData, List<int> test, List<float> rt, SomeDataB rf)
    {
        PurrLogger.Log($"CalculateSomething: {networkedData.data}", this);
        return Task.FromResult(networkedData.data == 69);
    }
}
