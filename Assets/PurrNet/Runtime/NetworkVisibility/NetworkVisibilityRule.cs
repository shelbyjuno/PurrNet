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
        
        public virtual bool? hardCodedValue => null;
        
        /// <summary>
        /// What can the player see?
        /// </summary>
        public abstract void GetObservedIdentities(IList<NetworkCluster> result, ISet<NetworkCluster> scope, PlayerID playerId);
        
        /// <summary>
        /// Who can see the identity?
        /// </summary>
        public abstract void GetObservers(IList<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity);
    }
}