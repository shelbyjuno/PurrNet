using PurrNet.Modules;
using UnityEngine.SceneManagement;

namespace PurrNet
{
    public class ScenesModule : INetworkModule
    {
        private readonly NetworkManager _networkManager;
        private readonly PlayersManager _playersManager;
        private readonly PlayersBroadcaster _broadcaster;
        
        private readonly SceneHistory _history;
        
        public ScenesModule(NetworkManager manager, PlayersManager playersManager, PlayersBroadcaster broadcaster)
        {
            _networkManager = manager;
            _playersManager = playersManager;
            _broadcaster = broadcaster;
            
            _history = new SceneHistory();
        }
        
        public void Enable(bool asServer)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            
        }
        
        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            
        }

        public void Disable(bool asServer)
        {
            throw new System.NotImplementedException();
        }
    }
}
