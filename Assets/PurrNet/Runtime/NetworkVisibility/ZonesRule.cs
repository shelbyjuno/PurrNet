using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Zones Rule")]
    public class ZonesRule : NetworkVisibilityRule
    {
        public override int complexity => 0;

        public override bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            return true;
        }
    }
}
