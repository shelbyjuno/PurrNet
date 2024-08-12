using System;
using System.Collections.Generic;
using System.Reflection;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Utils;

namespace PurrNet.Modules
{
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
            _playersManager.Subscribe<StaticRPCPacket>(ReceiveStaticRPC);

            _playersManager.onPlayerJoined += OnPlayerJoined;
            _scenePlayers.onPostPlayerLoadedScene += OnPlayerJoinedScene;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
            _hierarchyModule.onIdentityRemoved += OnIdentityRemoved;
        }
        
        public void Disable(bool asServer)
        {
            _playersManager.Unsubscribe<RPCPacket>(ReceiveRPC);
            _playersManager.Unsubscribe<StaticRPCPacket>(ReceiveStaticRPC);
            
            _playersManager.onPlayerJoined -= OnPlayerJoined;
            _scenePlayers.onPostPlayerLoadedScene -= OnPlayerJoinedScene;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
            _hierarchyModule.onIdentityRemoved -= OnIdentityRemoved;
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

        private void OnPlayerJoined(PlayerID player, bool asserver)
        {
            SendAnyStaticRPCs(player);
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            SendAnyInstanceRPCs(player, scene);
        }
        
        [UsedByIL]
        public static PlayerID GetLocalPlayer()
        {
            var nm = NetworkManager.main;

            if (!nm) return default;

            if (nm.TryGetModule<PlayersManager>(false, out var players))
                return default;

            return players.localPlayerId ?? default;
        }
        
        [UsedByIL]
        public static void SendStaticRPC(StaticRPCPacket packet, RPCSignature signature)
        {
            var nm = NetworkManager.main;

            if (!nm)
            {
                PurrLogger.LogError($"Can't send static RPC '{signature.rpcName}'. NetworkManager not found.");
                return;
            }
            
            if (!nm.TryGetModule<RPCModule>(nm.isServer, out var module))
            {
                PurrLogger.LogError("Failed to get RPC module while sending static RPC.", nm);
                return;
            }
            
            if (signature.requireServer && !nm.isServer)
            {
                PurrLogger.LogError($"Trying to send static RPC '{signature.rpcName}' of type {signature.type} without server.");
                return;
            }
            
            module.AppendToBufferedRPCs(packet, signature);
            
            switch (signature.type)
            {
                case RPCType.ServerRPC: nm.GetModule<PlayersManager>(false).SendToServer(packet, signature.channel); break;
                case RPCType.ObserversRPC: nm.GetModule<PlayersManager>(true).SendToAll(packet, signature.channel); break;
                case RPCType.TargetRPC: nm.GetModule<PlayersManager>(true).Send(signature.targetPlayer!.Value, packet, signature.channel); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
        [UsedByIL]
        public static bool ValidateReceivingStaticRPC(RPCInfo info, RPCSignature signature, bool asServer)
        {
            if (signature.type == RPCType.ServerRPC && !asServer ||
                signature.type != RPCType.ServerRPC && asServer)
            {
                PurrLogger.LogError($"Trying to receive {signature.type} '{signature.rpcName}' on {(asServer ? "server" : "client")}. Aborting RPC call.");
                return false;
            }
            
            return true;
        }

        readonly struct StaticGenericKey
        {
            readonly IntPtr _type;
            readonly string _methodName;
            readonly int _typesHash;
            
            public StaticGenericKey(IntPtr type, string methodName, Type[] types)
            {
                _type = type;
                _methodName = methodName;
                
                _typesHash = 0;
                
                for (int i = 0; i < types.Length; i++)
                    _typesHash ^= types[i].GetHashCode();
            }
            
            public override int GetHashCode()
            {
                return _type.GetHashCode() ^ _methodName.GetHashCode() ^ _typesHash;
            }
        }
        
        static readonly Dictionary<StaticGenericKey, MethodInfo> _staticGenericHandlers = new();
        
        [UsedByIL]
        public static void CallStaticGeneric(RuntimeTypeHandle type, string methodName, GenericRPCHeader rpcHeader)
        {
            var targetType = Type.GetTypeFromHandle(type);
            var key = new StaticGenericKey(type.Value, methodName, rpcHeader.types);

            if (!_staticGenericHandlers.TryGetValue(key, out var gmethod))
            {
                var method = targetType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                gmethod = method?.MakeGenericMethod(rpcHeader.types);

                _staticGenericHandlers[key] = gmethod;
            }
            
            if (gmethod == null)
            {
                PurrLogger.LogError($"Calling generic static RPC failed. Method '{methodName}' not found.");
                return;
            }

            gmethod.Invoke(null, rpcHeader.values);
        }

        private void SendAnyInstanceRPCs(PlayerID player, SceneID scene)
        {
            for (int i = 0; i < _bufferedRpcsDatas.Count; i++)
            {
                var data = _bufferedRpcsDatas[i];

                if (data.rpcid.sceneId != scene)
                {
                    continue;
                }

                if (!_hierarchyModule.TryGetIdentity(data.packet.sceneId, data.packet.networkId, out var identity))
                {
                    PurrLogger.LogError($"Can't find identity with id {data.packet.networkId} in scene {data.packet.sceneId}.");
                    continue;
                }

                if (data.sig.excludeOwner && _ownership.TryGetOwner(identity, out var owner) && owner == player)
                    continue;

                switch (data.sig.type)
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
                        if (data.sig.targetPlayer == player)
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
        
        private void SendAnyStaticRPCs(PlayerID player)
        {
            for (int i = 0; i < _bufferedStaticRpcsDatas.Count; i++)
            {
                var data = _bufferedStaticRpcsDatas[i];

                switch (data.sig.type)
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
                        if (data.sig.targetPlayer == player)
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
        
        readonly Dictionary<RPC_ID, RPC_DATA> _bufferedRpcsKeys = new();
        readonly Dictionary<RPC_ID, STATIC_RPC_DATA> _bufferedStaticRpcsKeys = new();
        readonly List<RPC_DATA> _bufferedRpcsDatas = new();
        readonly List<STATIC_RPC_DATA> _bufferedStaticRpcsDatas = new();

        private void AppendToBufferedRPCs(StaticRPCPacket packet, RPCSignature signature)
        {
            if (!signature.bufferLast) return;
            
            var rpcid = new RPC_ID(packet);

            if (_bufferedStaticRpcsKeys.TryGetValue(rpcid, out var data))
            {
                data.stream.ResetPointer();
                data.stream.Write(packet.data);
            }
            else
            {
                var newStream = AllocStream(false);
                newStream.Write(packet.data);
                    
                var newdata = new STATIC_RPC_DATA
                {
                    rpcid = rpcid,
                    packet = packet,
                    sig = signature,
                    stream = newStream
                };
                   
                _bufferedStaticRpcsKeys.Add(rpcid, newdata);
                _bufferedStaticRpcsDatas.Add(newdata);
            }
        }
        
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
                    sig = signature,
                    stream = newStream
                };
                   
                _bufferedRpcsKeys.Add(rpcid, newdata);
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
        
        [UsedByIL]
        public static StaticRPCPacket BuildStaticRawRPC<T>(byte rpcId, NetworkStream data)
        {
            var hash = Hasher.GetStableHashU32<T>();
            
            var rpc = new StaticRPCPacket
            {
                rpcId = rpcId,
                data = data.buffer.ToByteData(),
                typeHash = hash
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
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            var ptr = method != null ? method.MethodHandle.GetFunctionPointer() : IntPtr.Zero;
            
            if (ptr != IntPtr.Zero)
                _rpcHandlers[rpcKey] = ptr;
            
            return ptr;
        }

        static unsafe void ReceiveStaticRPC(PlayerID player, StaticRPCPacket data, bool asServer)
        {
            if (!Hasher.TryGetType(data.typeHash, out var type))
            {
                PurrLogger.LogError($"Failed to resolve type with hash {data.typeHash}.");
                return;
            }
            
            var stream = AllocStream(true);
            stream.Write(data.data);
            stream.ResetPointer();
            
            var rpcHandlerPtr = GetRPCHandler(type, data.rpcId);
            var info = new RPCInfo { sender = player };

            if (rpcHandlerPtr != IntPtr.Zero)
            {
                try
                {
                    // Call the RPC handler
                    ((delegate* managed<NetworkStream, StaticRPCPacket, RPCInfo, bool, void>)
                        rpcHandlerPtr)(stream, data, info, asServer);
                }
                catch (Exception e)
                {
                    PurrLogger.LogError($"{e.Message}\nWhile calling RPC handler for id {data.rpcId} on '{type.Name}'.\n{e.StackTrace}");
                }
            }
            else PurrLogger.LogError($"Can't find RPC handler for id {data.rpcId} on '{type.Name}'.");
            
            FreeStream(stream);
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
            // TODO: show this error if RPC was reliable ordered. otherwise, we cant assure this will arrive in time, sequence, etc so it isn't really an error
            // else PurrLogger.LogError($"Failed to find identity with id {packet.networkId} in scene {packet.sceneId} while trying to call RPC with id {packet.rpcId}.");
            
            FreeStream(stream);
        }
    }
}
