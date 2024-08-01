using PlasticGui.WorkspaceWindow.Home.Repositories;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        public bool HasDespawnAuthority(PlayerID player, bool asServer)
        {
            return networkManager.networkRules.HasDespawnAuthority(this, player, asServer);
        }
        
        public bool HasSpawnAuthority(bool asServer)
        {
            return networkManager.networkRules.HasSpawnAuthority(this, asServer);
        }
        
        public bool HasSetActiveAuthority(PlayerID player, bool asServer)
        {
            return networkManager.networkRules.HasSetActiveAuthority(this, player, asServer);
        }
        
        public bool HasSetActiveAuthority(bool asServer)
        {
            return networkManager.networkRules.HasSetActiveAuthority(this, localPlayer, asServer);
        }
        
        public bool HasSetEnabledAuthority(PlayerID player, bool asServer)
        {
            return networkManager.networkRules.HasSetEnabledAuthority(this, player, asServer);
        }
        
        public bool HasSetEnabledAuthority(bool asServer)
        {
            return networkManager.networkRules.HasSetEnabledAuthority(this, localPlayer, asServer);
        }

        public bool ShouldSyncParent(bool asServer)
        {
            return networkManager.networkRules.ShouldSyncParent(this, asServer);
        }
        
        public bool ShouldSyncSetActive(bool asServer)
        {
            return networkManager.networkRules.ShouldSyncSetActive(this, asServer);
        }
        
        public bool ShouldSyncSetEnabled(bool asServer)
        {
            return networkManager.networkRules.ShouldSyncSetEnabled(this, asServer);
        }
        
        public bool HasChangeParentAuthority(bool asServer)
        {
            return networkManager.networkRules.HasChangeParentAuthority(this, localPlayer, asServer);
        }
    }
}