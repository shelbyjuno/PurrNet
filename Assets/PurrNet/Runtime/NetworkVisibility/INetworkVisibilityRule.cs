namespace PurrNet
{
    public interface INetworkVisibilityRule
    {
        /// <summary>
        /// The higher the complexity the later the rule will be checked.
        /// We will prioritize rules with lower complexity first.
        /// </summary>
        int complexity { get; }
        
        bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }
}