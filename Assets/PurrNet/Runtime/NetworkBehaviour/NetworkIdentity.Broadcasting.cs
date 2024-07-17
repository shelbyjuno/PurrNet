using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public partial class NetworkIdentity : IPlayerBroadcaster
    {
        PlayersManager _playersManagerServer;
        ScenePlayersModule _scenePlayersModuleServer;
        
        PlayersManager _playersManagerClient;
        ScenePlayersModule _scenePlayersModuleClient;

        private void OnSpawnedBroadcasting(bool asServer)
        {
            if (asServer)
            {
                _playersManagerServer = networkManager.GetModule<PlayersManager>(true);
                _scenePlayersModuleServer = networkManager.GetModule<ScenePlayersModule>(true);

                if (networkManager.isClient)
                {
                    _playersManagerClient = networkManager.GetModule<PlayersManager>(false);
                    _scenePlayersModuleClient = networkManager.GetModule<ScenePlayersModule>(false);
                }
            }
            else
            {
                _playersManagerClient = networkManager.GetModule<PlayersManager>(false);
                _scenePlayersModuleClient = networkManager.GetModule<ScenePlayersModule>(false);
            }
        }
        
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            _playersManagerServer?.Unsubscribe(callback);
            _playersManagerClient?.Unsubscribe(callback);
        }

        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
        {
            _playersManagerServer?.Subscribe(callback);
            _playersManagerClient?.Subscribe(callback);
        }

        public void SendToAll<T>(T packet, Channel method = Channel.ReliableOrdered)
        {
            if (_playersManagerServer != null)
                SendToAll(_playersManagerServer, _scenePlayersModuleServer, packet, method);
            
            if (_playersManagerClient != null)
                SendToAll(_playersManagerClient, _scenePlayersModuleClient, packet, method);
        }
        
        private void SendToAll<T>(IPlayerBroadcaster players, ScenePlayersModule scene, T packet, Channel method = Channel.ReliableOrdered)
        {
            if (scene.TryGetPlayersInScene(sceneId, out var playersInScene))
                players.Send(playersInScene, packet, method);
        }

        public void Send<T>(PlayerID player, T data, Channel method = Channel.ReliableOrdered)
        {
            if (_playersManagerServer != null)
                Send(_playersManagerServer, _scenePlayersModuleServer, player, data, method);
            
            if (_playersManagerClient != null)
                Send(_playersManagerClient, _scenePlayersModuleClient, player, data, method);
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
            if (_playersManagerServer != null)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                Send(_playersManagerServer, _scenePlayersModuleServer, players, data, method);
            }

            if (_playersManagerClient != null)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                Send(_playersManagerClient, _scenePlayersModuleClient, players, data, method);
            }
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
            _playersManagerClient?.SendToServer(packet, method);
        }
    }
}
