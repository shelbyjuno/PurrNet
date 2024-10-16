using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/DistanceRule")]
    public class DistanceRule : NetworkVisibilityRule
    {
        [SerializeField] private float _distance = 30f;

        public override int complexity => 100;
        
        public override void GetObservers(List<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity)
        {
            var myPos = networkIdentity.transform.position;

            foreach(var player in players)
            {
                foreach (var playerIdentity in manager.EnumerateAllPlayerOwnedIds(player, true))
                {
                    var playerPos = playerIdentity.transform.position;
                    var distance = Vector3.Distance(myPos, playerPos);

                    if (!(distance <= _distance)) continue;
                    result.Add(player);
                    break;
                }
            }
        }
    }
}