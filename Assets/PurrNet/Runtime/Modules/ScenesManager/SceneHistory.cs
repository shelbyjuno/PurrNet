using System.Collections.Generic;
using PurrNet.Packets;

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
        public int sceneID;
    }
    
    internal partial struct UnloadSceneAction : IAutoNetworkedData
    {
        public int sceneID;
    }
    
    internal partial struct SetActiveSceneAction : IAutoNetworkedData
    {
        public int sceneID;
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
