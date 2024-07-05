using System.Collections.Generic;
using PurrNet.Packets;
using UnityEngine;

namespace PurrNet
{
    public enum HierarchyActionType : byte
    {
        Spawn,
        Despawn,
        ChangeParent
    }

    public enum DespawnType : byte
    {
        ComponentOnly,
        GameObject
    }
    
    public partial struct HierarchyAction : INetworkedData
    {
        public HierarchyActionType type;

        public DespawnAction despawnAction;
        public SpawnAction spawnAction;
        public ChangeParentAction changeParentAction;

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref type);
            
            switch (type)
            {
                case HierarchyActionType.Spawn:
                    packer.Serialize(ref spawnAction);
                    break;
                case HierarchyActionType.Despawn:
                    packer.Serialize(ref despawnAction);
                    break;
                case HierarchyActionType.ChangeParent:
                    packer.Serialize(ref changeParentAction);
                    break;
            }
        }
    }
    
    public partial struct HierarchyActionBatch : IAutoNetworkedData
    {
        public List<HierarchyAction> actions;
    }

    public partial struct DespawnAction : IAutoNetworkedData
    {
        public int identityId { get; set; }
        public DespawnType despawnType { get; set; }
    }

    public partial struct SpawnAction : IAutoNetworkedData
    {
        public int prefabId { get; set; }
        public int identityId { get; set; }
        public TransformInfo transformInfo { get; set; }

        /// <summary>
        /// Spawn a child of the root identity.
        /// This avoids the need to spawn the root, get the child and then despawn the root.
        /// </summary>
        public ushort childOffset { get; set; }
    }

    public partial struct ChangeParentAction : IAutoNetworkedData
    {
        public int identityId { get; set; }
        public int parentId { get; set; }
    }

    public partial struct TransformInfo : IAutoNetworkedData
    {
        public int parentId { get; set; }
        public bool activeInHierarchy { get; set; }
        public Vector3 localPos { get; set; }
        public Quaternion localRot { get; set; }
        public Vector3 localScale { get; set; }

        public TransformInfo(Transform trs)
        {
            activeInHierarchy = trs.gameObject.activeInHierarchy;
            parentId = trs.parent ? trs.parent.GetComponent<NetworkIdentity>().id : -1;
            localPos = trs.localPosition;
            localRot = trs.localRotation;
            localScale = trs.localScale;
        }
    }
}