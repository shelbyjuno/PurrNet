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
        
        private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asserver)
        {
            if (_manager.networkRules.ShouldRemovePlayerFromSceneOnLeave()) return;

            if (!_players.TryGetPlayersAttachedToScene(_sceneId, out var players))
                return;
            
            if (players.Contains(player))
                return;
            
            OnPlayerJoinedScene(player, _sceneId, asserver);
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
                {
                    identity.TriggerOnObserverRemoved(player);
                    onObserverRemoved?.Invoke(player, identity);
                }
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
                {
                    identity.TriggerOnObserverRemoved(player);
                    onObserverRemoved?.Invoke(player, identity);
                }
                _observers.Remove(identity.id.Value);
            }
        }

        private void EvaluateVisibility(NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (!_players.TryGetPlayersInScene(_sceneId, out var playersInScene))
            {
                foreach (var player in collection)
                {
                    nid.TriggerOnObserverRemoved(player);
                    onObserverRemoved?.Invoke(player, nid);
                }
                collection.Clear();
                return;
            }

            RemoveOld(nid, collection, playersInScene);
            
            foreach (var player in playersInScene)
            {
                if (!_playersManager.IsPlayerConnected(player))
                {
                    PurrLogger.LogError($"Player {player} is in scene {_sceneId} but is not connected.");
                    continue;
                }
                
                EvaluateVisibility(player, nid, collection);
            }
        }

        private void RemoveOld(NetworkIdentity nid, HashSet<PlayerID> collection, HashSet<PlayerID> playersInScene)
        {
            var collectionCopy = HashSetPool<PlayerID>.Instantiate();
            collectionCopy.UnionWith(collection);
            
            foreach (var player in collectionCopy)
            {
                if (playersInScene.Contains(player) && _playersManager.IsPlayerConnected(player))
                    continue;
                
                collection.Remove(player);
                
                nid.TriggerOnObserverRemoved(player);
                onObserverRemoved?.Invoke(player, nid);
            }
            
            HashSetPool<PlayerID>.Destroy(collectionCopy);
        }
        
        private void EvaluateVisibility(PlayerID target, NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (nid.HasVisibility(target, nid))
            {
                if (collection.Add(target))
                {
                    nid.TriggerOnObserverAdded(target);
                    onObserverAdded?.Invoke(target, nid);
                    //PropagateVisibility(target, nid); // TODO: This is an issue when removing observers
                }
            }
            else
            {
                if (collection.Remove(target))
                {
                    nid.TriggerOnObserverRemoved(target);
                    onObserverRemoved?.Invoke(target, nid);
                }
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
