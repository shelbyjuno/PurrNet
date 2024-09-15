using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Utils;
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
                children = new List<HierarchyNode>(),
                components = new List<NodeComponent>(),
            };

            var trs = target.transform;
            var siblings = target.GetComponents<NetworkIdentity>();

            for (var i = 0; i < siblings.Length; i++)
                node.components.Add(new NodeComponent(siblings[i]));
            
            prefabOffset += siblings.Length;

            for (var i = 0; i < trs.childCount; i++)
            {
                var child = trs.GetChild(i).GetComponentInChildren<NetworkIdentity>();
                if (!child) continue;

                var childNode = InternalGetHierarchyTree(child, prefabId, ref prefabOffset);
                node.children.Add(childNode);
            }

            return node;
        }
        
        public List<HierarchySpawnAction> GetHierarchyActionsToSpawnThis()
        {
            var actions = new List<HierarchySpawnAction>();
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
            
            var tree = GetHierarchyTree(first);
            
            if (!networkManager.prefabProvider.TryGetPrefab(tree.prefabId, tree.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {tree.prefabId} and offset {tree.prefabOffset}");
                return actions;
            }
            
            var instantiated = GetHierarchyTree(prefab, tree.prefabId, tree.prefabOffset);
            
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

        private bool HandleInstantiateAction(InstantiateAction action, out GameObject instantiated)
        {
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                instantiated = null;
                return false;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = Instantiate(prefab);
            
            var children = instantiated.GetComponentsInChildren<NetworkIdentity>(true);
            
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var nid = new NetworkID(action.networkId, (ushort)i);
                
                child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, true);
                child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, false);
            }
            
            return true;
        }
        
        private void HandleDestroyAction(DestroyAction action, GameObject instantiated)
        {
            var children = instantiated.GetComponentsInChildren<NetworkIdentity>(true);
            var child = children[action.childId];
            Destroy(child.gameObject);
        }
        
        private bool HandleInstantiateWithParentAction(InstantiateWithParentAction action, GameObject current, out GameObject instantiated)
        {
            var scopeChildren = current.GetComponentsInChildren<NetworkIdentity>(true);
            var parent = scopeChildren[action.parentChildId];
            
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                instantiated = null;
                return false;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = Instantiate(prefab, parent.transform);
            
            var hierarchy = networkManager.GetModule<HierarchyModule>(isServer);

            if (hierarchy.TryGetHierarchy(sceneId, out var scene))
            {
                var children = instantiated.GetComponentsInChildren<NetworkIdentity>(true);

                for (var i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    var nid = new NetworkID(action.networkId, (ushort)i);
                
                    child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, true);
                    child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, false);
                }
            }

            return true;
        }
        
        private void HandleReplaceAction(ReplaceAction action, GameObject instantiated)
        {
            var oldChildren = instantiated.GetComponentsInChildren<NetworkIdentity>(true);
            var oldChild = oldChildren[action.childId];
            var parent = oldChild.transform.parent;
            
            if (!networkManager.prefabProvider.TryGetPrefab(action.prefabId, action.prefabOffset, out var prefab))
            {
                PurrLogger.LogError($"Failed to get prefab with id {action.prefabId} and offset {action.prefabOffset}");
                return;
            }

            PrefabLink.IgnoreNextAutoSpawnAttempt();
            instantiated = Instantiate(prefab, parent);
            
            var children = instantiated.GetComponentsInChildren<NetworkIdentity>(true);
            
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var nid = new NetworkID(action.networkId, (ushort)i);
                
                child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, true);
                child.SetIdentity(networkManager, sceneId, action.prefabId, nid, action.prefabOffset, false);
            }
            
            return;
        }
        
        private void ReplayActions(IList<HierarchySpawnAction> actions)
        {
            Stack<GameObject> instantiatedStack = new();

            for (int i = 0; i < actions.Count; ++i)
            {
                var action = actions[i];
                
                switch (action.action)
                {
                    case HierarchySpawnAction.HierarchyActionType.Instantiate:
                    {
                        if (HandleInstantiateAction(action.instantiateAction, out var instantiated))
                            instantiatedStack.Push(instantiated);
                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Pop: instantiatedStack.Pop(); break;
                    case HierarchySpawnAction.HierarchyActionType.Destroy: 
                        HandleDestroyAction(action.destroyAction, instantiatedStack.Peek());
                        break;
                    case HierarchySpawnAction.HierarchyActionType.InstantiateWithParent:
                    {
                        if (instantiatedStack.Count == 0)
                        {
                            PurrLogger.LogError("No parent to instantiate with, bad stack state");
                            break;
                        }
                        
                        var peek = instantiatedStack.Peek();
                        if (HandleInstantiateWithParentAction(action.instantiateWithParentAction, peek,
                                out var instantiated))
                        {
                            instantiatedStack.Push(instantiated);
                        }

                        break;
                    }
                    case HierarchySpawnAction.HierarchyActionType.Replace: 
                        HandleReplaceAction(action.replaceAction, instantiatedStack.Peek());
                        break;
                    default: PurrLogger.LogError($"Unknown action type {action.action}"); break;
                }
            }
            
            if (instantiatedStack.Count > 0)
                PurrLogger.LogError("Stack is not empty, something went wrong");
        }

        [ContextMenu("Spawn")]
        void TestSpawnActions()
        {
            var actions = GetHierarchyActionsToSpawnThis();
            
            foreach (var action in actions)
            {
                PurrLogger.Log(action.ToString());
            }
            
            ReplayActions(actions);
        }
    }
}
