using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        private void GetHierarchyActionsToSpawnThis(List<HierarchySpawnAction> actions)
        {
            var prefabLink = GetComponentInParent<PrefabLink>();

            if (!prefabLink)
            {
                PurrLogger.LogError("Scene object is not a prefab, can't instantiate out of thin air");
                return;
            }
            
            var first = GetComponent<NetworkIdentity>();

            if (!first.isSpawned)
            {
                PurrLogger.LogError("Object is not spawned");
                return;
            }
            
            using var tree = HierarchyNode.GetHierarchyTree(first);
            
            if (!networkManager.prefabProvider.TryGetPrefab(tree.prefabId, tree.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {tree.prefabId} and offset {tree.prefabOffset}");
                return;
            }
            
            using var instantiated = HierarchyNode.GetHierarchyTree(prefab, tree.prefabId, tree.prefabOffset);
            
            actions.Add(new HierarchySpawnAction(
                new InstantiateAction(first.id!.Value, tree.prefabId, tree.prefabOffset)
            ));
            
            GetActions(instantiated, tree, actions);
            
            actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
        }

        private HierarchyNode? VInstantiate(HierarchyNode target)
        {
            if (target.prefabId == -1)
                return null;
            
            if (!networkManager.prefabProvider.TryGetPrefab(target.prefabId, target.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {target.prefabId} and offset {target.prefabOffset}");
                return null;
            }

            return HierarchyNode.GetHierarchyTree(prefab, target.prefabId, target.prefabOffset);
        }

        private void GetActions(HierarchyNode prefab, HierarchyNode expected, IList<HierarchySpawnAction> actions)
        {
            bool replaced = false;
            
            if (prefab.prefabId != expected.prefabId || 
                prefab.siblingIndex != expected.siblingIndex)
            {
                var instantiated = VInstantiate(expected);

                if (!instantiated.HasValue)
                    return;

                actions.Add(new HierarchySpawnAction(new ReplaceAction(
                    prefab.prefabOffset, expected.components[0].networkId, expected.prefabId,
                    expected.prefabOffset))
                );

                prefab = instantiated.Value;
                replaced = true;
            }
            
            if (prefab.isActive != expected.isActive)
                actions.Add(new HierarchySpawnAction(new ToggleGameObjectActiveAction(prefab.prefabOffset)));
            
            HandleComponents(prefab, expected, actions);
            HandleChildren(prefab, expected, actions);

            if (replaced)
                actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
        }

        private static void HandleComponents(HierarchyNode prefab, HierarchyNode expected, IList<HierarchySpawnAction> actions)
        {
            int currentComponentCount = prefab.components.Count;
            
            for (var i = 0; i < currentComponentCount; i++)
            {
                var currentComponent = prefab.components[i];
                NodeComponent? expectedComponent = expected.components.Count > i ? expected.components[i] : null;

                if (!expectedComponent.HasValue)
                {
                    actions.Add(new HierarchySpawnAction(new DestroyComponentAction(currentComponent.offset)));
                    continue;
                }

                if (currentComponent.offset != expectedComponent.Value.offset)
                {
                    prefab.components.RemoveAt(i--);
                    currentComponentCount = prefab.components.Count;
                    actions.Add(new HierarchySpawnAction(new DestroyComponentAction(currentComponent.offset)));
                    continue;
                }
                
                if (currentComponent.enabled != expectedComponent.Value.enabled)
                    actions.Add(new HierarchySpawnAction(new ToggleComponentEnabledAction(currentComponent.offset)));
            }
        }

        private void HandleChildren(HierarchyNode prefab, HierarchyNode expected, IList<HierarchySpawnAction> actions)
        {
            int expectedChildCount = expected.children.Count;
            int currentChildCount = prefab.children.Count;
            
            for (var i = 0; i < expectedChildCount; i++)
            {
                HierarchyNode? child = prefab.children.Count > i ? prefab.children[i] : null;
                var expectedChild = expected.children[i];
                
                bool wasSpawned = false;

                if (!child.HasValue)
                {
                    var instantiated = VInstantiate(expectedChild);
                    
                    if (!instantiated.HasValue)
                        continue;

                    actions.Add(new HierarchySpawnAction(new InstantiateWithParentAction(
                        prefab.prefabOffset, expectedChild.components[0].networkId, expectedChild.prefabId, expectedChild.prefabOffset))
                    );
                    
                    child = instantiated;
                    prefab.children.Add(instantiated.Value);
                    wasSpawned = true;
                }
                
                GetActions(child.Value, expectedChild, actions);
                
                if (wasSpawned)
                    actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
            }

            for (var i = expectedChildCount; i < currentChildCount; i++)
            {
                var child = prefab.children[i];
                actions.Add(new HierarchySpawnAction(new DestroyAction(child.prefabOffset)));
            }
        }

        private bool HandleInstantiateAction(InstantiateAction action, out NGameObject instantiated)
        {
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                instantiated = default;
                return false;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = new NGameObject(Instantiate(prefab));
            SpawnInstantiatedGo(action.prefabId, action.networkId, instantiated);
            return true;
        }
        
        private static void HandleDestroyAction(DestroyAction action, NGameObject instantiated)
        {
            var child = instantiated.identities[action.childId];
            Destroy(child.gameObject);
        }
        
        private bool HandleInstantiateWithParentAction(InstantiateWithParentAction action, NGameObject current, out NGameObject instantiated)
        {
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                instantiated = default;
                return false;
            }
            
            var parent = current.identities[action.parentChildId];

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = new NGameObject(Instantiate(prefab, parent.transform));
            SpawnInstantiatedGo(action.prefabId, action.networkId, instantiated);
            return true;
        }
        
        struct SpawnAction
        {
            public List<NetworkIdentity> identities;
            public NetworkID startingNetworkId;
            public int prefabId;
        }
        
        static readonly List<SpawnAction> _finalSpawnActions = new();

        private void SpawnInstantiatedGo(int pid, NetworkID nid, NGameObject instantiated)
        {
            Debug.Log($"Prepping the spawning of {instantiated.identities.Count} identities");
            
            for (ushort i = 0; i < instantiated.identities.Count; i++)
                instantiated.identities[i].siblingIndex = i;
            
            _finalSpawnActions.Add(new SpawnAction
            {
                identities = instantiated.identities,
                startingNetworkId = nid,
                prefabId = pid
            });
            
            /*for (ushort i = 0; i < instantiated.identities.Count; i++)
                scene.SpawnIdentity(instantiated.identities[i], pid, nid, i, asServer);*/
        }

        private void HandleReplaceAction(ReplaceAction action, NGameObject instantiated)
        {
            var oldChild = instantiated.identities[action.childId];
            var parent = oldChild.transform.parent;
            
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                return;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            using var copy = new NGameObject(Instantiate(prefab, parent));
            SpawnInstantiatedGo(action.prefabId, action.networkId, instantiated);
        }
        
        private static void HandleToggleGameObjectActiveAction(ToggleGameObjectActiveAction action, NGameObject instantiated)
        {
            var child = instantiated.identities[action.childId];
            child.gameObject.SetActive(!child.gameObject.activeSelf);
        }
        
        private static void HandleToggleComponentEnabledAction(ToggleComponentEnabledAction action, NGameObject instantiated)
        {
            var child = instantiated.identities[action.childId];
            child.enabled = !child.enabled;
        }
        
        private static void HandleDestroyComponentAction(DestroyComponentAction action, NGameObject instantiated)
        {
            Destroy(instantiated.identities[action.childId]);
        }
        
        static readonly Stack<NGameObject> _instantiatedStack = new();
        static readonly List<IDisposable> _toDispose = new();

        private void ReplayActions(IList<HierarchySpawnAction> actions, bool asServer)
        {
            for (int i = 0; i < actions.Count; ++i)
            {
                var action = actions[i];
                
                switch (action.action)
                {
                    case HierarchySpawnAction.HierarchyActionType.Instantiate:
                    {
                        if (HandleInstantiateAction(action.instantiateAction, out var instantiated))
                            _instantiatedStack.Push(instantiated);
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Pop:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to pop from, bad stack state");
                            break;
                        }
                        
                        var go = _instantiatedStack.Pop();
                        _toDispose.Add(go);
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Destroy:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to destroy from, bad stack state");
                            break;
                        }

                        HandleDestroyAction(action.destroyAction, _instantiatedStack.Peek());
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.InstantiateWithParent:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to instantiate with, bad stack state");
                            break;
                        }
                        
                        var peek = _instantiatedStack.Peek();
                        if (HandleInstantiateWithParentAction(action.instantiateWithParentAction, peek,
                                out var instantiated))
                        {
                            _instantiatedStack.Push(instantiated);
                        }

                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Replace:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to replace from, bad stack state");
                            break;
                        }

                        HandleReplaceAction(action.replaceAction, _instantiatedStack.Peek());
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.DestroyComponent:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to toggle active from, bad stack state");
                            break;
                        }

                        HandleDestroyComponentAction(action.destroyComponentAction, _instantiatedStack.Peek());
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.ToggleActive:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to toggle active from, bad stack state");
                            break;
                        }

                        HandleToggleGameObjectActiveAction(action.toggleGameObjectActiveAction, _instantiatedStack.Peek());
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.ToggleEnabled:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to toggle active from, bad stack state");
                            break;
                        }

                        HandleToggleComponentEnabledAction(action.toggleComponentEnabledAction, _instantiatedStack.Peek());
                        break;
                    }
                    default: PurrLogger.LogError($"Unknown action type {action.action}"); break;
                }
            }

            if (_instantiatedStack.Count > 0)
            {
                foreach (var go in _instantiatedStack)
                    go.Dispose();
                
                _instantiatedStack.Clear();
                
                PurrLogger.LogError("Stack is not empty, something went wrong");
            }

            var hierarchy = networkManager.GetModule<HierarchyModule>(asServer);
            if (hierarchy.TryGetHierarchy(sceneId, out var scene))
            {
                var finalActionsCount = _finalSpawnActions.Count;
                for (int i = 0; i < finalActionsCount; i++)
                {
                    var action = _finalSpawnActions[i];
                    for (ushort j = 0; j < action.identities.Count; j++)
                    {
                        var identity = action.identities[j];
                        if (identity)
                            scene.SpawnIdentity(identity, action.prefabId, identity.siblingIndex, action.startingNetworkId, j, asServer);
                    }
                }
            }

            _finalSpawnActions.Clear();
            
            foreach (var disposable in _toDispose)
                disposable.Dispose();
            
            _toDispose.Clear();
        }

        [ContextMenu("Spawn")]
        void TestSpawnActions()
        {
            var actions = ListPool<HierarchySpawnAction>.New();
            GetHierarchyActionsToSpawnThis(actions);
            
            Debug.Log("----");
            foreach (var action in actions)
                Debug.Log(action);
            Debug.Log("----");
            
            ReplayActions(actions, false);
            
            ListPool<HierarchySpawnAction>.Destroy(actions);
        }
    }
}
