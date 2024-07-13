using System.Collections.Generic;

namespace PurrNet.Modules
{
    public class GlobalOwnershipModule : INetworkModule
    {
        readonly NetworkManager _manager;
        readonly ScenesModule _scenes;
        readonly Dictionary<SceneID, SceneOwnership> _sceneOwnerships = new ();
        
        public GlobalOwnershipModule(NetworkManager manager, ScenesModule scenes)
        {
            _scenes = scenes;
            _manager = manager;
        }
        
        public void Enable(bool asServer)
        {
            _scenes.onPreSceneLoaded += OnSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
        }

        public void Disable(bool asServer)
        {
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            if (_sceneOwnerships.TryGetValue(scene, out var module))
            {
                module.Disable(asserver);
                _sceneOwnerships.Remove(scene);
            }
        }
        
        public void GiveOwnership(NetworkIdentity id, PlayerID player)
        {
            if (_sceneOwnerships.TryGetValue(id.sceneId, out var module))
                module.GiveOwnership(id, player);
        }
        
        public void RemoveOwnership(NetworkIdentity id)
        {
            if (_sceneOwnerships.TryGetValue(id.sceneId, out var module))
                module.RemoveOwnership(id);
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
            _sceneOwnerships[scene] = new SceneOwnership();
            _sceneOwnerships[scene].Enable(asServer);
        }
    }

    internal class SceneOwnership : INetworkModule
    {
        Dictionary<int, PlayerID> _owners = new ();
        
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

        public void GiveOwnership(NetworkIdentity id, PlayerID player)
        {
            
            
        }

        public void RemoveOwnership(NetworkIdentity id)
        {
            
        }
    }
}
