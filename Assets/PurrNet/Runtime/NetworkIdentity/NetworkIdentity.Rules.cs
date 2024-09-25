using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        [SerializeField, HideInInspector] private NetworkRules _networkRules;
        [SerializeField, HideInInspector] private NetworkVisibilityRuleSet _visitiblityRules;

        /// <summary>
        /// Whitelist of players that can interact with this identity.
        /// This doesn't block visibility for others but rather enforces visibility for these players.
        /// </summary>
        public readonly HashSet<PlayerID> whitelist = new();
        
        /// <summary>
        /// Blacklist of players that can't interact with this identity.
        /// </summary>
        public readonly HashSet<PlayerID> blacklist = new();

        private NetworkRules networkRules => _networkRules ? _networkRules : networkManager.networkRules;
        
        private NetworkVisibilityRuleSet visibilityRules => _visitiblityRules ? _visitiblityRules : networkManager.visibilityRules;

        public bool HasVisibility(PlayerID playerId, NetworkIdentity identity)
        {
            if (blacklist.Contains(playerId))
                return false;
            
            if (whitelist.Contains(playerId))
                return true;
            
            var rules = visibilityRules;
            return rules && rules.HasVisiblity(playerId, identity);
        }
        
        public bool HasDespawnAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && networkRules.HasDespawnAuthority(this, player, asServer);
        }
        
        public bool HasSpawnAuthority(NetworkManager manager, bool asServer)
        {
            var rules = _networkRules ? _networkRules : manager.networkRules;
            return rules && rules.HasSpawnAuthority(manager, asServer);
        }
        
        public bool HasSetActiveAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasSetActiveAuthority(this, player, asServer);
        }
        
        public bool HasSetActiveAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasSetActiveAuthority(this, localPlayer, asServer);
        }
        
        public bool HasSetEnabledAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasSetEnabledAuthority(this, player, asServer);
        }
        
        public bool HasSetEnabledAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasSetEnabledAuthority(this, localPlayer, asServer);
        }

        public bool ShouldSyncParent(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.ShouldSyncParent(this, asServer);
        }
        
        public bool ShouldSyncSetActive(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.ShouldSyncSetActive(this, asServer);
        }
        
        public bool ShouldSyncSetEnabled(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.ShouldSyncSetEnabled(this, asServer);
        }
        
        public bool ShouldPropagateToChildren(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.ShouldPropagateToChildren(this, asServer);
        }
        
        public bool ShouldOverrideExistingOwnership(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.ShouldOverrideExistingOwnership(this, asServer);
        }
        
        public bool HasPropagateOwnershipAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasPropagateOwnershipAuthority(this, asServer);
        }
        
        public bool HasChangeParentAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasChangeParentAuthority(this, localPlayer, asServer);
        }
        
        public bool HasChangeParentAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasChangeParentAuthority(this, player, asServer);
        }


        public bool HasTransferOwnershipAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasTransferOwnershipAuthority(this, localPlayer, asServer);
        }
        
        public bool HasTransferOwnershipAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasTransferOwnershipAuthority(this, player, asServer);
        }

        public bool HasGiveOwnershipAuthority(bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasGiveOwnershipAuthority(this, asServer);
        }
    }
}