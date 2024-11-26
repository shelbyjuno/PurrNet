using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.StateMachine;

public class SomeBehaviour : NetworkBehaviour
{
    SyncList<SomeBehaviour> _list = new ();
    
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            using var writer = BitPackerPool.Get();
            
            Packer<byte>.Write(writer, 100);
            
            for (int i = 0; i < 100; i++)
                Packer<string>.Write(writer, i.ToString());

            Stream(writer);
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void Stream(BitPacker data)
    {
        using (data)
        {
            byte count = 0;
            
            Packer<byte>.Read(data, ref count);
            
            for (int i = 0; i < count; i++)
            {
                string message = string.Empty;
                Packer<string>.Read(data, ref message);
                PurrLogger.Log(message); // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, ...
            }
        }
    }
}
