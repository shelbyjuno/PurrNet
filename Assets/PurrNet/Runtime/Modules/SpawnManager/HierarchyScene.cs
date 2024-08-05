using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
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
    
    internal partial struct SetSceneIds : IAutoNetworkedData
    {
        public SceneID sceneId;
        public NetworkID startingId;
    }
    
    internal class HierarchyScene : INetworkModule, IPreFixedUpdate
    {
        private readonly NetworkManager _manager;
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _playersManager;
        private readonly ScenesModule _scenes;
        private readonly IdentitiesCollection _identities;
        private readonly HierarchyHistory _history;
        private readonly ScenePlayersModule _scenePlayers;

        public event Action<NetworkIdentity> onIdentityRemoved;
        public event Action<NetworkIdentity> onIdentityAdded;
        
        private readonly SceneID _sceneID;
        private bool _asServer;
        private bool _isReady;

        // the id of the first network identity in the scene
        private NetworkID _sceneFirstNetworkID;
        
        public string GetActionsAsString()
        {
            string value = "";
            var history = _history.GetFullHistory();
            for (int i = 0; i < history.actions.Count; i++)
                value += history.actions[i].ToString() + '\n';
            return value;
        }
        
        public HierarchyScene(SceneID sceneId, ScenesModule scenes, NetworkManager manager, PlayersManager playersManager, ScenePlayersModule scenePlayers, NetworkPrefabs prefabs)
        {
            _manager = manager;
            _playersManager = playersManager;
            _prefabs = prefabs;
            _scenePlayers = scenePlayers;
            _scenes = scenes;
            _sceneID = sceneId;
            
            _identities = new IdentitiesCollection();
            _history = new HierarchyHistory(sceneId);
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            _playersManager.Subscribe<HierarchyActionBatch>(OnHierarchyActionBatch);

            if (!asServer)
            {
                _playersManager.Subscribe<SetSceneIds>(OnSetSceneIds);
            }
            else
            {
                var networkId = new NetworkID(_identities.PeekNextId());

                _sceneFirstNetworkID = networkId;
                
                if (_scenes.TryGetSceneState(_sceneID, out var sceneState))
                    SpawnSceneObjectsServer(SceneObjectsModule.GetSceneIdentities(sceneState.scene));
                if (_scenePlayers.TryGetPlayersInScene(_sceneID, out var players))
                {
                    foreach (var player in players)
                        OnPlayerJoinedScene(player, _sceneID, true);
                }
                
                _scenePlayers.onPrePlayerloadedScene += OnPlayerJoinedScene;
            }
        }

        public bool IsSceneReady() => _isReady;

        internal void TriggerSpawnEventOnClient()
        {
            foreach (var identity in _identities.collection)
            {
                if (identity.isSpawned)
                    identity.TriggetSpawnEvent(false);
            }
        }

        private void OnSetSceneIds(PlayerID player, SetSceneIds data, bool asserver)
        {
            _isReady = true;
            
            if (_sceneID != data.sceneId)
                return;
            
            _sceneFirstNetworkID = data.startingId;
            
            if (_scenes.TryGetSceneState(_sceneID, out var sceneState))
                SpawnSceneObjectsClient(SceneObjectsModule.GetSceneIdentities(sceneState.scene), _sceneFirstNetworkID);
        }

        private void SpawnSceneObjectsServer(IReadOnlyList<NetworkIdentity> sceneObjects)
        {
            _isReady = true;

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                if (sceneObjects[i].isSpawned)
                    continue;
                
                var nextId = _identities.GetNextId();
                var networkId = new NetworkID(nextId, _asServer ? default : _playersManager.localPlayerId!.Value);
                
                SpawnIdentity(new SpawnAction
                {
                    prefabId = -1,
                    identityId = networkId,
                    childCount = 0,
                    childOffset = 0,
                    transformInfo = default
                }, sceneObjects[i], 0, true);
            }
        }
        
        private void SpawnSceneObjectsClient(IReadOnlyList<NetworkIdentity> sceneObjects, NetworkID id)
        {
            for (ushort i = 0; i < sceneObjects.Count; i++)
            {
                if (sceneObjects[i].isSpawned && !sceneObjects[i].isSceneObject)
                    continue;
                
                SpawnIdentity(new SpawnAction
                {
                    prefabId = -1,
                    identityId = id,
                    childCount = 0,
                    childOffset = 0,
                    transformInfo = default
                }, sceneObjects[i], i, false);
            }
        }

        public void Disable(bool asServer)
        {
            if (asServer)
                 _scenePlayers.onPlayerLoadedScene -= OnPlayerJoinedScene;
            else
            {
                foreach (var identity in _identities.collection)
                    identity.TriggetDespawnEvent(false);
                
                _playersManager.Unsubscribe<HierarchyActionBatch>(OnHierarchyActionBatch);
            }

            _identities.DestroyAllNonSceneObjects();
        }
        
        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene != _sceneID)
                return;
            
            if (!asserver) return;
            
            _playersManager.Send(player, new SetSceneIds
            {
                sceneId = _sceneID,
                startingId = _sceneFirstNetworkID
            });
            
            var fullHistory = _history.GetFullHistory();
            if (fullHistory.actions.Count > 0)
                _playersManager.Send(player, fullHistory);
        }
        
        private readonly HashSet<NetworkID> _instancesAboutToBeRemoved = new ();
        
        private void OnHierarchyActionBatch(PlayerID player, HierarchyActionBatch data, bool asserver)
        {
            if (_manager.isHost && !asserver) return;
            
            if (_sceneID != data.sceneId)
                return;

            _instancesAboutToBeRemoved.Clear();
            
            for (int i = 0; i < data.actions.Count; i++)
            {
                var action = data.actions[i];
                if (action is { type: HierarchyActionType.Despawn, despawnAction: { despawnType: DespawnType.GameObject } })
                    _instancesAboutToBeRemoved.Add(data.actions[i].despawnAction.identityId);
            }
            
            for (int i = 0; i < data.actions.Count; i++)
            {
                var action = data.actions[i];
                OnHierarchyAction(player, ref action, data.isDelta);
                data.actions[i] = action;
            }
        }
        
        private void OnHierarchyAction(PlayerID player, ref HierarchyAction data, bool isDelta)
        {
            if (!_asServer && isDelta)
            {
                var localPlayer = _playersManager.localPlayerId;

                if (!localPlayer.HasValue)
                {
                    PurrLogger.LogError("Received hierarchy actions before having logged in. Ignoring them.");
                    return;
                }
                
                if (data.actor == localPlayer)
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
            }
        }

        private void HandleSetEnabled(PlayerID player, SetEnabledAction dataSetEnabledAction)
        {
            if (!_identities.TryGetIdentity(dataSetEnabledAction.identityId, out var identity))
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
            if (!_identities.TryGetIdentity(dataSetActiveAction.identityId, out var identity))
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

            if (_asServer) _history.AddSetActiveAction(dataSetActiveAction, player);
        }

        [UsedImplicitly]
        private void HandleChangeParent(PlayerID player, ChangeParentAction action)
        {
            if (!_identities.TryGetIdentity(action.identityId, out var identity))
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
            
            if (action.parentId.HasValue && !_identities.TryGetIdentity(action.parentId.Value, out parent))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }

            trs.StartIgnoreParentChanged();
            identity.transform.SetParent(parent ? parent.transform : null);
            trs.StopIgnoreParentChanged();
            trs.ValidateParent();

            if (_asServer) _history.AddChangeParentAction(action, player);
        }

        private void HandleSpawn(PlayerID player, ref SpawnAction action)
        {
            if (_asServer)
            {
                var nid = new NetworkID(action.identityId.id, player);
                action.identityId = nid;
            }

            if (!_prefabs.TryGetPrefab(action.prefabId, out var prefab))
            {
                PurrLogger.LogError($"Failed to find prefab with id {action.prefabId}");
                return;
            }
            
            if (!prefab.TryGetComponent<PrefabLink>(out var link))
            {
                PurrLogger.LogError($"Failed to find PrefabLink component on '{prefab.name}'");
                return;
            }
            
            if (!link.HasSpawnAuthority(_manager, !_asServer))
            {
                PurrLogger.LogError($"Spawn failed from '{player}' for prefab '{prefab.name}' due to lack of permissions.");
                return;
            }
            
            if (_identities.TryGetIdentity(action.identityId, out _))
                return;

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

            if (trsInfo.parentId.HasValue && _identities.TryGetIdentity(trsInfo.parentId, out var parentIdentity))
                parent = parentIdentity.transform;
            
            PrefabLink.IgnoreNextAutoSpawnAttempt();

            var oldActive = prefab.gameObject.activeInHierarchy;

            if (oldActive && parent == null)
            {
                prefab.gameObject.SetActive(false);
            }

            GameObject go;

            if (parent == null)
            {
                go = Object.Instantiate(prefab.gameObject, trsInfo.localPos, trsInfo.localRot, parent);
            }
            else
            {
                go = Object.Instantiate(prefab.gameObject, parent);
                go.transform.SetLocalPositionAndRotation(trsInfo.localPos, trsInfo.localRot);
            }

            go.transform.localScale = trsInfo.localScale;

            if (parent == null && _scenes.TryGetSceneState(_sceneID, out var state))
            {
                SceneManager.MoveGameObjectToScene(go, state.scene);
                if (oldActive) prefab.gameObject.SetActive(true);
            }
            
            MakeSureAwakeIsCalled(go);
            
            go.GetComponentsInChildren(true, CACHE);
            
            for (int i = 0; i < CACHE.Count; i++)
            {
                var child = CACHE[i];
                SpawnIdentity(action, child, (ushort)i, _asServer);
            }

            if (!trsInfo.activeInHierarchy)
                go.SetActive(false);

            if (_asServer) _history.AddSpawnAction(action, player);
        }
        
        private void SpawnIdentity(SpawnAction action, NetworkIdentity component, ushort offset, bool asServer)
        {
            component.SetIdentity(_manager, _sceneID, action.prefabId, new NetworkID(action.identityId, offset), asServer);

            _spawnedThisFrame.Add(component);
            
            _identities.RegisterIdentity(component);
            onIdentityAdded?.Invoke(component);

            component.onRemoved += OnIdentityRemoved;
            component.onEnabledChanged += OnIdentityEnabledChanged;
            component.onActivatedChanged += OnIdentityGoActivatedChanged;

            if (component is NetworkTransform transform)
               transform.onParentChanged += OnIdentityParentChanged;
        }

        internal static readonly List<NetworkIdentity> CACHE = new ();
        
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
        
        private static void RemoveChildren(Transform childTrs, int childIdx)
        {
            for (int i = childIdx + 1; i < CACHE.Count; i++)
            {
                var child = CACHE[i];
                if (child.transform.IsChildOf(childTrs))
                    CACHE.RemoveAt(i--);
            }
        }

        [UsedImplicitly]
        private void HandleDespawn(PlayerID player, DespawnAction action)
        {
            if (!_identities.TryGetIdentity(action.identityId, out var identity))
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
                var safeParent = identity.transform.parent;
                identity.gameObject.GetComponentsInChildren(true, CACHE);

                for (int i = 0; i < CACHE.Count; i++)
                {
                    var child = CACHE[i];

                    if (child.id.HasValue && !_instancesAboutToBeRemoved.Contains(child.id.Value))
                    {
                        var trs = child.transform;
                        RemoveChildren(trs, i);
                        trs.SetParent(safeParent);
                        continue;
                    }

                    if (_identities.UnregisterIdentity(child))
                    {
                        /*if (_asServer && child.id.HasValue) 
                        {
                            _history.AddDespawnAction(new DespawnAction
                            {
                                identityId = child.id.Value,
                                despawnType = DespawnType.GameObject
                            });
                        }*/
                        
                        onIdentityRemoved?.Invoke(child);
                        child.TriggetDespawnEvent(_asServer);
                    }
                    
                    child.IgnoreNextDestroyCallback();
                }
                
                Object.Destroy(identity.gameObject);
            }
            else
            {
                identity.IgnoreNextDestroyCallback();
                if (_identities.UnregisterIdentity(identity))
                {
                    onIdentityRemoved?.Invoke(identity);
                    identity.TriggetDespawnEvent(_asServer);
                }
                Object.Destroy(identity);
            }
            
            if (_asServer) _history.AddDespawnAction(action, player);
        }

        readonly List<NetworkIdentity> _spawnedThisFrame = new ();
        
        public void Spawn(GameObject instance)
        {
            MakeSureAwakeIsCalled(instance);

            if (!_manager.networkRules.HasSpawnAuthority(_manager, _asServer))
            {
                PurrLogger.LogError($"Failed to spawn '{instance.name}' due to lack of permissions.");
                return;
            }
            
            if (!instance.TryGetComponent<PrefabLink>(out var link))
            {
                PurrLogger.LogError($"Failed to find PrefabLink component on {instance.name}");
                return;
            }

            if (!_prefabs.TryGetPrefabFromGuid(link.prefabGuid, out var prefabId))
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

            for (int i = 0; i < CACHE.Count; i++)
            {
                var child = CACHE[i];

                if (child.isSpawned)
                {
                    PurrLogger.LogError($"Identity with id {child.id} is already spawned", child);
                    return;
                }

                var nid = new NetworkID(_identities.GetNextId(), actor);
                child.SetIdentity(_manager, _sceneID, prefabId, nid, _asServer);
                
                _spawnedThisFrame.Add(child);
                _identities.RegisterIdentity(child);
                
                onIdentityAdded?.Invoke(child);

                child.onRemoved += OnIdentityRemoved;
                child.onEnabledChanged += OnIdentityEnabledChanged;
                child.onActivatedChanged += OnIdentityGoActivatedChanged;

                if (child is NetworkTransform transform)
                    transform.onParentChanged += OnIdentityParentChanged;
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
        static readonly List<BehaviourState> _cache = new ();
        static readonly HashSet<GameObject> _gosToDeactivate = new ();

        /// <summary>
        /// Awake is not called on disabled game objects, so we need to ensure it's called for all components.
        /// </summary>
        internal static void MakeSureAwakeIsCalled(GameObject root)
        {
            _cache.Clear();
            
            // for components in disabled game objects, disabled them, activate game object, and reset their enabled state
            root.GetComponentsInChildren(true, _components);
            
            for (int i = 0; i < _components.Count; i++)
            {
                var child = _components[i];
                if (!child.gameObject.activeSelf)
                {
                    _cache.Add(new BehaviourState
                    {
                        component = child,
                        enabled = child.enabled
                    });
                    
                    child.enabled = false;
                    
                    _gosToDeactivate.Add(child.gameObject);
                }
            }

            foreach (var go in _gosToDeactivate)
            {
                go.SetActive(true);
                go.SetActive(false);
            }
            
            for (int i = 0; i < _cache.Count; i++)
            {
                var state = _cache[i];
                state.component.enabled = state.enabled;
            }

            _cache.Clear();
            _gosToDeactivate.Clear();
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
                    parentId = parent.id;
                }
                else
                {
                    PurrLogger.LogError($"Failed to find NetworkIdentity component on parent '{parentTrs.name}' aborting parent change.", trs);
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
            identity.TriggetDespawnEvent(_asServer);
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
                _identities.UnregisterIdentity(pair.networkID.Value);
            
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
                return;
            }
            
            if (!TryGetActor(out var actor))
            {
                PurrLogger.LogError($"Client is trying to toggle game object with id {identity.id} before having been assigned a local player id.");
                identity.IgnoreNextActivationCallback();
                identity.gameObject.SetActive(!active);
                return;
            }
            
            _history.AddSetActiveAction(new SetActiveAction
            {
                identityId = identity.id.Value,
                active = active
            }, actor);
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
            }
            
            if (_history.hasUnflushedActions)
            {
                var delta = _history.GetDelta();

                if (_asServer)
                {
                    if (_scenePlayers.TryGetPlayersInScene(_sceneID, out var players))
                        _playersManager.Send(players, delta);
                    
                    _history.Flush();
                }
                else
                {
                    _playersManager.SendToServer(delta);
                    _history.Clear();
                }
            }
            
            var spawnedThisFrameCount = _spawnedThisFrame.Count;

            if (spawnedThisFrameCount > 0)
            {
                for (int i = 0; i < spawnedThisFrameCount; i++)
                    _spawnedThisFrame[i].TriggetSpawnEvent(_asServer);
                _spawnedThisFrame.Clear();
            }
        }

        public bool TryGetIdentity(NetworkID id, out NetworkIdentity identity)
        {
            return _identities.TryGetIdentity(id, out identity);
        }
    }
}
