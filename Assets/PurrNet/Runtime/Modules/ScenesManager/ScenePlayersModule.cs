using System.Collections.Generic;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public delegate void OnPlayerSceneEvent(PlayerID player, SceneID scene, bool asserver);

    public class ScenePlayersModule : INetworkModule
    {
        private readonly Dictionary<SceneID, HashSet<PlayerID>> _scenePlayers = new();
        
        readonly ScenesModule _scenes;
        readonly PlayersManager _players;
        
        public event OnPlayerSceneEvent onPlayerJoinedScene;
        public event OnPlayerSceneEvent onPlayerLoadedScene;
        
        public event OnPlayerSceneEvent onPlayerLeftScene;
        public event OnPlayerSceneEvent onPlayerUnloadedScene;
        
        private bool _asServer;
        
        public ScenePlayersModule(ScenesModule scenes, PlayersManager players)
        {
            _scenes = scenes;
            _players = players;
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            if (asServer)
            {
                for (var i = 0; i < _scenes.scenes.Count; i++)
                {
                    var scene = _scenes.scenes[i];
                    OnSceneLoaded(scene, true);
                }

                _scenes.onSceneLoaded += OnSceneLoaded;
                _scenes.onSceneUnloaded += OnSceneUnloaded;
                _scenes.onSceneVisibilityChanged += OnSceneVisibilityChanged;
                
                _players.onPrePlayerJoined += OnPlayerJoined;
                _players.onPrePlayerLeft += OnPlayerLeft;
            }
        }
        
        public void Disable(bool asServer)
        {
            if (asServer)
            {
                _scenes.onSceneLoaded -= OnSceneLoaded;
                _scenes.onSceneUnloaded -= OnSceneUnloaded;
                _scenes.onSceneVisibilityChanged -= OnSceneVisibilityChanged;
                
                _players.onPrePlayerJoined -= OnPlayerJoined;
                _players.onPrePlayerLeft -= OnPlayerLeft;
            }
        }

        private void OnSceneVisibilityChanged(SceneID scene, bool isPublic, bool asServer)
        {
            if (!isPublic) return;
            
            if (!_scenePlayers.TryGetValue(scene, out var playersInScene))
                return;
            
            // if the scene is public, add all connected players to the scene
            int connectedPlayersCount = _players.connectedPlayers.Count;

            for (int i = 0; i < connectedPlayersCount; i++)
            {
                var player = _players.connectedPlayers[i];
                playersInScene.Add(player);

                onPlayerJoinedScene?.Invoke(player, scene, asServer);
            }
        }

        private void OnPlayerJoined(PlayerID player, bool asserver)
        {
            for (var i = 0; i < _scenes.scenes.Count; i++)
            {
                var scene = _scenes.scenes[i];
                if (!_scenes.TryGetSceneState(scene, out var state))
                    continue;

                if (!state.settings.isPublic)
                    continue;

                AddPlayerToScene(player, scene);
            }
        }
        
        private void OnPlayerLeft(PlayerID player, bool asserver)
        {
            foreach (var (scene, players) in _scenePlayers)
            {
                if (!players.Contains(player))
                    continue;
                
                RemovePlayerFromScene(player, scene);
            }
        }

        public bool IsPlayerInScene(PlayerID player, SceneID scene)
        {
            return _scenePlayers.TryGetValue(scene, out var playersInScene) && playersInScene.Contains(player);
        }
        
        public void AddPlayerToScene(PlayerID player, SceneID scene)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("AddPlayerToScene can only be called on the server; for now ;)");
                return;
            }
            
            if (!_scenePlayers.TryGetValue(scene, out var playersInScene))
            {
                PurrLogger.LogError($"SceneID '{scene}' not found in scenes module; aborting AddPlayerToScene");
                return;
            }
            
            playersInScene.Add(player);
            onPlayerJoinedScene?.Invoke(player, scene, _asServer);
        }
        
        public void RemovePlayerFromScene(PlayerID player, SceneID scene)
        {
            if (!_asServer)
            {
                PurrLogger.LogError("RemovePlayerFromScene can only be called on the server; for now ;)");
                return;
            }
            
            if (!_scenePlayers.TryGetValue(scene, out var playersInScene))
            {
                PurrLogger.LogError($"SceneID '{scene}' not found in scenes module; aborting RemovePlayerFromScene");
                return;
            }
            
            playersInScene.Remove(player);
            
            onPlayerLeftScene?.Invoke(player, scene, _asServer);
            onPlayerUnloadedScene?.Invoke(player, scene, _asServer);
        }

        private void OnSceneLoaded(SceneID scene, bool asServer)
        {
            if (!_scenes.TryGetSceneState(scene, out var state))
            {
                PurrLogger.LogError($"SceneID '{scene}' not found in scenes module");
                return;
            }

            var playersInScene = new HashSet<PlayerID>();
            _scenePlayers.Add(scene, playersInScene);
            
            OnSceneVisibilityChanged(scene, state.settings.isPublic, asServer);
        }
        
        private void OnSceneUnloaded(SceneID scene, bool asServer)
        {
            if (_scenePlayers.TryGetValue(scene, out var playersInScene))
            {
                // remove all players from the scene
                foreach (var player in playersInScene)
                {
                    onPlayerLeftScene?.Invoke(player, scene, asServer);
                    onPlayerUnloadedScene?.Invoke(player, scene, asServer);
                }
                
                _scenePlayers.Remove(scene);
            }
        }
    }
}
