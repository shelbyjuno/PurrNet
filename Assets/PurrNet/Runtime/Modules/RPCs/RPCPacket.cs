using PurrNet.Packets;
using PurrNet.Transports;

namespace PurrNet
{
    public partial struct RPCPacket : INetworkedData
    {
        public NetworkID networkId;
        public SceneID sceneId;
        public byte rpcId;
        public ByteData data;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref networkId);
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
    
    public partial struct ChildRPCPacket : INetworkedData
    {
        public NetworkID networkId;
        public SceneID sceneId;
        public byte rpcId;
        public byte childId;
        public ByteData data;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref networkId);
            packer.Serialize(ref sceneId);
            packer.Serialize(ref rpcId);
            packer.Serialize(ref childId);
            
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
    
    public partial struct StaticRPCPacket : INetworkedData
    {
        public uint typeHash;
        public byte rpcId;
        public ByteData data;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref typeHash);
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
    
    internal readonly struct RPC_ID
    {
        public readonly uint typeHash;
        public readonly SceneID sceneId;
        public readonly NetworkID networkId;
        private readonly byte rpcId;
            
        public RPC_ID(RPCPacket packet)
        {
            sceneId = packet.sceneId;
            networkId = packet.networkId;
            rpcId = packet.rpcId;
            typeHash = default;
        }
        
        public RPC_ID(StaticRPCPacket packet)
        {
            sceneId = default;
            networkId = default;
            rpcId = packet.rpcId;
            typeHash = packet.typeHash;
        }
        
        public override int GetHashCode()
        {
            return sceneId.GetHashCode() ^ networkId.GetHashCode() ^ rpcId.GetHashCode() ^ typeHash.GetHashCode();
        }
    }

    internal class RPC_DATA
    {
        public RPC_ID rpcid;
        public RPCPacket packet;
        public RPCSignature sig;
        public NetworkStream stream;
    }
        
    internal class STATIC_RPC_DATA
    {
        public RPC_ID rpcid;
        public StaticRPCPacket packet;
        public RPCSignature sig;
        public NetworkStream stream;
    }
}