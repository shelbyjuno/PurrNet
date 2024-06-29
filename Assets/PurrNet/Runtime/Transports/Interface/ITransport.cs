using System;
using System.Collections.Generic;

namespace PurrNet.Transports
{
    public delegate void OnConnectionState(ConnectionState state, bool asServer);
    public delegate void OnDataReceived(Connection conn, ByteData data, bool asServer);
    public delegate void OnConnected(Connection conn, bool asServer);
    public delegate void OnDisconnected(Connection conn, bool asServer);
    
    public enum ConnectionState
    {
        Connecting,
        Connected,
        
        Disconnected,
        Disconnecting
    }

    public readonly struct ByteData
    {
        public readonly byte[] data;
        public readonly int length;
        public readonly int offset;
        
        public ReadOnlySpan<byte> span => new (data, offset, length);

        public ByteData(byte[] data, int offset, int length)
        {
            this.data = data;
            this.offset = offset;
            this.length = length;
        }
    }
    
    public enum Channel : byte
    {
        ReliableUnordered,
        Sequenced,
        ReliableOrdered,
        ReliableSequenced,
        Unreliable
    }
    
    public interface IConnectable
    {
        ConnectionState clientState { get; }
        
        void Connect(string up, ushort port);
        
        void Disconnect();
    }
    
    public interface IListener
    {
        ConnectionState listenerState { get; }

        void Listen(ushort port);
        
        void StopListening();
    }
    
    public interface ITransport : IListener, IConnectable
    {
        event OnConnected onConnected;
        event OnDisconnected onDisconnected;
        event OnDataReceived onDataReceived;
        event OnConnectionState onConnectionState;
        
        public IReadOnlyList<Connection> connections { get; }

        void RaiseDataReceived(Connection conn, ByteData data, bool asServer);
        
        void SendToClient(Connection target, ByteData data, Channel method = Channel.Unreliable);
        
        void SendToServer(ByteData data, Channel method = Channel.Unreliable);
        
        void CloseConnection(Connection conn);
    }
}
