using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/No Visiblity")]
    public class NoVisibilityRule : NetworkVisibilityRule
    {
        public override int complexity => 0;
        
        public override bool constant => true;

        public override bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            return false;
        }
    }
}
