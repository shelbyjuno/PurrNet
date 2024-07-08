using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public class HierarchyHistory
    {
        readonly List<HierarchyAction> _actions = new ();
        readonly List<HierarchyAction> _pending = new ();
        
        [UsedImplicitly]
        private readonly List<IOptimizationRule> _optimizationRules = new();
        private readonly List<Prefab> _prefabs = new();
        
        public bool hasUnflushedActions { get; private set; }
        
        internal HierarchyActionBatch GetFullHistory()
        {
            return new HierarchyActionBatch
            {
                actions = _actions
            };
        }

        internal HierarchyActionBatch GetDelta()
        {
            return new HierarchyActionBatch
            {
                actions = _pending
            };
        }

        internal void Flush()
        {
            _actions.AddRange(_pending);
            OptimizeActions();
            _pending.Clear();
            hasUnflushedActions = false;
        }

        internal void AddSpawnAction(SpawnAction action)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.Spawn,
                spawnAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddDespawnAction(DespawnAction action)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.Despawn,
                despawnAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddChangeParentAction(ChangeParentAction action)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.ChangeParent,
                changeParentAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddSetActiveAction(SetActiveAction action)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.SetActive,
                setActiveAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddSetEnabledAction(SetEnabledAction action)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.SetEnabled,
                setEnabledAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        private void OptimizeActions()
        {
            CleanupPrefabs();
            RemoveIrrelevantActions();
            
            if(_optimizationRules.Count > 0)
                RunOptimizationRules();
        }

        private void CleanupPrefabs()
        {
            _prefabs.Clear();
            
            for (var i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                switch (action.type)
                {
                    case HierarchyActionType.Spawn:
                    {
                        var prefab = new Prefab
                        {
                            childCount = action.spawnAction.childCount,
                            identityId = action.spawnAction.identityId,
                            spawnActionIndex = i
                        };

                        _prefabs.Add(prefab);
                        break;
                    }
                    case HierarchyActionType.Despawn:
                    {
                        int index = -1;
                        for (int j = 0; j < _prefabs.Count; j++)
                        {
                            if (!_prefabs[j].IsChild(action.despawnAction.identityId))
                                continue;
                            index = j;
                        }

                        if (index == -1)
                        {
                            PurrLogger.LogError($"Despawn action for object {action.despawnAction.identityId} has no corresponding spawn action");
                            continue;
                        }

                        var prefab = _prefabs[index];
                        prefab.despawnedChildren++;
                        _prefabs[index] = prefab;

                        if (prefab.despawnedChildren >= prefab.childCount)
                            i -= RemoveRelevantActions(prefab.spawnActionIndex, i);
                        break;
                    }
                }
            }
        }
        
        private void RemoveIrrelevantActions()
        {
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                var action = _actions[i];
                switch (action.type)
                {
                    case HierarchyActionType.SetActive:
                    {
                        var identityId = action.setActiveAction.identityId;
                        
                        for (int j = i - 1; j >= 0; j--)
                        {
                            var previousAction = _actions[j];
                            if (previousAction.type == HierarchyActionType.SetActive &&
                                previousAction.setActiveAction.identityId == identityId)
                            {
                                _actions.RemoveAt(j);
                                i--;
                            }
                        }
                        
                        break;
                    }
                    case HierarchyActionType.SetEnabled:
                    {
                        var identityId = action.setEnabledAction.identityId;

                        for (int j = i - 1; j >= 0; j--)
                        {
                            var previousAction = _actions[j];
                            if (previousAction.type == HierarchyActionType.SetEnabled &&
                                previousAction.setEnabledAction.identityId == identityId)
                            {
                                _actions.RemoveAt(j);
                                i--;
                            }
                        }
                        
                        break;
                    }
                }
            }
        }

        private int RemoveRelevantActions(int spawnActionIndex, int lastIndex)
        {
            int removed = 0;
            var spawnAction = _actions[spawnActionIndex];
            
            for (int i = spawnActionIndex + 1; i <= lastIndex; i++)
            {
                var spawnIdentityId = spawnAction.spawnAction.identityId;
                var identityId = GetObjectId(_actions[i]);
                
                if (identityId >= spawnIdentityId + spawnAction.spawnAction.childCount || identityId < spawnIdentityId)
                    continue;
                
                _actions.RemoveAt(i);
                removed++;
                lastIndex--;
                i--;
            }
            
            _actions.RemoveAt(spawnActionIndex);
            removed++;
            return removed;
        }

        private void RunOptimizationRules()
        {
            Dictionary<int, List<HierarchyAction>> actionsByObject = new Dictionary<int, List<HierarchyAction>>();
            List<int> actionOrder = new List<int>();

            // Group actions by object ID and maintain overall order
            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                int objectId = GetObjectId(action);
                if (!actionsByObject.TryGetValue(objectId, out var objectActions))
                {
                    objectActions = new List<HierarchyAction>();
                    actionsByObject[objectId] = objectActions;
                }
                objectActions.Add(action);
                actionOrder.Add(objectId);
            }

            // Optimize actions for each object
            Dictionary<int, Queue<HierarchyAction>> optimizedActionsByObject = new Dictionary<int, Queue<HierarchyAction>>();
            foreach (var kvp in actionsByObject)
            {
                optimizedActionsByObject[kvp.Key] = new Queue<HierarchyAction>(OptimizeObjectActions(kvp.Value));
            }

            // Merge optimized actions back into a single list, preserving original order
            List<HierarchyAction> optimizedActions = new List<HierarchyAction>();
            foreach (int objectId in actionOrder)
            {
                if (optimizedActionsByObject.TryGetValue(objectId, out var queue) && queue.Count > 0)
                {
                    optimizedActions.Add(queue.Dequeue());
                }
            }

            _actions.Clear();
            _actions.AddRange(optimizedActions);
        }

        private List<HierarchyAction> OptimizeObjectActions(List<HierarchyAction> actions)
        {
            foreach (var rule in _optimizationRules)
            {
                actions = rule.Apply(actions);
            }

            return actions;
        }

        private int GetObjectId(HierarchyAction action)
        {
            switch (action.type)
            {
                case HierarchyActionType.Spawn: return action.spawnAction.identityId;
                case HierarchyActionType.Despawn: return action.despawnAction.identityId;
                case HierarchyActionType.ChangeParent: return action.changeParentAction.identityId;
                case HierarchyActionType.SetActive: return action.setActiveAction.identityId;
                case HierarchyActionType.SetEnabled: return action.setEnabledAction.identityId;
                default: throw new ArgumentException("Unknown action type");
            }
        }
        
        private interface IOptimizationRule
        {
            List<HierarchyAction> Apply(List<HierarchyAction> actions);
        }

        private struct Prefab
        {
            public int childCount;
            public int identityId;
            public int despawnedChildren;
            public int spawnActionIndex;

            public bool IsChild(int id)
            {
                return id >= identityId && id < identityId + childCount;
            }
        }
    }
}
