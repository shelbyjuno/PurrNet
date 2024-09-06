namespace PurrNet
{
    public interface INetworkVisibilityRule
    {
        bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }
}