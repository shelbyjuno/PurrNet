using System.Threading.Tasks;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;

public struct SomeData
{
    public int data;
}

public class SomeBehaviour : NetworkIdentity
{
    SyncTimer _timer = new ();
    
    protected override async void OnSpawned(bool asServer)
    {
        PurrLogger.Log($"OnSpawned ({asServer})", this);

        if (!asServer)
        {
            var stream = new BitStream();
            Packer<SomeData>.Write(stream, new SomeData { data = 69 });
            PurrLogger.Log($"Stream size: {stream.length}", this);
            /*var assetPath = new DirectoryInfo(".").Name;
            Debug.Log("Sending: " + assetPath);
            var res = await CalculateSomething(this, new SomeData { data = 59 });
            Debug.Log("Result: " + res);*/
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    Task<bool> CalculateSomething(SomeBehaviour reference, SomeData data)
    {
        PurrLogger.Log($"CalculateSomething: {data.data}", this);
        return Task.FromResult(data.data == 69);
    }
}
