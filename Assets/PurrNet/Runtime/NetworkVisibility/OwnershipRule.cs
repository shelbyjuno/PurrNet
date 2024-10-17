using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Ownership Vibility")]
    public class OwnershipRule : NetworkVisibilityRule
    {
        public override int complexity => 1;
        
        public override void GetObservers(List<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity)
        {
            var owner = networkIdentity.owner;
            if (owner.HasValue && players.Contains(owner.Value))
                result.Add(owner.Value);
        }
    }
}
