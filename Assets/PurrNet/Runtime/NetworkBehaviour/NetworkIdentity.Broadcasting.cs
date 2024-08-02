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
    public struct GenericRPCHeader
    {
        public NetworkStream stream;
        public uint hash;
        public Type[] types;
        public object[] values;
        public RPCInfo info;
        
        [UsedImplicitly]
        public void SetPlayerId(PlayerID player, int index)
        {
            values[index] = player;
        }
        
        [UsedImplicitly]
        public void SetInfo(int index)
        {
            values[index] = info;
        }
        
        [UsedImplicitly]
        public void Read(int genericIndex, int index)
        {
            object value = default;
            stream.Serialize(types[genericIndex], ref value);
            values[index] = value;
        }
        
        [UsedImplicitly]
        public void Read<T>(int index)
        {
            T value = default;
            stream.Serialize(ref value);
            values[index] = value;
        }
    }
    
    public partial class NetworkIdentity
    {
        static readonly Dictionary<string, MethodInfo> _rpcMethods = new ();
        
        [UsedImplicitly]
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
    
        [UsedImplicitly]
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
        
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            if (networkManager.isClient)
            {
                networkManager.GetModule<PlayersManager>(false).Unsubscribe(callback);
            }
            
            if (networkManager.isServer)
            {
                networkManager.GetModule<PlayersManager>(true).Unsubscribe(callback);
            }
        }

        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            if (networkManager.isClient)
            {
                networkManager.GetModule<PlayersManager>(false).Subscribe(callback);
            }
            
            if (networkManager.isServer)
            {
                networkManager.GetModule<PlayersManager>(true).Subscribe(callback);
            }
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
