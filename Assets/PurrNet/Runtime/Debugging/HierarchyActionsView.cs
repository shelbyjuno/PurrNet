using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class HierarchyActionsView : MonoBehaviour
    {
        [SerializeField] private NetworkManager _manager;

        public string GetActions()
        {
            if (!_manager)
                return string.Empty;
            
            if(!_manager.TryGetModule<ScenesModule>(_manager.isServer, out var scenes))
                return string.Empty;
                        
            if(!_manager.TryGetModule<HierarchyModule>(_manager.isServer, out var history))
                return string.Empty;
            
            return scenes.TryGetSceneID(gameObject.scene, out var sceneId) ? history.GetActionsAsString(sceneId) : string.Empty;
        }

        private void Reset()
        {
            _manager = GetComponent<NetworkManager>();
        }
    }
}
