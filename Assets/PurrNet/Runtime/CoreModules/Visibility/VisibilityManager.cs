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
        
        public event System.Action onTickChangesDone;
        
        private readonly Dictionary<NetworkID, HashSet<PlayerID>> _observers = new();
        
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
            
            _hierarchy.onIdentityRootSpawned += OnIdentityRootAdded;
            _hierarchy.onIdentityRemoved += OnIdentityRemoved;
            
            _players.onPrePlayerloadedScene += OnPlayerJoinedScene;
            _players.onPlayerLeftScene += OnPlayerLeftScene;

            _playersManager.onPlayerLeft += OnPlayerLeft;
        }
        
        public void Disable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentityRootSpawned -= OnIdentityRootAdded;
            _hierarchy.onIdentityRemoved -= OnIdentityRemoved;
            
            _players.onPrePlayerloadedScene -= OnPlayerJoinedScene;
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
                    OnIdentityRootAdded(root);
            }

            HashSetPool<NetworkIdentity>.Destroy(roots);
        }

        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if (_manager.networkRules.ShouldRemovePlayerFromSceneOnLeave()) return;

            if (!_players.TryGetPlayersAttachedToScene(_sceneId, out var players))
                return;
            
            if (!players.Contains(player))
                return;
            
            OnPlayerLeftScene(player, _sceneId, asServer);
        }

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (scene != _sceneId)
                return;

            var allIdentities = _hierarchy.identities.collection;
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            foreach (var identity in allIdentities)
            {
                var root = identity.root;
                if (!roots.Add(root)) continue;
                
                EvaluateVisibilityForNewPlayer(root, player);
            }
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
        }

        private void OnPlayerLeftScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (scene != _sceneId)
                return;
            
            var allIdentities = _hierarchy.identities.collection;
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            
            foreach (var identity in allIdentities)
            {
                var root = identity.root;
                
                if (!roots.Add(root)) continue;
                
                if (!root.id.HasValue)
                    continue;
                
                if (!_observers.TryGetValue(root.id.Value, out var observers))
                    continue;
                
                if (!observers.Remove(player))
                    continue;
                
                onObserverRemoved?.Invoke(player, identity);
            }
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
            
            onTickChangesDone?.Invoke();
        }
        
        public void ReEvaluateRoot(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being re-evaluated, won't keep track of observers.");
                return;
            }

            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
                return;
            
            var copy = HashSetPool<PlayerID>.Instantiate();
            copy.UnionWith(players);
            EvaluateVisibilityForAllPlayers(identity, copy);
            HashSetPool<PlayerID>.Destroy(copy);
            onTickChangesDone?.Invoke();
        }
        
        private void OnIdentityRootAdded(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being added, won't keep track of observers.");
                return;
            }

            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
                return;
            
            var copy = HashSetPool<PlayerID>.Instantiate();
            copy.UnionWith(players);
            EvaluateVisibilityForAllPlayers(identity, copy);
            HashSetPool<PlayerID>.Destroy(copy);
            onTickChangesDone?.Invoke();
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
        
        private void EvaluateVisibilityForAllPlayers(NetworkIdentity root, HashSet<PlayerID> players)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            var result = ListPool<PlayerID>.Instantiate();
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
            
            if (!_observers.TryGetValue(cluster.firstId, out var observers))
            {
                observers = new HashSet<PlayerID>(players.Count);
                _observers.Add(cluster.firstId, observers);
            }
            
            var oldPlayers = HashSetPool<PlayerID>.Instantiate();

            oldPlayers.UnionWith(observers);
            oldPlayers.ExceptWith(players);

            for (var childIdx = 0; childIdx < count; childIdx++)
            {
                foreach (var player in oldPlayers)
                {
                    var identity = children[childIdx];
                    if (observers.Remove(player))
                        onObserverRemoved?.Invoke(player, identity);
                }
            }
            
            oldPlayers.Clear();
            oldPlayers.UnionWith(players);
            oldPlayers.ExceptWith(observers);
            
            for (var childIdx = 0; childIdx < count; childIdx++)
            {
                foreach (var player in oldPlayers)
                {
                    var identity = children[childIdx];
                    if (observers.Add(player))
                        onObserverAdded?.Invoke(player, identity);
                }
            }
            
            HashSetPool<PlayerID>.Destroy(oldPlayers);
        }
        
        private void AddPlayerAsObserver(NetworkCluster cluster, PlayerID player)
        {
            var children = cluster.children;
            // var count = children.Count;
            
            if (!_observers.TryGetValue(cluster.firstId, out var observers))
            {
                observers = new HashSet<PlayerID>(1);
                _observers.Add(cluster.firstId, observers);
            }
            
            if (!observers.Add(player))
                return;
            
            onObserverAdded?.Invoke(player, children[0]);
            
            /*for (var childIdx = 0; childIdx < count; childIdx++)
            {
                var identity = children[childIdx];
                onObserverAdded?.Invoke(player, identity);
            }*/
        }

        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
            {
                PurrLogger.LogError("Identity has no ID when being removed, can't properly clean up.");
                return;
            }

            var root = identity.root.id;
            
            if (!root.HasValue || !_observers.TryGetValue(root.Value, out var observers))
                return;

            foreach (var player in observers)
                identity.TriggerOnObserverRemoved(player);
        }
        
        public void FixedUpdate()
        {
            if (!_players.TryGetPlayersInScene(_sceneId, out var players))
                return;
            
            var allIdentities = _hierarchy.identities.collection;
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var copy = HashSetPool<PlayerID>.Instantiate();
            
            foreach (var identity in allIdentities)
            {
                var root = identity.root;
                if (!roots.Add(root)) continue;
                
                copy.UnionWith(players);
                EvaluateVisibilityForAllPlayers(root, copy);
            }
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
            HashSetPool<PlayerID>.Destroy(copy);
            
            onTickChangesDone?.Invoke();
        }

        public HashSet<PlayerID> GetObservers(NetworkID id)
        {
            return _observers.TryGetValue(id, out var observers) ? observers : VisibilityFactory.EMPTY_OBSERVERS;
        }
        
        public bool TryGetObservers(NetworkID id, out HashSet<PlayerID> observers)
        {
            return _observers.TryGetValue(id, out observers);
        }
    }
}
