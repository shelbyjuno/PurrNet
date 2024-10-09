using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Rule Set", fileName = "New Rule Set")]
    public class NetworkVisibilityRuleSet : ScriptableObject
    {
        [SerializeField] private NetworkVisibilityRule[] _rules;
        
        private readonly List<INetworkVisibilityRule> _raw_rules = new ();
        
        private bool _initialized;

        public void Setup(NetworkManager manager)
        {
            if (_initialized)
                return;
            
            _initialized = true;
            
            for (int i = 0; i < _rules.Length; i++)
            {
                _rules[i].Setup(manager);
                _raw_rules.Add(_rules[i]);
            }
            
            _raw_rules.Sort((a, b) => 
                a.complexity.CompareTo(b.complexity));
        }
        
        public void AddRule(NetworkManager manager, INetworkVisibilityRule rule)
        {
            if (rule is NetworkVisibilityRule nrule)
                nrule.Setup(manager);
            
            // insert the rule in the correct order
            for (int i = 0; i < _raw_rules.Count; i++)
            {
                if (_raw_rules[i].complexity > rule.complexity)
                {
                    _raw_rules.Insert(i, rule);
                    return;
                }
            }
        }

        public void RemoveRule(INetworkVisibilityRule rule)
        {
            _raw_rules.Remove(rule);
        }
        
        public bool HasVisiblity(PlayerID playerId, NetworkIdentity identity)
        {
            if (_raw_rules == null || _raw_rules.Count == 0)
            {
                PurrLogger.LogWarning("No visibility rules found, defaulting to true");
                return true;
            }

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
