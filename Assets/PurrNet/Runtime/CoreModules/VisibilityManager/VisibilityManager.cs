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
                roots.Add(existingIdentity.root);

            foreach (var root in roots)
                OnIdentityAdded(root);
            
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
            
            var result = ListPool<NetworkCluster>.Instantiate();
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var clusters = HashSetPool<NetworkCluster>.Instantiate();

            foreach (var identity in allIdentities)
                roots.Add(identity.root);
            
            foreach (var root in roots)
            {
                var children = ListPool<NetworkIdentity>.Instantiate();
                root.GetComponentsInChildren(true, children);

                var cluster = new NetworkCluster(children);
                clusters.Add(cluster);
            }

            // evaluate global rules
            var rules = _manager.visibilityRules;
            
            if (!rules)
                 result.AddRange(clusters);
            else rules.GetObservedIdentities(result, clusters, player);
            
            // TODO: if we have missing things in result, we need to evaluate them individually
            /*var leftOver = HashSetPool<NetworkCluster>.Instantiate();
            leftOver.UnionWith(clusters);
            leftOver.ExceptWith(result);*/
            
            // cleanup
            foreach (var cluster in clusters)
            {
                UpdateVisiblityCluster(cluster, player);
                ListPool<NetworkIdentity>.Destroy(cluster.children);
            }
            
            HashSetPool<NetworkCluster>.Destroy(clusters);
            HashSetPool<NetworkIdentity>.Destroy(roots);
            ListPool<NetworkCluster>.Destroy(result);
            // HashSetPool<NetworkCluster>.Destroy(leftOver); TODO: dont forget to uncomment this
        }

        private void OnPlayerLeftScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;
            
            var allIdentities = _hierarchy.identities.collection;

            foreach (var identity in allIdentities)
            {
                if (identity._observers.Remove(player))
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
            
            var root = identity.root;

            if (_players.TryGetPlayersInScene(_sceneId, out var players))
            {
                var result = ListPool<PlayerID>.Instantiate();

                var rules = identity.visibilityRules;

                if (!rules)
                {
                    result.AddRange(players);
                }
                else
                {
                    rules.GetObservers(result, players, root);
                    // TODO: if we have missing players, we need to evaluate them individually
                }

                UpdateVisiblityTree(identity, result);
                ListPool<PlayerID>.Destroy(result);
            }
        }
        
        private void UpdateVisiblityTree(NetworkIdentity identity, List<PlayerID> players)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            identity.GetComponentsInChildren(true, children);
            UpdateVisiblityCluster(new NetworkCluster(children), players);
            ListPool<NetworkIdentity>.Destroy(children);
        }
        
        private void UpdateVisiblityCluster(NetworkCluster cluster, List<PlayerID> players)
        {
            var children = cluster.children;
            var count = children.Count;
            
            for (var childIdx = 0; childIdx < count; childIdx++)
                UpdateVisiblity(children[childIdx], players);
        }
        
        private void UpdateVisiblityCluster(NetworkCluster cluster, PlayerID player)
        {
            var children = cluster.children;
            var count = children.Count;
            
            for (var childIdx = 0; childIdx < count; childIdx++)
                UpdateVisiblity(children[childIdx], player);
        }
        
        private void UpdateVisiblity(NetworkIdentity identity, PlayerID player)
        {
            if (identity._observers.Add(player))
            {
                identity.TriggerOnObserverAdded(player);
                onObserverAdded?.Invoke(player, identity);
            }
        }

        private void UpdateVisiblity(NetworkIdentity identity, List<PlayerID> players)
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

        // Isolate only roots and check roots, once one is visible, the whole tree is visible
        /*private void EvaluateVisibilityTree(PlayerID player, NetworkIdentity root)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            
            root.GetComponentsInChildren(true, children);
            
            bool visible = false;
            
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                if (child.HasVisibility(player))
                {
                    visible = true;
                    break;
                }
            }
            
            if (visible)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child._observers.Add(player))
                    {
                        child.TriggerOnObserverAdded(player);
                        onObserverAdded?.Invoke(player, child);
                    }
                }
            }
            else
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child._observers.Remove(player))
                    {
                        child.TriggerOnObserverRemoved(player);
                        onObserverRemoved?.Invoke(player, child);
                    }
                }
            }
            
            ListPool<NetworkIdentity>.Destroy(children);
        }*/
        
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
