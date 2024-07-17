using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    public partial struct RPCPacket : INetworkedData
    {
        public int networkId;
        public byte rpcId;
        public ByteData data;

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref networkId, false);
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
    
    public class RPCModule : INetworkModule, IUpdate
    {
        readonly PlayersManager _playersManager;
        private bool _asServer;
        
        public RPCModule(PlayersManager playersManager)
        {
            _playersManager = playersManager;
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

        public static RPCPacket BuildRawRPC(int networkId, byte rpcId, NetworkStream data)
        {
            var rpc = new RPCPacket
            {
                networkId = networkId,
                rpcId = rpcId,
                data = data.buffer.ToByteData()
            };
            
            return rpc;
        }

        public void BuildRPC()
        {
            var stream = AllocStream(false);

            int test = 52;
            stream.Serialize(ref test, false);
            
            var data = BuildRawRPC(11, 22, stream);
            
            _playersManager.SendToAll(data);
            FreeStream(stream);
        }
        
        public void ReceiveRPC(PlayerID player, RPCPacket packet, bool asServer)
        {
            var stream = AllocStream(true);
            
            stream.Write(packet.data);
            stream.ResetPointer();
            
            int test = 0;
            stream.Serialize(ref test, false);
            
            FreeStream(stream);
            
            PurrLogger.Log($"Received RPC with data: {test}, length: {packet.data.offset}:{packet.data.length} ({packet.networkId}, {packet.rpcId})");
        }

        public void Update()
        {
            if (_asServer && Input.GetKeyDown(KeyCode.Space))
                BuildRPC();
        }
    }
}
