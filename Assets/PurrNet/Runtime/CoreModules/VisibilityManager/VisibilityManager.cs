using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Pooling;

namespace PurrNet
{
    public delegate void VisibilityChanged(PlayerID player, NetworkIdentity identity);
    
    public class VisibilityManager : INetworkModule, IFixedUpdate
    {
        [UsedImplicitly]
        private readonly NetworkManager _manager;
        private readonly HierarchyScene _hierarchy;
        private readonly ScenePlayersModule _players;
        private readonly PlayersManager _playersManager;
        private readonly SceneID _sceneId;
        
        private readonly Dictionary<NetworkID, HashSet<PlayerID>> _observers = new();
        
        public event VisibilityChanged onObserverAdded;
        
        public event VisibilityChanged onObserverRemoved;
        
        internal VisibilityManager(NetworkManager manager, PlayersManager playersManager, HierarchyScene hierarchy, ScenePlayersModule players, SceneID sceneId)
        {
            _playersManager = playersManager;
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

            foreach (var existingIdentity in _hierarchy.identities.collection)
                OnIdentityAdded(existingIdentity);
            
            _hierarchy.onIdentityAdded += OnIdentityAdded;
            _hierarchy.onIdentityRemoved += OnIdentityRemoved;
            
            _players.onPlayerLoadedScene += OnPlayerJoinedScene;
            _players.onPlayerLeftScene += OnPlayerLeftScene;

            _playersManager.onPlayerLeft += OnPlayerLeft;
        }
        
        public void Disable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentityAdded -= OnIdentityAdded;
            _hierarchy.onIdentityRemoved -= OnIdentityRemoved;
            
            _players.onPlayerLoadedScene -= OnPlayerJoinedScene;
            _players.onPlayerLeftScene -= OnPlayerLeftScene;
            
            _playersManager.onPlayerLeft -= OnPlayerLeft;
        }

        private void OnPlayerLeft(PlayerID player, bool asserver)
        {
            if (_manager.networkRules.ShouldRemovePlayerFromSceneOnLeave()) return;

            if (!_players.TryGetPlayersAttachedToScene(_sceneId, out var players))
                return;
            
            if (!players.Contains(player))
                return;
            
            OnPlayerLeftScene(player, _sceneId, asserver);
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;

            var allIdentities = _hierarchy.identities.collection;
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            
            foreach (var identity in allIdentities)
                roots.Add(identity.root);
            
            foreach (var root in roots)
                EvaluateVisibilityTree(player, root);
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
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
                {
                    identity.TriggerOnObserverRemoved(player);
                    onObserverRemoved?.Invoke(player, identity);
                }
            }
        }
        
        public bool TryGetObservers(NetworkIdentity identity, out HashSet<PlayerID> observers)
        {
            if (!identity.id.HasValue)
            {
                observers = null;
                return false;
            }

            return _observers.TryGetValue(identity.id.Value, out observers);
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
            
            var root = identity.root;

            if (_players.TryGetPlayersInScene(_sceneId, out var players))
            {
                foreach (var player in players)
                {
                    EvaluateVisibilityTree(player, root);
                }
            }
        }

        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being removed, can't properly clean up.");
                return;
            }
            if (_observers.TryGetValue(identity.id.Value, out var observers))
            {
                foreach (var player in observers)
                {
                    identity.TriggerOnObserverRemoved(player);
                    onObserverRemoved?.Invoke(player, identity);
                }
                _observers.Remove(identity.id.Value);
            }
        }

        // Isolate only roots and check roots, once one is visible, the whole tree is visible
        private void EvaluateVisibilityTree(PlayerID player, NetworkIdentity root)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            
            root.GetComponentsInChildren(children);
            
            bool visible = false;
            
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                if (child.HasVisibility(player))
                {
                    PurrLogger.Log($"Player {player} is visible to root {root} due to child {child}");
                    visible = true;
                    break;
                }
            }
            
            
            if (visible)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    
                    if (!child.id.HasValue)
                        continue;
                    
                    if (_observers.TryGetValue(child.id.Value, out var observers))
                    {
                        if (observers.Add(player))
                        {
                            child.TriggerOnObserverAdded(player);
                            onObserverAdded?.Invoke(player, child);
                        }
                    }
                }
            }
            else
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    
                    if (!child.id.HasValue)
                        continue;
                    
                    if (_observers.TryGetValue(child.id.Value, out var observers))
                    {
                        if (observers.Remove(player))
                        {
                            child.TriggerOnObserverRemoved(player);
                            onObserverRemoved?.Invoke(player, child);
                        }
                    }
                }
            }
            
            ListPool<NetworkIdentity>.Destroy(children);
        }
        
        public void FixedUpdate()
        {
            /* TODO: This is a very naive implementation, we should only evaluate the visibility of identities that have changed.
            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                var id = identity.id;
                
                if (!id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(id.Value, out var observers))
                    continue;
                
                EvaluateVisibility(identity, observers);
            }*/
        }
    }
}
