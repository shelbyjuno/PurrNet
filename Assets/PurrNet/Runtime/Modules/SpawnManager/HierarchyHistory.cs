using System;
using System.Collections.Generic;
namespace PurrNet.Modules
{
    public class HierarchyHistory
    {
        readonly List<HierarchyAction> _actions = new ();
        readonly List<HierarchyAction> _pending = new ();
        
        readonly SceneID _sceneId;
        
        private readonly List<Prefab> _prefabs = new();
        
        public bool hasUnflushedActions { get; private set; }

        public HierarchyHistory(SceneID sceneId)
        {
            _sceneId = sceneId;
        }
        
        internal HierarchyActionBatch GetFullHistory()
        {
            return new HierarchyActionBatch
            {
                sceneId = _sceneId,
                actions = _actions
            };
        }

        internal HierarchyActionBatch GetDelta()
        {
            return new HierarchyActionBatch
            {
                sceneId = _sceneId,
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
                            continue;

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

        private static NetworkID GetObjectId(HierarchyAction action)
        {
            return action.type switch
            {
                HierarchyActionType.Spawn => action.spawnAction.identityId,
                HierarchyActionType.Despawn => action.despawnAction.identityId,
                HierarchyActionType.ChangeParent => action.changeParentAction.identityId,
                HierarchyActionType.SetActive => action.setActiveAction.identityId,
                HierarchyActionType.SetEnabled => action.setEnabledAction.identityId,
                _ => throw new ArgumentException("Unknown action type")
            };
        }

        private struct Prefab
        {
            public ushort childCount;
            public NetworkID identityId;
            public int despawnedChildren;
            public int spawnActionIndex;

            public bool IsChild(NetworkID id)
            {
                return id >= identityId && id < identityId + childCount;
            }
        }
    }
}
