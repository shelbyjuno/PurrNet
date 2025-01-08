using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public static class SceneObjectsModule
    {
        private static readonly List<NetworkIdentity> _sceneIdentities = new List<NetworkIdentity>();
        
        public static void GetSceneIdentities(Scene scene, List<NetworkIdentity> networkIdentities)
        {
            var rootGameObjects = scene.GetRootGameObjects();

            foreach (var rootObject in rootGameObjects)
            {
                rootObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;
                
                rootObject.MakeSureAwakeIsCalled();
                networkIdentities.AddRange(_sceneIdentities);
            }
        }
        
        public static List<NetworkIdentity> GetSceneIdentities(Scene scene)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            var networkIdentities = new List<NetworkIdentity>();

            foreach (var rootObject in rootGameObjects)
            {
                rootObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;
                
                rootObject.MakeSureAwakeIsCalled();
                networkIdentities.AddRange(_sceneIdentities);
            }

            return networkIdentities;
        }
    }
}
