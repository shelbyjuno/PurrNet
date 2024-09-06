using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        [SerializeField, HideInInspector] private NetworkRules _networkRules;
        
        protected NetworkRules networkRules => _networkRules ? networkManager.networkRules : _networkRules;
        
        public bool HasDespawnAuthority(PlayerID player, bool asServer)
        {
            return networkRules.HasDespawnAuthority(this, player, asServer);
        }
        
        public bool HasSpawnAuthority(NetworkManager manager, bool asServer)
        {
            var rules = _networkRules ? manager.networkRules : _networkRules;
            return rules.HasSpawnAuthority(manager, asServer);
        }
        
        public bool HasSetActiveAuthority(PlayerID player, bool asServer)
        {
            return networkRules.HasSetActiveAuthority(this, player, asServer);
        }
        
        public bool HasSetActiveAuthority(bool asServer)
        {
            return networkRules.HasSetActiveAuthority(this, localPlayer, asServer);
        }
        
        public bool HasSetEnabledAuthority(PlayerID player, bool asServer)
        {
            return networkRules.HasSetEnabledAuthority(this, player, asServer);
        }
        
        public bool HasSetEnabledAuthority(bool asServer)
        {
            return networkRules.HasSetEnabledAuthority(this, localPlayer, asServer);
        }

        public bool ShouldSyncParent(bool asServer)
        {
            return networkRules.ShouldSyncParent(this, asServer);
        }
        
        public bool ShouldSyncSetActive(bool asServer)
        {
            return networkRules.ShouldSyncSetActive(this, asServer);
        }
        
        public bool ShouldSyncSetEnabled(bool asServer)
        {
            return networkRules.ShouldSyncSetEnabled(this, asServer);
        }
        
        public bool ShouldPropagateToChildren(bool asServer)
        {
            return networkRules.ShouldPropagateToChildren(this, asServer);
        }
        
        public bool ShouldOverrideExistingOwnership(bool asServer)
        {
            return networkRules.ShouldOverrideExistingOwnership(this, asServer);
        }
        
        public bool HasPropagateOwnershipAuthority(bool asServer)
        {
            return networkRules.HasPropagateOwnershipAuthority(this, asServer);
        }
        
        public bool HasChangeParentAuthority(bool asServer)
        {
            return networkRules.HasChangeParentAuthority(this, localPlayer, asServer);
        }
        
        public bool HasChangeParentAuthority(PlayerID player, bool asServer)
        {
            return networkRules.HasChangeParentAuthority(this, player, asServer);
        }


        public bool HasTransferOwnershipAuthority(bool asServer)
        {
            return networkRules.HasTransferOwnershipAuthority(this, localPlayer, asServer);
        }
        
        public bool HasTransferOwnershipAuthority(PlayerID player, bool asServer)
        {
            return networkRules.HasTransferOwnershipAuthority(this, player, asServer);
        }

        public bool HasGiveOwnershipAuthority(bool asServer)
        {
            return networkRules.HasGiveOwnershipAuthority(this, asServer);
        }
    }
}