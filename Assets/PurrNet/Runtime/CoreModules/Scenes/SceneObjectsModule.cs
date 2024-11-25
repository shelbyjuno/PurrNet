using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public static class SceneObjectsModule
    {
        private static readonly List<NetworkIdentity> _sceneIdentities = new();
        
        private static readonly HashSet<int> _markedScenes = new();

        public static void ClearScene(Scene scene)
        {
            _markedScenes.Remove(scene.handle);
        }
        
        public static void MarkSceneIdentities(Scene scene)
        {
            if (!_markedScenes.Add(scene.handle)) 
                return;
            
            var rootGameObjects = scene.GetRootGameObjects();
            
            foreach (var rootObject in rootGameObjects)
            {
                rootObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;

                foreach (var netid in _sceneIdentities)
                    netid.MarkForDelayedAutoSpawn();
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
                
                HierarchyScene.MakeSureAwakeIsCalled(rootObject);
                networkIdentities.AddRange(_sceneIdentities);
            }

            return networkIdentities;
        }
    }
}
