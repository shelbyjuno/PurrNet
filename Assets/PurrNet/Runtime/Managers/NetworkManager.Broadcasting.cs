using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public sealed partial class NetworkManager
    {
        [UsedImplicitly]
        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback, bool asServer)  where T : new()
        {
            var broadcaster = GetModule<PlayersBroadcaster>(asServer);
            broadcaster.Subscribe(callback);
        }
        
        [UsedImplicitly]
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback, bool asServer) where T : new()
        {
            var broadcaster = GetModule<PlayersBroadcaster>(asServer);
            broadcaster.Unsubscribe(callback);
        }

        [UsedImplicitly]
        public void Send<T>(PlayerID player, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.Send(player, data, method);
        }

        [UsedImplicitly]
        public void Send<T>(IEnumerable<PlayerID> playersCollection, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.Send(playersCollection, data, method);
        }
        
        [UsedImplicitly]
        public void SendToScene<T>(SceneID sceneId, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            var scenePlayers = GetModule<ScenePlayersModule>(true);
            
            if (scenePlayers.TryGetPlayersInScene(sceneId, out var playersInScene))
                broadcaster.Send(playersInScene, data, method);
        }

        [UsedImplicitly]
        public void SendToServer<T>(T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(false);
            broadcaster.SendToServer(data, method);
        }

        [UsedImplicitly]
        public void SendToAll<T>(T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.SendToAll(data, method);
        }
    }
}
