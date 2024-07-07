using System.Collections.Generic;

namespace PurrNet.Modules
{
    public class ScenePlayersModule : INetworkModule
    {
        readonly Dictionary<SceneID, HashSet<PlayerID>> _scenePlayers = new ();
        
        readonly ScenesModule _scenes;
        readonly PlayersManager _players;
        
        public ScenePlayersModule(ScenesModule scenes, PlayersManager players)
        {
            _scenes = scenes;
            _players = players;
        }
        
        public void Enable(bool asServer)
        {
            if (asServer)
            {
                _scenes.onSceneLoaded += OnSceneLoaded;
                _scenes.onSceneUnloaded += OnSceneUnloaded;
            }
        }

        private void OnSceneLoaded(SceneID scene, bool asServer)
        {
            throw new System.NotImplementedException();
        }

        private void OnSceneUnloaded(SceneID scene, bool asServer)
        {
            throw new System.NotImplementedException();
        }

        private void HandleNewPlayer(PlayerID player)
        {
            
        }
        
        public void Disable(bool asServer)
        {
            if (asServer)
            {
                _scenes.onSceneLoaded -= OnSceneLoaded;
                _scenes.onSceneUnloaded -= OnSceneUnloaded;
            }
        }
    }
}
