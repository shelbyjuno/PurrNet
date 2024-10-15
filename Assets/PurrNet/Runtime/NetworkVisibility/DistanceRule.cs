using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/DistanceRule")]
    public class DistanceRule : NetworkVisibilityRule
    {
        [SerializeField] private float _distance = 30f;

        public override int complexity => 100;
        
        public override void GetObservedIdentities(IList<NetworkCluster> result, ISet<NetworkCluster> scope, PlayerID playerId)
        {
            foreach(var rootIdentity in scope)
            {
                if (result.Contains(rootIdentity))
                    continue;

                foreach (var playerIdentity in manager.EnumerateAllPlayerOwnedIds(playerId, true))
                {
                    var playerPos = playerIdentity.transform.position;
                    
                    for (var childIdx = 0; childIdx < rootIdentity.children.Count; childIdx++)
                    {
                        var childIdentity = rootIdentity.children[childIdx];
                        if (childIdentity.owner == playerId)
                        {
                            result.Add(rootIdentity);
                            break;
                        }
                        
                        var distance = Vector3.Distance(playerPos, childIdentity.transform.position);

                        if (distance <= _distance)
                        {
                            result.Add(rootIdentity);
                            break;
                        }
                    }
                }
            }
        }

        public override void GetObservers(IList<PlayerID> result, ISet<PlayerID> players, NetworkIdentity networkIdentity)
        {
            var myPos = networkIdentity.transform.position;

            foreach(var player in players)
            {
                foreach (var playerIdentity in manager.EnumerateAllPlayerOwnedIds(player, true))
                {
                    var playerPos = playerIdentity.transform.position;
                    var distance = Vector3.Distance(myPos, playerPos);

                    if (distance <= _distance)
                    {
                        result.Add(player);
                        break;
                    }
                }
            }
        }
    }
}