using System;
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
        public bool propagateOwnership;

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
            propagateOwnership = true,
            despawnIfOwnerDisconnects = true
        };
        
        [SerializeField] private OwnershipRules _defaultOwnershipRules = new()
        {
            assignAuth = ConnectionAuth.Server,
            transferAuth = ActionAuth.Owner | ActionAuth.Server,
            removeAuth = ActionAuth.Owner | ActionAuth.Server
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
        
        /*[Tooltip("Who can modify syncvars")]
        public ConnectionAuth syncVarAuth;
        [Tooltip("Who can send ObserversRpc and TargetRpc")]
        public ConnectionAuth clientRpcAuth;*/

        public bool HasDespawnAuthority(NetworkIdentity identity, PlayerID player)
        {
            return HasAuthority(_defaultSpawnRules.despawnAuth, identity, player);
        }
        
        public bool HasSpawnAuthority(NetworkIdentity identity)
        {
            return HasAuthority(_defaultSpawnRules.spawnAuth, identity);
        }
        
        public bool HasSetActiveAuthority(NetworkIdentity identity, PlayerID player)
        {
            return HasAuthority(_defaultIdentityRules.syncGameObjectActiveAuth, identity, player);
        }
        
        public bool HasSetEnabledAuthority(NetworkIdentity identity, PlayerID player)
        {
            return HasAuthority(_defaultIdentityRules.syncComponentAuth, identity, player);
        }
        
        public bool ShouldSyncParent(NetworkIdentity identity)
        {
            return _defaultTransformRules.syncParent;
        }
        
        public bool HasChangeParentAuthority(NetworkIdentity identity, PlayerID player)
        {
            return HasAuthority(_defaultTransformRules.changeParentAuth, identity, player);
        }
        
        static bool HasAuthority(ConnectionAuth connAuth, NetworkIdentity identity)
        {
            return connAuth == ConnectionAuth.Everyone || identity.networkManager.isServer;
        }
        
        static bool HasAuthority(ActionAuth action, NetworkIdentity identity, PlayerID player)
        {
            if (action.HasFlag(ActionAuth.Server) && identity.networkManager.isServer)
                return true;
            
            if (action.HasFlag(ActionAuth.Owner) && identity.owner == player)
                return true;
            
            return identity.owner != player && action.HasFlag(ActionAuth.Observer);
        }
    }
}