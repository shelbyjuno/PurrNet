using System.Collections.Generic;
using System.Text;
using PurrNet.Packets;
using UnityEngine;

namespace PurrNet.Modules
{
    internal enum HierarchyActionType : byte
    {
        Spawn,
        Despawn,
        ChangeParent,
        SetActive,
        SetEnabled
    }

    internal enum DespawnType : byte
    {
        ComponentOnly,
        GameObject
    }
    
    internal partial struct HierarchyAction : INetworkedData
    {
        public HierarchyActionType type;
        public PlayerID actor;

        public DespawnAction despawnAction;
        public SpawnAction spawnAction;
        public ChangeParentAction changeParentAction;
        public SetActiveAction setActiveAction;
        public SetEnabledAction setEnabledAction;

        static readonly StringBuilder _sb = new ();
        
        public override string ToString()
        {
            _sb.Clear();
            
            switch (type)
            {
                case HierarchyActionType.Spawn:
                    _sb.Append(spawnAction);
                    break;
                case HierarchyActionType.Despawn:
                    _sb.Append(despawnAction);
                    break;
                case HierarchyActionType.ChangeParent:
                    _sb.Append(changeParentAction);
                    break;
                case HierarchyActionType.SetActive:
                    _sb.Append(setActiveAction);
                    break;
                case HierarchyActionType.SetEnabled:
                    _sb.Append(setEnabledAction);
                    break;
            }

            return _sb.ToString();
        }

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref type);
            packer.Serialize(ref actor);
            
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
                case HierarchyActionType.SetActive:
                    packer.Serialize(ref setActiveAction);
                    break;
                case HierarchyActionType.SetEnabled:
                    packer.Serialize(ref setEnabledAction);
                    break;
            }
        }
        
        public NetworkID? GetIdentityId()
        {
            return type switch
            {
                HierarchyActionType.Spawn => spawnAction.identityId,
                HierarchyActionType.Despawn => despawnAction.identityId,
                HierarchyActionType.ChangeParent => changeParentAction.identityId,
                HierarchyActionType.SetActive => setActiveAction.identityId,
                HierarchyActionType.SetEnabled => setEnabledAction.identityId,
                _ => null
            };
        }
    }
    
    internal partial struct HierarchyActionBatch : IAutoNetworkedData
    {
        public SceneID sceneId;
        public List<HierarchyAction> actions;
        public bool isDelta;
    }

    internal partial struct DespawnAction : IAutoNetworkedData
    {
        public NetworkID identityId { get; set; }
        public DespawnType despawnType { get; set; }

        public override string ToString() => $"Despawn: {identityId} ({despawnType})";
    }
    
    internal partial struct SetActiveAction : IAutoNetworkedData
    {
        public NetworkID identityId { get; set; }
        public bool active { get; set; }
        
        public override string ToString() => $"SetActive: {identityId} ({active})";
    }
    
    internal partial struct SetEnabledAction : IAutoNetworkedData
    {
        public NetworkID identityId { get; set; }
        public bool enabled { get; set; }
        
        public override string ToString() => $"SetEnabled: {identityId} ({enabled})";
    }

    internal partial struct SpawnAction : IAutoNetworkedData
    {
        public int prefabId { get; set; }
        public NetworkID identityId { get; set; }
        public ushort childCount { get; set; }
        public TransformInfo transformInfo { get; set; }

        /// <summary>
        /// Spawn a child of the root identity.
        /// This avoids the need to spawn the root, get the child and then despawn the root.
        /// </summary>
        public ushort childOffset { get; set; }
        
        public override string ToString() => $"Spawn: {identityId} (pid: {prefabId}, children: {childCount})";
    }

    public partial struct ChangeParentAction : IAutoNetworkedData
    {
        public NetworkID identityId { get; set; }
        public NetworkID? parentId { get; set; }
        
        public override string ToString() => $"ChangeParent: {identityId} (target parent id: {parentId})";
    }

    public partial struct TransformInfo : IAutoNetworkedData
    {
        public NetworkID? parentId { get; set; }
        public bool activeSelf { get; set; }
        public Vector3 localPos { get; set; }
        public Quaternion localRot { get; set; }
        public Vector3 localScale { get; set; }

        public TransformInfo(Transform trs)
        {
            activeSelf = trs.gameObject.activeSelf;

            var parent = trs.parent;

            if (parent)
            {
                parentId = parent.TryGetComponent(out NetworkIdentity parentIdentity) ? parentIdentity.id : null;
            }
            else
            {
                parentId = null;
            }
            
            localPos = trs.localPosition;
            localRot = trs.localRotation;
            localScale = trs.localScale;
        }
    }
}