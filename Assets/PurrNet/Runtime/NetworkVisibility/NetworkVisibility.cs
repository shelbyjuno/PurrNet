using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public interface INetworkVisibilityRule
    {
        bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }

    public abstract class NetworkVisibilityRule : MonoBehaviour, INetworkVisibilityRule
    {
        public abstract bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }

    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkVisibility : MonoBehaviour
    {
        [SerializeField] private List<NetworkVisibilityRule> _rules = new ();

        private List<INetworkVisibilityRule> _raw_rules = new ();

        private void Awake()
        {
            _raw_rules.AddRange(_rules);
        }

        public void AddRule(INetworkVisibilityRule rule)
        {
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
                INetworkVisibilityRule rule = _raw_rules[i];
                if (!rule.HasVisiblity(playerId, identity))
                    return false;
            }

            return true;
        }
    }
}
