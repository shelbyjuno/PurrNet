using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        [SerializeField, HideInInspector] private NetworkRules _networkRules;
        [SerializeField, HideInInspector] private NetworkVisibilityRuleSet _visitiblityRules;

        private NetworkRules networkRules => _networkRules ? _networkRules : networkManager.networkRules;
        
        public NetworkVisibilityRuleSet visibilityRules => _visitiblityRules ? _visitiblityRules : networkManager.visibilityRules;
        
        public bool HasDespawnAuthority(PlayerID player, bool asServer)
        {
            return networkRules && networkRules.HasDespawnAuthority(this, player, asServer);
        }
        
        public bool HasSpawnAuthority(NetworkManager manager, bool asServer)
        {
            var rules = _networkRules ? manager.networkRules : _networkRules;
            
            return rules && rules.HasSpawnAuthority(manager, asServer);
        }
        
        public bool HasSetActiveAuthority(PlayerID player, bool asServer)
        {
            return networkRules && networkRules.HasSetActiveAuthority(this, player, asServer);
        }
        
        public bool HasSetActiveAuthority(bool asServer)
        {
            return networkRules && networkRules.HasSetActiveAuthority(this, localPlayer, asServer);
        }
        
        public bool HasSetEnabledAuthority(PlayerID player, bool asServer)
        {
            return networkRules && networkRules.HasSetEnabledAuthority(this, player, asServer);
        }
        
        public bool HasSetEnabledAuthority(bool asServer)
        {
            return networkRules && networkRules.HasSetEnabledAuthority(this, localPlayer, asServer);
        }

        public bool ShouldSyncParent(bool asServer)
        {
            return networkRules && networkRules.ShouldSyncParent(this, asServer);
        }
        
        public bool ShouldSyncSetActive(bool asServer)
        {
            return networkRules && networkRules.ShouldSyncSetActive(this, asServer);
        }
        
        public bool ShouldSyncSetEnabled(bool asServer)
        {
            return networkRules && networkRules.ShouldSyncSetEnabled(this, asServer);
        }
        
        public bool ShouldPropagateToChildren(bool asServer)
        {
            return networkRules && networkRules.ShouldPropagateToChildren(this, asServer);
        }
        
        public bool ShouldOverrideExistingOwnership(bool asServer)
        {
            return networkRules && networkRules.ShouldOverrideExistingOwnership(this, asServer);
        }
        
        public bool HasPropagateOwnershipAuthority(bool asServer)
        {
            return networkRules && networkRules.HasPropagateOwnershipAuthority(this, asServer);
        }
        
        public bool HasChangeParentAuthority(bool asServer)
        {
            return networkRules && networkRules.HasChangeParentAuthority(this, localPlayer, asServer);
        }
        
        public bool HasChangeParentAuthority(PlayerID player, bool asServer)
        {
            return networkRules && networkRules.HasChangeParentAuthority(this, player, asServer);
        }


        public bool HasTransferOwnershipAuthority(bool asServer)
        {
            return networkRules && networkRules.HasTransferOwnershipAuthority(this, localPlayer, asServer);
        }
        
        public bool HasTransferOwnershipAuthority(PlayerID player, bool asServer)
        {
            return networkRules && networkRules.HasTransferOwnershipAuthority(this, player, asServer);
        }

        public bool HasGiveOwnershipAuthority(bool asServer)
        {
            return networkRules && networkRules.HasGiveOwnershipAuthority(this, asServer);
        }
    }
}