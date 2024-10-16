using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Always Visible")]
    public class AlwaysVisibleRule : NetworkVisibilityRule
    {
        public override int complexity => 0;

        public override void GetObservers(List<PlayerID> result, ISet<PlayerID> players,
            NetworkIdentity networkIdentity)
        {
            result.AddRange(players);
        }
    }
}