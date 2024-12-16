using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public class VisibilityFactory : INetworkModule, IFixedUpdate
    {
        private readonly NetworkManager _manager;
        private readonly HierarchyModule _hierarchy;
        private readonly ScenePlayersModule _players;
        private readonly PlayersManager _playersManager;

        private readonly Dictionary<SceneID, VisibilityManager> _sceneToVisibilityManager = new ();
        private readonly List<VisibilityManager> _visibilityManagers = new ();
        
        public event VisibilityChanged onLateObserverAdded;
        public event VisibilityChanged onLateObserverRemoved;
        
        public void TriggerLateObserverAdded(PlayerID player, NetworkIdentity id) => onLateObserverAdded?.Invoke(player, id);

        public void TriggerLateObserverRemoved(PlayerID player, NetworkIdentity id) => onLateObserverRemoved?.Invoke(player, id);

        public VisibilityFactory(NetworkManager manager, PlayersManager playersManager, HierarchyModule hierarchy, ScenePlayersModule players)
        {
            _manager = manager;
            _playersManager = playersManager;
            _hierarchy = hierarchy;
            _players = players;
        }

        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }
        
        public bool OnSceneLoaded(SceneID scene, bool asServer, out VisibilityManager manager)
        {
            if (!_hierarchy.TryGetHierarchy(scene, out var hierarchy))
            {
                PurrLogger.LogError("Hierarchy not found for scene " + scene);
                manager = null;
                return false;
            }
            
            if (!_sceneToVisibilityManager.ContainsKey(scene))
            {
                var visibility = new VisibilityManager(_manager, _playersManager, hierarchy, _players, scene);
                
                _visibilityManagers.Add(visibility);
                _sceneToVisibilityManager.Add(scene, visibility);
                
                visibility.Enable(asServer);

                manager = visibility;
                return true;
            }

            manager = null;
            return false;
        }

        public void OnSceneUnloaded(SceneID scene, bool asServer)
        {
            if (_sceneToVisibilityManager.TryGetValue(scene, out var visibility))
            {
                visibility.Disable(asServer);
                
                _visibilityManagers.Remove(visibility);
                _sceneToVisibilityManager.Remove(scene);
            }
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < _visibilityManagers.Count; i++)
                _visibilityManagers[i].FixedUpdate();
        }

        internal static readonly HashSet<PlayerID> EMPTY_OBSERVERS = new();

        public HashSet<PlayerID> GetObservers(SceneID sceneId, NetworkID id)
        {
            return _sceneToVisibilityManager.TryGetValue(sceneId, out var visibility) ? visibility.GetObservers(id) : EMPTY_OBSERVERS;
        }
        
        public bool TryGetObservers(SceneID sceneId, NetworkID id, out HashSet<PlayerID> player)
        {
            player = null;
            return _sceneToVisibilityManager.TryGetValue(sceneId, out var visibility) && visibility.TryGetObservers(id, out player);
        }
    }
}
