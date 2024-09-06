using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;

namespace PurrNet.Modules
{
    internal partial struct OwnershipInfo : IAutoNetworkedData
    {
        public NetworkID identity;
        public PlayerID player;
    }
    
    internal struct FullOwnershipChange
    {
        public PlayerID actor;
        public OwnershipChange data;
    }
    
    internal partial struct OwnershipChangeBatch : IAutoNetworkedData
    {
        public SceneID scene;
        public List<OwnershipInfo> state;
    }
    
    internal partial struct OwnershipChange : INetworkedData
    {
        public SceneID sceneId;
        public List<NetworkID> identities;
        public bool isAdding;
        public PlayerID player;

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref sceneId);
            packer.Serialize(ref identities);
            packer.Serialize(ref isAdding);
            
            if (isAdding)
                packer.Serialize(ref player);
        }
    }
    
    public delegate void OwnershipChanged(NetworkID identity, PlayerID? player, bool asServer);
    
    public class GlobalOwnershipModule : INetworkModule, IPreFixedUpdate
    {
        readonly PlayersManager _playersManager;
        readonly ScenePlayersModule _scenePlayers;
        readonly HierarchyModule _hierarchy;
        
        readonly ScenesModule _scenes;
        readonly Dictionary<SceneID, SceneOwnership> _sceneOwnerships = new ();
        
        public event OwnershipChanged onOwnershipChanged;

        private bool _asServer;
        
        public GlobalOwnershipModule(HierarchyModule hierarchy, PlayersManager players, ScenePlayersModule scenePlayers, ScenesModule scenes)
        {
            _hierarchy = hierarchy;
            _scenes = scenes;
            _playersManager = players;
            _scenePlayers = scenePlayers;
        }
        
        readonly List<FullOwnershipChange> _ownershipChanges = new ();
        readonly List<OwnershipChangeBatch> _ownershipBatches = new ();
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            for (int i = 0; i < _scenes.scenes.Count; i++)
                OnSceneLoaded(_scenes.scenes[i], asServer);
            
            _scenes.onPreSceneLoaded += OnSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
            
            _hierarchy.onIdentityRemoved += OnIdentityDespawned;

            _scenePlayers.onPostPlayerLoadedScene += OnPlayerJoined;
            _scenePlayers.onPlayerLeftScene += OnPlayerLeft;

            _playersManager.Subscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Subscribe<OwnershipChange>(OnOwnershipChange);
        }

        public void Disable(bool asServer)
        {
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
            
            _hierarchy.onIdentityRemoved -= OnIdentityDespawned;

            _scenePlayers.onPlayerLoadedScene -= OnPlayerJoined;
            _scenePlayers.onPlayerLeftScene -= OnPlayerLeft;

            _playersManager.Unsubscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Unsubscribe<OwnershipChange>(OnOwnershipChange);
        }
        
        /// <summary>
        /// Gets all the objects owned by the given player.
        /// This creates a new list every time it's called.
        /// So it's recommended to cache the result if you're going to use it multiple times.
        /// </summary>
        public List<NetworkIdentity> GetAllPlayerOwnedIds(PlayerID player)
        {
            List<NetworkIdentity> ids = new ();

            foreach (var (scene, owned) in _sceneOwnerships)
            {
                if (!_hierarchy.TryGetHierarchy(scene, out var hierarchy))
                    continue;

                var ownedIds = owned.TryGetOwnedObjects(player);
                foreach (var id in ownedIds)
                {
                    if (hierarchy.TryGetIdentity(id, out var identity))
                        ids.Add(identity);
                }
            }
            
            return ids;
        }
        
        public IEnumerable<NetworkIdentity> EnumerateAllPlayerOwnedIds(PlayerID player)
        {
            foreach (var (scene, owned) in _sceneOwnerships)
            {
                if (!_hierarchy.TryGetHierarchy(scene, out var hierarchy))
                    continue;

                var ownedIds = owned.TryGetOwnedObjects(player);
                foreach (var id in ownedIds)
                {
                    if (hierarchy.TryGetIdentity(id, out var identity))
                        yield return identity;
                }
            }
        }

        private void OnIdentityDespawned(NetworkIdentity identity)
        {
            if (!identity.id.HasValue)
                return;
            
            if (_sceneOwnerships.TryGetValue(identity.sceneId, out var module))
            {
                if (module.TryGetOwner(identity, out var oldOwner) && module.RemoveOwnership(identity))
                {
                    identity.TriggerOnOwnerChanged(oldOwner, null, _asServer);
                    onOwnershipChanged?.Invoke(identity.id.Value, null, _asServer);
                }
            }
        }

        private void OnPlayerJoined(PlayerID player, SceneID scene, bool asserver)
        {
            if (!_sceneOwnerships.TryGetValue(scene, out var ownerships)) return;

            var owned = ownerships.TryGetOwnedObjects(player);

            foreach (var id in owned)
            {
                if (_hierarchy.TryGetIdentity(scene, id, out var identity))
                    identity.TriggerOnOwnerReconnected(player, asserver);
            }

            if (!asserver)
                return;

            var state = ownerships.GetState();
            
            if (state.Count == 0)
                return;
            
            _playersManager.Send(player, new OwnershipChangeBatch
            {
                scene = scene,
                state = state
            });
        }

        private void OnPlayerLeft(PlayerID player, SceneID scene, bool asserver)
        {
            if (!_sceneOwnerships.TryGetValue(scene, out var ownerships)) return;

            var owned = ownerships.TryGetOwnedObjects(player);

            foreach (var id in owned)
            {
                if (_hierarchy.TryGetIdentity(scene, id, out var identity))
                    identity.TriggerOnOwnerDisconnected(player, asserver);
            }
        }


        private void OnOwnershipChange(PlayerID player, OwnershipChangeBatch data, bool asserver)
        {
            _ownershipBatches.Add(data);
        }

        private void OnOwnershipChange(PlayerID player, OwnershipChange change, bool asserver)
        {
            _ownershipChanges.Add(new FullOwnershipChange
            {
                actor = player,
                data = change
            });
        }
        
        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            _sceneOwnerships.Remove(scene);
        }

        private static readonly List<NetworkID> _idsCache = new ();

        public void GiveOwnership(NetworkIdentity nid, PlayerID player, bool? propagateToChildren = null, bool? overrideExistingOwners = null)
        {
            if (!nid.id.HasValue)
            {
                PurrLogger.LogError($"Failed to give ownership of '{nid.gameObject.name}' to {player} because it isn't spawned.");
                return;
            }
            
            bool hadOwnerPreviously = nid.hasOwner;

            if (hadOwnerPreviously && !nid.HasTransferOwnershipAuthority(_asServer) || !hadOwnerPreviously && !nid.HasGiveOwnershipAuthority(_asServer))
            {
                PurrLogger.LogError($"Failed to give ownership of '{nid.gameObject.name}' to {player} because of missing authority.");
                return;
            }

            if (!_sceneOwnerships.TryGetValue(nid.sceneId, out var module))
            {
                PurrLogger.LogError($"No ownership module avaible for scene {nid.sceneId} '{nid.gameObject.scene.name}'");
                return;
            }
            
            var shouldOverride = overrideExistingOwners ?? nid.ShouldOverrideExistingOwnership(_asServer);
            var affectedIds = GetAllChildrenOrSelf(nid, propagateToChildren, _asServer);

            _idsCache.Clear();

            for (var i = 0; i < affectedIds.Count; i++)
            {
                var identity = affectedIds[i];
                
                if (!identity.id.HasValue) continue;

                if (identity.hasOwner)
                {
                    if (!shouldOverride)
                        continue;

                    if (!identity.HasTransferOwnershipAuthority(_asServer))
                    {
                        PurrLogger.LogError($"Failed to override ownership of '{identity.gameObject.name}' because of missing authority.");
                        continue;
                    }
                }

                var oldOwner = identity.GetOwner(_asServer);

                if (module.GiveOwnership(identity, player))
                {
                    identity.TriggerOnOwnerChanged(oldOwner, player, _asServer);
                    onOwnershipChanged?.Invoke(identity.id.Value, player, _asServer);
                }

                _idsCache.Add(identity.id.Value);
            }

            if (_idsCache.Count == 0)
            {
                PurrLogger.LogError($"Failed to give ownership of '{nid.gameObject.name}' to {player} because no identities were affected.");
                return;
            }

            // TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = nid.sceneId,
                identities = _idsCache,
                isAdding = true,
                player = player
            };

            if (_asServer)
            {
                if (_scenePlayers.TryGetPlayersInScene(nid.sceneId, out var players))
                    _playersManager.Send(players, data);
            }
            else _playersManager.SendToServer(data);
        }

        /// <summary>
        /// Clears all ownerships of the given identity and its children.
        /// </summary>
        public void ClearOwnerships(NetworkIdentity id, bool supressErrorMessages = false)
        {
            if (!id.id.HasValue)
            {
                PurrLogger.LogError($"Failed to remove ownership of '{id.gameObject.name}' because it isn't spawned.");
                return;
            }
            
            if (!id.owner.HasValue)
                return;
            
            if (!id.HasTransferOwnershipAuthority(_asServer))
            {
                PurrLogger.LogError($"Failed to remove ownership of '{id.gameObject.name}' because of missing authority.");
                return;
            }

            if (!_sceneOwnerships.TryGetValue(id.sceneId, out var module))
            {
                PurrLogger.LogError($"No ownership module avaible for scene {id.sceneId} '{id.gameObject.scene.name}'");
                return;
            }
            
            var children = GetAllChildrenOrSelf(id, true, _asServer);
            
            _idsCache.Clear();

            for (var i = 0; i < children.Count; i++)
            {
                var identity = children[i];
                
                if (!identity.id.HasValue) continue;
                if (!identity.hasOwner) continue;
                if (!identity.HasTransferOwnershipAuthority(_asServer))
                {
                    if (!supressErrorMessages)
                        PurrLogger.LogError($"Failed to override ownership of '{identity.gameObject.name}' because of missing authority.");
                    continue;
                }

                _idsCache.Add(identity.id.Value);
            }

            //TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = id.sceneId,
                identities = _idsCache,
                isAdding = false,
                player = default
            };

            var oldOwner = id.GetOwner(_asServer);

            if (module.RemoveOwnership(id))
            {
                id.TriggerOnOwnerChanged(oldOwner, null, _asServer);
                onOwnershipChanged?.Invoke(id.id.Value, null, _asServer);
            }

            if (_asServer)
            {
                if (_scenePlayers.TryGetPlayersInScene(id.sceneId, out var players))
                    _playersManager.Send(players, data);
            }
            else _playersManager.SendToServer(data);
        }
        
        /// <summary>
        /// Only removes ownership for the existing owner.
        /// This won't remove ownership of children with different owners.
        /// </summary>
        public void RemoveOwnership(NetworkIdentity id, bool? propagateToChildren = null, bool supressErrorMessages = false)
        {
            if (!id.id.HasValue)
            {
                PurrLogger.LogError($"Failed to remove ownership of '{id.gameObject.name}' because it isn't spawned.");
                return;
            }
            
            if (!id.owner.HasValue)
                return;
            
            if (!id.HasTransferOwnershipAuthority(_asServer))
            {
                PurrLogger.LogError($"Failed to remove ownership of '{id.gameObject.name}' because of missing authority.");
                return;
            }

            if (!_sceneOwnerships.TryGetValue(id.sceneId, out var module))
            {
                PurrLogger.LogError($"No ownership module avaible for scene {id.sceneId} '{id.gameObject.scene.name}'");
                return;
            }
            
            var originalOwner = id.owner.Value;
            var children = GetAllChildrenOrSelf(id, propagateToChildren, _asServer);
            
            _idsCache.Clear();

            for (var i = 0; i < children.Count; i++)
            {
                var identity = children[i];
                
                if (!identity.id.HasValue) continue;
                if (!module.TryGetOwner(identity, out var player) || player != originalOwner) continue;
                if (!identity.HasTransferOwnershipAuthority(_asServer))
                {
                    if (!supressErrorMessages)
                        PurrLogger.LogError($"Failed to override ownership of '{identity.gameObject.name}' because of missing authority.");
                    continue;
                }
                    
                _idsCache.Add(identity.id.Value);
            }

            //TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = id.sceneId,
                identities = _idsCache,
                isAdding = false,
                player = default
            };

            var oldOwner = id.GetOwner(_asServer);

            if (module.RemoveOwnership(id))
            {
                id.TriggerOnOwnerChanged(oldOwner, id.owner, _asServer);
                onOwnershipChanged?.Invoke(id.id.Value, null, _asServer);
            }

            if (_asServer)
            {
                if (_scenePlayers.TryGetPlayersInScene(id.sceneId, out var players))
                    _playersManager.Send(players, data);
            }
            else _playersManager.SendToServer(data);
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
            _sceneOwnerships[scene] = new SceneOwnership(asServer);
        }
        
        public void PreFixedUpdate()
        {
            HandleBatches();
            HandleChanges();
        }

        private void HandleBatches()
        {
            int count = _ownershipBatches.Count;

            if (count == 0)
                return;

            for (var i = 0; i < count; i++)
            {
                var data = _ownershipBatches[i];
                HandleOwenshipBatch(data);
            }
            
            _ownershipBatches.Clear();
        }

        private void HandleOwenshipBatch(OwnershipChangeBatch data)
        {
            var stateCount = data.state.Count;

            for (var j = 0; j < stateCount; j++)
                HandleOwnershipBatch(data.scene, data.state[j]);
        }

        private void HandleOwnershipBatch(SceneID scene, OwnershipInfo change)
        {
            if (!_hierarchy.TryGetIdentity(scene, change.identity, out var identity))
            {
                PurrLogger.LogError(
                    $"Failed to find identity {change.identity} in scene {scene} when applying ownership change for identity.");
                return;
            }

            if (!identity.id.HasValue)
            {
                PurrLogger.LogError(
                    $"Can't apply ownership change for identity {change.identity} because it isn't spawned.");
                return;
            }

            if (!identity.HasGiveOwnershipAuthority(!_asServer))
            {
                PurrLogger.LogError(
                    $"Failed to give ownership of '{identity.gameObject.name}' to {change.player} because of missing authority.");
                return;
            }

            if (!_sceneOwnerships.TryGetValue(scene, out var module))
            {
                PurrLogger.LogError(
                    $"Failed to find ownership module for scene {scene} when applying ownership change for identity {change.identity}");
                return;
            }

            var oldOwner = identity.GetOwner(_asServer);

            if (oldOwner == change.player)
            {
                PurrLogger.LogError(
                    $"Failed to give ownership of '{identity.gameObject.name}' to {change.player} because it already has it.");
                return;
            }

            if (module.GiveOwnership(identity, change.player))
            {
                identity.TriggerOnOwnerChanged(oldOwner, change.player, _asServer);
                onOwnershipChanged?.Invoke(identity.id.Value, change.player, _asServer);
            }
        }

        private void HandleChanges()
        {
            int count = _ownershipChanges.Count;

            if (count == 0)
                return;

            for (var i = 0; i < count; i++)
            {
                var change = _ownershipChanges[i];
                var idCount = change.data.identities.Count;
                
                for (var j = 0; j < idCount; j++)
                    HandleOwnershipChange(change.actor, change.data, change.data.identities[j]);
            }

            _ownershipChanges.Clear();
        }

        private void HandleOwnershipChange(PlayerID actor, OwnershipChange change, NetworkID id)
        {
            string verb = change.isAdding ? "give" : "remove";

            if (!_hierarchy.TryGetIdentity(change.sceneId, id, out var identity))
                return;

            if (!_sceneOwnerships.TryGetValue(change.sceneId, out var module))
            {
                PurrLogger.LogError(
                    $"Failed to find ownership module for scene {change.sceneId} when applying ownership change for identity {id}");
                return;
            }

            if (identity.hasOwner)
            {
                if (!identity.HasTransferOwnershipAuthority(actor, !_asServer))
                {
                    PurrLogger.LogError(
                        $"Failed to {verb} (transfer) ownership of '{identity.gameObject.name}' to {change.player} because of missing authority.");
                    return;
                }
            }
            else if (!identity.HasGiveOwnershipAuthority(!_asServer))
            {
                PurrLogger.LogError(
                    $"Failed to {verb} ownership of '{identity.gameObject.name}' to {change.player} because of missing authority.");
                return;
            }

            var oldOwner = identity.GetOwner(_asServer);

            if (change.isAdding)
            {
                module.GiveOwnership(identity, change.player);

                if (oldOwner != change.player)
                {
                    identity.TriggerOnOwnerChanged(oldOwner, change.player, _asServer);

                    if (identity.id.HasValue)
                        onOwnershipChanged?.Invoke(identity.id.Value, change.player, _asServer);
                }
            }
            else
            {
                if (module.RemoveOwnership(identity) && identity.id.HasValue)
                {
                    identity.TriggerOnOwnerChanged(oldOwner, null, _asServer);
                    onOwnershipChanged?.Invoke(identity.id.Value, null, _asServer);
                }
            }
        }

        internal static List<NetworkIdentity> GetAllChildrenOrSelf(NetworkIdentity id, bool? propagateToChildren, bool asServer)
        {
            var cache = HierarchyScene.CACHE;
            bool shouldPropagate = propagateToChildren ?? id.ShouldPropagateToChildren(asServer);

            if (shouldPropagate && id.HasPropagateOwnershipAuthority(asServer))
            {
                id.GetComponentsInChildren(true, cache);
            }
            else
            {
                if (propagateToChildren == true)
                    PurrLogger.LogError(
                        $"Failed to propagate ownership of '{id.gameObject.name}' because of missing authority, assigning only to the identity.");

                cache.Clear();
                cache.Add(id);
            }

            return cache;
        }
    }

    internal class SceneOwnership
    {
        static readonly List<OwnershipInfo> _cache = new ();
        
        readonly Dictionary<NetworkID, PlayerID> _owners = new ();

        readonly Dictionary<PlayerID, HashSet<NetworkID>> _playerOwnedIds = new ();

        private readonly bool _asServer;
        
        public SceneOwnership(bool asServer)
        {
            _asServer = asServer;
        }
        
        public List<OwnershipInfo> GetState()
        {
            _cache.Clear();
            
            foreach (var (id, player) in _owners)
                _cache.Add(new OwnershipInfo { identity = id, player = player });

            return _cache;
        }

        public IEnumerable<NetworkID> TryGetOwnedObjects(PlayerID player)
        {
            if (_playerOwnedIds.TryGetValue(player, out _))
                return _playerOwnedIds[player];
            return Array.Empty<NetworkID>();
        }
        
        public bool TryGetOwner(NetworkIdentity id, out PlayerID player)
        {
            if (!id.id.HasValue)
            {
                player = default;
                return false;
            }
            
            return _owners.TryGetValue(id.id.Value, out player);
        }

        public bool GiveOwnership(NetworkIdentity identity, PlayerID player)
        {
            if (identity.id == null)
                return false;
            
            _owners[identity.id.Value] = player;

            if (!_playerOwnedIds.TryGetValue(player, out var ownedIds))
            {
                ownedIds = new HashSet<NetworkID>() { identity.id.Value };
                _playerOwnedIds[player] = ownedIds;
            }
            else ownedIds.Add(identity.id.Value);
            
            if (_asServer)
                 identity.internalOwnerServer = player;
            else identity.internalOwnerClient = player;

            return true;
        }

        public bool RemoveOwnership(NetworkIdentity identity)
        {
            if (identity.id.HasValue && _owners.TryGetValue(identity.id.Value, out var oldOwner))
            {
                _owners.Remove(identity.id.Value);

                if (_playerOwnedIds.TryGetValue(oldOwner, out HashSet<NetworkID> ownedIds))
                {
                    ownedIds.Remove(identity.id.Value);

                    if (ownedIds.Count == 0)
                        _playerOwnedIds.Remove(oldOwner);
                }

                if (_asServer)
                     identity.internalOwnerServer = null;
                else identity.internalOwnerClient = null;
                return true;
            }
            
            return false;
        }
    }
}
