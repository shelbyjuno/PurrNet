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

                if (rootObject.TryGetComponent<SceneObjectIdentitfier>(out var sid))
                {
                    gameObjectsWithHash[i] = new GameObjectWithHash
                    {
                        gameObject = rootObject,
                        hash = sid.order
                    };
                }
                else
                {
                    gameObjectsWithHash[i] = new GameObjectWithHash
                    {
                        gameObject = rootObject,
                        hash = uint.MaxValue
                    };
                }
            }
            
            Array.Sort(gameObjectsWithHash, (a, b) =>
                a.hash.CompareTo(b.hash));

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
