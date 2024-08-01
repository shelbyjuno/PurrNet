namespace PurrNet
{
    public partial class NetworkIdentity
    {
        public bool HasDespawnAuthority(PlayerID player)
        {
            return networkManager.networkRules.HasDespawnAuthority(this, player);
        }
        
        public bool HasSpawnAuthority()
        {
            return networkManager.networkRules.HasSpawnAuthority(this);
        }
        
        public bool HasSetActiveAuthority(PlayerID player)
        {
            return networkManager.networkRules.HasSetActiveAuthority(this, player);
        }
        
        public bool HasSetEnabledAuthority(PlayerID player)
        {
            return networkManager.networkRules.HasSetEnabledAuthority(this, player);
        }
        
        public bool ShouldSyncParent()
        {
            return networkManager.networkRules.ShouldSyncParent(this);
        }
        
        public bool HasChangeParentAuthority()
        {
            return networkManager.networkRules.HasChangeParentAuthority(this, localPlayer);
        }
    }
}