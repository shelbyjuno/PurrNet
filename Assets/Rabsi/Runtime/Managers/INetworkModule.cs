using Rabsi.Transports;

namespace Rabsi
{
    internal interface INetworkModule
    {
        int priority { get; }

        void Setup(NetworkManager manager);
    }
    
    internal interface IConnectionListener
    {
        void OnConnected(Connection conn, bool asServer);
        
        void OnDisconnected(Connection conn, bool asServer);
    }
    
    internal interface IConnectionStateListener
    {
        void OnConnectionState(ConnectionState state, bool asServer);
    }
    
    internal interface IDataListener
    {
        void OnDataReceived(Connection conn, ByteData data, bool asServer);
    }
}
