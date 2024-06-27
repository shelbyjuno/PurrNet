using Rabsi.Transports;

namespace Rabsi.Modules
{
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private CookiesModule _cookiesModule;

        public PlayersManager(CookiesModule cookiesModule)
        {
            _cookiesModule = cookiesModule;
        }
        
        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }
        
        public void OnConnected(Connection conn, bool asServer)
        {
            
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            
        }
    }
}
