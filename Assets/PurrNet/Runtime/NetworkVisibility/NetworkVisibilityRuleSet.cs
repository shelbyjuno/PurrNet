using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Rule Set", fileName = "New Rule Set")]
    public class NetworkVisibilityRuleSet : ScriptableObject
    {
        [SerializeField] private NetworkVisibilityRule[] _rules;
        
        private readonly List<INetworkVisibilityRule> _raw_rules = new ();
        
        public IReadOnlyList<INetworkVisibilityRule> rules => _raw_rules;

        public void Setup(NetworkManager manager)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                _rules[i].Setup(manager);
                _raw_rules.Add(_rules[i]);
            }
        }
        
        public void AddRule(NetworkManager manager, INetworkVisibilityRule rule)
        {
            if (rule is NetworkVisibilityRule nrule)
                nrule.Setup(manager);
            _raw_rules.Add(rule);
        }

        public void RemoveRule(INetworkVisibilityRule rule)
        {
            _raw_rules.Remove(rule);
        }
        
        public bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            if (_raw_rules == null || _raw_rules.Count == 0)
                return true;

            if (identity.owner == playerId)
                return true;

            for (int i = 0; i < _raw_rules.Count; i++)
            {
                var rule = _raw_rules[i];
                if (!rule.HasVisiblity(playerId, identity))
                    return false;
            }

            return true;
        }
    }
}
