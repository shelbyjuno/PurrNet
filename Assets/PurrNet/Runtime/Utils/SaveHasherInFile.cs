using System.Collections.Generic;
using System.IO;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class SaveHasherInFile : MonoBehaviour
    {
        private void Start()
        {
            string names = "";
            
            var rootGameObjects = gameObject.scene.GetRootGameObjects();

            for (var i = 0; i < rootGameObjects.Length; i++)
            {
                var rootObject = rootGameObjects[i];

                if (rootObject.TryGetComponent<SceneObjectIdentitfier>(out var sid))
                {
                    names += rootObject.name + " " + sid.order + "\n";
                }
                else
                {
                    names += rootObject.name + " ?\n";
                }
            }

            File.WriteAllText("scene.txt", names);
        }
    }
}
