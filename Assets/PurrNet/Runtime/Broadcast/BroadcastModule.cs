using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MemoryPack;
using PurrNet.Packets;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet.Modules
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
        const string MODULENAME = nameof(PurrNet) + "." + nameof(BroadcastModule);

#if UNITY_EDITOR
        const string PREFIX = "<b>[" + MODULENAME + "]</b> ";
#else
        const string PREFIX = "[" + MODULENAME + "] ";
#endif
        
        private readonly ITransport _transport;

        private readonly bool _asServer;

        private readonly Dictionary<uint, List<IBroadcastCallback>> _clientActions = new();
        private readonly Dictionary<uint, List<IBroadcastCallback>> _serverActions = new();
        
        public BroadcastModule(NetworkManager manager, bool asServer)
        {
            _transport = manager.transport.transport;
            _asServer = asServer;
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
        
        private static ByteData GetData<T>(T data)
        {
            var dataStream = ByteBufferPool.Alloc();
            var stream = new NetworkStream(dataStream, false);
            
            WriteHeader(stream, typeof(T));

            try
            {
                stream.Serialize(ref data);
            }
            catch (MemoryPackSerializationException e)
            {
                throw new MemoryPackSerializationException($"{PREFIX}Cannot serialize {typeof(T).Name}, add the IAutoNetworkedData interface to the class.\n{e.Message}");
            }

            var value = dataStream.ToByteData();
            ByteBufferPool.Free(dataStream);
            return value;
        }
        
        
        public void SendToAll<T>(T data, Channel method = Channel.ReliableOrdered)
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
            var dataStream = ByteBufferPool.Alloc();

            dataStream.Write(data);
            dataStream.ResetPointer();

            var stream = new NetworkStream(dataStream, true);

            byte type = dataStream.ReadByte();

            const byte expected = (byte)PacketType.Broadcast;

            if (type != expected)
                return;
            
            uint typeId = 0;
            stream.Serialize<uint>(ref typeId);

            if (!Hasher.TryGetType(typeId, out var typeInfo))
            {
                Debug.LogWarning($"{PREFIX}Cannot find type with id {typeId}; probably nothing is listening to this type.");
                return;
            }

            //var instance = Activator.CreateInstance(typeInfo);
            object instance = null;
            
            //T obj = default (T);

            stream.Serialize(typeInfo, ref instance);
            
            ByteBufferPool.Free(dataStream);

            TriggerCallback(conn, typeId, instance, asServer);
        }

        public void RegisterCallback<T>(BroadcastDelegate<T> callback, bool asServer) where T : new()
        {
            RegisterTypeForSerializer<T>();

            var hash = Hasher.GetStableHashU32(typeof(T));
            var action = asServer ? _serverActions : _clientActions;

            if (action.TryGetValue(hash, out var actions))
            {
                actions.Add(new BroadcastCallback<T>(callback));
                return;
            }
            
            action.Add(hash, new List<IBroadcastCallback>
            {
                new BroadcastCallback<T>(callback)
            });
        }

        private static void RegisterTypeForSerializer<T>() where T : new()
        {
            if (!MemoryPackFormatterProvider.IsRegistered<T>())
            {
                if (typeof(INetworkedData).IsAssignableFrom(typeof(T)))
                {
                    Debug.LogWarning($"{PREFIX}Type {typeof(T).Name} is not registered in the MemoryPackFormatterProvider. Registering it now.");
                    RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
                }
                else if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    MemoryPackFormatterProvider.Register(new UnmanagedFormatterUnsage<T>());
                }
            }
        }

        public void UnregisterCallback<T>(BroadcastDelegate<T> callback, bool asServer)
        {
            var hash = Hasher.GetStableHashU32(typeof(T));
            var action = asServer ? _serverActions : _clientActions;
            if (!action.TryGetValue(hash, out var actions))
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
            var action = asServer ? _serverActions : _clientActions;

            if (action.TryGetValue(hash, out var actions))
            {
                for (int i = 0; i < actions.Count; i++)
                    actions[i].TriggerCallback(conn, instance, asServer);
            }
        }
    }
}
