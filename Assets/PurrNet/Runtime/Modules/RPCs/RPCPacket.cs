using System;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;

namespace PurrNet
{
    public struct SaturatedRPCPacket : IDisposable
    {
        public RPCPacket packet;
        public RPCInfo info;
        public NetworkStream stream;

        public void Dispose()
        {
            RPCModule.FreeStream(stream);
        }
    }
    
    public partial struct RPCPacket : INetworkedData
    {
        public const string GET_ID_METHOD = nameof(GetID);
        
        public int networkId;
        public SceneID sceneId;
        public byte rpcId;
        public ByteData data;
        
        public int GetID() => rpcId;

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref networkId, false);
            packer.Serialize(ref sceneId);
            packer.Serialize(ref rpcId);
            
            if (packer.isReading)
            {
                int length = 0;
                packer.Serialize(ref length, false);
                data = packer.Read(length);
            }
            else
            {
                int length = data.length;
                packer.Serialize(ref length, false);
                packer.Write(data);
            }
        }
    }
}