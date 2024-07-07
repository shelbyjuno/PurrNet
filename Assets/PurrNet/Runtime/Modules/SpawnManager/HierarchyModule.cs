using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using UnityEngine;

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
    
    public class HierarchyModule : INetworkModule, IFixedUpdate, IUpdate
    {
        private readonly NetworkManager _manager;
        private readonly NetworkPrefabs _prefabs;
        private readonly PlayersManager _players;
        private readonly IdentitiesCollection _identities;
        private readonly HierarchyHistory _history;

        private bool _asServer;
        
        public HierarchyModule(NetworkManager manager, PlayersManager players, NetworkPrefabs prefabs)
        {
            _manager = manager;
            _players = players;
            _prefabs = prefabs;
            
            _identities = new IdentitiesCollection();
            _history = new HierarchyHistory();
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            _players.Subscribe<HierarchyActionBatch>(OnHierarchyActionBatch);

            if (asServer)
            {
                _players.onPlayerJoined += OnPlayerJoined;
            }
        }

        public void Disable(bool asServer)
        {
            _players.Unsubscribe<HierarchyActionBatch>(OnHierarchyActionBatch);
        }
        
        private void OnPlayerJoined(PlayerID player, bool asserver)
        {
            var fullHistory = _history.GetFullHistory();
            if (fullHistory.actions.Count > 0)
                _players.Send(player, fullHistory);
        }
        
        private readonly HashSet<int> _instancesAboutToBeRemoved = new ();
        
        private void OnHierarchyActionBatch(PlayerID player, HierarchyActionBatch data, bool asserver)
        {
            if (_manager.isHost && !asserver) return;
            
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
            if (asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            if (!_identities.TryGetIdentity(action.identityId, out var identity))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }

            NetworkIdentity parent = null;
            
            if (action.parentId != -1 && !_identities.TryGetIdentity(action.parentId, out parent))
            {
                PurrLogger.LogError($"Failed to find identity with id {action.identityId}");
                return;
            }

            if (identity is not NetworkTransform trs)
            {
                PurrLogger.LogError($"Identity with id {action.identityId} is not a NetworkTransform");
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
            if (asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            if (_identities.TryGetIdentity(action.identityId, out _))
            {
                PurrLogger.LogError($"Identity with id {action.identityId} already exists");
                return;
            }

            if (!_prefabs.TryGetPrefab(action.prefabId, out var prefab))
            {
                PurrLogger.LogError($"Failed to find prefab with id {action.prefabId}");
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

            var go = Object.Instantiate(prefab.gameObject, parent);
            go.transform.SetLocalPositionAndRotation(trsInfo.localPos, trsInfo.localRot);
            go.transform.localScale = trsInfo.localScale;
            
            MakeSureAwakeIsCalled(go);
            
            go.GetComponentsInChildren(true, _children);
            
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                child.SetIdentity(action.prefabId, action.identityId + i);
                _identities.RegisterIdentity(child);
                
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

            if (!trsInfo.activeInHierarchy)
                go.SetActive(false);
        }

        static readonly List<NetworkIdentity> _children = new ();
        
        private static GameObject GetChildPrefab(GameObject root, int child)
        {
            root.GetComponentsInChildren(true, _children);
            
            if (child >= _children.Count)
            {
                PurrLogger.LogError($"Failed to find child with index {child}");
                return null;
            }
            
            return _children[child].gameObject;
        }
        
        private static void RemoveChildren(Transform childTrs, int childIdx)
        {
            for (int i = childIdx + 1; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child.transform.IsChildOf(childTrs))
                    _children.RemoveAt(i--);
            }
        }

        [UsedImplicitly]
        private void HandleDespawn(PlayerID player, DespawnAction action, bool asServer)
        {
            if (asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            if (!_identities.TryGetIdentity(action.identityId, out var identity))
                return;

            if (action.despawnType == DespawnType.GameObject)
            {
                var safeParent = identity.transform.parent;
                identity.gameObject.GetComponentsInChildren(true, _children);

                for (int i = 0; i < _children.Count; i++)
                {
                    var child = _children[i];

                    if (!_instancesAboutToBeRemoved.Contains(child.id))
                    {
                        var trs = child.transform;
                        RemoveChildren(trs, i);
                        trs.SetParent(safeParent);
                        continue;
                    }
                    
                    _identities.UnregisterIdentity(child);
                    child.IgnoreNextDestroyCallback();
                }
                
                Object.Destroy(identity.gameObject);
            }
            else
            {
                identity.IgnoreNextDestroyCallback();
                _identities.UnregisterIdentity(identity);
                Object.Destroy(identity);
            }
        }

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

            instance.GetComponentsInChildren(true, _children);

            if (_children.Count == 0)
            {
                PurrLogger.LogError($"Failed to find networked components for '{instance.name}'");
                return;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];

                if (child.isSpawned)
                {
                    PurrLogger.LogError($"Identity with id {child.id} is already spawned", child);
                    return;
                }
                
                child.SetIdentity(prefabId, _identities.GetNextId());
                _identities.RegisterIdentity(child);

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
                identityId = _children[0].id,
                transformInfo = new TransformInfo(instance.transform)
            };

            if (_asServer)
            {
                _history.AddSpawnAction(action);
            }
            else
            {
                Debug.Log("TODO: Implement client spawn logic.");
            }
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
        private static void MakeSureAwakeIsCalled(GameObject root)
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
            if (!asServer)
            {
                Debug.Log("TODO: Implement client parent change logic.");
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

        private void OnIdentityRemoved(ComponentGameObjectPair pair, bool asServer)
        {
            if (!asServer)
            {
                _identities.UnregisterIdentity(pair.identity);
                PurrLogger.LogError("TODO: Implement client despawn logic.");
                return;
            }

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
        
        private void OnToggledComponent(NetworkIdentity identity, bool active, bool asServer)
        {
            if (!asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
            _history.AddSetEnabledAction(new SetEnabledAction
            {
                identityId = identity.id,
                enabled = active
            });
        }
        
        private void OnToggledGameObject(NetworkIdentity identity, bool active, bool asServer)
        {
            if (!asServer)
            {
                Debug.Log("TODO: Implement client actions with permissions");
                return;
            }
            
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

                    OnToggledComponent(identity, identity.enabled, _asServer);
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
                    
                    OnToggledGameObject(active.identity, active.isActive, _asServer);
                }
                _activatedLastFrame.Clear();
            }
            
            if (_removedLastFrame.Count > 0)
            {
                for (int i = 0; i < _removedLastFrame.Count; i++)
                    OnIdentityRemoved(_removedLastFrame[i], _asServer);
                _removedLastFrame.Clear();
            }
            
            if (!_history.hasUnflushedActions) 
                return;

            if (_asServer)
            {
                var delta = _history.GetDelta();
                _players.SendToAll(delta);
                _history.Flush();
            }
            else
            {
                Debug.Log("TODO: Implement client flush logic.");
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.X) && _asServer)
            {
                var history = _history.GetFullHistory();
                string output = $"Actions count: {history.actions.Count}\n";
                
                for (int i = 0; i < history.actions.Count; i++)
                {
                    var action = history.actions[i];
                    output += $"Action {i}: {action.type}\n";
                }
                
                Debug.Log(output);
            }
        }
    }
}
