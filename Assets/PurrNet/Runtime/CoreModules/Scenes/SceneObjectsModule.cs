using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public static class SceneObjectsModule
    {
        private static readonly List<NetworkIdentity> _sceneIdentities = new List<NetworkIdentity>();
        
        struct GameObjectWithHash
        {
            public GameObject gameObject;
            public uint hash;
        }
        
        public static void GetSceneIdentities(Scene scene, List<NetworkIdentity> networkIdentities)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            var gameObjectsWithHash = new GameObjectWithHash[rootGameObjects.Length];
            
            for (var i = 0; i < rootGameObjects.Length; i++)
            {
                var rootObject = rootGameObjects[i];
                var hash = GameObjectHasher.ComputeHashRecursive(rootObject);
                gameObjectsWithHash[i] = new GameObjectWithHash
                {
                    gameObject = rootObject,
                    hash = hash
                };
            }
            
            Array.Sort(gameObjectsWithHash, (a, b) => 
            {
                // First compare by name
                int nameComparison = string.Compare(
                    a.gameObject.name,
                    b.gameObject.name, 
                    StringComparison.Ordinal
                );
    
                // If names are equal, then compare by hash
                if (nameComparison == 0)
                {
                    return a.hash.CompareTo(b.hash);
                }
    
                return nameComparison;
            });

            foreach (var rootObject in gameObjectsWithHash)
            {
                rootObject.gameObject.GetComponentsInChildren(true, _sceneIdentities);
                
                if (_sceneIdentities.Count == 0) continue;
                
                rootObject.gameObject.MakeSureAwakeIsCalled();
                networkIdentities.AddRange(_sceneIdentities);
            }
        }
    }
}
