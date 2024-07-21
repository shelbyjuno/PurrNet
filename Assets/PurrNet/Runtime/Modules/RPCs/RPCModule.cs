using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;

namespace PurrNet
{
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
    
    public class RPCModule : INetworkModule
    {
        readonly HierarchyModule _hierarchyModule;
        readonly PlayersManager _playersManager;
        private bool _asServer;
        
        public RPCModule(PlayersManager playersManager, HierarchyModule hierarchyModule)
        {
            _playersManager = playersManager;
            _hierarchyModule = hierarchyModule;
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            _playersManager.Subscribe<RPCPacket>(ReceiveRPC);
        }
        
        public void Disable(bool asServer)
        {
            _playersManager.Unsubscribe<RPCPacket>(ReceiveRPC);
        }
        
        public static NetworkStream AllocStream(bool reading)
        {
            return new NetworkStream(ByteBufferPool.Alloc(), reading);
        }
        
        public static void FreeStream(NetworkStream stream)
        {
            ByteBufferPool.Free(stream.buffer);
        }

        public static RPCPacket BuildRawRPC(int networkId, SceneID id, byte rpcId, NetworkStream data)
        {
            var rpc = new RPCPacket
            {
                networkId = networkId,
                rpcId = rpcId,
                sceneId = id,
                data = data.buffer.ToByteData()
            };
            
            return rpc;
        }
        
        void ReceiveRPC(PlayerID player, RPCPacket packet, bool asServer)
        {
            var stream = AllocStream(true);
            stream.Write(packet.data);
            stream.ResetPointer();
            
            if (_hierarchyModule.TryGetIdentity(packet.sceneId, packet.networkId, out var identity))
                 identity.HandleRPCGenerated(packet, stream);
            else PurrLogger.LogError($"Can't find identity with id {packet.networkId} in scene {packet.sceneId}.");
            
            FreeStream(stream);
        }
    }
}
