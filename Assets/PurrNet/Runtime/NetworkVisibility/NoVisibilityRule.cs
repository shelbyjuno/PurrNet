using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/NoVisibilityRule")]
    public class NoVisibilityRule : NetworkVisibilityRule
    {
        public override int complexity => 0;

        public override bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            return false;
        }
    }
}
