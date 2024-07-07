using System;
using System.Collections.Generic;

namespace PurrNet.Modules
{
    public class HierarchyHistory
    {
        readonly List<HierarchyAction> _actions = new ();
        readonly List<HierarchyAction> _pending = new ();
        
        private readonly List<IOptimizationRule> _optimizationRules = new();
        
        public bool hasUnflushedActions { get; private set; }
        
        public HierarchyHistory()
        {
            //We add all our rules here in order to easily add or remove rules
            _optimizationRules.Add(new DespawnOptimizationRule());
        }
        
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
        
        internal void Clear()
        {
            _actions.Clear();
            _pending.Clear();
        }

        private void OptimizeActions()
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

        /// <summary>
        /// Removes spawning action and despawn action if they are after each other without any other actions in between
        /// Also removes Despawn action if it is the first action
        /// </summary>
        private class DespawnOptimizationRule : IOptimizationRule
        {
            public List<HierarchyAction> Apply(List<HierarchyAction> actions)
            {
                for (var i = 0; i < actions.Count; i++)
                {
                    if (actions[i].type == HierarchyActionType.Spawn && i + 1 < actions.Count && actions[i + 1].type == HierarchyActionType.Despawn)
                    {
                        actions.RemoveAt(i + 1);
                        actions.RemoveAt(i);
                        i--;
                        continue;
                    }
                    
                    if (actions[i].type == HierarchyActionType.Despawn && i == 0)
                    {
                        actions.RemoveAt(i);
                        i--;
                        continue;
                    }
                }
                
                return actions;
            }
        }

    }
}
