using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;

namespace PurrNet
{
    public struct RPCInfo
    {
        public PlayerID sender;
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
    
    public class RPCModule : INetworkModule
    {
        readonly HierarchyModule _hierarchyModule;
        readonly PlayersManager _playersManager;
        
        public RPCModule(PlayersManager playersManager, HierarchyModule hierarchyModule)
        {
            _playersManager = playersManager;
            _hierarchyModule = hierarchyModule;
        }
        
        public void Enable(bool asServer)
        {
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

        [UsedImplicitly]
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
        
        readonly struct RPCKey
        {
            public readonly IReflect type;
            public readonly byte rpcId;
            
            public override int GetHashCode()
            {
                return type.GetHashCode() ^ rpcId.GetHashCode();
            }
            
            public RPCKey(IReflect type, byte rpcId)
            {
                this.type = type;
                this.rpcId = rpcId;
            }
        }
        
        static readonly Dictionary<RPCKey, IntPtr> _rpcHandlers = new();

        static IntPtr GetRPCHandler(IReflect type, byte rpcId)
        {
            var rpcKey = new RPCKey(type, rpcId);
            
            if (_rpcHandlers.TryGetValue(rpcKey, out var handler))
                return handler;
            
            string methodName = $"HandleRPCGenerated_{rpcId}";
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            var ptr = method != null ? method.MethodHandle.GetFunctionPointer() : IntPtr.Zero;
            
            if (ptr != IntPtr.Zero)
                _rpcHandlers[rpcKey] = ptr;
            
            return ptr;
        }
        
        unsafe void ReceiveRPC(PlayerID player, RPCPacket packet, bool asServer)
        {
            var stream = AllocStream(true);
            stream.Write(packet.data);
            stream.ResetPointer();
            
            if (_hierarchyModule.TryGetIdentity(packet.sceneId, packet.networkId, out var identity))
            {
                var rpcHandlerPtr = GetRPCHandler(identity.GetType(), packet.rpcId);

                if (rpcHandlerPtr != IntPtr.Zero)
                {
                    var info = new RPCInfo { sender = player };
                    
                    // Call the RPC handler
                    ((delegate* managed<NetworkIdentity, NetworkStream, RPCPacket, RPCInfo, void>)rpcHandlerPtr)(identity, stream, packet, info);
                }
                else PurrLogger.LogError($"Can't find RPC handler for id {packet.rpcId} in identity {identity.GetType().Name}.");
            }
            else PurrLogger.LogError($"Can't find identity with id {packet.networkId} in scene {packet.sceneId}.");
            
            FreeStream(stream);
        }
    }
}
