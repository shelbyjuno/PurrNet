using Rabsi.Transports;
using UnityEngine;

namespace Rabsi
{
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        public const int PRIORITY = -1000;
        
        public int priority => PRIORITY;
        
        private NetworkManager _manager;
        
        public void Setup(NetworkManager manager)
        {
            _manager = manager;
        }

        public void OnConnected(Connection conn, bool asServer)
        {
            Debug.Log($"Player connected: {conn}; {asServer}");
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            Debug.Log($"Player disconnected: {conn}; {asServer}");
        }
    }
}
