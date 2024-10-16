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
        
        public void Enable(bool asServer)
        {
            if (!asServer)
                return;

            HandleExistingObjects();
            
            _hierarchy.onIdentitySpawned += OnIdentityAdded;
            _hierarchy.onIdentityRemoved += OnIdentityRemoved;
            
            _players.onPlayerLoadedScene += OnPlayerJoinedScene;
            _players.onPlayerLeftScene += OnPlayerLeftScene;

            _playersManager.onPlayerLeft += OnPlayerLeft;
        }
        
        public void Disable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentitySpawned -= OnIdentityAdded;
            _hierarchy.onIdentityRemoved -= OnIdentityRemoved;
            
            _players.onPlayerLoadedScene -= OnPlayerJoinedScene;
            _players.onPlayerLeftScene -= OnPlayerLeftScene;
            
            _playersManager.onPlayerLeft -= OnPlayerLeft;
        }

        private void HandleExistingObjects()
        {
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            
            foreach (var existingIdentity in _hierarchy.identities.collection)
            {
                var root = existingIdentity.root;
                if (roots.Add(root))
                    OnIdentityAdded(root);
            }

            HashSetPool<NetworkIdentity>.Destroy(roots);
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

        // GetObservedIdentities(List<NetworkCluster> result, HashSet<NetworkCluster> scope, PlayerID playerId)

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;

            var allIdentities = _hierarchy.identities.collection;
            
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var players = HashSetPool<PlayerID>.Instantiate();

            foreach (var identity in allIdentities)
            {
                var root = identity.root;
                if (!roots.Add(root)) continue;
                EvaluateVisibilityForNewPlayer(root, player);
            }
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
            HashSetPool<PlayerID>.Destroy(players);
        }

        private void OnPlayerLeftScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;
            
            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                if (!identity._observers.Remove(player)) continue;
                
                identity.TriggerOnObserverRemoved(player);
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

            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
            {
                PurrLogger.LogError("No players in scene, can't evaluate visibility.");
                return;
            }

            var copy = HashSetPool<PlayerID>.Instantiate();
            copy.UnionWith(players);
            EvaluateVisibilityForAllPlayers(identity, copy);
            HashSetPool<PlayerID>.Destroy(copy);
        }

        private void EvaluateVisibilityForNewPlayer(NetworkIdentity root, PlayerID player)
        {
            var players = HashSetPool<PlayerID>.Instantiate();
            var children = ListPool<NetworkIdentity>.Instantiate();
            var result = ListPool<PlayerID>.Instantiate();
            var rules = _manager.visibilityRules;

            root.GetComponentsInChildren(true, children);
            
            players.Add(player);

            for (int i = 0; i < children.Count; ++i)
            {
                var child = children[i];
                rules = child.GetOverrideOrDefault(rules);

                if (!rules)
                {
                    result.Add(player);
                    break;
                }
                    
                rules.GetObservers(result, players, child);
                players.ExceptWith(result);
                    
                if (players.Count == 0)
                    break;
            }

            if (result.Count > 0)
            {
                var cluster = new NetworkCluster(children);
                AddPlayerAsObserver(cluster, player);
            }

            HashSetPool<PlayerID>.Destroy(players);
            ListPool<NetworkIdentity>.Destroy(children);
            ListPool<PlayerID>.Destroy(result);
        }
        
        private void EvaluateVisibilityForAllPlayers(NetworkIdentity identity, HashSet<PlayerID> players)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            var result = ListPool<PlayerID>.Instantiate();
            var root = identity.root;
            var rules = _manager.visibilityRules;

            root.GetComponentsInChildren(true, children);

            for (int i = 0; i < children.Count; ++i)
            {
                var child = children[i];
                rules = child.GetOverrideOrDefault(rules);
                    
                if (!rules)
                {
                    result.AddRange(players);
                    break;
                }
                    
                rules.GetObservers(result, players, child);
                players.ExceptWith(result);
                    
                if (players.Count == 0)
                    break;
            }
            
            var cluster = new NetworkCluster(children);
            SetObservers(cluster, result);
            
            ListPool<NetworkIdentity>.Destroy(children);
            ListPool<PlayerID>.Destroy(result);
        }

        private void SetObservers(NetworkCluster cluster, List<PlayerID> players)
        {
            var children = cluster.children;
            var count = children.Count;
            
            for (var childIdx = 0; childIdx < count; childIdx++)
                SetObservers(children[childIdx], players);
        }
        
        private void AddPlayerAsObserver(NetworkCluster cluster, PlayerID player)
        {
            var children = cluster.children;
            var count = children.Count;
            
            for (var childIdx = 0; childIdx < count; childIdx++)
                AddPlayerAsObserver(children[childIdx], player);
        }
        
        private void AddPlayerAsObserver(NetworkIdentity identity, PlayerID player)
        {
            if (!identity._observers.Add(player)) return;
            
            identity.TriggerOnObserverAdded(player);
            onObserverAdded?.Invoke(player, identity);
        }

        private void SetObservers(NetworkIdentity identity, List<PlayerID> players)
        {
            var oldPlayers = HashSetPool<PlayerID>.Instantiate();
            
            oldPlayers.UnionWith(identity._observers);
            oldPlayers.ExceptWith(players);
                        
            foreach (var player in oldPlayers)
            {
                identity._observers.Remove(player);
                identity.TriggerOnObserverRemoved(player);
                onObserverRemoved?.Invoke(player, identity);
            }
            
            HashSetPool<PlayerID>.Destroy(oldPlayers);

            var newPlayers = HashSetPool<PlayerID>.Instantiate();

            newPlayers.UnionWith(players);
            newPlayers.ExceptWith(identity._observers);
            
            foreach (var player in newPlayers)
            {
                identity._observers.Add(player);
                identity.TriggerOnObserverAdded(player);
                onObserverAdded?.Invoke(player, identity);
            }
            
            HashSetPool<PlayerID>.Destroy(newPlayers);
        }

        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being removed, can't properly clean up.");
                return;
            }
            
            foreach (var player in identity._observers)
            {
                identity.TriggerOnObserverRemoved(player);
                onObserverRemoved?.Invoke(player, identity);
            }
            
            identity._observers.Clear();
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
