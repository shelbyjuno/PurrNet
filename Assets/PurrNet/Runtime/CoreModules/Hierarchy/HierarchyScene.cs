using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.Pooling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    internal struct TriggerQueuedSpawnEvents : IPackedAuto
    {
        public SceneID sceneId;
    }

    internal struct ComponentGameObjectPair
    {
        public GameObject gameObject;
        public NetworkID? networkID;
    }
    
    internal struct GameObjectActive
    {
        public NetworkIdentity identity;
        public bool isActive;
    }
    
    internal class HierarchyScene : INetworkModule
    {
        private readonly NetworkManager _manager;
        private readonly IPrefabProvider _prefabs;
        private readonly PlayersManager _playersManager;
        private readonly ScenesModule _scenes;
        private readonly HierarchyHistory _history;
        private readonly ScenePlayersModule _scenePlayers;
        public readonly IdentitiesCollection identities;
        
        private VisibilityFactory _visibilityFactory;
        private VisibilityManager _visibilityManager;

        internal event Action<SceneID> onBeforeSpawnTrigger;

        /// <summary>
        /// Called for each identity that is removed from the hierarchy.
        /// </summary>
        public event Action<NetworkIdentity> onIdentityRemoved;
        
        /// <summary>
        /// Called for each identity that is added to the hierarchy.
        /// </summary>
        public event Action<NetworkIdentity> onIdentityAdded;
        
        /// <summary>
        /// Called only for the root of the identity that was spawned.
        /// </summary>
        public event Action<NetworkIdentity> onIdentityRootSpawned;
        
        private readonly SceneID _sceneID;
        private readonly bool _asServer;

        public string GetActionsAsString()
        {
            return GetActionsAsString(_history.GetFullHistory());
        }
        
        public static string GetActionsAsString(HierarchyActionBatch batch)
        {
            string value = "";
            for (int i = 0; i < batch.actions.Count; i++)
                value += batch.actions[i].ToString() + '\n';
            return value;
        }

        private HierarchyActionBatch GetActionsToSpawnTarget(List<NetworkIdentity> roots)
        {
            return _history.GetHistoryThatAffects(roots);
        }
        
        public HierarchyScene(bool asServer, SceneID sceneId, ScenesModule scenes, NetworkManager manager, 
            PlayersManager playersManager, ScenePlayersModule scenePlayers, IPrefabProvider prefabs)
        {
            _manager = manager;
            _playersManager = playersManager;
            _prefabs = prefabs;
            _scenePlayers = scenePlayers;
            _scenes = scenes;
            _sceneID = sceneId;
            _asServer = asServer;
            
            identities = new IdentitiesCollection(_asServer);
            _history = new HierarchyHistory(sceneId);
        }
        
        List<NetworkIdentity> _sceneObjects;
        
        public void Enable(bool asServer)
        {
            _visibilityManager.onObserverAdded += AddedObserverToIdentity;
            _visibilityManager.onObserverRemoved += RemovedObserverFromIdentity;
            _visibilityManager.onTickChangesDone += PostObserverEvents;
            
            if (!_asServer)
                _playersManager.onLocalPlayerReceivedID += OnLocalClientReady;
            
            _playersManager.Subscribe<HierarchyActionBatch>(OnHierarchyActionBatch);
            _playersManager.Subscribe<TriggerQueuedSpawnEvents>(OnTriggerSpawnEvents);

            if (_scenes.TryGetSceneState(_sceneID, out var state))
                _sceneObjects = SceneObjectsModule.GetSceneIdentities(state.scene);

            if (_asServer)
                SpawnSceneObjects(_sceneObjects);
            else if (_playersManager.localPlayerId.HasValue)
                SpawnSceneObjects(_sceneObjects);
            
            if (asServer)
                identities.SkipIds((ushort)_sceneObjects.Count);
        }

        private void OnLocalClientReady(PlayerID player)
        {
            SpawnSceneObjects(_sceneObjects);
        }

        public void Disable(bool asServer)
        {
            _visibilityManager.onObserverAdded -= AddedObserverToIdentity;
            _visibilityManager.onObserverRemoved -= RemovedObserverFromIdentity;
            _visibilityManager.onTickChangesDone -= PostObserverEvents;
            _playersManager.onLocalPlayerReceivedID -= OnLocalClientReady;

            _playersManager.Unsubscribe<HierarchyActionBatch>(OnHierarchyActionBatch);
            _playersManager.Unsubscribe<TriggerQueuedSpawnEvents>(OnTriggerSpawnEvents);

            foreach (var identity in identities.collection)
                identity.TriggerDespawnEvent(false);

            identities.DestroyAllNonSceneObjects();
        }

        private void OnTriggerSpawnEvents(PlayerID player, TriggerQueuedSpawnEvents data, bool asServer)
        {
            if (data.sceneId != _sceneID)
                return;
            
            TriggerSpawnEvents();
        }

        private void SpawnSceneObjects(IReadOnlyList<NetworkIdentity> sceneObjects)
        {
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                var obj = sceneObjects[i];
                var root = obj.GetRootIdentity();

                if (!roots.Add(root)) continue;
                
                if (!_asServer && !_manager.isServer)
                    root.gameObject.SetActive(false);
                
                CACHE.Clear();
                obj.GetComponentsInChildren(true, CACHE);

                var action = new SpawnAction
                {
                    prefabId = -1 - i,
                    identityId = new NetworkID((ushort)i),
                    childCount = (ushort)CACHE.Count,
                    childOffset = 0,
                    transformInfo = new TransformInfo(CACHE[0].transform)
                };

                for (int j = 0; j < CACHE.Count; ++j)
                    SpawnIdentity(action, CACHE[j], (ushort)j, _asServer);
                
                if (_asServer)
                    _history.AddSpawnAction(action, default);
                onIdentityRootSpawned?.Invoke(root);
            }

            HashSetPool<NetworkIdentity>.Destroy(roots);
        }
        
        readonly Dictionary<PlayerID, DisposableHashSet<NetworkID>> _identitiesToSpawnHashset = new ();
        readonly Dictionary<PlayerID, DisposableList<NetworkIdentity>> _identitiesToSpawn = new ();
        readonly Dictionary<PlayerID, NetworkNodes> _identitiesToDespawn = new ();
        
        private void AddedObserverToIdentity(PlayerID player, NetworkIdentity identity)
        {
            if (_identitiesToSpawnHashset.TryGetValue(player, out var actualSet))
            {
                actualSet.Add(identity.id!.Value);
            }
            else
            {
                var set = new DisposableHashSet<NetworkID>(16);
                set.Add(identity.id!.Value);
                
                _identitiesToSpawnHashset.Add(player, set);
            }
            
            if (_identitiesToSpawn.TryGetValue(player, out var actual))
            {
                actual.Add(identity);
            }
            else
            {
                var list = new DisposableList<NetworkIdentity>(16);
                list.Add(identity);
                _identitiesToSpawn.Add(player, list);
            }
        }
        
        private void RemovedObserverFromIdentity(PlayerID player, NetworkIdentity identity)
        {
            if (!identity.IsSpawned(true))
                return;
            
            if (_identitiesToDespawn.TryGetValue(player, out var actual))
            {
                actual.Add(identity);
            }
            else
            {
                var list = new NetworkNodes();
                list.Add(identity);
                _identitiesToDespawn.Add(player, list);
            }
        }
        
        private readonly HashSet<NetworkID> _instancesAboutToBeRemoved = new ();
        
        private void OnHierarchyActionBatch(PlayerID player, HierarchyActionBatch data, bool asServer)
        {
            if (_manager.isHost && !asServer) return;
            
            if (_sceneID != data.sceneId)
                return;

            _instancesAboutToBeRemoved.Clear();
            
            for (int i = 0; i < data.actions.Count; i++)
            {
                var action = data.actions[i];
                if (action is
                    {
                        type: HierarchyActionType.Despawn, despawnAction: { despawnType: DespawnType.GameObject }
                    })
                {
                    _instancesAboutToBeRemoved.Add(data.actions[i].despawnAction.identityId);
                }
            }
            
            for (int i = 0; i < data.actions.Count; i++)
            {
                var action = data.actions[i];
                OnHierarchyAction(player, ref action);
                data.actions[i] = action;
            }
            
            _instancesAboutToBeRemoved.Clear();
        }

        readonly struct ActionSignature
        {
            public readonly HierarchyActionType type;
            public readonly NetworkID identityId;
            
            public ActionSignature(HierarchyAction action)
            {
                type = action.type;
                identityId = action.GetIdentityId() ?? default;
            }

            public bool Equals(HierarchyAction other)
            {
                return type == other.type && identityId.Equals(other.GetIdentityId().GetValueOrDefault());
            }
        }
        
        readonly Queue<ActionSignature> _ignoreActions = new ();
        
        private void OnHierarchyAction(PlayerID player, ref HierarchyAction data)
        {
            var localPlayer = _playersManager.localPlayerId;

            if (_ignoreActions.Count > 0 && localPlayer.HasValue && data.actor == localPlayer.Value &&
                _ignoreActions.Peek().Equals(data))
            {
                _ignoreActions.Dequeue();
                return;
            }
            
            switch (data.type)
            {
                case HierarchyActionType.Despawn:
                    HandleDespawn(player, data.despawnAction);
                    break;
                
                case HierarchyActionType.Spawn:
                    HandleSpawn(player, ref data.spawnAction);
                    break;
                
                case HierarchyActionType.ChangeParent:
                    HandleChangeParent(player, data.changeParentAction);
                    break;
                
                case HierarchyActionType.SetActive:
                    HandleSetActive(player, data.setActiveAction);
                    break;
                
                case HierarchyActionType.SetEnabled:
                    HandleSetEnabled(player, data.setEnabledAction);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleSetEnabled(PlayerID player, SetEnabledAction dataSetEnabledAction)
        {
            if (!identities.TryGetIdentity(dataSetEnabledAction.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {dataSetEnabledAction.identityId}");
                return;
            }
            
            if (!identity.HasSetEnabledAuthority(player, !_asServer))
            {
                PurrLogger.LogError($"SetEnabled failed from '{player}' for '{identity.name}' due to lack of permissions.", identity);
                return;
            }

            identity.IgnoreNextEnableCallback();
            identity.enabled = dataSetEnabledAction.enabled;

            if (_asServer) _history.AddSetEnabledAction(dataSetEnabledAction, player);
        }

        private void HandleSetActive(PlayerID player, SetActiveAction dataSetActiveAction)
        {
            if (!identities.TryGetIdentity(dataSetActiveAction.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {dataSetActiveAction.identityId}");
                return;
            }
            
            if (!identity.ShouldSyncSetActive(!_asServer))
                return;
            
            if (!identity.HasSetActiveAuthority(player, !_asServer))
            {
                PurrLogger.LogError($"SetActive failed from '{player}' for '{identity.name}' due to lack of permissions.", identity);
                return;
            }

            identity.IgnoreNextActivationCallback();
            identity.IgnoreNextEnableCallback();
            
            identity.gameObject.SetActive(dataSetActiveAction.active);
            
            identity.ResetIgnoreNextActivation();
            identity.ResetIgnoreNextEnable();
            
            if (_asServer) _history.AddSetActiveAction(dataSetActiveAction, player);
        }

        private void HandleChangeParent(PlayerID player, ChangeParentAction action)
        {
            if (!identities.TryGetIdentity(action.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }
            
            if (identity is not NetworkTransform trs)
            {
                PurrLogger.LogError($"Identity with id {action.identityId} is not a NetworkTransform");
                return;
            }
            
            if (!trs.HasChangeParentAuthority(player, !_asServer))
            {
                PurrLogger.LogError($"Parent change from '{player}' failed for '{trs.name}' due to lack of permissions.", trs);
                return;
            }
            
            NetworkIdentity parent = null;
            
            if (action.parentId.HasValue && !identities.TryGetIdentity(action.parentId.Value, out parent))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }
            
            if (parent && !parent.isSceneObject && trs.isSceneObject)
            {
                PurrLogger.LogError($"Failed to change parent of '{trs.name}' to '{parent.name}' because scene objects can't be parented to non-scene objects.", trs);
                return;
            }

            trs.StartIgnoreParentChanged();
            identity.transform.SetParent(parent ? parent.transform : null);
            trs.StopIgnoreParentChanged();
            trs.ValidateParent();

            if (_asServer) _history.AddChangeParentAction(action, player);
        }
        
        bool TryGetPrefab(int prefabId, out GameObject prefab)
        {
            if (prefabId < 0)
            {
                int actualIndex = -1 - prefabId;
                
                if (actualIndex >= _sceneObjects.Count)
                {
                    prefab = null;
                    return false;
                }
                
                prefab = _sceneObjects[actualIndex].gameObject;
                return true;
            }
            
            return _prefabs.TryGetPrefab(prefabId, out prefab);
        }

        private void HandleSpawn(PlayerID player, ref SpawnAction action)
        {
            if (_asServer)
            {
                var nid = new NetworkID(action.identityId.id, player);
                action.identityId = nid;
            }
            
            bool isScenePrefab = action.prefabId < 0;

            if (!TryGetPrefab(action.prefabId, out var prefab))
            {
                PurrLogger.LogError($"Failed to find prefab with id {action.prefabId}");
                return;
            }

            NetworkIdentity link;

            if (isScenePrefab)
            {
                link = prefab.GetComponent<NetworkIdentity>();
            }
            else
            {
                if (!prefab.TryGetComponent<PrefabLink>(out var plink))
                {
                    PurrLogger.LogError($"Failed to find PrefabLink component on '{prefab.name}'");
                    return;
                }
                
                link = plink;
            }

            if (!link.HasSpawnAuthority(_manager, !_asServer))
            {
                PurrLogger.LogError($"Spawn failed from '{player}' for prefab '{prefab.name}' due to lack of permissions.");
                return;
            }

            if (!isScenePrefab && identities.TryGetIdentity(action.identityId, out _))
            {
                // PurrLogger.LogError($"Identity with id {action.identityId} is already spawned");
                return;
            }

            if (action.childOffset != 0)
            {
                prefab = GetChildPrefab(prefab, action.childOffset);

                if (prefab == null)
                {
                    PurrLogger.LogError($"Failed to find child with offset {action.childOffset} for prefab {prefab.name}");
                    return;
                }
            }

            var trsInfo = action.transformInfo;
            Transform parent = null;

            if (trsInfo.parentId.HasValue && identities.TryGetIdentity(trsInfo.parentId, out var parentIdentity))
                parent = parentIdentity.transform;
            
            if (!isScenePrefab)
                PrefabLink.StartIgnoreAutoSpawn();

            var oldActive = prefab.gameObject.activeInHierarchy;

            if (!isScenePrefab && oldActive && parent == null)
            {
                prefab.gameObject.SetActive(false);
            }

            GameObject go;

            if (isScenePrefab)
            {
                go = prefab;

                if (parent)
                {
                    go.transform.SetParent(parent);
                    go.transform.SetLocalPositionAndRotation(trsInfo.localPos, trsInfo.localRot);
                }
            }
            else
            {
                if (parent == null)
                {
                    go = Object.Instantiate(prefab.gameObject, trsInfo.localPos, trsInfo.localRot);
                }
                else
                {
                    go = Object.Instantiate(prefab.gameObject, parent);
                    go.transform.SetLocalPositionAndRotation(trsInfo.localPos, trsInfo.localRot);
                }
            }

            go.transform.localScale = trsInfo.localScale;

            if (!isScenePrefab && parent == null && _scenes.TryGetSceneState(_sceneID, out var state))
            {
                SceneManager.MoveGameObjectToScene(go, state.scene);
                if (oldActive) prefab.gameObject.SetActive(true);
            }
            
            MakeSureAwakeIsCalled(go);
            
            go.GetComponentsInChildren(true, CACHE);
            
            for (int i = 0; i < CACHE.Count; i++)
            {
                var child = CACHE[i];
                if (child.IsSpawned(_asServer))
                    continue;
                
                SpawnIdentity(action, child, (ushort)i, _asServer);
            }



            if (CACHE.Count > 0)
                onIdentityRootSpawned?.Invoke(CACHE[0]);

            if (go.activeSelf != trsInfo.activeHierarchy)
            {
                var identity = go.GetComponent<NetworkIdentity>();
                
                identity.IgnoreNextActivationCallback();
                identity.IgnoreNextEnableCallback();
                
                go.SetActive(trsInfo.activeHierarchy);

                identity.ResetIgnoreNextActivation();
                identity.ResetIgnoreNextEnable();
            }
            
            if (_asServer) _history.AddSpawnAction(action, player);
            
            PrefabLink.StopIgnoreAutoSpawn();
        }
        
        private void SpawnIdentity(SpawnAction action, NetworkIdentity component, ushort offset, bool asServer)
        {
            var siblingIdx = component.transform.parent ? component.transform.GetSiblingIndex() : 0;
            SpawnIdentity(component, action.prefabId, siblingIdx, action.identityId, offset, asServer);
        }
        
        void SpawnIdentity(NetworkIdentity component, int prefabId, int siblingId, NetworkID nid, ushort offset, bool asServer)
        {
            var identityId = new NetworkID(nid, offset);

            if (component.IsSpawned(asServer) &&
                (asServer ? component.idServer == identityId : component.idClient == identityId))
            {
                return;
            }
            
            component.SetIdentity(_manager, _sceneID, prefabId, siblingId, identityId, offset, asServer);

            identities.TryRegisterIdentity(component);
            onIdentityAdded?.Invoke(component);

            component.onFlush += OnIdentityFlush;
            component.onRemoved += OnIdentityRemoved;
            component.onEnabledChanged += OnIdentityEnabledChanged;
            component.onActivatedChanged += OnIdentityGoActivatedChanged;

            if (component is NetworkTransform { syncParent: true } transform)
                transform.onParentChanged += OnIdentityParentChanged;

            _spawnedThisFrame.Add(component);
        }
        
        internal static readonly List<NetworkIdentity> CACHE = new ();
        internal static readonly List<NetworkIdentity> CACHE2 = new ();
        
        private static GameObject GetChildPrefab(GameObject root, int child)
        {
            root.GetComponentsInChildren(true, CACHE);
            
            if (child >= CACHE.Count)
            {
                PurrLogger.LogError($"Failed to find child with index {child}");
                return null;
            }
            
            return CACHE[child].gameObject;
        }
        
        private static void RemoveChildrenFromCache(Transform childTrs, int childIdx)
        {
            for (int i = childIdx + 1; i < CACHE.Count; i++)
            {
                var child = CACHE[i];
                if (child.transform.IsChildOf(childTrs))
                    CACHE.RemoveAt(i--);
            }
        }

        private void HandleDespawn(PlayerID player, DespawnAction action)
        {
            if (!identities.TryGetIdentity(action.identityId, out var identity))
            {
                if (_asServer) _history.AddDespawnAction(action, player);
                return;
            }

            if (_asServer && !identity.HasDespawnAuthority(player, !_asServer))
            {
                PurrLogger.LogError($"Despawn failed from '{player}' for '{identity.name}' due to lack of permissions.", identity);
                return;
            }

            if (action.despawnType == DespawnType.GameObject)
            {
                bool sceneObject = identity.isSceneObject;
                var safeParent = identity.transform.parent;
                
                identity.gameObject.GetComponentsInChildren(true, CACHE);

                for (int i = 0; i < CACHE.Count; i++)
                {
                    var child = CACHE[i];

                    if (child.id.HasValue && !_instancesAboutToBeRemoved.Contains(child.id.Value))
                    {
                        var trs = child.transform;
                        RemoveChildrenFromCache(trs, i);
                        trs.SetParent(safeParent);
                        continue;
                    }

                    if (!sceneObject)
                    {
                        if (identities.UnregisterIdentity(child))
                        {
                            onIdentityRemoved?.Invoke(child);
                            child.TriggerDespawnEvent(_asServer);
                            child.IgnoreNextDestroyCallback();
                        }
                    }
                    else
                    {
                        child.TriggerDespawnEvent(_asServer);
                        child.SetIsSpawned(false, _asServer);
                    }
                }

                if (!sceneObject)
                     Object.Destroy(identity.gameObject);
                else
                {
                    var go = identity.gameObject;

                    if (go.activeInHierarchy)
                    {
                        identity.IgnoreNextActivationCallback();
                        go.SetActive(false);
                    }
                }
            }
            else
            {
                identity.IgnoreNextDestroyCallback();
                if (identities.UnregisterIdentity(identity))
                {
                    onIdentityRemoved?.Invoke(identity);
                    identity.TriggerDespawnEvent(_asServer);
                }
                
                Object.Destroy(identity);
            }
            
            if (_asServer) _history.AddDespawnAction(action, player);
            
            PostObserverEvents();
        }

        List<NetworkIdentity> _spawnedThisFrame = new ();
        List<NetworkIdentity> _spawnedThisFrameBuffer = new ();
        
        readonly List<NetworkIdentity> _triggerPostIdentityFunc = new ();
        
        public void Spawn(ref GameObject instance)
        {
            MakeSureAwakeIsCalled(instance);

            if (!_manager.networkRules.HasSpawnAuthority(_manager, _asServer))
            {
                PurrLogger.LogError($"Failed to spawn '{instance.name}' due to lack of permissions.");
                instance = null;
                Object.Destroy(instance);
                return;
            }
            
            if (!instance.TryGetComponent<PrefabLink>(out var link))
            {
                PurrLogger.LogError($"Failed to find PrefabLink component on {instance.name}");
                return;
            }

            if (!_prefabs.TryGetPrefabID(link.prefabGuid, out var prefabId))
            {
                PurrLogger.LogError($"Failed to find prefab with guid {link.prefabGuid}");
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to spawn '{instance.name}' before having been assigned a local player id.");
                return;
            }

            instance.GetComponentsInChildren(true, CACHE);

            if (CACHE.Count == 0)
            {
                PurrLogger.LogError($"Failed to find networked components for '{instance.name}'");
                return;
            }
            
            CACHE2.Clear();
            for (int i = 0; i < CACHE.Count; i++)
            {
                var child = CACHE[i];

                if (child.isSpawned)
                {
                    PurrLogger.LogError($"Identity with id {child.id} is already spawned", child);
                    continue;
                }
                
                var nid = new NetworkID(identities.GetNextId(), actor);

                while (identities.HasIdentity(nid))
                    nid = new NetworkID(identities.GetNextId(), actor);
                
                var siblingIdx = child.transform.parent ? child.transform.GetSiblingIndex() : 0;
                
                child.SetIdentity(_manager, _sceneID, prefabId, siblingIdx, nid, (ushort)i, _asServer);
                _spawnedThisFrame.Add(child);
                identities.RegisterIdentity(child);
                
                onIdentityAdded?.Invoke(child);

                child.onFlush += OnIdentityFlush;
                child.onRemoved += OnIdentityRemoved;
                child.onEnabledChanged += OnIdentityEnabledChanged;
                child.onActivatedChanged += OnIdentityGoActivatedChanged;

                if (child is NetworkTransform transform)
                    transform.onParentChanged += OnIdentityParentChanged;
                
                CACHE2.Add(child);
            }

            for (int i = 0; i < CACHE2.Count; i++)
            {
                _triggerPostIdentityFunc.Add(CACHE2[i]);
            }
            
            var action = new SpawnAction
            {
                prefabId = prefabId,
                childOffset = 0,
                identityId = CACHE[0].id!.Value,
                childCount = (ushort)CACHE.Count,
                transformInfo = new TransformInfo(instance.transform)
            };

            _history.AddSpawnAction(action, actor);
        }

        private bool TryGetActor(out PlayerID player)
        {
            if (!_asServer)
            {
                if (_playersManager.localPlayerId == null)
                {
                    player = default;
                    return false;
                }
                
                player = _playersManager.localPlayerId.Value;
                return true;
            }
            
            player = default;
            return true;
        }

        struct BehaviourState
        {
            public Behaviour component;
            public bool enabled;
        }
        
        static readonly List<Behaviour> _components = new ();

        /// <summary>
        /// Awake is not called on disabled game objects, so we need to ensure it's called for all components.
        /// </summary>
        internal static void MakeSureAwakeIsCalled(GameObject root)
        {
            var cache = ListPool<BehaviourState>.Instantiate();
            var gosToDeactivate = HashSetPool<GameObject>.Instantiate();
            
            // for components in disabled game objects, disabled them, activate game object, and reset their enabled state
            root.GetComponentsInChildren(true, _components);
            
            for (int i = 0; i < _components.Count; i++)
            {
                var child = _components[i];
                
                if (!child)
                    continue;
                
                if (!child.gameObject.activeSelf)
                {
                    cache.Add(new BehaviourState
                    {
                        component = child,
                        enabled = child.enabled
                    });
                    
                    child.enabled = false;
                    
                    gosToDeactivate.Add(child.gameObject);
                }
            }

            foreach (var go in gosToDeactivate)
            {
                go.SetActive(true);
                go.SetActive(false);
            }
            
            for (int i = 0; i < cache.Count; i++)
            {
                var state = cache[i];
                state.component.enabled = state.enabled;
            }

            HashSetPool<GameObject>.Destroy(gosToDeactivate);
            ListPool<BehaviourState>.Destroy(cache);
        }

        private void OnIdentityParentChanged(NetworkTransform trs)
        {
            if (!trs.id.HasValue)
                return;
            
            if (!trs.ShouldSyncParent(_asServer))
            {
                trs.ResetToLastValidParent();
                return;
            }
            
            if (!trs.HasChangeParentAuthority(_asServer))
            {
                PurrLogger.LogError($"Parent change failed for '{trs.name}' due to lack of permissions.", trs);
                trs.ResetToLastValidParent();
                return;
            }

            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to change parent of '{trs.name}' before having been assigned a local player id.");
                return;
            }
            
            var parentTrs = trs.transform.parent;
            NetworkID? parentId;

            if (parentTrs)
            {
                if (parentTrs.TryGetComponent<NetworkIdentity>(out var parent))
                {
                    if (trs.isSceneObject && !parent.isSceneObject)
                    {
                        PurrLogger.LogError(
                            $"Failed to change parent of '{trs.name}' to '{parent.name}' because scene objects can't be parented to non-scene objects.",
                            trs);
                        trs.ResetToLastValidParent();
                        return;
                    }
                    parentId = parent.id;
                }
                else
                {
                    PurrLogger.LogError(
                        $"Failed to find NetworkIdentity component on parent '{parentTrs.name}' aborting parent change.",
                        trs);

                    trs.ResetToLastValidParent();
                    return;
                }
            }
            else parentId = null;
            
            var action = new ChangeParentAction
            {
                identityId = trs.id.Value,
                parentId = parentId
            };
            
            _history.AddChangeParentAction(action, actor);
            trs.ValidateParent();
            
            _visibilityManager.ReEvaluateRoot(trs);
        }
        
        readonly List<ComponentGameObjectPair> _removedLastFrame = new ();
        readonly List<NetworkIdentity> _toggledLastFrame = new ();
        readonly List<GameObjectActive> _activatedLastFrame = new ();
        
        private void OnIdentityEnabledChanged(NetworkIdentity identity, bool enabled)
        {
            _toggledLastFrame.Add(identity);
        }
        
        private void OnIdentityRemoved(NetworkIdentity identity)
        {
            _removedLastFrame.Add(new ComponentGameObjectPair
            {
                gameObject = identity.gameObject,
                networkID = identity.id
            });
            
            onIdentityRemoved?.Invoke(identity);
            identity.TriggerDespawnEvent(_asServer);

            if (!identity.isSceneObject && _asServer && _manager.isHost)
                identity.TriggerDespawnEvent(false);
        }
        
        private void OnIdentityGoActivatedChanged(NetworkIdentity identity, bool active)
        {
            _activatedLastFrame.Add(new GameObjectActive
            {
                identity = identity,
                isActive = active
            });
        }

        private void OnIdentityRemoved(ComponentGameObjectPair pair)
        {
            if (pair.networkID.HasValue)
                identities.UnregisterIdentity(pair.networkID.Value);
            
            if (!pair.gameObject)
                 OnDestroyedObject(pair.networkID);
            else OnRemovedComponent(pair.networkID);
        }

        private void OnDestroyedObject(NetworkID? entityId)
        {
            if (!entityId.HasValue)
            {
                PurrLogger.LogError("Trying to destroy object with no id.");
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to destroy object with id {entityId} before having been assigned a local player id.");
                return;
            }
            
            _history.AddDespawnAction(new DespawnAction
            {
                identityId = entityId.Value,
                despawnType = DespawnType.GameObject
            }, actor);
        }

        private void OnRemovedComponent(NetworkID? entityId)
        {
            if (!entityId.HasValue)
            {
                PurrLogger.LogError("Trying to remove component with no id.");
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to remove component with id {entityId} before having been assigned a local player id.");
                return;
            }
            
            _history.AddDespawnAction(new DespawnAction
            {
                identityId = entityId.Value,
                despawnType = DespawnType.ComponentOnly
            }, actor);
        }
        
        private void OnToggledComponent(NetworkIdentity identity, bool active)
        {
            if (!identity.id.HasValue)
                return;
            
            if (!identity.ShouldSyncSetEnabled(_asServer))
                return;
            
            if (!identity.HasSetEnabledAuthority(_asServer))
            {
                PurrLogger.LogError($"SetEnabled failed for '{identity.name}' due to lack of permissions.", identity);
                identity.IgnoreNextEnableCallback();
                identity.enabled = !active;
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to toggle component with id {identity.id} before having been assigned a local player id.");
                identity.IgnoreNextEnableCallback();
                identity.enabled = !active;
                return;
            }
            
            _history.AddSetEnabledAction(new SetEnabledAction
            {
                identityId = identity.id.Value,
                enabled = active
            }, actor);
        }
        
        private void OnToggledGameObject(NetworkIdentity identity, bool active)
        {
            if (!identity.id.HasValue)
                return;
            
            if (!identity.ShouldSyncSetActive(_asServer))
                return;
            
            if (!identity.HasSetActiveAuthority(_asServer))
            {
                PurrLogger.LogError($"SetActive failed for '{identity.name}' due to lack of permissions.", identity);
                identity.IgnoreNextActivationCallback();
                identity.gameObject.SetActive(!active);
                identity.ResetIgnoreNextActivation();
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to toggle game object with id {identity.id} before having been assigned a local player id.");
                identity.IgnoreNextActivationCallback();
                identity.gameObject.SetActive(!active);
                identity.ResetIgnoreNextActivation();
                return;
            }
            
            _history.AddSetActiveAction(new SetActiveAction
            {
                identityId = identity.id.Value,
                active = active
            }, actor);
        }
        
        private void OnIdentityFlush(NetworkIdentity obj)
        {
            if (_history.hasUnflushedActions)
            {
                SendDeltaToPlayers(_history.GetDelta());
                SendOwnershipChanges();
                TriggerSpawnEvents();
            }
        }
        
        public void PreFixedUpdate()
        {
            if (_toggledLastFrame.Count > 0)
            {
                for (int i = 0; i < _toggledLastFrame.Count; i++)
                {
                    var identity = _toggledLastFrame[i];
                    
                    if (!identity)
                        continue;

                    OnToggledComponent(identity, identity.enabled);
                }
                
                _toggledLastFrame.Clear();
            }
            
            if (_activatedLastFrame.Count > 0)
            {
                for (int i = 0; i < _activatedLastFrame.Count; i++)
                {
                    var active = _activatedLastFrame[i];
                    
                    if (!active.identity) 
                        continue;
                    
                    OnToggledGameObject(active.identity, active.isActive);
                }
                _activatedLastFrame.Clear();
            }
            
            if (_removedLastFrame.Count > 0)
            {
                for (int i = 0; i < _removedLastFrame.Count; i++)
                    OnIdentityRemoved(_removedLastFrame[i]);
                _removedLastFrame.Clear();
                PostObserverEvents();
            }

            if (_history.hasUnflushedActions)
            {
                SendDeltaToPlayers(_history.GetDelta());
                SendOwnershipChanges();
                TriggerSpawnEvents();
            }

            if (_identitiesToSpawn.Count > 0)
            {
                HandleIdentitiesThatNeedToBeSpawned(_identitiesToSpawn);

                foreach (var (_, set) in _identitiesToSpawnHashset)
                    set.Dispose();
                
                _identitiesToSpawnHashset.Clear();
                _identitiesToSpawn.Clear();
            }
            
            /*if (!_manager.pendingHost)
                TriggerSpawnEvents();*/
        }

        private void SendOwnershipChanges()
        {
            if (_triggerPostIdentityFunc.Count > 0)
            {
                for (var i = 0; i < _triggerPostIdentityFunc.Count; i++)
                {
                    var child = _triggerPostIdentityFunc[i];
                    child.PostSetIdentity();
                }

                if (_asServer)
                {
                    _playersManager.SendToAll(new TriggerQueuedSpawnEvents
                    {
                        sceneId = _sceneID
                    });
                }
                else _playersManager.SendToServer(new TriggerQueuedSpawnEvents
                {
                    sceneId = _sceneID
                });

                _triggerPostIdentityFunc.Clear();
            }
        }

        private void PostObserverEvents()
        {
            if (_identitiesToDespawn.Count > 0)
            {
                HandleIdentitiesThatNeedToBeDespawned(_identitiesToDespawn);
                _identitiesToDespawn.Clear();
            }
        }

        private void TriggerSpawnEvents()
        {
            var spawnedThisFrameCount = _spawnedThisFrame.Count;

            if (spawnedThisFrameCount > 0)
            {
                (_spawnedThisFrame, _spawnedThisFrameBuffer) = (_spawnedThisFrameBuffer, _spawnedThisFrame);
            
                _spawnedThisFrame.Clear();
                
                for (int i = 0; i < spawnedThisFrameCount; i++)
                {
                    try
                    {
                        var identity = _spawnedThisFrameBuffer[i];

                        if (!identity)
                            continue;

                        identity.TriggerSpawnEvent(_asServer);
                        
                        if (identity.id.HasValue && !identity.isSceneObject && _asServer && _manager.isHost)
                        {
                            identity.SetIdentity(_manager, _sceneID, 
                                identity.prefabId, 
                                identity.siblingIndex,
                                identity.id.Value, identity.prefabOffset, 
                                false
                            );
                            
                            identity.TriggerSpawnEvent(false);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                if (_spawnedThisFrame.Count > 0)
                    TriggerSpawnEvents();
            }
        }

        private void SendDeltaToPlayers(HierarchyActionBatch delta)
        {
            // if client, no filtering is needed
            if (!_asServer)
            {
                for (int i = 0; i < delta.actions.Count; i++)
                {
                    if (delta.actions[i].type == HierarchyActionType.Spawn)
                        _ignoreActions.Enqueue(new ActionSignature(delta.actions[i]));
                }
                
                _playersManager.SendToServer(delta);
                _history.Clear();
                return;
            }

            if (!_scenePlayers.TryGetPlayersInScene(_sceneID, out var players))
            {
                PurrLogger.LogError($"Failed to find players in scene {_sceneID}");
                return;
            }

            if (players.Count == 0)
            {
                _history.Flush();
                return;
            }

            var filtered = ListPool<HierarchyAction>.Instantiate();

            foreach (var player in players)
            {
                FilterActions(delta.actions, filtered, player);
                
                if (filtered.Count > 0)
                {
                    var data = new HierarchyActionBatch
                    {
                        actions = filtered,
                        sceneId = _sceneID
                    };

                    _playersManager.Send(player, data);
                }

                filtered.Clear();
            }
            
            ListPool<HierarchyAction>.Destroy(filtered);

            _history.Flush();
        }

        private void FilterActions(List<HierarchyAction> original, List<HierarchyAction> filtered, PlayerID target)
        {
            if (_identitiesToSpawnHashset.ContainsKey(target))
                return;
            
            for (int i = 0; i < original.Count; i++)
            {
                var action = original[i];
                var netId = action.GetIdentityId();
                
                if (!netId.HasValue)
                    continue;
                
                if (action.type == HierarchyActionType.Spawn)
                {
                    filtered.Add(action);
                    continue;
                }
                
                bool isSceneObject = identities.TryGetIdentity(netId.Value, out var identity) && identity.isSceneObject;

                if (isSceneObject || _visibilityManager.TryGetObservers(netId.Value, out var observers) &&
                    observers.Contains(target))
                {
                    filtered.Add(action);
                }
            }
        }

        private void HandleIdentitiesThatNeedToBeSpawned(Dictionary<PlayerID, DisposableList<NetworkIdentity>> identitiesToSpawn)
        {
            var roots = HashSetPool<NetworkIdentity>.Instantiate();
            var rootsList = ListPool<NetworkIdentity>.Instantiate();
            foreach (var (player, all) in identitiesToSpawn)
            {
                roots.Clear();
                rootsList.Clear();
                
                for (var i = 0; i < all.Count; i++)
                {
                    var id = all[i].GetRootIdentity();

                    if (!roots.Add(id))
                        continue;

                    rootsList.Add(id);
                }

                var actions = GetActionsToSpawnTarget(rootsList);
                _playersManager.Send(player, actions);

                for (var i = 0; i < all.Count; i++)
                {
                    var id = all[i];

                    if (id && id.observers.Contains(player))
                    {
                        var children = ListPool<NetworkIdentity>.Instantiate();
                        id.GetComponentsInChildren(true, children);
                        
                        for (var j = 0; j < children.Count; j++)
                        {
                            var child = children[j];
                            _visibilityFactory.TriggerLateObserverAdded(player, child);
                            child.TriggerOnObserverAdded(player);
                        }
                    }
                }
                
                onBeforeSpawnTrigger?.Invoke(_sceneID);
                
                TriggerSpawnEvents();
                
                _playersManager.Send(player, new TriggerQueuedSpawnEvents
                {
                    sceneId = _sceneID
                });
                
                all.Dispose();
            }
            
            ListPool<NetworkIdentity>.Destroy(rootsList);
            HashSetPool<NetworkIdentity>.Destroy(roots);
        }

        private static readonly List<HierarchyAction> _actionsCache = new ();
        
        private void HandleIdentitiesThatNeedToBeDespawned(Dictionary<PlayerID, NetworkNodes> identitiesToDespawn)
        {
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            foreach (var (player, nodes) in identitiesToDespawn)
            {
                roots.Clear();
                _actionsCache.Clear();
                
                var actions = new HierarchyActionBatch
                {
                    actions = _actionsCache,
                    sceneId = _sceneID
                };

                foreach (var (_, children) in nodes.children)
                    AddDespawnActionForAllChildren(children, _actionsCache);

                if (_actionsCache.Count > 0)
                    _playersManager.Send(player, actions);

                foreach (var (_, children) in nodes.children)
                {
                    foreach (var child in children)
                    {
                        if (identities.TryGetIdentity(child, out var childObj) && childObj)
                        {
                            _visibilityFactory.TriggerLateObserverRemoved(player, childObj);
                            childObj.TriggerOnObserverRemoved(player);
                        }
                    }
                }
            }
            
            HashSetPool<NetworkIdentity>.Destroy(roots);
        }

        private static void AddDespawnActionForAllChildren(HashSet<NetworkID> children, List<HierarchyAction> actions)
        {
            foreach (var child in children)
            {
                actions.Add(new HierarchyAction
                {
                    type = HierarchyActionType.Despawn,
                    despawnAction = new DespawnAction
                    {
                        identityId = child,
                        despawnType = DespawnType.GameObject
                    }
                });
            }
        }

        public bool TryGetIdentity(NetworkID id, out NetworkIdentity identity)
        {
            return identities.TryGetIdentity(id, out identity);
        }

        public void SetVisibilityManager(VisibilityFactory factory, VisibilityManager vmanager)
        {
            _visibilityFactory = factory;
            _visibilityManager = vmanager;
        }
    }
}
