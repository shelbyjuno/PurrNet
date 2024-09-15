using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        static HierarchyNode GetHierarchyTree(GameObject target, int prefabId = 0, int prefabOffset = 0)
        {
            var first = target.GetComponent<NetworkIdentity>();
            return InternalGetHierarchyTree(first, prefabId, ref prefabOffset);
        }
        
        static HierarchyNode GetHierarchyTree(NetworkIdentity target, int prefabId = 0, int prefabOffset = 0)
        {
            return InternalGetHierarchyTree(target, prefabId, ref prefabOffset);
        }
        
        static HierarchyNode InternalGetHierarchyTree(NetworkIdentity target, int prefabId, ref int prefabOffset)
        {
            var node = new HierarchyNode
            {
                prefabId = target.isSpawned ? target.prefabId : prefabId,
                prefabOffset = target.isSpawned ? target.prefabOffset : (ushort)prefabOffset,
                children = ListPool<HierarchyNode>.New(),
                components = ListPool<NodeComponent>.New()
            };

            var trs = target.transform;
            
            var siblings = ListPool<NetworkIdentity>.New();
            
            target.GetComponents(siblings);

            for (var i = 0; i < siblings.Count; i++)
                node.components.Add(new NodeComponent(siblings[i]));
            prefabOffset += siblings.Count;
            
            ListPool<NetworkIdentity>.Destroy(siblings);

            for (var i = 0; i < trs.childCount; i++)
            {
                var child = trs.GetChild(i).GetComponentInChildren<NetworkIdentity>();
                if (!child) continue;

                var childNode = InternalGetHierarchyTree(child, prefabId, ref prefabOffset);
                node.children.Add(childNode);
            }

            return node;
        }
        
        private List<HierarchySpawnAction> GetHierarchyActionsToSpawnThis()
        {
            var actions = ListPool<HierarchySpawnAction>.New();
            var prefabLink = GetComponentInParent<PrefabLink>();

            if (!prefabLink)
            {
                PurrLogger.LogError("Scene object is not a prefab, can't instantiate out of thin air");
                return actions;
            }
            
            var first = GetComponent<NetworkIdentity>();

            if (!first.isSpawned)
            {
                PurrLogger.LogError("Object is not spawned");
                return actions;
            }
            
            using var tree = GetHierarchyTree(first);
            
            if (!networkManager.prefabProvider.TryGetPrefab(tree.prefabId, tree.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {tree.prefabId} and offset {tree.prefabOffset}");
                return actions;
            }
            
            using var instantiated = GetHierarchyTree(prefab, tree.prefabId, tree.prefabOffset);
            
            actions.Add(new HierarchySpawnAction(
                new InstantiateAction(first.id!.Value, tree.prefabId, tree.prefabOffset)
            ));
            
            GetActions(instantiated, tree, actions);
            
            actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
            
            return actions;
        }

        private HierarchyNode? VInstantiate(HierarchyNode target)
        {
            if (!networkManager.prefabProvider.TryGetPrefab(target.prefabId, target.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {target.prefabId} and offset {target.prefabOffset}");
                return null;
            }

            return GetHierarchyTree(prefab, target.prefabId, target.prefabOffset);
        }

        private void GetActions(HierarchyNode current, HierarchyNode expected, IList<HierarchySpawnAction> actions)
        {
            bool replaced = false;
            
            if (current.prefabId != expected.prefabId || 
                current.prefabOffset != expected.prefabOffset)
            {
                var instantiated = VInstantiate(expected);
                
                if (!instantiated.HasValue)
                    return;
                
                actions.Add(new HierarchySpawnAction(new ReplaceAction(
                    current.prefabOffset, expected.components[0].networkId, expected.prefabId, expected.prefabOffset))
                );
                
                current = instantiated.Value;
                replaced = true;
            }
            
            int expectedChildCount = expected.children.Count;
            int currentChildCount = current.children.Count;
            
            for (var i = 0; i < expectedChildCount; i++)
            {
                HierarchyNode? child = current.children.Count > i ? current.children[i] : null;
                var expectedChild = expected.children[i];
                
                bool wasSpawned = false;

                if (!child.HasValue)
                {
                    var instantiated = VInstantiate(expectedChild);
                    
                    if (!instantiated.HasValue)
                        continue;

                    actions.Add(new HierarchySpawnAction(new InstantiateWithParentAction(
                        current.prefabOffset, expectedChild.components[0].networkId, expectedChild.prefabId, expectedChild.prefabOffset))
                    );
                    
                    child = instantiated;
                    current.children.Add(instantiated.Value);
                    wasSpawned = true;
                }
                
                GetActions(child.Value, expectedChild, actions);
                
                if (wasSpawned)
                    actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
            }

            for (var i = expectedChildCount; i < currentChildCount; i++)
            {
                var child = current.children[i];
                actions.Add(new HierarchySpawnAction(new DestroyAction(child.prefabOffset)));
            }
            
            if (replaced)
                actions.Add(new HierarchySpawnAction(HierarchySpawnAction.HierarchyActionType.Pop));
        }

        private bool HandleInstantiateAction(InstantiateAction action, out NGameObject instantiated, bool asServer)
        {
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                instantiated = default;
                return false;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = new NGameObject(Instantiate(prefab));
            
            var hierarchy = networkManager.GetModule<HierarchyModule>(asServer);
            if (hierarchy.TryGetHierarchy(sceneId, out var scene))
            {
                for (ushort i = 0; i < instantiated.identities.Count; i++)
                {
                    var child = instantiated.identities[i];
                    scene.SpawnIdentity(child, action.prefabId, action.networkId, i, asServer);
                }
            }
            
            return true;
        }
        
        private static void HandleDestroyAction(DestroyAction action, NGameObject instantiated)
        {
            var child = instantiated.identities[action.childId];
            Destroy(child.gameObject);
        }
        
        private bool HandleInstantiateWithParentAction(InstantiateWithParentAction action, NGameObject current, out NGameObject instantiated, bool asServer)
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

            var hierarchy = networkManager.GetModule<HierarchyModule>(asServer);

            if (hierarchy.TryGetHierarchy(sceneId, out var scene))
            {
                for (ushort i = 0; i < instantiated.identities.Count; i++)
                {
                    var child = instantiated.identities[i];
                    scene.SpawnIdentity(child, action.prefabId, action.networkId, i, asServer);
                }
            }

            return true;
        }
        
        private void HandleReplaceAction(ReplaceAction action, NGameObject instantiated, bool asServer)
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
            
            var hierarchy = networkManager.GetModule<HierarchyModule>(asServer);
            if (hierarchy.TryGetHierarchy(sceneId, out var scene))
            {
                for (ushort i = 0; i < copy.identities.Count; i++)
                {
                    var child = copy.identities[i];
                    scene.SpawnIdentity(child, action.prefabId, action.networkId, i, asServer);
                }
            }
        }
        
        static readonly Stack<NGameObject> _instantiatedStack = new();
        
        private void ReplayActions(IList<HierarchySpawnAction> actions, bool asServer)
        {

            for (int i = 0; i < actions.Count; ++i)
            {
                var action = actions[i];
                
                switch (action.action)
                {
                    case HierarchySpawnAction.HierarchyActionType.Instantiate:
                    {
                        if (HandleInstantiateAction(action.instantiateAction, out var instantiated, asServer))
                            _instantiatedStack.Push(instantiated);
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Pop:
                    {
                        var go = _instantiatedStack.Pop();
                        go.Dispose();
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Destroy: 
                        HandleDestroyAction(action.destroyAction, _instantiatedStack.Peek());
                        break;
                    case HierarchySpawnAction.HierarchyActionType.InstantiateWithParent:
                    {
                        if (_instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to instantiate with, bad stack state");
                            break;
                        }
                        
                        var peek = _instantiatedStack.Peek();
                        if (HandleInstantiateWithParentAction(action.instantiateWithParentAction, peek,
                                out var instantiated, asServer))
                        {
                            _instantiatedStack.Push(instantiated);
                        }

                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Replace: 
                        HandleReplaceAction(action.replaceAction, _instantiatedStack.Peek(), asServer);
                        break;
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
        }

        [ContextMenu("Spawn")]
        void TestSpawnActions()
        {
            var actions = GetHierarchyActionsToSpawnThis();
            
            foreach (var action in actions)
                PurrLogger.Log(action.ToString());
            
            ReplayActions(actions, false);
            
            ListPool<HierarchySpawnAction>.Destroy(actions);
        }
    }
}
