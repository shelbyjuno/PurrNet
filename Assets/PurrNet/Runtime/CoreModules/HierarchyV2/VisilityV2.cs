using System.Collections.Generic;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet.Modules
{
    internal class VisilityV2
    {
        readonly NetworkVisibilityRuleSet _defaultRuleSet;
        
        public VisilityV2(NetworkManager manager)
        {
            _defaultRuleSet = manager.visibilityRules;
        }
        
        public void RefreshVisibilityForGameObject(PlayerID player, Transform transform)
        {
            if (!transform)
                return;
            
            RefreshVisibilityForGameObject(player, transform, _defaultRuleSet, true);
        }
        
        private static void RefreshVisibilityForGameObject(PlayerID player, Transform transform, NetworkVisibilityRuleSet rules, bool isParentVisible)
        {
            using var identities = new DisposableList<NetworkIdentity>(16);
            using var directChildren = new DisposableList<TransformIdentityPair>(16);

            transform.GetComponents(identities.list);
            
            var isVisible = Evaluate(player, identities.list, ref rules, isParentVisible);
            HierarchyPool.GetDirectChildren(transform.transform, directChildren);
            
            for (var i = 0; i < directChildren.list.Count; i++)
            {
                var pair = directChildren.list[i];
                RefreshVisibilityForGameObject(player, pair.transform, rules, isVisible);
            }
        }

        /// <summary>
        /// Evaluate visibility of the object.
        /// Also adds/removes observers based on the visibility.
        /// </summary>
        private static bool Evaluate(PlayerID player, List<NetworkIdentity> identities, ref NetworkVisibilityRuleSet rules, bool isParentVisible)
        {
            if (!isParentVisible)
            {
                for (var i = 0; i < identities.Count; i++)
                    identities[i].TryRemoveObserver(player);
                return false;
            }
            
            bool isAnyVisible = false;

            for (var i = 0; i < identities.Count; i++)
            {
                var identity = identities[i];
                var r = identity.GetOverrideOrDefault(rules);

                if (r.childrenInherit)
                    rules = r;

                if (r == null)
                {
                    isAnyVisible = true;
                    identity.TryAddObserver(player);
                    continue;
                }

                if (identity.owner == player)
                {
                    isAnyVisible = true;
                    identity.TryAddObserver(player);
                    continue;
                }

                if (identity.whitelist.Contains(player))
                {
                    isAnyVisible = true;
                    identity.TryAddObserver(player);
                    continue;
                }

                if (identity.blacklist.Contains(player))
                {
                    identity.TryRemoveObserver(player);
                    continue;
                }

                if (!r.CanSee(player, identity))
                    identity.TryRemoveObserver(player);
            }
            
            return isAnyVisible;
        }
    }
}