using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public delegate void VisibilityChanged(PlayerID player, NetworkIdentity identity);
    
    public class VisibilityManager : INetworkModule
    {
        [UsedImplicitly]
        private readonly NetworkManager _manager;
        private readonly HierarchyScene _hierarchy;
        private readonly ScenePlayersModule _players;
        private readonly SceneID _sceneId;
        
        private readonly Dictionary<NetworkID, HashSet<PlayerID>> _observers = new();
        
        public event VisibilityChanged onObserverAdded;
        
        public event VisibilityChanged onObserverRemoved;
        
        internal VisibilityManager(NetworkManager manager, HierarchyScene hierarchy, ScenePlayersModule players, SceneID sceneId)
        {
            _manager = manager;
            _hierarchy = hierarchy;
            _players = players;
            _sceneId = sceneId;
        }
        
        public IEnumerable<PlayerID> GetObservers(NetworkID networkId)
        {
            return _observers.TryGetValue(networkId, out var observers) ? 
                observers : System.Array.Empty<PlayerID>();
        }
        
        public void Enable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentityAdded += OnIdentityAdded;
            _hierarchy.onIdentityRemoved += OnIdentityRemoved;
            
            _players.onPlayerLoadedScene += OnPlayerJoinedScene;
            _players.onPlayerLeftScene += OnPlayerLeftScene;
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentityAdded -= OnIdentityAdded;
            _hierarchy.onIdentityRemoved -= OnIdentityRemoved;
            
            _players.onPlayerLoadedScene -= OnPlayerJoinedScene;
            _players.onPlayerLeftScene -= OnPlayerLeftScene;
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;

            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                var id = identity.id;
                
                if (!id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(id.Value, out var observers))
                    continue;
                
                EvaluateVisibility(player, identity, observers);
            }
        }

        private void OnPlayerLeftScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;
            
            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                var id = identity.id;
                
                if (!id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(id.Value, out var observers))
                    continue;
                
                if (observers.Remove(player))
                    onObserverRemoved?.Invoke(player, identity);
            }
        }

        private void OnIdentityAdded(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being added, won't keep track of observers.");
                return;
            }

            var collection = new HashSet<PlayerID>();
            _observers.Add(identity.id.Value, collection);
            
            EvaluateVisibility(identity, collection);
        }

        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being removed, can't properly clean up.");
                return;
            }
            
            _observers.Remove(identity.id.Value);
        }

        private void EvaluateVisibility(NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            collection.Clear();

            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
                return;

            foreach (var player in players)
                EvaluateVisibility(player, nid, collection);
        }
        
        private void EvaluateVisibility(PlayerID target, NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (nid.HasVisiblity(target, nid))
            {
                if (collection.Add(target))
                    onObserverAdded?.Invoke(target, nid);
            }
            else
            {
                if (collection.Remove(target))
                    onObserverRemoved?.Invoke(target, nid);
            }
        }
    }
}
