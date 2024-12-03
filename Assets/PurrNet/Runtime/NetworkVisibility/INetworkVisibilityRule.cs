using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Pooling;

namespace PurrNet
{
    public class NetworkNodes
    {
        public readonly Dictionary<NetworkID, HashSet<NetworkID>> children = new();
        
        public void Add(NetworkIdentity node)
        {
            var root = node.root;
            
            if (!root || !root.id.HasValue)
                return;

            if (!children.TryGetValue(root.id.Value, out var set))
            {
                set = new HashSet<NetworkID>();
                children[root.id.Value] = set;
            }

            var childrenList = ListPool<NetworkIdentity>.Instantiate();
            root.GetComponentsInChildren(childrenList);

            for (var i = 0; i < childrenList.Count; i++)
            {
                var child = childrenList[i];
                
                if (child.id.HasValue)
                    set.Add(child.id.Value);
            }

            ListPool<NetworkIdentity>.Destroy(childrenList);
        }
    }
    
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
        
        /// <summary>
        /// Who can see the identity?
        /// </summary>
        void GetObservers(List<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity);
    }
}