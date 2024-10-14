using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/DistanceRule")]
    public class DistanceRule : NetworkVisibilityRule
    {
        [SerializeField] private float _distance = 30f;

        public override int complexity => 100;
        
        public override bool constant => false;

        public override bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            if (identity.owner == playerId)
                return true;
            
            var ownedIds = manager.EnumerateAllPlayerOwnedIds(playerId, true);
            var myPos = identity.transform.position;
            
            foreach (var id in ownedIds)
            {
                var distance = Vector3.Distance(myPos, id.transform.position);
                
                if (distance <= _distance)
                    return true;
            }
            
            return false;
        }
    }
}