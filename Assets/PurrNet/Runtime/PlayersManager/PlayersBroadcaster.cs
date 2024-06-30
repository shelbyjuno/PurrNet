using System.Collections.Generic;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    internal interface IPlayerBroadcastCallback
    {
        bool IsSame(object callback);
        
        void TriggerCallback(Connection conn, object data, bool asServer);
    }
    
    public class PlayersBroadcaster : INetworkModule
    {
        private BroadcastModule _broadcastModule;
        private PlayersManager _playersManager;
        
        private readonly Dictionary<uint, List<IPlayerBroadcastCallback>> _clientActions = new();
        private readonly Dictionary<uint, List<IBroadcastCallback>> _serverActions = new();
        
        public PlayersBroadcaster(BroadcastModule broadcastModule, PlayersManager playersManager)
        {
            _broadcastModule = broadcastModule;
            _playersManager = playersManager;
        }
        
        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }
        
        private void SendToPlayer<T>(PlayerID player, ByteData data)
        {
            
        }
        
        public void Send<T>(PlayerID player, T data)
        {
            var byteData = BroadcastModule.GetData(data);
            
        }
        
        public void Send<T>(IEnumerator<PlayerID> players, T data)
        {
            var byteData = BroadcastModule.GetData(data);

        }
    }
}
