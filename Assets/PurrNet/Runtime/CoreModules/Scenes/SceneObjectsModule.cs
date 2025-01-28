using System;
using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public static class SceneObjectsModule
    {
        private static readonly List<NetworkIdentity> _sceneIdentities = new List<NetworkIdentity>();
        
        public static void GetSceneIdentities(Scene scene, List<NetworkIdentity> networkIdentities)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            
            PurrSceneInfo sceneInfo = null;
            
            
            foreach (var rootObject in rootGameObjects)
            {
                if (rootObject.TryGetComponent<PurrSceneInfo>(out var si))
                {
                    sceneInfo = si;
                    break;
                }
            }
            
            if (sceneInfo)
                rootGameObjects = sceneInfo.rootGameObjects.ToArray();
            
            foreach (var rootObject in rootGameObjects)
            {
                rootObject.gameObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;
                
                rootObject.gameObject.MakeSureAwakeIsCalled();
                networkIdentities.AddRange(_sceneIdentities);
            }
        }
    }
}
