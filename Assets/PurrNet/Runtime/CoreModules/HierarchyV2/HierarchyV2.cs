using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.Pooling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public struct SpawnPacket : IPackedAuto
    {
        public SceneID sceneId;
        public SpawnID packetIdx;
        public GameObjectPrototype prototype;
        
        public override string ToString()
        {
            return $"SpawnPacket: {{ sceneId: {sceneId}, packetIdx: {packetIdx}, prototype: {prototype} }}";
        }
    }
    
    public struct FinishSpawnPacket : IPackedAuto
    {
        public SceneID sceneId;
        public SpawnID packetIdx;

        public override string ToString()
        {
            return $"FinishSpawnPacket: {{ sceneId: {sceneId}, packetIdx: {packetIdx} }}";
        }
    }
        
    public struct DespawnPacket : IPackedAuto
    {
        public SceneID sceneId;
        public NetworkID parentId;
    }

    public readonly struct SpawnID : IEquatable<SpawnID>
    {
        readonly int packetIdx;
        public readonly PlayerID player;
        
        public SpawnID(int packetIdx, PlayerID player)
        {
            this.packetIdx = packetIdx;
            this.player = player;
        }

        public bool Equals(SpawnID other)
        {
            return packetIdx == other.packetIdx && player.Equals(other.player);
        }

        public override bool Equals(object obj)
        {
            return obj is SpawnID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(packetIdx, player);
        }
    }
    
    public delegate void IdentityAction(NetworkIdentity identity);
    public delegate void ObserverAction(PlayerID player, NetworkIdentity identity);
    
    public class HierarchyV2
    {
        private readonly NetworkManager _manager;
        private readonly bool _asServer;
        private readonly SceneID _sceneId;
        private readonly Scene _scene;
        private readonly ScenePlayersModule _scenePlayers;
        private readonly PlayersManager _playersManager;
        private readonly VisilityV2 _visibility;
        
        private readonly HierarchyPool _scenePool;
        private readonly HierarchyPool _prefabsPool;
        
        private readonly List<NetworkIdentity> _spawnedIdentities = new();
        private readonly Dictionary<NetworkID, NetworkIdentity> _spawnedIdentitiesMap = new();
        
        private int _nextId;

        public bool areSceneObjectsReady { get; private set; }
        
        public event IdentityAction onEarlyIdentityAdded;

        public event IdentityAction onIdentityAdded;
        
        public event IdentityAction onIdentityRemoved;

        public event ObserverAction onEarlyObserverAdded;
        
        // public event ObserverAction onObserverAdded;
        
        public HierarchyV2(NetworkManager manager, SceneID sceneId, Scene scene, 
            ScenePlayersModule players, PlayersManager playersManager, bool asServer)
        {
            _manager = manager;
            _sceneId = sceneId;
            _scene = scene;
            _scenePlayers = players;
            _visibility = new VisilityV2(_manager);
            _asServer = asServer;
            _playersManager = playersManager;
            
            _scenePool = NetworkPoolManager.GetScenePool(sceneId);
            _prefabsPool = NetworkPoolManager.GetPool(manager);
            
            SetupSceneObjects(scene);
        }

        private void SetupSceneObjects(Scene scene)
        {
            if (_manager.TryGetModule<HierarchyFactory>(!_asServer, out var factory) &&
                factory.TryGetHierarchy(_sceneId, out var other))
            {
                if (other.areSceneObjectsReady)
                {
                    areSceneObjectsReady = true;
                    return;
                }
            }
            
            if (areSceneObjectsReady)
                return;
            
            var allSceneIdentities = ListPool<NetworkIdentity>.Instantiate();
            SceneObjectsModule.GetSceneIdentities(scene, allSceneIdentities);
            
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            var count = allSceneIdentities.Count;
            for (int i = 0; i < count; i++)
            {
                var identity = allSceneIdentities[i];
                var root = identity.GetRootIdentity();
                
                if (!roots.Add(root))
                    continue;
                
                var children = ListPool<NetworkIdentity>.Instantiate();
                root.GetComponentsInChildren(true, children);
                
                var cc = children.Count;
                var pid = -i - 2;
                var rootDepth = root.transform.GetTransformDepth();
                
                for (int j = 0; j < cc; j++)
                {
                    var child = children[j];
                    var trs = child.transform;
                    int depth = trs.GetTransformDepth() - rootDepth;
                    child.PreparePrefabInfo(pid, trs.parent ? trs.GetSiblingIndex() : 0, depth, true, true);
                    
                    if (!_asServer)
                        child.ResetIdentity();
                }

                SpawnSceneObject(children);
                ListPool<NetworkIdentity>.Destroy(children);
                
                if (!_asServer)
                    _scenePool.PutBackInPool(root.gameObject);
            }
            
            ListPool<NetworkIdentity>.Destroy(allSceneIdentities);
            areSceneObjectsReady = true;
        }

        public void Enable()
        {
            PurrNetGameObjectUtils.onGameObjectCreated += OnGameObjectCreated;
            _visibility.visibilityChanged += OnVisibilityChanged;
            _scenePlayers.onPrePlayerloadedScene += OnPlayerLoadedScene;
            _scenePlayers.onPlayerUnloadedScene += OnPlayerUnloadedScene;
            
            _playersManager.Subscribe<SpawnPacket>(OnSpawnPacket);
            _playersManager.Subscribe<DespawnPacket>(OnDespawnPacket);
            _playersManager.Subscribe<FinishSpawnPacket>(OnFinishSpawnPacket);
        }

        public void Disable()
        {
            /*if (_manager.isOffline)
                PutAllBackInPool();*/
            
            PurrNetGameObjectUtils.onGameObjectCreated -= OnGameObjectCreated;
            _visibility.visibilityChanged -= OnVisibilityChanged;
            _scenePlayers.onPrePlayerloadedScene -= OnPlayerLoadedScene;
            _scenePlayers.onPlayerUnloadedScene -= OnPlayerUnloadedScene;

            _playersManager.Unsubscribe<SpawnPacket>(OnSpawnPacket);
            _playersManager.Unsubscribe<DespawnPacket>(OnDespawnPacket);
            _playersManager.Unsubscribe<FinishSpawnPacket>(OnFinishSpawnPacket);
        }
        
        private readonly Dictionary<SpawnID, DisposableList<NetworkIdentity>> _pendingSpawns = new();

        private void OnFinishSpawnPacket(PlayerID player, FinishSpawnPacket data, bool asServer)
        {
            if (data.sceneId != _sceneId)
                return;

            if (_pendingSpawns.Remove(data.packetIdx, out var list))
            {
                using (list)
                {
                    int count = list.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var nid = list[i];
                        if (!nid || !nid.isSpawned) continue;
                        
                        nid.TriggerSpawnEvent(_asServer);
                        onIdentityAdded?.Invoke(nid);
                    }
                }
            }
        }

        private void OnPlayerUnloadedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;

            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var count = _spawnedIdentities.Count;

            for (var i = 0; i < count; i++)
            {
                var id = _spawnedIdentities[i];
                
                if (!id) continue;
                
                var root = id.GetRootIdentity();
                
                if (!roots.Add(root))
                    continue;
                
                _visibility.ClearVisibilityForGameObject(root.transform, player);
            }

            HashSetPool<NetworkIdentity>.Destroy(roots);
        }

        private void OnSpawnPacket(PlayerID player, SpawnPacket data, bool asServer)
        {
            if (_asServer && !_manager.networkRules.HasSpawnAuthority(_manager, false))
            {
                PurrLogger.LogError($"Spawn failed from client due to lack of permissions.");
                return;
            }
            
            if (data.sceneId != _sceneId)
                return;
            
            // when in host mode, let the server handle the spawning on their module
            if (!_asServer && _manager.isServer)
                return;
            
            var createdNids = new DisposableList<NetworkIdentity>(16);
            CreatePrototype(data.prototype, createdNids.list);

            if (_asServer)
            {
                foreach (var nid in createdNids)
                {
                    nid.SetIdentity(_manager, this, _sceneId, _asServer);
                    RegisterIdentity(nid, false);
                    nid.TryAddObserver(player);
                    
                    // I think it makes more sense to not trigger this event here
                    // as it makes sense to assume they were already observers before the spawn
                    /*nid.TriggerOnObserverAdded(player);
                    onEarlyObserverAdded?.Invoke(player, nid);*/
                }
            }
            else
            {
                foreach (var nid in createdNids)
                {
                    nid.SetIdentity(_manager, this, _sceneId, _asServer);
                    RegisterIdentity(nid, false);
                }
            }

            if (createdNids.Count > 0 & _asServer && 
                _scenePlayers.TryGetPlayersInScene(_sceneId, out var players))
            {
                foreach (var playerInScene in players)
                    _visibility.RefreshVisibilityForGameObject(playerInScene, createdNids[0].transform);
            }

            _pendingSpawns.Add(data.packetIdx, createdNids);
        }

        private void OnDespawnPacket(PlayerID player, DespawnPacket data, bool asServer)
        {
            if (data.sceneId != _sceneId)
                return;
            
            if (!TryGetIdentity(data.parentId, out var identity))
                return;
            
            if (_asServer && !identity.HasDespawnAuthority(player, !_asServer))
            {
                PurrLogger.LogError($"Despawn failed for '{identity.gameObject.name}' due to lack of permissions.", identity.gameObject);
                return;
            }
            
            Despawn(identity.gameObject, true);
        }

        private void OnPlayerLoadedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneId)
                return;

            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var count = _spawnedIdentities.Count;

            for (var i = 0; i < count; i++)
            {
                var id = _spawnedIdentities[i];
                
                if (!id) continue;
                
                var root = id.GetRootIdentity();
                
                if (!roots.Add(root))
                    continue;
                
                _visibility.RefreshVisibilityForGameObject(player, root.transform);
            }

            HashSetPool<NetworkIdentity>.Destroy(roots);
        }
        
        private int _nextPacketIdx;
        
        private void OnVisibilityChanged(PlayerID player, Transform scope, bool isVisible)
        {
            if (!_scenePlayers.IsPlayerInScene(player, _sceneId))
                return;

            if (isVisible)
            {
                var children = ListPool<NetworkIdentity>.Instantiate();
                if (HierarchyPool.TryGetPrototype(scope, player, children, out var prototype))
                {
                    using (prototype)
                    {
                        SendSpawnPacket(player, prototype);

                        for (var i = 0; i < children.Count; i++)
                        {
                            var nid = children[i];
                            nid.TriggerOnObserverAdded(player);
                            onEarlyObserverAdded?.Invoke(player, nid);
                        }
                    }
                }
                else PurrLogger.LogError($"Failed to get prototype for '{scope.name}'.", scope);
                
                ListPool<NetworkIdentity>.Destroy(children);
                return;
            }
            
            if (scope.TryGetComponent<NetworkIdentity>(out var identity))
                SendDespawnPacket(player, identity);
        }

        private void SendDespawnPacket(PlayerID player, NetworkIdentity identity)
        {
            if (!identity.id.HasValue || !identity.IsSpawned(_asServer))
                return;
            
            var packet = new DespawnPacket
            {
                sceneId = _sceneId,
                parentId = identity.id.Value
            };

            if (player.isServer)
                 _playersManager.SendToServer(packet);
            else _playersManager.Send(player, packet);
        }

        private void SendSpawnPacket(PlayerID player, GameObjectPrototype prototype)
        {
            var spawnId = new SpawnID(_nextPacketIdx++, player);
            var packet = new SpawnPacket
            {
                sceneId = _sceneId,
                packetIdx = spawnId,
                prototype = prototype
            };

            if (player.isServer)
                 _playersManager.SendToServer(packet);
            else _playersManager.Send(player, packet);

            _toCompleteNextFrame.Add(spawnId);
        }

        private void OnGameObjectCreated(GameObject obj, GameObject prefab)
        {
            if (!obj)
                return;
            
            if (!_asServer && _manager.isServer)
                return;

            if (obj.scene.handle != _scene.handle)
                return;

            if (!_manager.TryGetPrefabData(prefab, out var data, out var idx))
                return;
            
            var rootOffset = obj.transform.GetTransformDepth();

            NetworkManager.SetupPrefabInfo(obj, idx, data.pool, false, -rootOffset);

            Spawn(obj);
        }

        public void Spawn(GameObject gameObject)
        {
            if (!gameObject)
                return;
            
            if (!gameObject.TryGetComponent<NetworkIdentity>(out var id))
            {
                PurrLogger.LogError($"Failed to spawn object '{gameObject.name}'. No NetworkIdentity found.", gameObject);
                return;
            }

            if (id.isSpawned)
                return;
            
            if (!id.HasSpawnAuthority(_manager, _asServer))
            {
                PurrLogger.LogError($"Spawn failed from for '{gameObject.name}' due to lack of permissions.", gameObject);
                return;
            }

            PlayerID scope = default;
            
            if (!_asServer)
            {
                if (!_playersManager.localPlayerId.HasValue)
                {
                    PurrLogger.LogError($"Failed to spawn object '{gameObject.name}'. No local player id found.", gameObject);
                    return;
                }
                
                scope = _playersManager.localPlayerId.Value;
            }
            
            var baseNid = new NetworkID(_nextId++, scope);
            SetupIdsLocally(id, ref baseNid);

            if (_scenePlayers.TryGetPlayersInScene(_sceneId, out var players))
            {
                foreach (var player in players)
                    _visibility.RefreshVisibilityForGameObject(player, gameObject.transform);
            }

            if (!_asServer)
                SendSpawnPacket(default, HierarchyPool.GetFullPrototype(gameObject.transform));
            
            AutoAssignOwnership(id);
        }

        private void AutoAssignOwnership(NetworkIdentity id)
        {
            if (!id.ShouldClientGiveOwnershipOnSpawn(_manager))
                return;
            
            PlayersManager playersManager;
            
            switch (_asServer)
            {
                case true when _manager.isClient:
                    playersManager = _manager.GetModule<PlayersManager>(false);
                    break;
                case false:
                    playersManager = _playersManager;
                    break;
                default:
                    return;
            }

            if (playersManager.localPlayerId.HasValue)
                id.GiveOwnership(playersManager.localPlayerId.Value);
        }

        public static void GetComponentsInChildren(GameObject go, List<NetworkIdentity> list)
        {
            // workaround for the fact that GetComponents clears the list
            var tmpList = ListPool<NetworkIdentity>.Instantiate();
            int startIdx = list.Count;
            go.GetComponents(tmpList);
            list.AddRange(tmpList);
            ListPool<NetworkIdentity>.Destroy(tmpList);
            
            if (list.Count <= startIdx)
                return;
            
            var identity = list[startIdx];
            var children = identity.directChildren;
            var dcount = children.Count;
            
            for (int j = 0; j < dcount; j++)
                GetComponentsInChildren(children[j].gameObject, list);
        }
        
        public void Despawn(GameObject gameObject, bool bypassPermissions)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            GetComponentsInChildren(gameObject, children);

            if (children.Count == 0)
            {
                ListPool<NetworkIdentity>.Destroy(children);
                return;
            }
            
            if (!bypassPermissions && !children[0].HasDespawnAuthority(_playersManager?.localPlayerId ?? default, _asServer))
            {
                PurrLogger.LogError($"Despawn failed for '{gameObject.name}' due to lack of permissions.", gameObject);
                ListPool<NetworkIdentity>.Destroy(children);
                return;
            }

            if (_asServer)
                _visibility.ClearVisibilityForGameObject(gameObject.transform);
            else SendDespawnPacket(default, children[0]);

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                
                UnregisterIdentity(child);
                
                if (child.shouldBePooled)
                    child.ResetIdentity();
            }

            var pair = new PoolPair(_scenePool, _prefabsPool);
            HierarchyPool.PutBackInPool(pair, gameObject);
            
            ListPool<NetworkIdentity>.Destroy(children);
        }

        private void SetupIdsLocally(NetworkIdentity root, ref NetworkID baseNid)
        {
            using var siblings = new DisposableList<NetworkIdentity>(16);
            root.GetComponents(siblings.list);
            
            // handle root
            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                sibling.SetID(new NetworkID(baseNid, i));
                sibling.SetIdentity(_manager, this, _sceneId, _asServer);
                RegisterIdentity(sibling, true);
            }

            // update next id
            _nextId += siblings.list.Count;
            baseNid = new NetworkID(_nextId, baseNid.scope);
            
            // handle children
            if (root.directChildren == null)
                return;
            
            for (var i = 0; i < root.directChildren.Count; i++)
            {
                SetupIdsLocally(root.directChildren[i], ref baseNid);
            }
        }
        
        private void SpawnSceneObject(List<NetworkIdentity> children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.isSceneObject)
                {
                    var id = new NetworkID(default, _nextId++);
                    child.SetID(id);
                    if (_asServer)
                    {
                        child.SetIdentity(_manager, this, _sceneId, _asServer);
                        RegisterIdentity(child, true);
                    }
                }
            }
        }
        
        public void PreNetworkMessages()
        {
            SendDelayedCompleteSpawns();
        }
        
        public void PostNetworkMessages()
        {
            SpawnDelayedIdentities();
        }

        private void SendDelayedCompleteSpawns()
        {
            for (var i = 0; i < _toCompleteNextFrame.Count; i++)
            {
                var toComplete = _toCompleteNextFrame[i];
                var packet = new FinishSpawnPacket
                {
                    sceneId = _sceneId,
                    packetIdx = toComplete
                };
                
                if (_asServer)
                     _playersManager.Send(toComplete.player, packet);
                else _playersManager.SendToServer(packet);
            }
            
            _toCompleteNextFrame.Clear();
        }

        private void SpawnDelayedIdentities()
        {
            for (var i = 0; i < _toSpawnNextFrame.Count; i++)
            {
                var toSpawn = _toSpawnNextFrame[i];
                
                if (!toSpawn || !toSpawn.isSpawned) continue;

                toSpawn.TriggerSpawnEvent(_asServer);

                if (_asServer && _manager.isClient)
                    toSpawn.TriggerSpawnEvent(false);
                
                onIdentityAdded?.Invoke(toSpawn);
            }

            _toSpawnNextFrame.Clear();
        }

        public GameObject CreatePrototype(GameObjectPrototype prototype, List<NetworkIdentity> createdNids)
        {
            var pair = new PoolPair(_scenePool, _prefabsPool);
            
            if (!HierarchyPool.TryBuildPrototype(pair, prototype, createdNids, out var result, out var shouldActivate))
            {
                PurrLogger.LogError("Failed to create prototype");
                return null;
            }
            
            result.transform.SetParent(null, false);
            
            var resultTrs = result.transform;
            resultTrs.SetLocalPositionAndRotation(prototype.position, prototype.rotation);
            
            SceneManager.MoveGameObjectToScene(result, _scene);
            
            if (shouldActivate)
                result.SetActive(true);

            return result;
        }
        
        readonly List<NetworkIdentity> _toSpawnNextFrame = new List<NetworkIdentity>();
        readonly List<SpawnID> _toCompleteNextFrame = new List<SpawnID>();

        /// <summary>
        /// Local spawn will trigger the spawn event next frame immediately after the identity is registered.
        /// </summary>
        private void RegisterIdentity(NetworkIdentity identity, bool isLocalSpawn)
        {
            if (identity.id.HasValue)
            {
                _spawnedIdentities.Add(identity);
                _spawnedIdentitiesMap.Add(identity.id.Value, identity);
                
                identity.TriggerEarlySpawnEvent(_asServer);
                if (_asServer && _manager.isClient)
                    identity.TriggerEarlySpawnEvent(false);
                
                onEarlyIdentityAdded?.Invoke(identity);
                
                if (isLocalSpawn)
                    _toSpawnNextFrame.Add(identity);
            }
        }
        
        private void UnregisterIdentity(NetworkIdentity identity)
        {
            if (identity.id.HasValue)
            {
                _spawnedIdentities.Remove(identity);
                _spawnedIdentitiesMap.Remove(identity.id.Value);
                
                identity.TriggerDespawnEvent(_asServer);
                if (_asServer && _manager.isClient)
                    identity.TriggerDespawnEvent(false);
                
                onIdentityRemoved?.Invoke(identity);
            }
        }
        
        public bool TryGetIdentity(NetworkID id, out NetworkIdentity identity)
        {
            return _spawnedIdentitiesMap.TryGetValue(id, out identity);
        }
    }
}