using System;
using System.Collections.Generic;
using Rabsi.Packets;
using Rabsi.Transports;
using Rabsi.Utils;
using UnityEngine;

namespace Rabsi.Modules
{
    public delegate void BroadcastDelegate<in T>(Connection conn, T data, bool asServer);
    
    public enum PacketType : byte
    {
        Ping = 0,
        Broadcast = 69
    }

    internal interface IBroadcastCallback
    {
        bool IsSame(object callback);
        
        void TriggerCallback(Connection conn, object data, bool asServer);
    }

    internal readonly struct BroadcastCallback<T> : IBroadcastCallback
    {
        readonly BroadcastDelegate<T> callback;
        
        public BroadcastCallback(BroadcastDelegate<T> callback)
        {
            this.callback = callback;
        }

        public bool IsSame(object callbackToCmp)
        {
            return callbackToCmp is BroadcastDelegate<T> action && action == callback;
        }

        public void TriggerCallback(Connection conn, object data, bool asServer)
        {
            if (data is T value)
                callback?.Invoke(conn, value, asServer);
        }
    }
    
    public class BroadcastModule : INetworkModule, IDataListener
    {
        const string MODULENAME = nameof(Rabsi) + "." + nameof(BroadcastModule);

#if UNITY_EDITOR
        const string PREFIX = "<b>[" + MODULENAME + "]</b> ";
#else
        const string PREFIX = "[" + MODULENAME + "] ";
#endif
        
        private readonly ITransport _transport;

        private readonly bool _asServer;

        private readonly ByteBuffer _stream;
        
        private readonly Dictionary<uint, List<IBroadcastCallback>> _actions = new();
        
        public BroadcastModule(NetworkManager manager, bool asServer)
        {
            _transport = manager.transport.transport;
            _asServer = asServer;
            _stream = new ByteBuffer();
        }

        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }

        void AssertIsServer(string message)
        {
            if (!_asServer)
                throw new InvalidOperationException(PREFIX + message);
        }

        private static void WriteHeader(NetworkStream stream, Type typeData)
        {
            byte type = (byte)PacketType.Broadcast;
            var typeId = Hasher.GetStableHashU32(typeData);

            stream.Serialize(ref type);
            stream.Serialize<uint>(ref typeId);
        }

        private ByteData GetData<T>(T data)
        {
            _stream.Clear();
            
            var stream = new NetworkStream(_stream, false);
            
            WriteHeader(stream, typeof(T));

            if (data is INetworkedData networkedData)
            {
                networkedData.Serialize(stream);
            }
            else
            {
                stream.Serialize(ref data);
            }

            return _stream.ToByteData();
        }
        
        public void SendToAll<T>(T data, Channel method = Channel.ReliableOrdered) where T : unmanaged
        {
            AssertIsServer("Cannot send data to all clients from client.");

            var byteData = GetData(data);
            
            for (int i = 0; i < _transport.connections.Count; i++)
            {
                var conn = _transport.connections[i];
                _transport.SendToClient(conn, byteData, method);
            }
        }
        
        public void SendToClient<T>(Connection conn, T data, Channel method = Channel.ReliableOrdered)
        {
            if (!_asServer)
                throw new InvalidOperationException(PREFIX + "Cannot send data to client from client.");
            
            var byteData = GetData(data);
            _transport.SendToClient(conn, byteData, method);
        }
        
        public void SendToServer<T>(T data, Channel method = Channel.ReliableOrdered)
        {
            var byteData = GetData(data);

            if (_asServer)
            {
                _transport.RaiseDataReceived(default, byteData, true);
                return;
            }

            _transport.SendToServer(byteData, method);
        }
        
        public void OnDataReceived(Connection conn, ByteData data, bool asServer)
        {
            _stream.Clear();
            _stream.Write(data);
            _stream.ResetPointer();

            var stream = new NetworkStream(_stream, true);

            byte type = _stream.ReadByte();

            const byte expected = (byte)PacketType.Broadcast;

            if (type != expected)
                return;
            
            uint typeId = 0;
            stream.Serialize<uint>(ref typeId);

            if (!Hasher.TryGetType(typeId, out var typeInfo))
            {
                Debug.LogWarning($"{PREFIX} Cannot find type with id {typeId}; probably nothing is listening to this type.");
                return;
            }

            var instance = Activator.CreateInstance(typeInfo);
            
            if (instance is INetworkedData networkData)
                 networkData.Serialize(stream);
            else stream.Serialize(ref instance);
            
            TriggerCallback(conn, typeId, instance, asServer);
        }

        public void RegisterCallback<T>(BroadcastDelegate<T> callback)
        {
            var hash = Hasher.GetStableHashU32(typeof(T));
            
            if (_actions.TryGetValue(hash, out var actions))
            {
                actions.Add(new BroadcastCallback<T>(callback));
                return;
            }
            
            _actions.Add(hash, new List<IBroadcastCallback>()
            {
                new BroadcastCallback<T>(callback)
            });
        }
        
        public void UnregisterCallback<T>(BroadcastDelegate<T> callback)
        {
            var hash = Hasher.GetStableHashU32(typeof(T));
            
            if (!_actions.TryGetValue(hash, out var actions))
                return;
            
            object boxed = callback;

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].IsSame(boxed))
                {
                    actions.RemoveAt(i);
                    return;
                }
            }
        }

        private void TriggerCallback(Connection conn, uint hash, object instance, bool asServer)
        {
            if (_actions.TryGetValue(hash, out var actions))
            {
                for (int i = 0; i < actions.Count; i++)
                    actions[i].TriggerCallback(conn, instance, asServer);
            }
        }
    }
}
