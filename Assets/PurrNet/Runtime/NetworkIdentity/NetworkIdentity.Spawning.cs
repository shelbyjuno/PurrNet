using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public partial struct InstantiateAction : IAutoNetworkedData
    {
        public readonly NetworkID networkId;
        public readonly int prefabId;
        public readonly ushort prefabOffset;
        
        public InstantiateAction(NetworkID networkId, int prefabId, ushort prefabOffset)
        {
            this.networkId = networkId;
            this.prefabId = prefabId;
            this.prefabOffset = prefabOffset;
        }
    }
    
    public partial struct InstantiateWithParentAction : IAutoNetworkedData
    {
        public readonly int parentChildId;
        public readonly NetworkID networkId;
        public readonly int prefabId;
        public readonly ushort prefabOffset;
        
        public InstantiateWithParentAction(int childId, NetworkID networkId, int prefabId, ushort prefabOffset)
        {
            parentChildId = childId;
            this.networkId = networkId;
            this.prefabId = prefabId;
            this.prefabOffset = prefabOffset;
        }
    }
    
    public partial struct ReplaceAction : IAutoNetworkedData
    {
        public readonly int childId;
        public readonly NetworkID networkId;
        public readonly int prefabId;
        public readonly ushort prefabOffset;
        
        public ReplaceAction(int childId, NetworkID networkId, int prefabId, ushort prefabOffset)
        {
            this.childId = childId;
            this.networkId = networkId;
            this.prefabId = prefabId;
            this.prefabOffset = prefabOffset;
        }
    }
    
    public partial struct DestroyAction : IAutoNetworkedData
    {
        public readonly int childId;
        
        public DestroyAction(int childId)
        {
            this.childId = childId;
        }
    }
    
    public partial struct HierarchyAction : INetworkedData
    {
        public enum HierarchyActionType : byte
        {
            Instantiate,
            Destroy,
            InstantiateWithParent,
            Replace,
        }
        
        public HierarchyActionType action;
        
        public InstantiateAction instantiateAction;
        public DestroyAction destroyAction;
        public InstantiateWithParentAction instantiateWithParentAction;
        public ReplaceAction replaceAction;
        
        public HierarchyAction(InstantiateAction instantiateAction)
        {
            action = HierarchyActionType.Instantiate;
            this.instantiateAction = instantiateAction;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
        }
        
        public HierarchyAction(InstantiateWithParentAction instantiateWithParentAction)
        {
            action = HierarchyActionType.InstantiateWithParent;
            instantiateAction = default;
            destroyAction = default;
            this.instantiateWithParentAction = instantiateWithParentAction;
            replaceAction = default;
        }
        
        public HierarchyAction(DestroyAction destroyAction)
        {
            action = HierarchyActionType.Destroy;
            instantiateAction = default;
            this.destroyAction = destroyAction;
            instantiateWithParentAction = default;
            replaceAction = default;
        }
        
        public HierarchyAction(ReplaceAction replaceAction)
        {
            action = HierarchyActionType.Replace;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            this.replaceAction = replaceAction;
        }

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref action);

            switch (action)
            {
                case HierarchyActionType.Instantiate:
                    packer.Serialize(ref instantiateAction);
                    break;
                case HierarchyActionType.Destroy:
                    packer.Serialize(ref destroyAction);
                    break;
                case HierarchyActionType.InstantiateWithParent:
                    packer.Serialize(ref instantiateWithParentAction);
                    break;
                case HierarchyActionType.Replace:
                    packer.Serialize(ref replaceAction);
                    break;
            }
        }

        public override string ToString()
        {
            return action switch
            {
                HierarchyActionType.Instantiate =>
                    $"Instantiate: pid {instantiateAction.prefabId}:{instantiateAction.prefabOffset} : nid {instantiateAction.networkId}",
                HierarchyActionType.Destroy => "Destroy childId " + destroyAction.childId,
                HierarchyActionType.InstantiateWithParent =>
                    $"InstantiateWithParent: pid {instantiateWithParentAction.prefabId}:{instantiateWithParentAction.prefabOffset} : nid {instantiateWithParentAction.networkId}; parentChildId {instantiateWithParentAction.parentChildId}",
                HierarchyActionType.Replace =>
                    $"Replace: pid {replaceAction.prefabId}:{replaceAction.prefabOffset} : nid {replaceAction.networkId}; childId {replaceAction.childId}",
                _ => "Unknown action"
            };
        }
    }

    public struct HierarchyNode
    {
        public int prefabId;
        public ushort prefabOffset;

        public List<HierarchyNode> children;
        public List<NodeComponent> components;
    }
    
    public struct NodeComponent
    {
        public uint typeHash;
        public NetworkID networkId;
        public bool enabled;

        public NodeComponent(NetworkIdentity identity)
        {
            typeHash = Hasher.PrepareType(identity.GetType());
            networkId = identity.id ?? default;
            enabled = identity.enabled;
        }
    }
    
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
        
        public List<HierarchyAction> GetHierarchyActionsToSpawnThis()
        {
            var actions = new List<HierarchyAction>();
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
            
            actions.Add(new HierarchyAction(
                new InstantiateAction(first.id!.Value, tree.prefabId, tree.prefabOffset)
            ));
            
            GetActions(instantiated, tree, actions);
            
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

        private void GetActions(HierarchyNode current, HierarchyNode expected, IList<HierarchyAction> actions)
        {
            if (current.prefabId != expected.prefabId || 
                current.prefabOffset != expected.prefabOffset)
            {
                var instantiated = VInstantiate(expected);
                
                if (!instantiated.HasValue)
                    return;
                
                actions.Add(new HierarchyAction(new ReplaceAction(
                    current.prefabOffset, expected.components[0].networkId, expected.prefabId, expected.prefabOffset))
                );
                
                current = instantiated.Value;
            }
            
            int expectedChildCount = expected.children.Count;
            int currentChildCount = current.children.Count;
            
            for (var i = 0; i < expectedChildCount; i++)
            {
                HierarchyNode? child = current.children.Count > i ? current.children[i] : null;
                var expectedChild = expected.children[i];

                if (!child.HasValue)
                {
                    var instantiated = VInstantiate(expectedChild);
                    if (!instantiated.HasValue)
                        continue;

                    actions.Add(new HierarchyAction(new InstantiateWithParentAction(
                        current.prefabOffset, expectedChild.components[0].networkId, expectedChild.prefabId, expectedChild.prefabOffset))
                    );
                    
                    child = instantiated;
                    current.children.Add(instantiated.Value);
                }
                
                GetActions(child.Value, expectedChild, actions);
            }

            for (var i = expectedChildCount; i < currentChildCount; i++)
            {
                var child = current.children[i];
                actions.Add(new HierarchyAction(new DestroyAction(child.prefabOffset)));
            }
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
                
                //TODO: probably need to set the prefab id and offset here, this is just a placeholder
                child.SetIdentity(networkManager, sceneId, prefabId, nid, action.prefabOffset, true);
                child.SetIdentity(networkManager, sceneId, prefabId, nid, action.prefabOffset, false);
            }
            
            return true;
        }
        
        private void HandleDestroyAction(DestroyAction action)
        {
            
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
                    if (isServer) scene.SpawnIdentity(children[i], action.prefabId, action.networkId, 0, true);
                    if (isClient) scene.SpawnIdentity(children[i], action.prefabId, action.networkId, 0, false);
                }
            }

            return true;
        }
        
        private void HandleReplaceAction(ReplaceAction action)
        {
            
        }
        
        private void ReplayActions(IList<HierarchyAction> actions)
        {
            GameObject instantiated = null;

            for (int i = 0; i < actions.Count; ++i)
            {
                var action = actions[i];
                
                switch (action.action)
                {
                    case HierarchyAction.HierarchyActionType.Instantiate:
                    {
                        HandleInstantiateAction(action.instantiateAction, out instantiated);
                        break;
                    }
                    case HierarchyAction.HierarchyActionType.Destroy: 
                        HandleDestroyAction(action.destroyAction);
                        break;
                    case HierarchyAction.HierarchyActionType.InstantiateWithParent:
                    {
                        if (HandleInstantiateWithParentAction(action.instantiateWithParentAction, instantiated,
                                out var inst))
                        {
                            instantiated = inst;
                        }

                        break;
                    }
                    case HierarchyAction.HierarchyActionType.Replace: 
                        HandleReplaceAction(action.replaceAction);
                        break;
                    default: PurrLogger.LogError($"Unknown action type {action.action}"); break;
                }
            }
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
