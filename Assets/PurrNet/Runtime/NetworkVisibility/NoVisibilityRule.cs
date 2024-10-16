using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/No Visiblity")]
    public class NoVisibilityRule : NetworkVisibilityRule
    {
        public override int complexity => 0;

        public override void GetObservers(List<PlayerID> result, ISet<PlayerID> players,
            NetworkIdentity networkIdentity)
        {
            result.Clear();
        }
    }
}
