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
        private readonly List<int> _toRemove = new();
        
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
                actions = _actions,
                isDelta = false
            };
        }

        internal HierarchyActionBatch GetDelta()
        {
            return new HierarchyActionBatch
            {
                sceneId = _sceneId,
                actions = _pending,
                isDelta = true
            };
        }

        internal void Flush()
        {
            _actions.AddRange(_pending);
            OptimizeActions();
            _pending.Clear();
            hasUnflushedActions = false;
        }

        internal void AddSpawnAction(SpawnAction action, PlayerID actor)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.Spawn,
                actor = actor,
                spawnAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddDespawnAction(DespawnAction action, PlayerID actor)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.Despawn,
                actor = actor,
                despawnAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddChangeParentAction(ChangeParentAction action, PlayerID actor)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.ChangeParent,
                actor = actor,
                changeParentAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddSetActiveAction(SetActiveAction action, PlayerID actor)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.SetActive,
                actor = actor,
                setActiveAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddSetEnabledAction(SetEnabledAction action, PlayerID actor)
        {
            _pending.Add(new HierarchyAction
            {
                type = HierarchyActionType.SetEnabled,
                actor = actor,
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
            
            for (var i = 0; i < _actions.Count; ++i)
            {
                var action = _actions[i];
                switch (action.type)
                {
                    case HierarchyActionType.Spawn:
                    {
                        var prefab = new Prefab
                        {
                            childCount = action.spawnAction.childCount,
                            identityId = action.spawnAction.identityId
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
                            break;
                        }

                        if (index == -1)
                            continue;

                        var prefab = _prefabs[index];
                        prefab.despawnedChildren++;
                        _prefabs[index] = prefab;
                        break;
                    }
                }
            }

            for (var i = 0; i < _prefabs.Count; ++i)
            {
                var prefab = _prefabs[i];
                
                if (prefab.despawnedChildren == prefab.childCount)
                    RemoveRelevantActions(prefab);
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

        private void RemoveRelevantActions(Prefab prefab)
        {
            _toRemove.Clear();
            
            for (int i = 0; i < _actions.Count; i++)
            {
                var identityId = GetObjectId(_actions[i]);

                if (prefab.IsChild(identityId))
                {
                    _toRemove.Add(i);
                }
            }
            
            for (int i = _toRemove.Count - 1; i >= 0; i--)
            {
                _actions.RemoveAt(_toRemove[i]);
            }
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

            public bool IsChild(NetworkID id)
            {
                if (id.scope != identityId.scope)
                    return false;
                
                return id.id >= identityId.id && id.id < identityId.id + childCount;
            }
        }

        public void Clear()
        {
            _actions.Clear();
            _pending.Clear();
            hasUnflushedActions = false;
        }
    }
}
