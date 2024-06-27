using Rabsi.Packets;
using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    public class BroadcastModule : INetworkModule
    {
        const string MODULENAME = nameof(Rabsi) + "." + nameof(BroadcastModule);

#if UNITY_EDITOR
        const string PREFIX = "<b>[" + MODULENAME + "]</b> ";
#else
        const string PREFIX = "[" + MODULENAME + "] ";
#endif
        
        private readonly ITransport _transport;

        private readonly bool _asServer;

        public BroadcastModule(NetworkManager manager, bool asServer)
        {
            _transport = manager.transport.transport;
            _asServer = asServer;
        }

        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }
        
        public void SendToAll(INetworkedData data, Channel method)
        {
            if (!_asServer)
            {
                Debug.LogError(PREFIX + "Cannot send data to all clients from client.");
                return;
            }
            
            for (int i = 0; i < _transport.connections.Count; i++)
            {
                var conn = _transport.connections[i];
                _transport.SendToClient(conn, default, method);
            }
        }
        
        public void SendToClient(Connection conn, INetworkedData data, Channel method)
        {
            if (!_asServer)
            {
                Debug.LogError(PREFIX + "Cannot send data to client from client.");
                return;
            }
            
            _transport.SendToClient(conn, default, method);
        }
        
        public void SendToServer(INetworkedData data, Channel method)
        {
            if (_asServer)
            {
                // TODO: skip the server and raise the event directly
                _transport.RaiseDataReceived(default, default, true);
                return;
            }

            _transport.SendToServer(default, method);
        }
    }
}
