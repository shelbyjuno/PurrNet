using UnityEngine;

namespace PurrNet
{
    public abstract class NetworkVisibilityRule : ScriptableObject, INetworkVisibilityRule
    {
        protected NetworkManager manager;
        
        public void Setup(NetworkManager nmanager)
        {
            manager = nmanager;
        }

        /// <summary>
        /// Complexity of the rule.
        /// Lower complexity means that the rule will be checked first.
        /// If a rule is cheaper to check, it should have a lower complexity.
        /// </summary>
        public abstract int complexity { get; }
        
        /// <summary>
        /// If the rule is constant, it will be checked only once.
        /// This is useful for rules that are not dependent on the state of the game.
        /// </summary>
        public abstract bool constant { get; }
        
        public abstract bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }
}