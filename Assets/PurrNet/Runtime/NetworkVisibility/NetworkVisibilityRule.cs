using System.Collections.Generic;
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
        /// The higher the complexity the later the rule will be checked.
        /// We will prioritize rules with lower complexity first.
        /// </summary>
        public abstract int complexity { get; }
        
        /// <summary>
        /// Who can see the identity?
        /// </summary>
        /// <param name="result">The list of players that can see the, it should always be a subset of players</param>
        /// <param name="players">The set of all players to check visibility for</param>
        /// <param name="networkIdentity">The identity to check</param>
        public abstract void GetObservers(List<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity);
    }
}