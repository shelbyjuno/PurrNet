using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public static class SceneObjectsModule
    {
        private static readonly List<NetworkIdentity> _sceneIdentities = new();
        
        public static List<NetworkIdentity> GetSceneIdentities(Scene scene)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            var networkIdentities = new List<NetworkIdentity>();

            foreach (var rootObject in rootGameObjects)
            {
                rootObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;
                
                HierarchyScene.MakeSureAwakeIsCalled(rootObject);
                networkIdentities.AddRange(_sceneIdentities);
            }

            return networkIdentities;
        }
    }
}
