using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public partial class NetworkIdentity : IPlayerBroadcaster
    {
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            if (networkManager.isClient)
            {
                networkManager.GetModule<PlayersManager>(false).Unsubscribe(callback);
            }
            
            if (networkManager.isServer)
            {
                networkManager.GetModule<PlayersManager>(true).Unsubscribe(callback);
            }
        }

        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            if (networkManager.isClient)
            {
                networkManager.GetModule<PlayersManager>(false).Subscribe(callback);
            }
            
            if (networkManager.isServer)
            {
                networkManager.GetModule<PlayersManager>(true).Subscribe(callback);
            }
        }

        public void SendToAll<T>(T packet, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isServer)
                networkManager.GetModule<PlayersManager>(true).SendToAll(packet, method);
        }

        public void Send<T>(PlayerID player, T packet, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isServer)
                networkManager.GetModule<PlayersManager>(true).Send(player, packet, method);
        }
        
        private void Send<T>(IPlayerBroadcaster players, ScenePlayersModule scene, PlayerID player, T packet, Channel method = Channel.ReliableOrdered)
        {
            if (scene.TryGetPlayersInScene(sceneId, out var playersInScene))
            {
                if (playersInScene.Contains(player))
                    players.Send(playersInScene, packet, method);
                else PurrLogger.LogError($"Player {player} is not in scene {sceneId}, can't send packet '{typeof(T).Name}' to him.");
            }
        }

        public void Send<T>(IEnumerable<PlayerID> players, T data, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isServer)
                networkManager.GetModule<PlayersManager>(true).Send(players, data, method);
        }
        
        static readonly List<PlayerID> _playersList = new ();
        
        private void Send<T>(IPlayerBroadcaster players, ScenePlayersModule scene, IEnumerable<PlayerID> targets, T data, Channel method = Channel.ReliableOrdered)
        {
            _playersList.Clear();
            
            if (scene.TryGetPlayersInScene(sceneId, out var playersInScene))
            {
                foreach (var target in targets)
                {
                    if (!playersInScene.Contains(target))
                        PurrLogger.LogError($"Player {target} is not in scene {sceneId}, can't send packet '{typeof(T).Name}' to him.");
                    else _playersList.Add(target);
                }
                
                if (_playersList.Count > 0)
                    players.Send(_playersList, data, method);
            }
        }

        public void SendToServer<T>(T packet, Channel method = Channel.ReliableOrdered)
        {
            if (networkManager.isClient)
                networkManager.GetModule<PlayersManager>(false).SendToServer(packet, method);
        }
    }
}
