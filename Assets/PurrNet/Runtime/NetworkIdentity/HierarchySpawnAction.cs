using System;
using System.Collections.Generic;
using PurrNet.Packets;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet
{
    internal readonly struct NGameObject : IDisposable
    {
        public readonly List<NetworkIdentity> identities;
            
        public NGameObject(GameObject gameObject)
        {
            identities = ListPool<NetworkIdentity>.New();
            gameObject.GetComponentsInChildren(true, identities);
        }

        public void Dispose() => ListPool<NetworkIdentity>.Destroy(identities);
    }
    
    internal partial struct InstantiateAction : IAutoNetworkedData
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
    
    internal partial struct InstantiateWithParentAction : IAutoNetworkedData
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
    
    internal partial struct ReplaceAction : IAutoNetworkedData
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
    
    internal partial struct DestroyAction : IAutoNetworkedData
    {
        public readonly int childId;
        
        public DestroyAction(int childId)
        {
            this.childId = childId;
        }
    }
        
    internal partial struct DestroyComponentAction : IAutoNetworkedData
    {
        public readonly int childId;
        
        public DestroyComponentAction(int childId)
        {
            this.childId = childId;
        }
    }
    
    internal partial struct ToggleGameObjectActiveAction : IAutoNetworkedData
    {
        public readonly int childId;
        
        public ToggleGameObjectActiveAction(int childId)
        {
            this.childId = childId;
        }
    }
    
    internal partial struct ToggleComponentEnabledAction : IAutoNetworkedData
    {
        public readonly int childId;
        
        public ToggleComponentEnabledAction(int childId)
        {
            this.childId = childId;
        }
    }

    internal struct HierarchyNode : IDisposable
    {
        public bool isActive;
        public int prefabId;
        public ushort prefabOffset;
        public int siblingIndex;

        public List<HierarchyNode> children;
        public List<NodeComponent> components;
        
        public void Dispose()
        {
            
            for (var i = 0; i < children.Count; i++)
                children[i].Dispose();
            
            ListPool<HierarchyNode>.Destroy(children);
            ListPool<NodeComponent>.Destroy(components);
        }
        
        public static HierarchyNode GetHierarchyTree(GameObject target, int prefabId = -1, int prefabOffset =  -1)
        {
            var first = target.GetComponent<NetworkIdentity>();
            return InternalGetHierarchyTree(first, prefabId, ref prefabOffset);
        }
        
        public static HierarchyNode GetHierarchyTree(NetworkIdentity target, int prefabId = -1, int prefabOffset =  -1)
        {
            return InternalGetHierarchyTree(target, prefabId, ref prefabOffset);
        }
        
        static HierarchyNode InternalGetHierarchyTree(NetworkIdentity target, int prefabId, ref int prefabOffset)
        {
            var currentSiblingIdx = target.transform.parent ? target.transform.GetSiblingIndex() : 0;
            
            var node = new HierarchyNode
            {
                prefabId = target.isSpawned ? target.prefabId : prefabId,
                prefabOffset = target.isSpawned ? target.prefabOffset : (ushort)prefabOffset,
                children = ListPool<HierarchyNode>.New(),
                components = ListPool<NodeComponent>.New(),
                isActive = target.gameObject.activeSelf,
                siblingIndex = target.isSpawned ? target.siblingIndex : currentSiblingIdx
            };

            var trs = target.transform;
            
            var siblings = ListPool<NetworkIdentity>.New();
            
            target.GetComponents(siblings);

            for (var i = 0; i < siblings.Count; i++)
            {
                node.components.Add(new NodeComponent(siblings[i], prefabOffset));
                prefabOffset += 1;
            }

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

    }
    
    internal struct NodeComponent
    {
        public readonly int offset;
        public readonly NetworkID networkId;
        public readonly bool enabled;

        public NodeComponent(NetworkIdentity identity, int offset)
        {
            this.offset = identity.isSpawned ? identity.prefabOffset : offset;
            networkId = identity.id ?? default;
            enabled = identity.enabled;
        }
    }
    
    internal partial struct HierarchySpawnAction : INetworkedData
    {
        public enum HierarchyActionType : byte
        {
            Instantiate,
            Destroy,
            ToggleActive,
            DestroyComponent,
            ToggleEnabled,
            InstantiateWithParent,
            Replace,
            Pop
        }
        
        public HierarchyActionType action;
        
        public InstantiateAction instantiateAction;
        public DestroyAction destroyAction;
        public InstantiateWithParentAction instantiateWithParentAction;
        public ReplaceAction replaceAction;
        public ToggleGameObjectActiveAction toggleGameObjectActiveAction;
        public DestroyComponentAction destroyComponentAction;
        public ToggleComponentEnabledAction toggleComponentEnabledAction;
        
        public HierarchySpawnAction(HierarchyActionType type)
        {
            action = type;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(InstantiateAction instantiateAction)
        {
            action = HierarchyActionType.Instantiate;
            this.instantiateAction = instantiateAction;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(InstantiateWithParentAction instantiateWithParentAction)
        {
            action = HierarchyActionType.InstantiateWithParent;
            instantiateAction = default;
            destroyAction = default;
            this.instantiateWithParentAction = instantiateWithParentAction;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(DestroyAction destroyAction)
        {
            action = HierarchyActionType.Destroy;
            instantiateAction = default;
            this.destroyAction = destroyAction;
            instantiateWithParentAction = default;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(ReplaceAction replaceAction)
        {
            action = HierarchyActionType.Replace;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            this.replaceAction = replaceAction;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(ToggleGameObjectActiveAction toggleGameObjectActiveAction)
        {
            action = HierarchyActionType.ToggleActive;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
            this.toggleGameObjectActiveAction = toggleGameObjectActiveAction;
            destroyComponentAction = default;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(DestroyComponentAction destroyComponentAction)
        {
            action = HierarchyActionType.DestroyComponent;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            this.destroyComponentAction = destroyComponentAction;
            toggleComponentEnabledAction = default;
        }
        
        public HierarchySpawnAction(ToggleComponentEnabledAction toggleComponentEnabledAction)
        {
            action = HierarchyActionType.ToggleEnabled;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
            toggleGameObjectActiveAction = default;
            destroyComponentAction = default;
            this.toggleComponentEnabledAction = toggleComponentEnabledAction;
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
                case HierarchyActionType.ToggleActive: 
                    packer.Serialize(ref toggleGameObjectActiveAction);
                    break; 
                case HierarchyActionType.DestroyComponent: 
                    packer.Serialize(ref destroyComponentAction);
                    break;
                case HierarchyActionType.ToggleEnabled: 
                    packer.Serialize(ref toggleComponentEnabledAction);
                    break;
            }
        }

        public override string ToString()
        {
            return action switch
            {
                HierarchyActionType.Instantiate =>
                    $"Instantiate: pid {instantiateAction.prefabId}:{instantiateAction.prefabOffset} : nid {instantiateAction.networkId}",
                HierarchyActionType.Destroy => "Destroy GO childId " + destroyAction.childId,
                HierarchyActionType.InstantiateWithParent =>
                    $"InstantiateWithParent: pid {instantiateWithParentAction.prefabId}:{instantiateWithParentAction.prefabOffset} : nid {instantiateWithParentAction.networkId}; parentChildId {instantiateWithParentAction.parentChildId}",
                HierarchyActionType.Replace =>
                    $"Replace: pid {replaceAction.prefabId}:{replaceAction.prefabOffset} : nid {replaceAction.networkId}; childId {replaceAction.childId}",
                HierarchyActionType.Pop => "Pop",
                HierarchyActionType.ToggleActive => "Disable GO childId " + toggleGameObjectActiveAction.childId,
                HierarchyActionType.DestroyComponent => "Destroy Component childId " + destroyComponentAction.childId,
                HierarchyActionType.ToggleEnabled => "Disable Component childId " + toggleComponentEnabledAction.childId,
                _ => "Unknown action"
            };
        }
    }
}