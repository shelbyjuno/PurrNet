using System.Collections.Generic;

namespace PurrNet
{
    public class HierarchyHistory
    {
        readonly List<HierarchyAction> _actions = new ();
        readonly List<HierarchyAction> _pending = new ();
        
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
    }
}
