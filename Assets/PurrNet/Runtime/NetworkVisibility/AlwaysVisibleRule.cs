using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Always Visible")]
    public class AlwaysVisibleRule : NetworkVisibilityRule
    {
        public override int complexity => 0;
        
        public override bool constant => true;

        public override bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            return true;
        }
    }
}