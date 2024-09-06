using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public class VisibilityManager : INetworkModule
    {
        private readonly NetworkManager _manager;
        private readonly HierarchyScene _hierarchy;
        private readonly ScenePlayersModule _players;
        private readonly SceneID _sceneId;
        
        private readonly Dictionary<NetworkID, HashSet<PlayerID>> _observers = new();
        
        internal VisibilityManager(NetworkManager manager, HierarchyScene hierarchy, ScenePlayersModule players, SceneID sceneId)
        {
            _manager = manager;
            _sceneId = sceneId;
            _hierarchy = hierarchy;
            _players = players;
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
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
                return;
            
            _hierarchy.onIdentityAdded -= OnIdentityAdded;
            _hierarchy.onIdentityRemoved -= OnIdentityRemoved;
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
        
        private static void EvaluateVisibility(PlayerID target, NetworkIdentity nid, HashSet<PlayerID> collection)
        {
            if (nid.HasVisiblity(target, nid))
                collection.Add(target);
        }
    }
}
