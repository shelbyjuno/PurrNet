using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;

namespace PurrNet.Modules
{
    internal partial struct OwnershipInfo : IAutoNetworkedData
    {
        public int identity;
        public PlayerID player;
    }
    
    internal partial struct OwnershipChangeBatch : IAutoNetworkedData
    {
        public SceneID scene;
        public List<OwnershipInfo> state;
    }
    
    internal partial struct OwnershipChange : INetworkedData
    {
        public SceneID sceneId;
        public int identity;
        public bool isAdding;
        public PlayerID player;

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref sceneId);
            packer.Serialize(ref identity);
            packer.Serialize(ref isAdding);
            
            if (isAdding)
                packer.Serialize(ref player);
        }
    }
    
    public class GlobalOwnershipModule : INetworkModule
    {
        readonly PlayersManager _playersManager;
        readonly ScenePlayersModule _scenePlayers;
        readonly HierarchyModule _hierarchy;
        
        readonly ScenesModule _scenes;
        readonly Dictionary<SceneID, SceneOwnership> _sceneOwnerships = new ();

        private bool _asServer;
        
        public GlobalOwnershipModule(HierarchyModule hierarchy, PlayersManager players, ScenePlayersModule scenePlayers, ScenesModule scenes)
        {
            _hierarchy = hierarchy;
            _scenes = scenes;
            _playersManager = players;
            _scenePlayers = scenePlayers;
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            _scenes.onPreSceneLoaded += OnSceneLoaded;
            _scenes.onSceneUnloaded += OnSceneUnloaded;
            
            if (asServer)
                _scenePlayers.onPlayerJoinedScene += OnPlayerJoined;
            
            _playersManager.Subscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Subscribe<OwnershipChange>(OnOwnershipChange);
        }

        public void Disable(bool asServer)
        {
            _scenes.onPreSceneLoaded -= OnSceneLoaded;
            _scenes.onSceneUnloaded -= OnSceneUnloaded;
            
            if (asServer)
                _scenePlayers.onPlayerJoinedScene -= OnPlayerJoined;

            _playersManager.Unsubscribe<OwnershipChangeBatch>(OnOwnershipChange);
            _playersManager.Unsubscribe<OwnershipChange>(OnOwnershipChange);
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
            if (asserver)
            {
                PurrLogger.LogError("TODO: Implement ownership changes on server from client");
                return;
            }

            for (var i = 0; i < data.state.Count; i++)
            {
                var change = data.state[i];
                if (_hierarchy.TryGetIdentity(data.scene, change.identity, out var identity))
                {
                    GiveOwnership(identity, change.player);
                }
            }
        }

        private void OnOwnershipChange(PlayerID player, OwnershipChange change, bool asserver)
        {
            if (asserver)
            {
                PurrLogger.LogError("TODO: Implement ownership changes on server from client");
                return;
            }
            
            if (_hierarchy.TryGetIdentity(change.sceneId, change.identity, out var identity))
            {
                if (change.isAdding)
                     GiveOwnership(identity, change.player);
                else RemoveOwnership(identity);
            }
        }
        
        private void OnSceneUnloaded(SceneID scene, bool asserver)
        {
            _sceneOwnerships.Remove(scene);
        }
        
        public void GiveOwnership(NetworkIdentity id, PlayerID player)
        {
            if (_sceneOwnerships.TryGetValue(id.sceneId, out var module))
            {
                module.GiveOwnership(id, player);

                if (_asServer)
                {
                    if (_scenePlayers.TryGetPlayersInScene(id.sceneId, out var players))
                    {
                        _playersManager.Send(players, new OwnershipChange
                        {
                            sceneId = id.sceneId,
                            identity = id.id,
                            isAdding = true,
                            player = player
                        });
                    }
                }
            }
        }
        
        public void RemoveOwnership(NetworkIdentity id)
        {
            if (_sceneOwnerships.TryGetValue(id.sceneId, out var module))
            {
                module.RemoveOwnership(id);

                if (_asServer)
                {
                    if (_scenePlayers.TryGetPlayersInScene(id.sceneId, out var players))
                    {
                        _playersManager.Send(players, new OwnershipChange
                        {
                            sceneId = id.sceneId,
                            identity = id.id,
                            isAdding = false,
                            player = default
                        });
                    }
                }
            }
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
            _sceneOwnerships[scene] = new SceneOwnership();
        }
    }

    internal class SceneOwnership
    {
        static readonly List<OwnershipInfo> _cache = new ();

        readonly Dictionary<int, PlayerID> _owners = new ();
        
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
        }

        public void RemoveOwnership(NetworkIdentity identity)
        {
            _owners.Remove(identity.id);
        }
    }
}
