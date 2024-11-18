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
            
            Packer<SomeBehaviour>.Write(stream, this);
            PurrLogger.Log($"Stream size: {stream.length}", this);

            stream.ResetPositionAndMode(true);
            SomeBehaviour data = this;
            
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
