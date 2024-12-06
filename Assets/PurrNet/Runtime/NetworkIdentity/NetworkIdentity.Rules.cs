using System.Collections.Generic;
using JetBrains.Annotations;
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
        [UsedImplicitly]
        public readonly HashSet<PlayerID> whitelist = new();
        
        /// <summary>
        /// Blacklist of players that can't interact with this identity.
        /// </summary>
        [UsedImplicitly]
        public readonly HashSet<PlayerID> blacklist = new();

        private NetworkRules networkRules => _networkRules ? _networkRules : networkManager.networkRules;
        
        [UsedImplicitly]
        public NetworkVisibilityRuleSet visibilityRules => _visitiblityRules ? _visitiblityRules : networkManager.visibilityRules;

        public NetworkVisibilityRuleSet GetOverrideOrDefault(NetworkVisibilityRuleSet defaultValue)
        {
            return _visitiblityRules ? _visitiblityRules : defaultValue;
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
        
        public bool ShouldDespawnOnOwnerDisconnect()
        {
            var rules = networkRules;
            return rules && rules.ShouldDespawnOnOwnerDisconnect();
        }
        
        public NetworkRules GetNetworkRules(NetworkManager manager)
        {
            return _networkRules ? _networkRules : manager.networkRules;
        }
        
        public bool ShouldClientGiveOwnershipOnSpawn(NetworkManager manager)
        {
            var rules = GetNetworkRules(manager);
            return rules && rules.ShouldClientGiveOwnershipOnSpawn();
        }

        public bool ShouldPlayRPCsWhenDisabled()
        {
            var rules = networkRules;
            return rules && rules.ShouldPlayRPCsWhenDisabled();
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
        
        public bool ShouldPropagateToChildren()
        {
            var rules = networkRules;
            return rules && rules.ShouldPropagateToChildren();
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
        
        public bool HasRemoveOwnershipAuthority(PlayerID player, bool asServer)
        {
            var rules = networkRules;
            return rules && rules.HasRemoveOwnershipAuthority(this, player, asServer);
        }
    }
}