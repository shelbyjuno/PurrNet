using System.Collections.Generic;
using PurrNet.Packets;
using PurrNet.Utils;

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
    
    
    public partial struct HierarchySpawnAction : INetworkedData
    {
        public enum HierarchyActionType : byte
        {
            Instantiate,
            Destroy,
            InstantiateWithParent,
            Replace,
            Pop
        }
        
        public HierarchyActionType action;
        
        public InstantiateAction instantiateAction;
        public DestroyAction destroyAction;
        public InstantiateWithParentAction instantiateWithParentAction;
        public ReplaceAction replaceAction;
        
        public HierarchySpawnAction(HierarchyActionType type)
        {
            action = type;
            instantiateAction = default;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
        }
        
        public HierarchySpawnAction(InstantiateAction instantiateAction)
        {
            action = HierarchyActionType.Instantiate;
            this.instantiateAction = instantiateAction;
            destroyAction = default;
            instantiateWithParentAction = default;
            replaceAction = default;
        }
        
        public HierarchySpawnAction(InstantiateWithParentAction instantiateWithParentAction)
        {
            action = HierarchyActionType.InstantiateWithParent;
            instantiateAction = default;
            destroyAction = default;
            this.instantiateWithParentAction = instantiateWithParentAction;
            replaceAction = default;
        }
        
        public HierarchySpawnAction(DestroyAction destroyAction)
        {
            action = HierarchyActionType.Destroy;
            instantiateAction = default;
            this.destroyAction = destroyAction;
            instantiateWithParentAction = default;
            replaceAction = default;
        }
        
        public HierarchySpawnAction(ReplaceAction replaceAction)
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
                HierarchyActionType.Pop => "Pop",
                _ => "Unknown action"
            };
        }
    }
}