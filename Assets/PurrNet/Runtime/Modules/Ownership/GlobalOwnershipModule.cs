using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;

namespace PurrNet.Modules
{
    public enum OwnershipChangeType
    {
        Give,
        Remove,
        Transfer
    }
    
    internal partial struct OwnershipInfo : IAutoNetworkedData
    {
        public int identity;
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
        public List<int> identities;
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
    
    public delegate void OwnershipChanged(int identity, PlayerID? player, bool asServer);
    
    public class GlobalOwnershipModule : INetworkModule, IFixedUpdate
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

            if (asServer)
                _scenePlayers.onPostPlayerLoadedScene += OnPlayerJoined;

            _playersManager.Subscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Subscribe<OwnershipChange>(OnOwnershipChange);
        }

        public void Disable(bool asServer)
        {
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
            
            _hierarchy.onIdentityRemoved -= OnIdentityDespawned;
            
            if (asServer)
                _scenePlayers.onPlayerLoadedScene -= OnPlayerJoined;

            _playersManager.Unsubscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Unsubscribe<OwnershipChange>(OnOwnershipChange);
        }

        private void OnIdentityDespawned(NetworkIdentity identity)
        {
            if (_sceneOwnerships.TryGetValue(identity.sceneId, out var module))
            {
                if (module.RemoveOwnership(identity))
                    onOwnershipChanged?.Invoke(identity.id, null, _asServer);
            }
        }

        private void OnPlayerJoined(PlayerID player, SceneID scene, bool asserver)
        {
            if (!asserver)
                return;

            if (_sceneOwnerships.TryGetValue(scene, out var module))
            {
                _playersManager.Send(player, new OwnershipChangeBatch
                {
                    scene = scene,
                    state = module.GetState()
                });
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

        private static readonly List<int> _idsCache = new ();

        public void GiveOwnership(NetworkIdentity id, PlayerID player, bool? propagateToChildren = null, bool? overrideExistingOwners = null)
        {
            bool hadOwnerPreviously = id.hasOwner;

            if (hadOwnerPreviously && !id.HasTransferOwnershipAuthority(_asServer) || !hadOwnerPreviously && !id.HasGiveOwnershipAuthority(_asServer))
            {
                PurrLogger.LogError($"Failed to give ownership of '{id.gameObject.name}' to {player} because of missing authority.");
                return;
            }

            if (!_sceneOwnerships.TryGetValue(id.sceneId, out var module))
            {
                PurrLogger.LogError($"No ownership module avaible for scene {id.sceneId} '{id.gameObject.scene.name}'");
                return;
            }
            
            var shouldOverride = overrideExistingOwners ?? id.ShouldOverrideExistingOwnership(_asServer);
            var affectedIds = GetAllChildrenOrSelf(id, propagateToChildren);

            _idsCache.Clear();

            for (var i = 0; i < affectedIds.Count; i++)
            {
                var identity = affectedIds[i];
                
                if (!identity.isSpawned) continue;

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

                module.GiveOwnership(identity, player);
                onOwnershipChanged?.Invoke(identity.id, player, _asServer);
                _idsCache.Add(identity.id);
            }

            // TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = id.sceneId,
                identities = _idsCache,
                isAdding = true,
                player = player
            };

            if (_asServer)
            {
                if (_scenePlayers.TryGetPlayersInScene(id.sceneId, out var players))
                    _playersManager.Send(players, data);
            }
            else _playersManager.SendToServer(data);
        }

        /// <summary>
        /// Clears all ownerships of the given identity and its children.
        /// </summary>
        public void ClearOwnerships(NetworkIdentity id, bool supressErrorMessages = false)
        {
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
            
            var children = GetAllChildrenOrSelf(id, true);
            
            _idsCache.Clear();

            for (var i = 0; i < children.Count; i++)
            {
                var identity = children[i];
                
                if (!identity.isSpawned) continue;
                if (!identity.hasOwner) continue;
                if (!identity.HasTransferOwnershipAuthority(_asServer))
                {
                    if (!supressErrorMessages)
                        PurrLogger.LogError($"Failed to override ownership of '{identity.gameObject.name}' because of missing authority.");
                    continue;
                }

                _idsCache.Add(identity.id);
            }

            //TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = id.sceneId,
                identities = _idsCache,
                isAdding = false,
                player = default
            };
            
            module.RemoveOwnership(id);
            onOwnershipChanged?.Invoke(id.id, null, _asServer);

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
            var children = GetAllChildrenOrSelf(id, propagateToChildren);
            
            _idsCache.Clear();

            for (var i = 0; i < children.Count; i++)
            {
                var identity = children[i];
                
                if (!identity.isSpawned) continue;
                if (!module.TryGetOwner(identity, out var player) || player != originalOwner) continue;
                if (!identity.HasTransferOwnershipAuthority(_asServer))
                {
                    if (!supressErrorMessages)
                        PurrLogger.LogError($"Failed to override ownership of '{identity.gameObject.name}' because of missing authority.");
                    continue;
                }
                    
                _idsCache.Add(identity.id);
            }

            //TODO: compress _idsCache using RLE
            var data = new OwnershipChange
            {
                sceneId = id.sceneId,
                identities = _idsCache,
                isAdding = false,
                player = default
            };
            
            module.RemoveOwnership(id);
            onOwnershipChanged?.Invoke(id.id, null, _asServer);

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
        
        public void FixedUpdate()
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
                    $"Failed to find scene {scene} when applying ownership change for identity {change.identity}");
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
            
            module.GiveOwnership(identity, change.player);
            onOwnershipChanged?.Invoke(identity.id, change.player, _asServer);
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

        private void HandleOwnershipChange(PlayerID actor, OwnershipChange change, int id)
        {
            string verb = change.isAdding ? "give" : "remove";
            string verb2 = change.isAdding ? "giving" : "removing";
            
            if (!_hierarchy.TryGetIdentity(change.sceneId, id, out var identity))
            {
                PurrLogger.LogError(
                    $"Failed to find scene {change.sceneId} when {verb2} ownership change for identity {id}");
                return;
            }

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
            
            if (change.isAdding)
            {
                module.GiveOwnership(identity, change.player);
                onOwnershipChanged?.Invoke(identity.id, change.player, _asServer);
            }
            else
            {
                if (module.RemoveOwnership(identity))
                    onOwnershipChanged?.Invoke(identity.id, null, _asServer);
            }
        }

        private List<NetworkIdentity> GetAllChildrenOrSelf(NetworkIdentity id, bool? propagateToChildren)
        {
            var cache = HierarchyScene.CACHE;
            bool shouldPropagate = propagateToChildren ?? id.ShouldPropagateToChildren(_asServer);

            if (shouldPropagate && id.HasPropagateOwnershipAuthority(_asServer))
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
        
        readonly Dictionary<int, PlayerID> _owners = new ();

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
        
        public bool TryGetOwner(NetworkIdentity id, out PlayerID player)
        {
            return _owners.TryGetValue(id.id, out player);
        }

        public void GiveOwnership(NetworkIdentity identity, PlayerID player)
        {
            _owners[identity.id] = player;
            
            if (_asServer)
                 identity.internalOwnerServer = player;
            else identity.internalOwnerClient = player;
        }

        public bool RemoveOwnership(NetworkIdentity identity)
        {
            if (_owners.Remove(identity.id))
            {
                if (_asServer)
                     identity.internalOwnerServer = null;
                else identity.internalOwnerClient = null;
                return true;
            }
            
            return false;
        }
    }
}
