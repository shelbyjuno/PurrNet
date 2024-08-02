using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine.Scripting;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        static readonly Dictionary<string, MethodInfo> _rpcMethods = new ();
        
        [UsedByIL]
        protected static void ReadGenericHeader(NetworkStream stream, RPCInfo info, int genericCount, int paramCount, out GenericRPCHeader rpcHeader)
        {
            uint hash = 0;

            rpcHeader = new GenericRPCHeader
            {
                stream = stream,
                types = new Type[genericCount],
                values = new object[paramCount],
                info = info
            };
            
            for (int i = 0; i < genericCount; i++)
            {
                stream.Serialize<uint>(ref hash);
                var type = Hasher.ResolveType(hash);

                rpcHeader.types[i] = type;
            }
        }
    
        [UsedByIL]
        protected void CallGeneric(string methodName, GenericRPCHeader rpcHeader)
        { 
            if (_rpcMethods.TryGetValue(methodName, out var genericMethod))
            {
                genericMethod.Invoke(this, rpcHeader.values);
                return;
            }
            
            var method = GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        
            if (method == null)
            {
                PurrLogger.LogError("Calling generic RPC failed. Method not found.");
                return;
            }

            var gmethod = method.MakeGenericMethod(rpcHeader.types);
            _rpcMethods.Add(methodName, gmethod);
            gmethod.Invoke(this, rpcHeader.values);
        }
        
        [UsedByIL]
        protected void SendRPC(RPCPacket packet, RPCSignature signature)
        {
            if (!isSpawned)
            {
                PurrLogger.LogError($"Trying to send RPC from '{name}' which is not spawned.", this);
                return;
            }

            if (!networkManager.TryGetModule<RPCModule>(networkManager.isServer, out var module))
            {
                PurrLogger.LogError("Failed to get RPC module.", this);
                return;
            }
            
            if (signature.requireOwnership && !isOwner)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{name}' without ownership.", this);
                return;
            }
            
            if (signature.requireServer && !networkManager.isServer)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{name}' without server.", this);
                return;
            }
            
            module.AppendToBufferedRPCs(packet, signature);

            Func<PlayerID, bool> predicate = null;
            
            if (signature.excludeOwner)
                predicate = IsNotOwnerPredicate;

            switch (signature.type)
            {
                case RPCType.ServerRPC: SendToServer(packet, signature.channel); break;
                case RPCType.ObserversRPC: SendToObservers(packet, predicate, signature.channel); break;
                case RPCType.TargetRPC: SendToTarget(signature.targetPlayer!.Value, packet, signature.channel); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
        [UsedByIL]
        protected bool ValidateReceivingRPC(RPCInfo info, RPCSignature signature, bool asServer)
        {
            if (signature.requireOwnership && info.sender != owner)
            {
                PurrLogger.LogError($"Sender '{info.sender}' of RPC '{signature.rpcName}' from '{name}' is not the owner. Aborting RPC call.", this);
                return false;
            }

            if (signature.type == RPCType.ServerRPC && !asServer)
            {
                PurrLogger.LogError($"Trying to receive server RPC '{signature.rpcName}' from '{name}' on client. Aborting RPC call.", this);
                return false;
            }
            
            if (signature.type != RPCType.ServerRPC && asServer)
            {
                PurrLogger.LogError($"Trying to receive client RPC '{signature.rpcName}' from '{name}' on server. Aborting RPC call.", this);
                return false;
            }

            if (signature.excludeOwner && isOwner)
            {
                PurrLogger.LogError($"Trying to receive RPC '{signature.rpcName}' from '{name}' with exclude owner on owner. Aborting RPC call.", this);
                return false;
            }
            
            return true;
        }
        
        static readonly List<PlayerID> _players = new ();

        public void SendToObservers<T>(T packet, [CanBeNull] Func<PlayerID, bool> predicate, Channel method = Channel.ReliableOrdered)
        {
            if (!networkManager.TryGetModule<ScenePlayersModule>(isServer, out var scene))
            {
                PurrLogger.LogError("Trying to send packet to observers without scene module.", this);
                return;
            }
                
            if (scene.TryGetPlayersInScene(sceneId, out var playersInScene))
            {
                _players.Clear();
                _players.AddRange(playersInScene);

                if (predicate != null)
                {
                    for (int i = 0; i < _players.Count; i++)
                    {
                        if (!predicate(_players[i]))
                            _players.RemoveAt(i--);
                    }
                }

                Send(_players, packet, method);
            }
        }

        public void Send<T>(PlayerID player, T packet, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isServer)
                networkManager.GetModule<PlayersManager>(true).Send(player, packet, method);
        }
        
        [Preserve]
        public void SendToTarget<T>(PlayerID player, T packet, Channel method = Channel.ReliableOrdered)
        {
            Send(player, packet, method);
        }
        
        public void Send<T>(IEnumerable<PlayerID> players, T data, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isServer)
                networkManager.GetModule<PlayersManager>(true).Send(players, data, method);
        }
        
        public void SendToServer<T>(T packet, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isClient)
                networkManager.GetModule<PlayersManager>(false).SendToServer(packet, method);
        }
    }
}
