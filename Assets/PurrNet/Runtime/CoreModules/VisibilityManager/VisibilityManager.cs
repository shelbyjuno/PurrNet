using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class VisibilityManager : INetworkModule
    {
        private readonly SceneID _sceneId;
        
        public VisibilityManager(SceneID sceneId)
        {
            _sceneId = sceneId;
        }
        
        public void Enable(bool asServer)
        {
            // Debug.Log("VisibilityManager Enabled " + _sceneId + " " + asServer);
        }

        public void Disable(bool asServer)
        {
            // Debug.Log("VisibilityManager Disabled " + _sceneId + " " + asServer);
        }
    }
}
