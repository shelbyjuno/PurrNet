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
            if (_observers.TryGetValue(identity.id.Value, out var observers))
            {
                foreach (var player in observers)
                    onObserverRemoved?.Invoke(player, identity);
                _observers.Remove(identity.id.Value);
            }
        }

        static readonly HashSet<PlayerID> _collection = new();
        
        private void EvaluateVisibility(NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
            {
                foreach (var player in collection)
                    onObserverRemoved?.Invoke(player, nid);
                collection.Clear();
                return;
            }

            _collection.Clear();
            _collection.UnionWith(collection);
            _collection.ExceptWith(players);
            
            foreach (var player in _collection)
            {
                collection.Remove(player);
                onObserverRemoved?.Invoke(player, nid);
            }

            foreach (var player in players)
                EvaluateVisibility(player, nid, collection);
        }
        
        private void PropagateVisibility(PlayerID target, NetworkIdentity child)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            child.transform.root.GetComponentsInChildren(true, children);

            for (var i = 0; i < children.Count; i++)
            {
                var identity = children[i];
                
                if (identity == child)
                    continue;
                
                if (!identity.id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(identity.id.Value, out var observers))
                    continue;

                if (observers.Add(target))
                    onObserverAdded?.Invoke(target, child);
            }
        }
        
        private void EvaluateVisibility(PlayerID target, NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (nid.HasVisibility(target, nid))
            {
                if (collection.Add(target))
                {
                    onObserverAdded?.Invoke(target, nid);
                    //PropagateVisibility(target, nid); TODO: This is an issue when removing observers
                }
            }
            else
            {
                if (collection.Remove(target))
                    onObserverRemoved?.Invoke(target, nid);
            }
        }

        public void FixedUpdate()
        {
            // TODO: This is a very naive implementation, we should only evaluate the visibility of identities that have changed.
            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                var id = identity.id;
                
                if (!id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(id.Value, out var observers))
                    continue;
                
                EvaluateVisibility(identity, observers);
            }
        }
    }
}
