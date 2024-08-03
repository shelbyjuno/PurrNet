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
        public readonly NetworkID networkId;
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
            _scenePlayers.onPostPlayerLoadedScene += OnPlayerJoinedScene;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
            _hierarchyModule.onIdentityRemoved += OnIdentityRemoved;
        }
        
        public void Disable(bool asServer)
        {
            _playersManager.Unsubscribe<RPCPacket>(ReceiveRPC);
            _scenePlayers.onPostPlayerLoadedScene -= OnPlayerJoinedScene;
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
                
                if (data.Signature.excludeOwner && _ownership.TryGetOwner(identity, out var owner) && owner == player)
                    break;
                
                switch (data.Signature.type)
                {
                    case RPCType.ObserversRPC:
                    {
                        var packet = data.packet;
                        packet.data = data.stream.buffer.ToByteData();
                        _playersManager.Send(player, packet);
                        break;
                    }

                    case RPCType.TargetRPC:
                    {
                        if (data.Signature.targetPlayer == player)
                        {
                            var packet = data.packet;
                            packet.data = data.stream.buffer.ToByteData();
                            _playersManager.Send(player, packet);
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
            public RPCSignature Signature;
            public NetworkStream stream;
        }
        
        readonly Dictionary<RPC_ID, RPC_DATA> _bufferedRpcsKeys = new();
        readonly List<RPC_DATA> _bufferedRpcsDatas = new();
        
        public void AppendToBufferedRPCs(RPCPacket packet, RPCSignature signature)
        {
            if (!signature.bufferLast) return;
            
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
                    Signature = signature,
                    stream = newStream
                };
                    
                _bufferedRpcsKeys[rpcid] = newdata;
                _bufferedRpcsDatas.Add(newdata);
            }
        }

        [UsedByIL]
        public static RPCPacket BuildRawRPC(NetworkID? networkId, SceneID id, byte rpcId, NetworkStream data)
        {
            var rpc = new RPCPacket
            {
                networkId = networkId!.Value,
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
            
            var info = new RPCInfo { sender = player };

            if (_hierarchyModule.TryGetIdentity(packet.sceneId, packet.networkId, out var identity) && identity)
            {
                var rpcHandlerPtr = GetRPCHandler(identity.GetType(), packet.rpcId);

                if (rpcHandlerPtr != IntPtr.Zero)
                {
                    try
                    {
                        // Call the RPC handler
                        ((delegate* managed<NetworkIdentity, NetworkStream, RPCPacket, RPCInfo, bool, void>)
                            rpcHandlerPtr)(identity, stream, packet, info, asServer);
                    }
                    catch (Exception e)
                    {
                        PurrLogger.LogError($"{e.Message}\nWhile calling RPC handler for id {packet.rpcId} in identity {identity.GetType().Name}.\n{e.StackTrace}");
                    }
                }
                else PurrLogger.LogError($"Can't find RPC handler for id {packet.rpcId} in identity {identity.GetType().Name}.");
            }
            else if (!asServer)
            {
                PurrLogger.LogError($"Failed to find identity with id {packet.networkId} in scene {packet.sceneId} while trying to call RPC with id {packet.rpcId}.");
            }
            
            FreeStream(stream);
        }
    }
}
