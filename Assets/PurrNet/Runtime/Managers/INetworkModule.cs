using PurrNet.Transports;

namespace PurrNet.Modules
{
    public interface INetworkModule
    {
        void Enable(bool asServer);

        void Disable(bool asServer);
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
