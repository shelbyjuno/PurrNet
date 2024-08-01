namespace PurrNet
{
    public partial class NetworkIdentity
    {
        public bool HasDespawnAuthority(PlayerID player)
        {
            return networkManager.networkRules.HasDespawnAuthority(this, player);
        }
    }
}