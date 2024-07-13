using System.Collections.Generic;

namespace PurrNet.Modules
{
    public class GlobalOwnershipModule : INetworkModule
    {
        readonly ScenesModule _scenes;
        readonly Dictionary<SceneID, SceneOwnership> _sceneOwnerships = new ();
        
        public GlobalOwnershipModule(ScenesModule scenes)
        {
            _scenes = scenes;
        }
        
        public void Enable(bool asServer)
        {
            _scenes.onPreSceneLoaded += OnSceneLoaded;
        }

        public void Disable(bool asServer)
        {
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
        }

        public bool TryGetOwner(NetworkIdentity id, out PlayerID player)
        {
            if (_sceneOwnerships.TryGetValue(id.sceneId, out var module) && module.TryGetOwner(id, out player))
                return true;
            
            player = default;
            return false;
        }

        private void OnSceneLoaded(SceneID scene, bool asServer)
        {
            if (_sceneOwnerships.TryGetValue(scene, out var module))
            {
                module.Enable(asServer);
                return;
            }
            
            _sceneOwnerships[scene] = new SceneOwnership();
            _sceneOwnerships[scene].Enable(asServer);
        }
    }

    internal class SceneOwnership : INetworkModule
    {
        public void Enable(bool asServer)
        {
            
        }
        
        public bool TryGetOwner(NetworkIdentity id, out PlayerID player)
        {
            player = default;
            return false;
        }

        public void Disable(bool asServer)
        {
            
        }
    }
}
