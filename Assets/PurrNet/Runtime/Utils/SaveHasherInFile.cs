using System.IO;
using UnityEngine;

namespace PurrNet
{
    public class SaveHasherInFile : MonoBehaviour
    {
        private void Start()
        {
            string names = "";
            
            var rootGameObjects = gameObject.scene.GetRootGameObjects();
            
            PurrSceneInfo sceneInfo = null;
            
            foreach (var rootObject in rootGameObjects)
            {
                if (rootObject.TryGetComponent<PurrSceneInfo>(out var si))
                {
                    sceneInfo = si;
                    break;
                }
            }

            if (!sceneInfo)
            {
                File.WriteAllText("scene.txt", "No PurrSceneInfo found");
                return;
            }
            
            for (var i = 0; i < sceneInfo.rootGameObjects.Count; i++)
            {
                var rootObject = sceneInfo.rootGameObjects[i];
                names += rootObject.name + "\n";
            }

            File.WriteAllText("scene.txt", names);
        }
    }
}
