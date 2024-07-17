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

        public void BuildRPC()
        {
            var byteStream = ByteBufferPool.Alloc();
            var stream = new NetworkStream(byteStream, false);

            int test = 52;
            stream.Serialize(ref test, false);
            
            var data = new RPCPacket
            {
                networkId = 11,
                rpcId = 22,
                data = byteStream.ToByteData()
            };
            
            _playersManager.SendToAll(data);
            ByteBufferPool.Free(byteStream);
        }
        
        public void ReceiveRPC(PlayerID player, RPCPacket packet, bool asServer)
        {
            var buffer = ByteBufferPool.Alloc();
            buffer.Write(packet.data);
            buffer.ResetPointer();
            var stream = new NetworkStream(buffer, true);
            
            int test = 0;
            stream.Serialize(ref test, false);
            
            ByteBufferPool.Free(buffer);
            
            PurrLogger.Log($"Received RPC with data: {test}, length: {packet.data.offset}:{packet.data.length} ({packet.networkId}, {packet.rpcId})");
        }

        public void Update()
        {
            if (_asServer && Input.GetKeyDown(KeyCode.Space))
                BuildRPC();
        }
    }
}
