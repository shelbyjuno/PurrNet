using System;
using System.Collections.Generic;
using System.Reflection;
using PurrNet.Logging;
using PurrNet.Packets;

namespace PurrNet.Modules
{
    internal readonly struct RPC_ID
    {
        public readonly SceneID sceneId;
        public readonly int networkId;
        private readonly byte rpcId;
            
        public RPC_ID(RPCPacket packet)
        {
            sceneId = packet.sceneId;
            networkId = packet.networkId;
            rpcId = packet.rpcId;
        }
        
        public override int GetHashCode()
        {
            return sceneId.GetHashCode() ^ networkId.GetHashCode() ^ rpcId.GetHashCode();
        }
    }

    public class RPCModule : INetworkModule
    {
        readonly HierarchyModule _hierarchyModule;
        readonly PlayersManager _playersManager;
        readonly ScenesModule _scenes;
        readonly ScenePlayersModule _scenePlayers;
        readonly GlobalOwnershipModule _ownership;

        public RPCModule(PlayersManager playersManager, HierarchyModule hierarchyModule, GlobalOwnershipModule ownerships, ScenesModule scenes, ScenePlayersModule scenePlayers)
        {
            _playersManager = playersManager;
            _hierarchyModule = hierarchyModule;
            _scenes = scenes;
            _scenePlayers = scenePlayers;
            _ownership = ownerships;
        }
        
        public void Enable(bool asServer)
        {
            _playersManager.Subscribe<RPCPacket>(ReceiveRPC);
            _scenePlayers.onPlayerLoadedScene += OnPlayerJoinedScene;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
            _hierarchyModule.onIdentityRemoved += OnIdentityRemoved;
        }
        
        public void Disable(bool asServer)
        {
            _playersManager.Unsubscribe<RPCPacket>(ReceiveRPC);
            _scenePlayers.onPlayerLoadedScene -= OnPlayerJoinedScene;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
            _hierarchyModule.onIdentityRemoved -= OnIdentityRemoved;
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            for (int i = 0; i < _bufferedRpcsDatas.Count; i++)
            {
                var data = _bufferedRpcsDatas[i];
                
                if (data.rpcid.sceneId != scene) continue;

                if (!_hierarchyModule.TryGetIdentity(data.packet.sceneId, data.packet.networkId, out var identity))
                {
                    PurrLogger.LogError($"Can't find identity with id {data.packet.networkId} in scene {data.packet.sceneId}.");
                    continue;
                }
                
                if (data.details.excludeOwner && _ownership.TryGetOwner(identity, out var owner) && owner == player)
                    break;
                
                switch (data.details.type)
                {
                    case RPCType.ObserversRPC:
                    {
                        var packet = data.packet;
                        packet.data = data.stream.buffer.ToByteData();
                        _playersManager.Send(player, packet, data.details.channel);
                        break;
                    }

                    case RPCType.TargetRPC:
                    {
                        if (data.details.targetPlayer == player)
                        {
                            var packet = data.packet;
                            packet.data = data.stream.buffer.ToByteData();
                            _playersManager.Send(player, packet, data.details.channel);
                        }
                        break;
                    }
                }
            }
        }

        // Clean up buffered RPCs when an identity is removed
        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            for (int i = 0; i < _bufferedRpcsDatas.Count; i++)
            {
                var data = _bufferedRpcsDatas[i];
                
                if (data.rpcid.sceneId != identity.sceneId) continue;
                if (data.rpcid.networkId != identity.id) continue;
                
                FreeStream(data.stream);
                
                _bufferedRpcsKeys.Remove(data.rpcid);
                _bufferedRpcsDatas.RemoveAt(i--);
            }
        }

        // Clean up buffered RPCs when a scene is unloaded
        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            for (int i = 0; i < _bufferedRpcsDatas.Count; i++)
            {
                var data = _bufferedRpcsDatas[i];
                
                if (data.rpcid.sceneId != scene) continue;
                
                var key = data.rpcid;
                FreeStream(data.stream);
                
                _bufferedRpcsKeys.Remove(key);
                _bufferedRpcsDatas.RemoveAt(i--);
            }
        }
        
        [UsedByIL]
        public static NetworkStream AllocStream(bool reading)
        {
            return new NetworkStream(ByteBufferPool.Alloc(), reading);
        }
        
        [UsedByIL]
        public static void FreeStream(NetworkStream stream)
        {
            ByteBufferPool.Free(stream.buffer);
        }

        class RPC_DATA
        {
            public RPC_ID rpcid;
            public RPCPacket packet;
            public RPCDetails details;
            public NetworkStream stream;
        }
        
        readonly Dictionary<RPC_ID, RPC_DATA> _bufferedRpcsKeys = new();
        readonly List<RPC_DATA> _bufferedRpcsDatas = new();
        
        public void OnRPCSent(RPCPacket packet, RPCDetails details)
        {
            if (details.bufferLast)
            {
                var rpcid = new RPC_ID(packet);

                if (_bufferedRpcsKeys.TryGetValue(rpcid, out var data))
                {
                    data.stream.ResetPointer();
                    data.stream.Write(packet.data);
                }
                else
                {
                    var newStream = AllocStream(false);
                    newStream.Write(packet.data);
                    
                    var newdata = new RPC_DATA
                    {
                        rpcid = rpcid,
                        packet = packet,
                        details = details,
                        stream = newStream
                    };
                    
                    _bufferedRpcsKeys[rpcid] = newdata;
                    _bufferedRpcsDatas.Add(newdata);
                }
            }
        }

        [UsedByIL]
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
            private readonly IReflect type;
            private readonly byte rpcId;
            
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
