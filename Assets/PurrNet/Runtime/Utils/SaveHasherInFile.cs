using System.IO;
using PurrNet.Modules;
using PurrNet.Pooling;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public class SaveHasherInFile : MonoBehaviour
    {
        private void Awake()
        {
            string text = Hasher.GetAllHashesAsText();
            File.WriteAllText("hashes.txt", text);
            
            string scenetext = Hasher.GetAllHashesAsText();
            var allSceneIdentities = ListPool<NetworkIdentity>.Instantiate();
            SceneObjectsModule.GetSceneIdentities(gameObject.scene, allSceneIdentities);
            
            foreach (var identity in allSceneIdentities)
            {
                var go = identity.gameObject;
                var hash = GameObjectHasher.ComputeHashRecursive(go);
                scenetext += $"{go.name} {hash}\n";
            }
            
            File.WriteAllText("scenehashes.txt", scenetext);
        }
    }
}
