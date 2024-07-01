using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Transports;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    internal partial struct SpawnPrefabMessage : IAutoNetworkedData
    {
        public int prefabId { get; set; }
        public int childrenCount { get; set; }
        public int rootIdentityId { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 scale { get; set; }
    }
    
    internal partial struct DespawnIdentityMessage : IAutoNetworkedData
    {
        public int identityId { get; set; }
    }
    
    internal partial struct ParentInfoMessage : IAutoNetworkedData
    {
        public int identityId { get; set; }
        public int parentId { get; set; }
    }
    
    internal enum GameSpawnActionType
    {
        Spawn,
        Despawn,
        ChangeParent
    }
    
    internal partial struct GameActionSnapshot : IAutoNetworkedData
    {
        public List<GameSpawnActionEvent> events { get; set; }
        
        public GameActionSnapshot(List<GameSpawnActionEvent> events)
        {
            this.events = events;
        }
    }

    internal partial struct GameSpawnActionEvent : INetworkedData
    {
        public GameSpawnActionType type;
        
        public ParentInfoMessage setParentMessage;
        public SpawnPrefabMessage spawnPrefabMessage;
        public DespawnIdentityMessage despawnIdentityMessage;
        
        public GameSpawnActionEvent(DespawnIdentityMessage despawn)
        {
            type = GameSpawnActionType.Despawn;
            setParentMessage = default;
            spawnPrefabMessage = default;
            despawnIdentityMessage = despawn;
        }
        
        public GameSpawnActionEvent(ParentInfoMessage setParent)
        {
            type = GameSpawnActionType.ChangeParent;
            setParentMessage = setParent;
            spawnPrefabMessage = default;
            despawnIdentityMessage = default;
        }
        
        public GameSpawnActionEvent(SpawnPrefabMessage spawnPrefab)
        {
            type = GameSpawnActionType.Spawn;
            setParentMessage = default;
            despawnIdentityMessage = default;
            spawnPrefabMessage = spawnPrefab;
        }

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref type);

            switch (type)
            {
                case GameSpawnActionType.Spawn:
                    packer.Serialize(ref spawnPrefabMessage);
                    break;
                case GameSpawnActionType.ChangeParent:
                    packer.Serialize(ref setParentMessage);
                    break;
                case GameSpawnActionType.Despawn:
                    packer.Serialize(ref despawnIdentityMessage);
                    break;
            }
        }
    }
    
    public class SpawnManager : INetworkModule, IConnectionStateListener, IFixedUpdate
    {
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _playersManager;
        private readonly PlayersBroadcaster _broadcaster;
        internal static readonly List<NetworkIdentity> _identitiesCache = new ();
        
        private readonly Dictionary<int, NetworkIdentity> _allObjects = new ();
        private readonly List<GameSpawnActionEvent> _allServerEvents = new ();
        private readonly List<GameSpawnActionEvent> _serverEvents = new ();
        
        private bool _asServer;
        private int _nextIdentity;
        
        public SpawnManager(PlayersManager players, PlayersBroadcaster broadcaster, NetworkPrefabs prefabs)
        {
            _playersManager = players;
            _broadcaster = broadcaster;
            _prefabs = prefabs;
        }

        public void Enable(bool asServer)
        {
            _asServer = asServer;

            if (!asServer)
            {
                _broadcaster.Subscribe<GameActionSnapshot>(ClientBatchedSpawnEvent);
            }
            else
            {
                _playersManager.onPrePlayerJoined += PlayerJoined;
            }
        }
        
        
        private void PlayerJoined(PlayerID player, bool asserver)
        {
            if (_allServerEvents.Count == 0)
                return;

            _broadcaster.Send(player, new GameActionSnapshot(_allServerEvents));
        }
        
        private void ClientBatchedSpawnEvent(PlayerID player, GameActionSnapshot data, bool asserver)
        {
            if (data.events == null) return;
            
            for (int i = 0; i < data.events.Count; i++)
            {
                var serverEvent = data.events[i];
                
                switch (serverEvent.type)
                {
                    case GameSpawnActionType.Spawn:
                        HandleSpawnMessage(serverEvent.spawnPrefabMessage);
                        break;
                    case GameSpawnActionType.ChangeParent:
                        HandleParentChangedMessage(serverEvent.setParentMessage);
                        break;
                    case GameSpawnActionType.Despawn:
                        HandleDespawnMessage(serverEvent.despawnIdentityMessage);
                        break;
                }
            }
        }

        private void HandleDespawnMessage(DespawnIdentityMessage data)
        {
            if (_allObjects.TryGetValue(data.identityId, out var identity))
                Object.Destroy(identity.gameObject);
        }
        
        private void HandleParentChangedMessage(ParentInfoMessage data)
        {
            if (!_allObjects.TryGetValue(data.identityId, out var identity))
            {
                PurrLogger.LogError($"UpdateParent - The specified identity id '{data.identityId}' does not exist.");
                return;
            }

            NetworkIdentity parent = null;
            
            if (data.parentId != -1 && !_allObjects.TryGetValue(data.parentId, out parent))
            {
                PurrLogger.LogError($"The specified parent for '{identity.name}' with id '{data.parentId}' does not exist.", identity);
                return;
            }

            identity.transform.SetParent(parent == null ? null : parent.transform);
        }
        
        private void HandleSpawnMessage(SpawnPrefabMessage data)
        {
            if (!_prefabs.TryGetPrefab(data.prefabId, out var prefab))
            {
                PurrLogger.Throw<InvalidOperationException>(
                    $"SpawnAction - The specified prefab id '{data.prefabId}' is not a network prefab.");
            }

            var instance = Object.Instantiate(prefab, data.position, data.rotation);
            instance.transform.localScale = data.scale;

            if (!instance.TryGetComponent<NetworkIdentity>(out _))
                instance.AddComponent<NetworkIdentity>();

            instance.GetComponentsInChildren(true, _identitiesCache);

            var identitiesCount = _identitiesCache.Count;
            int firstIdentity = data.rootIdentityId;

            for (int i = 0; i < identitiesCount; i++)
            {
                _identitiesCache[i].SetIdentity(data.prefabId, firstIdentity + i);
                _allObjects.Add(firstIdentity + i, _identitiesCache[i]);
            }
        }

        public void Disable(bool asServer) { }
        
        private int AllocateNewIdentity()
        {
            return _nextIdentity++;
        }
        
        public void Spawn(GameObject prefab, GameObject instance)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can spawn objects.");

            if (!_prefabs.TryGetPrefabId(prefab, out var prefabId))
                PurrLogger.Throw<InvalidOperationException>($"The specified object '{prefab.name}' is not a network prefab.");
            
            // Check if the object already has a NetworkIdentity if not add one
            if (!instance.TryGetComponent<NetworkIdentity>(out _))
                instance.AddComponent<NetworkIdentity>();
            
            // Get all NetworkIdentities in the object
            instance.GetComponentsInChildren(true, _identitiesCache);
            
            instance.name += "_Server";

            var identitiesCount = _identitiesCache.Count;
            for (int i = 0; i < identitiesCount; i++)
            {
                var allocatedIdentity = AllocateNewIdentity();
                var identity = _identitiesCache[i];
                
                identity.SetIdentity(prefabId, allocatedIdentity);
                identity.onParentChanged += OnParentChangedServer;
                identity.onDestroy += OnSpawnedObjectGotDestroyedServer;
                
                _allObjects.Add(allocatedIdentity, identity);
            }
            
            var rootIdentity = _identitiesCache[0];
            var message = rootIdentity.GetSpawnMessage(_identitiesCache.Count);
            
            _serverEvents.Add(new GameSpawnActionEvent(message));
            _parentsDirty = true;
        }

        public void Despawn(GameObject instance)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can despawn objects.");
            
            if (!instance.TryGetComponent<NetworkIdentity>(out var identity))
                PurrLogger.Throw<InvalidOperationException>("The specified object does not have a NetworkIdentity component.");
            
            Despawn(identity);
        }
        
        public void Despawn(NetworkIdentity identity)
        {
            if (!_asServer)
                PurrLogger.Throw<InvalidOperationException>("Only clients can despawn objects.");

            if (!identity.isValid)
                PurrLogger.Throw<InvalidOperationException>("The specified object isn't spawn, can't despawn it.");
            
            identity.onDestroy -= OnSpawnedObjectGotDestroyedServer;
            identity.GetComponentsInChildren(true, _identitiesCache);
            
            for (int i = 0; i < _identitiesCache.Count; i++)
            {
                var id = _identitiesCache[i].id;
                _allObjects.Remove(id);
            }
            
            Object.Destroy(identity.gameObject);

            _serverEvents.Add(new GameSpawnActionEvent(new DespawnIdentityMessage
            {
                identityId = identity.id
            }));
            
            _parentsDirty = true;
        }
        
        private void OnParentChangedServer(NetworkIdentity obj)
        {
            int parentId = -1;
            var parentTrs = obj.transform.parent;
            
            if (parentTrs != null)
            {
                if (parentTrs.TryGetComponent<NetworkIdentity>(out var parent))
                     parentId = parent.id;
                else
                {
                    PurrLogger.LogError($"The parent object '{parentTrs.name}' does not have a NetworkIdentity component.", obj);
                    return;
                }
            }

            _serverEvents.Add(new GameSpawnActionEvent(new ParentInfoMessage
            {
                identityId = obj.id,
                parentId = parentId
            }));
            
            _parentsDirty = true;
        }

        private static void CompressEvents(List<GameSpawnActionEvent> events)
        {
            if (events.Count <= 1)
                return;

        }

        private void OnSpawnedObjectGotDestroyedServer(NetworkIdentity ni)
        {
            Despawn(ni);
            
            _allObjects.Remove(ni.id);
        }

        public void OnConnectionState(ConnectionState state, bool asServer)
        {
            if (state == ConnectionState.Disconnecting)
            {
                foreach (var (_, identity) in _allObjects)
                    Object.Destroy(identity.gameObject);
                
                _allObjects.Clear();
            }
        }
        
        private bool _parentsDirty;

        public void FixedUpdate()
        {
            if (!_parentsDirty) return;

            CompressEvents(_serverEvents);
            _allServerEvents.AddRange(_serverEvents);
            CompressEvents(_allServerEvents);
            
            string log = "Server events: \n";
            
            for (int i = 0; i < _allServerEvents.Count; i++)
            {
                var serverEvent = _allServerEvents[i];
                
                switch (serverEvent.type)
                {
                    case GameSpawnActionType.Spawn:
                        log += $"Spawn action {serverEvent.spawnPrefabMessage.rootIdentityId}\n";
                        break;
                    case GameSpawnActionType.ChangeParent:
                        log += $"Change parent action {serverEvent.setParentMessage.identityId} to {serverEvent.setParentMessage.parentId}\n";
                        break;
                    case GameSpawnActionType.Despawn:
                        log += $"Despawn action {serverEvent.despawnIdentityMessage.identityId}\n";
                        break;
                }
            }

            Debug.Log(log);
            
            _broadcaster.SendToAll(new GameActionSnapshot(_serverEvents));
            _serverEvents.Clear();
            _parentsDirty = false;
        }
    }
}
