using System;
using JetBrains.Annotations;
using UnityEngine;

namespace PurrNet
{
    [Serializable]
    public struct SpawnRules
    {
        public ConnectionAuth spawnAuth;
        public ActionAuth despawnAuth;
        
        [Tooltip("Who gains ownership upon spawning of the identity")]
        public DefaultOwner defaultOwner;

        [Tooltip("Propagate ownership to all children of the object")]
        public bool propagateOwnershipByDefault;

        [Tooltip("If owner disconnects, should the object despawn or stay in the scene?")]
        public bool despawnIfOwnerDisconnects;
    }

    [Serializable]
    public struct OwnershipRules
    {
        [Tooltip("Who can assign ownership to objects")]
        public ConnectionAuth assignAuth;
        
        [Tooltip("Who can transfer existing ownership from objects")]
        public ActionAuth transferAuth;
        
        [Tooltip("Who can remove ownership from objects")]
        public ActionAuth removeAuth;
        
        [Tooltip("If object already has an owner, should the new owner override the existing owner?")]
        public bool overrideWhenPropagating;
    }

    [Serializable]
    public struct NetworkIdentityRules
    {
        public bool syncComponentActive;
        public ActionAuth syncComponentAuth;

        public bool syncGameObjectActive;
        public ActionAuth syncGameObjectActiveAuth;
    }

    [Serializable]
    public struct NetworkTransformRules
    {
        public bool syncParent;
        public ActionAuth changeParentAuth;
    }
    
    [CreateAssetMenu(fileName = "NetworkRules", menuName = "PurrNet/Network Rules", order = -201)]
    public class NetworkRules : ScriptableObject
    {
        [SerializeField] private SpawnRules _defaultSpawnRules = new()
        {
            despawnAuth = ActionAuth.Server | ActionAuth.Owner,
            spawnAuth = ConnectionAuth.Server,
            defaultOwner = DefaultOwner.SpawnerIfClient,
            propagateOwnershipByDefault = true,
            despawnIfOwnerDisconnects = true
        };
        
        [SerializeField] private OwnershipRules _defaultOwnershipRules = new()
        {
            assignAuth = ConnectionAuth.Server,
            transferAuth = ActionAuth.Owner | ActionAuth.Server,
            overrideWhenPropagating = true
        };
        
        [SerializeField] private NetworkIdentityRules _defaultIdentityRules = new()
        {
            syncComponentActive = true,
            syncComponentAuth = ActionAuth.Server | ActionAuth.Owner,
            syncGameObjectActive = true,
            syncGameObjectActiveAuth = ActionAuth.Server | ActionAuth.Owner
        };
        
        [SerializeField] private NetworkTransformRules _defaultTransformRules = new()
        {
            changeParentAuth = ActionAuth.Server | ActionAuth.Owner,
            syncParent = true
        };
        
        public SpawnRules GetDefaultSpawnRules() => _defaultSpawnRules;
        public OwnershipRules GetDefaultOwnershipRules() => _defaultOwnershipRules;
        public NetworkIdentityRules GetDefaultIdentityRules() => _defaultIdentityRules;

        public bool HasDespawnAuthority(NetworkIdentity identity, PlayerID player, bool asServer)
        {
            return HasAuthority(_defaultSpawnRules.despawnAuth, identity, player, asServer);
        }
        
        [UsedImplicitly]
        public bool HasSpawnAuthority(NetworkManager manager, bool asServer)
        {
            return HasAuthority(_defaultSpawnRules.spawnAuth, asServer);
        }
        
        public bool HasSetActiveAuthority(NetworkIdentity identity, PlayerID player, bool asServer)
        {
            return HasAuthority(_defaultIdentityRules.syncGameObjectActiveAuth, identity, player, asServer);
        }
        
        public bool HasSetEnabledAuthority(NetworkIdentity identity, PlayerID player, bool asServer)
        {
            return HasAuthority(_defaultIdentityRules.syncComponentAuth, identity, player, asServer);
        }
        
        [UsedImplicitly]
        public bool ShouldSyncParent(NetworkIdentity identity, bool asServer)
        {
            return _defaultTransformRules.syncParent;
        }
        
        [UsedImplicitly]
        public bool ShouldSyncSetActive(NetworkIdentity identity, bool asServer)
        {
            return _defaultIdentityRules.syncGameObjectActive;
        }
        
        [UsedImplicitly]
        public bool ShouldSyncSetEnabled(NetworkIdentity identity, bool asServer)
        {
            return _defaultIdentityRules.syncComponentActive;
        }
        
        [UsedImplicitly]
        public bool HasPropagateOwnershipAuthority(NetworkIdentity identity, bool asServer)
        {
            return true;
        }
        
        public bool HasChangeParentAuthority(NetworkIdentity identity, PlayerID player, bool asServer)
        {
            return HasAuthority(_defaultTransformRules.changeParentAuth, identity, player, asServer);
        }
        
        static bool HasAuthority(ConnectionAuth connAuth, bool asServer)
        {
            return connAuth == ConnectionAuth.Everyone || asServer;
        }
        
        static bool HasAuthority(ActionAuth action, NetworkIdentity identity, PlayerID player, bool asServer)
        {
            if (action.HasFlag(ActionAuth.Server) && asServer)
                return true;
            
            if (action.HasFlag(ActionAuth.Owner) && identity.owner == player)
                return true;
            
            return identity.owner != player && action.HasFlag(ActionAuth.Observer);
        }
        
        public bool HasTransferOwnershipAuthority(NetworkIdentity networkIdentity, PlayerID localPlayer, bool asServer)
        {
            return HasAuthority(_defaultOwnershipRules.transferAuth, networkIdentity, localPlayer, asServer);
        }

        [UsedImplicitly]
        public bool HasGiveOwnershipAuthority(NetworkIdentity networkIdentity, bool asServer)
        {
            return HasAuthority(_defaultOwnershipRules.assignAuth, asServer);
        }
        
        [UsedImplicitly]
        public bool ShouldPropagateToChildren(NetworkIdentity networkIdentity, bool asServer)
        {
            return _defaultSpawnRules.propagateOwnershipByDefault;
        }

        [UsedImplicitly]
        public bool ShouldOverrideExistingOwnership(NetworkIdentity networkIdentity, bool asServer)
        {
            return _defaultOwnershipRules.overrideWhenPropagating;
        }
    }
}