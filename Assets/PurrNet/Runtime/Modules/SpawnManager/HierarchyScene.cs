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
        public NetworkIdentity identity;
        public GameObject gameObject;
    }
    
    internal struct GameObjectActive
    {
        public NetworkIdentity identity;
        public bool isActive;
    }
    
    internal partial struct SetSceneIds : IAutoNetworkedData
    {
        public SceneID sceneId;
        public int startingId;
    }
    
    internal class HierarchyScene : INetworkModule, IFixedUpdate
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
        private int _sceneFirstNetworkID;
        
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

            if (!asServer)
            {
                _playersManager.Subscribe<HierarchyActionBatch>(OnHierarchyActionBatch);
                _playersManager.Subscribe<SetSceneIds>(OnSetSceneIds);
            }
            else
            {
                _sceneFirstNetworkID = _identities.PeekNextId();
                
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
                    identity.TriggetClientSpawnEvent();
            }
        }

        //private bool _initiatedHostOnce;

        private void OnSetSceneIds(PlayerID player, SetSceneIds data, bool asserver)
        {
            _isReady = true;
            
            /*if (_manager.isHost)
            {
                if (!_initiatedHostOnce)
                {
                    _initiatedHostOnce = true;
                    _manager.GetModule<HierarchyModule>(true).TriggerOnSpawnedEventForClient();
                }

                return;
            }*/
            
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
                SpawnIdentity(new SpawnAction
                {
                    prefabId = -1,
                    identityId = _identities.GetNextId(),
                    childCount = -1,
                    childOffset = 0,
                    transformInfo = default
                }, sceneObjects[i], 0, true);
            }
        }
        
        private void SpawnSceneObjectsClient(IReadOnlyList<NetworkIdentity> sceneObjects, int id)
        {
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                SpawnIdentity(new SpawnAction
                {
                    prefabId = -1,
                    identityId = id,
                    childCount = -1,
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
                    identity.TriggetClientDespawnEvent();
                
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
            {
                /*foreach (var action in fullHistory.actions)
                {
                    PurrLogger.Log($"Action {action}");
                }*/
                _playersManager.Send(player, fullHistory);
            }
        } 
        
        private readonly HashSet<int> _instancesAboutToBeRemoved = new ();
        
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
                OnHierarchyAction(player, data.actions[i], asserver);
            }
        }
        
        private void OnHierarchyAction(PlayerID player, HierarchyAction data, bool asserver)
        {
            switch (data.type)
            {
                case HierarchyActionType.Despawn:
                    HandleDespawn(player, data.despawnAction, asserver);
                    break;
                
                case HierarchyActionType.Spawn:
                    HandleSpawn(player, data.spawnAction, asserver);
                    break;
                
                case HierarchyActionType.ChangeParent:
                    HandleChangeParent(player, data.changeParentAction, asserver);
                    break;
                
                case HierarchyActionType.SetActive:
                    HandleSetActive(player, data.setActiveAction, asserver);
                    break;
                
                case HierarchyActionType.SetEnabled:
                    HandleSetEnabled(player, data.setEnabledAction, asserver);
                    break;
            }
        }

        [UsedImplicitly]
        private void HandleSetEnabled(PlayerID player, SetEnabledAction dataSetEnabledAction, bool asServer)
        {
            if (asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            if (!_identities.TryGetIdentity(dataSetEnabledAction.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {dataSetEnabledAction.identityId}");
                return;
            }

            identity.IgnoreNextEnableCallback();
            identity.enabled = dataSetEnabledAction.enabled;
        }

        [UsedImplicitly]
        private void HandleSetActive(PlayerID player, SetActiveAction dataSetActiveAction, bool asServer)
        {
            if (asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            if (!_identities.TryGetIdentity(dataSetActiveAction.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {dataSetActiveAction.identityId}");
                return;
            }

            identity.IgnoreNextActivationCallback();
            identity.IgnoreNextEnableCallback();
            identity.gameObject.SetActive(dataSetActiveAction.active);
        }

        [UsedImplicitly]
        private void HandleChangeParent(PlayerID player, ChangeParentAction action, bool asServer)
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
            
            if (asServer && !trs.HasParentSyncAuthority(player))
            {
                PurrLogger.LogError($"Parent change from '{player}' failed for '{trs.name}' due to lack of permissions.", trs);
                return;
            }
            
            NetworkIdentity parent = null;
            
            if (action.parentId != -1 && !_identities.TryGetIdentity(action.parentId, out parent))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }

            trs.StartIgnoreParentChanged();
            identity.transform.SetParent(parent ? parent.transform : null);
            trs.StopIgnoreParentChanged();
            trs.ValidateParent();
        }

        [UsedImplicitly]
        private void HandleSpawn(PlayerID player, SpawnAction action, bool asServer)
        {
            if (!_prefabs.TryGetPrefab(action.prefabId, out var prefab))
            {
                PurrLogger.LogError($"Failed to find prefab with id {action.prefabId}");
                return;
            }
            
            if (!_manager.networkRules.HasSpawnAuthority(_manager))
            {
                PurrLogger.LogError($"Spawn failed from '{player}' for prefab '{prefab.name}' due to lack of permissions.");
                return;
            }
            
            if (_identities.TryGetIdentity(action.identityId, out _))
            {
                PurrLogger.LogError($"Identity with id {action.identityId} already exists");
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

            if (trsInfo.parentId != -1 && _identities.TryGetIdentity(trsInfo.parentId, out var parentIdentity))
                parent = parentIdentity.transform;
            
            PrefabLink.IgnoreNextAutoSpawnAttempt();

            var oldActive = prefab.gameObject.activeInHierarchy;

            if (oldActive && parent == null)
            {
                prefab.gameObject.SetActive(false);
            }

            var go = Object.Instantiate(prefab.gameObject, parent);
            go.transform.SetLocalPositionAndRotation(trsInfo.localPos, trsInfo.localRot);
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
                SpawnIdentity(action, child, i, _asServer);
            }

            if (!trsInfo.activeInHierarchy)
                go.SetActive(false);
        }
        
        private void SpawnIdentity(SpawnAction action, NetworkIdentity component, int i, bool asServer)
        {
            component.SetIdentity(_manager, _sceneID, action.prefabId, action.identityId + i, asServer);

            _spawnedThisFrame.Add(component);
            
            _identities.RegisterIdentity(component);
            onIdentityAdded?.Invoke(component);

            component.onRemoved += OnIdentityRemoved;
            component.onEnabledChanged += OnIdentityEnabledChanged;
            component.onActivatedChanged += OnIdentityGoActivatedChanged;

            if (component is NetworkTransform transform)
            {
                if (_asServer)
                    transform.onParentChanged += OnIdentityParentChangedServer;
                else transform.onParentChanged += OnIdentityParentChangedClient;
            }
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
        private void HandleDespawn(PlayerID player, DespawnAction action, bool asServer)
        {
            if (!_identities.TryGetIdentity(action.identityId, out var identity))
                return;

            if (!identity.HasDespawnAuthority(player))
            {
                PurrLogger.LogError($"Despawn failed from '{player}' for '{identity.name}' due to lack of permissions.", identity);
                return;
            }
            
            if (!identity)
                return;

            if (action.despawnType == DespawnType.GameObject)
            {
                var safeParent = identity.transform.parent;
                identity.gameObject.GetComponentsInChildren(true, CACHE);

                for (int i = 0; i < CACHE.Count; i++)
                {
                    var child = CACHE[i];

                    if (!_instancesAboutToBeRemoved.Contains(child.id))
                    {
                        var trs = child.transform;
                        RemoveChildren(trs, i);
                        trs.SetParent(safeParent);
                        continue;
                    }
                    
                    if (_identities.UnregisterIdentity(child))
                        onIdentityRemoved?.Invoke(child);
                    
                    child.IgnoreNextDestroyCallback();
                }
                
                Object.Destroy(identity.gameObject);
            }
            else
            {
                identity.IgnoreNextDestroyCallback();
                if (_identities.UnregisterIdentity(identity))
                    onIdentityRemoved?.Invoke(identity);
                Object.Destroy(identity);
            }
        }

        readonly List<NetworkIdentity> _spawnedThisFrame = new ();
        
        public void Spawn(GameObject instance)
        {
            MakeSureAwakeIsCalled(instance);
            
            if (!_asServer)
            {
                Debug.Log("TODO: Implement client spawn logic.");
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
                
                child.SetIdentity(_manager, _sceneID, prefabId, _identities.GetNextId(), _asServer);
                
                _spawnedThisFrame.Add(child);
                _identities.RegisterIdentity(child);
                
                onIdentityAdded?.Invoke(child);

                child.onRemoved += OnIdentityRemoved;
                child.onEnabledChanged += OnIdentityEnabledChanged;
                child.onActivatedChanged += OnIdentityGoActivatedChanged;

                if (child is NetworkTransform transform)
                {
                    if (_asServer)
                         transform.onParentChanged += OnIdentityParentChangedServer;
                    else transform.onParentChanged += OnIdentityParentChangedClient;
                }
            }
            
            var action = new SpawnAction
            {
                prefabId = prefabId,
                childOffset = 0,
                identityId = CACHE[0].id,
                childCount = CACHE.Count,
                transformInfo = new TransformInfo(instance.transform)
            };

            _history.AddSpawnAction(action);
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

        private void OnIdentityParentChangedClient(NetworkTransform obj) => OnIdentityParentChanged(obj, false);
        
        private void OnIdentityParentChangedServer(NetworkTransform obj) => OnIdentityParentChanged(obj, true);
        
        private void OnIdentityParentChanged(NetworkTransform trs, bool asServer)
        {
            if (!trs.HasParentSyncAuthority(asServer))
            {
                bool isOwner = trs.isOwner;
                string parentName = trs.transform.parent ? trs.transform.parent.name : "null";
                
                PurrLogger.LogError($"Parent change failed for '{trs.name}' to '{parentName}' due to lack of permissions.\n" +
                                    $"You called this as {(asServer ? "server" : "client")} and you are {(isOwner ? "the owner" : "not the owner")}.\n" +
                                    "The parent will be reset to the last known one.", trs);
                
                trs.ResetToLastValidParent();
                return;
            }
            
            var parentTrs = trs.transform.parent;
            int parentId = parentTrs ? parentTrs.GetComponent<NetworkIdentity>().id : -1;
            
            var action = new ChangeParentAction
            {
                identityId = trs.id,
                parentId = parentId
            };
            
            _history.AddChangeParentAction(action);
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
            onIdentityRemoved?.Invoke(identity);
            
            _removedLastFrame.Add(new ComponentGameObjectPair
            {
                identity = identity,
                gameObject = identity.gameObject
            });
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
            if (_identities.UnregisterIdentity(pair.identity))
                onIdentityRemoved?.Invoke(pair.identity);

            if (!pair.gameObject)
                 OnDestroyedObject(pair.identity.id);
            else OnRemovedComponent(pair.identity.id);
        }

        private void OnDestroyedObject(int entityId)
        {
            _history.AddDespawnAction(new DespawnAction
            {
                identityId = entityId,
                despawnType = DespawnType.GameObject
            });
        }

        private void OnRemovedComponent(int entityId)
        {
            _history.AddDespawnAction(new DespawnAction
            {
                identityId = entityId,
                despawnType = DespawnType.ComponentOnly
            });
        }
        
        private void OnToggledComponent(NetworkIdentity identity, bool active)
        {
            _history.AddSetEnabledAction(new SetEnabledAction
            {
                identityId = identity.id,
                enabled = active
            });
        }
        
        private void OnToggledGameObject(NetworkIdentity identity, bool active)
        {
            _history.AddSetActiveAction(new SetActiveAction
            {
                identityId = identity.id,
                active = active
            });
        }

        public void FixedUpdate()
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

                if (_scenePlayers.TryGetPlayersInScene(_sceneID, out var players))
                {
                    if (_asServer)
                         _playersManager.Send(players, delta);
                    else _playersManager.SendToServer(delta);
                }

                _history.Flush();
            }
            
            var spawnedThisFrameCount = _spawnedThisFrame.Count;

            if (spawnedThisFrameCount > 0)
            {
                for (int i = 0; i < spawnedThisFrameCount; i++)
                    _spawnedThisFrame[i].TriggetSpawnEvent(_asServer);
                _spawnedThisFrame.Clear();
            }

        }

        public bool TryGetIdentity(int id, out NetworkIdentity identity)
        {
            return _identities.TryGetIdentity(id, out identity);
        }
    }
}
