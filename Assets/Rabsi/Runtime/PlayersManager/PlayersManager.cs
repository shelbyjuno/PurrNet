using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private readonly CookiesModule _cookiesModule;
        private readonly BroadcastModule _broadcastModule;

        public PlayersManager(CookiesModule cookiesModule, BroadcastModule broadcaste)
        {
            _cookiesModule = cookiesModule;
            _broadcastModule = broadcaste;
        }

        public void Enable(bool asServer)
        {
            if (!asServer)
                _broadcastModule.RegisterCallback<string>(OnReceivedString);
        }

        private void OnReceivedString(Connection conn, string data, bool asserver)
        {
            Debug.Log($"Received string: {data} from {conn} as {(asserver ? "server" : "client")}");
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
                _broadcastModule.UnregisterCallback<string>(OnReceivedString);
        }
        
        public void OnConnected(Connection conn, bool asServer)
        {
            if (!asServer) return;
            
            _broadcastModule.SendToClient(conn, "Hello world");
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            
        }
    }
}
