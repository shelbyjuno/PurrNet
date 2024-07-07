using System.Collections.Generic;
using PurrNet.Packets;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    internal enum SceneActionType : byte
    {
        Load,
        Unload,
        SetActive
    }
    
    internal partial struct SceneAction : INetworkedData
    {
        public SceneActionType type;
        
        public LoadSceneAction loadSceneAction;
        public UnloadSceneAction unloadSceneAction;
        public SetActiveSceneAction setActiveSceneAction;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref type);
            
            switch (type)
            {
                case SceneActionType.Load:
                    packer.Serialize(ref loadSceneAction);
                    break;
                case SceneActionType.Unload:
                    packer.Serialize(ref unloadSceneAction);
                    break;
                case SceneActionType.SetActive:
                    packer.Serialize(ref setActiveSceneAction);
                    break;
            }
        }
    }
    
    internal partial struct SceneActionsBatch : INetworkedData
    {
        public List<SceneAction> actions;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref actions);
        }
    }
    
    internal partial struct LoadSceneAction : IAutoNetworkedData
    {
        public int buildIndex;
        public SceneID sceneID;
        public PurrSceneSettings parameters; 
        
        public LoadSceneParameters GetLoadSceneParameters()
        {
            return new LoadSceneParameters
            {
                loadSceneMode = parameters.mode,
                localPhysicsMode = parameters.physicsMode
            };
        }
    }
    
    internal partial struct UnloadSceneAction : IAutoNetworkedData
    {
        public SceneID sceneID;
        public UnloadSceneOptions options;
    }
    
    internal partial struct SetActiveSceneAction : IAutoNetworkedData
    {
        public SceneID sceneID;
    }
    
    internal class SceneHistory
    {
        readonly List<SceneAction> _actions = new ();
        readonly List<SceneAction> _pending = new ();
        
        public bool hasUnflushedActions { get; private set; }
        
        internal SceneActionsBatch GetFullHistory()
        {
            return new SceneActionsBatch
            {
                actions = _actions
            };
        }
        
        internal SceneActionsBatch GetDelta()
        {
            return new SceneActionsBatch
            {
                actions = _pending
            };
        }
        
        internal void Flush()
        {
            _actions.AddRange(_pending);
            _pending.Clear();
            hasUnflushedActions = false;
            OptimizeHistory();
        }

        private readonly List<SceneID> _sceneIds = new();

        private void OptimizeHistory()
        {
            _sceneIds.Clear();
            
            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                switch (action.type)
                {
                    case SceneActionType.Load:
                        _sceneIds.Add(action.loadSceneAction.sceneID);
                        break;
                    case SceneActionType.Unload:
                        _sceneIds.Remove(action.unloadSceneAction.sceneID);
                        break;
                }
            }
            
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                var action = _actions[i];
                switch (action.type)
                {
                    case SceneActionType.Load:
                        if (!_sceneIds.Contains(action.loadSceneAction.sceneID))
                            _actions.RemoveAt(i);
                        break;
                    case SceneActionType.Unload:
                        if (!_sceneIds.Contains(action.unloadSceneAction.sceneID))
                            _actions.RemoveAt(i);
                        break;
                    case SceneActionType.SetActive:
                        if (!_sceneIds.Contains(action.setActiveSceneAction.sceneID))
                            _actions.RemoveAt(i);
                        break;
                }
            }
        }
        
        internal void AddLoadAction(LoadSceneAction action)
        {
            _pending.Add(new SceneAction
            {
                type = SceneActionType.Load,
                loadSceneAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddUnloadAction(UnloadSceneAction action)
        {
            _pending.Add(new SceneAction
            {
                type = SceneActionType.Unload,
                unloadSceneAction = action
            });
            
            hasUnflushedActions = true;
        }
        
        internal void AddSetActiveAction(SetActiveSceneAction action)
        {
            _pending.Add(new SceneAction
            {
                type = SceneActionType.SetActive,
                setActiveSceneAction = action
            });
            
            hasUnflushedActions = true;
        }
    }
}
