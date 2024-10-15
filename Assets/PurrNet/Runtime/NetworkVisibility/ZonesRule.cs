using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Zones Rule")]
    public class ZonesRule : NetworkVisibilityRule
    {
        public override int complexity => 0;
        
        public override bool? hardCodedValue => false;

        public override void GetObservedIdentities(IList<NetworkCluster> result, ISet<NetworkCluster> scope, PlayerID playerId) 
            => throw new System.NotImplementedException();

        public override void GetObservers(IList<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity) 
            => throw new System.NotImplementedException();
    }
}
