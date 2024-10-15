using System.Collections.Generic;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(menuName = "PurrNet/NetworkVisibility/Rule Set", fileName = "New Rule Set")]
    public class NetworkVisibilityRuleSet : ScriptableObject
    {
        [SerializeField] private NetworkVisibilityRule[] _rules;
        
        private readonly List<INetworkVisibilityRule> _raw_rules = new ();

        public bool isInitialized { get; private set; }

        public void Setup(NetworkManager manager)
        {
            if (isInitialized)
                return;
            
            isInitialized = true;
            
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

        public void GetObservedIdentities(List<NetworkCluster> result, HashSet<NetworkCluster> scope, PlayerID playerId)
        {
            using var tmpPool = new DisposableHashSet<NetworkCluster>(scope.Count);
            tmpPool.UnionWith(scope);
            
            for (int i = 0; i < _raw_rules.Count; i++)
            {
                var rule = _raw_rules[i];
                
                if (rule.hardCodedValue == true)
                {
                    result.AddRange(tmpPool);
                    break;
                }
                
                var resultTmp = ListPool<NetworkCluster>.Instantiate();
                _raw_rules[i].GetObservedIdentities(resultTmp, tmpPool, playerId);

                tmpPool.ExceptWith(resultTmp);

                result.AddRange(resultTmp);
                ListPool<NetworkCluster>.Destroy(resultTmp);
            }
        }

        public void GetObservers(List<PlayerID> result, HashSet<PlayerID> players,
            NetworkIdentity networkIdentity)
        {
            using var tmpPool = new DisposableHashSet<PlayerID>(players.Count);
            tmpPool.UnionWith(players);
            
            for (int i = 0; i < _raw_rules.Count; i++)
            {
                var rule = _raw_rules[i];
                
                if (rule.hardCodedValue == true)
                {
                    result.AddRange(tmpPool);
                    break;
                }
                
                var resultTmp = ListPool<PlayerID>.Instantiate();
                _raw_rules[i].GetObservers(resultTmp, tmpPool, networkIdentity);

                tmpPool.ExceptWith(resultTmp);
                
                result.AddRange(resultTmp);
                ListPool<PlayerID>.Destroy(resultTmp);
            }
        }
    }
}
