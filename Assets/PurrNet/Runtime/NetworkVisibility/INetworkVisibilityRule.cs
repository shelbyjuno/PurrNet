using System;
using System.Collections.Generic;

namespace PurrNet
{
    public readonly struct NetworkCluster : IEquatable<NetworkCluster>
    {
        public readonly NetworkID firstId;
        public readonly List<NetworkIdentity> children;
        
        public NetworkCluster(List<NetworkIdentity> children)
        {
            if (children.Count == 0)
                throw new ArgumentException("Cluster must have at least one child");
            
            var first = children[0];
            
            if (first.id == null)
                throw new ArgumentException("Child must have an id");
            
            firstId = first.id.Value;
            this.children = children;
        }

        public bool Equals(NetworkCluster other)
        {
            return Equals(firstId, other.firstId);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkCluster other && Equals(other);
        }

        public override int GetHashCode()
        {
            return firstId.GetHashCode();
        }
    }
    
    public interface INetworkVisibilityRule
    {
        /// <summary>
        /// The higher the complexity the later the rule will be checked.
        /// We will prioritize rules with lower complexity first.
        /// </summary>
        int complexity { get; }
        
        bool? hardCodedValue { get; }
        
        /// <summary>
        /// What can the player see?
        /// </summary>
        void GetObservedIdentities(IList<NetworkCluster> result, ISet<NetworkCluster> scope, PlayerID playerId);

        /// <summary>
        /// Who can see the identity?
        /// </summary>
        void GetObservers(IList<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity);
    }
}