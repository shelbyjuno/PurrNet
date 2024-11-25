using System.Collections.Generic;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.StateMachine;
using PurrNet.Transports;

public class SomeBehaviour : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            using var writer = BitPackerPool.Get();
            
            Packer<string>.Write(writer, "Hello, server!");
            Packer<string>.Write(writer, "Maybe some audio data being pumped?");

            Stream(writer);
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void Stream(BitPacker data)
    {
        using (data)
        {
            string message = default;
            string audioData = default;

            Packer<string>.Read(data, ref message);
            Packer<string>.Read(data, ref audioData);

            PurrLogger.Log(message); // Hello, server!
            PurrLogger.Log(audioData); // Maybe some audio data being pumped?
        }
    }
}
