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
            bool isVisible = isParentVisible;

            if (!isVisible)
            {
                for (var i = 0; i < identities.Count; i++)
                    identities[i].TryRemoveObserver(player);
                return false;
            }
            
            bool canSee = true;

            for (var i = 0; i < identities.Count; i++)
            {
                var identity = identities[i];
                rules = identity.GetOverrideOrDefault(rules);

                if (rules == null)
                    continue;

                if (identity.owner == player)
                    continue;

                if (identity.whitelist.Contains(player))
                    continue;

                if (identity.blacklist.Contains(player))
                {
                    canSee = false;
                    break;
                }

                if (!rules.CanSee(player, identity))
                {
                    canSee = false;
                    break;
                }
            }

            if (!canSee)
            {
                isVisible = false;

                for (var i = 0; i < identities.Count; i++)
                    identities[i].TryRemoveObserver(player);
            }
            else
            {
                for (var i = 0; i < identities.Count; i++)
                    identities[i].TryAddObserver(player);
            }

            return isVisible;
        }
    }
}