using System.IO;
using PurrNet.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace PurrNet.Editor
{
    public class PurrNetSceneProcessor : IProcessSceneWithReport, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        
        public void OnPostprocessBuild(BuildReport report)
        {
            const string PATH = "Assets/Resources/PurrHashes.json";
            
            if (File.Exists(PATH))
                File.Delete(PATH);
            
            if (File.Exists(PATH + ".meta"))
                File.Delete(PATH + ".meta");
            
            bool isResourcesFolderEmpty = Directory.GetFiles("Assets/Resources").Length == 0 &&
                                          Directory.GetDirectories("Assets/Resources").Length == 0;

            if (isResourcesFolderEmpty)
            {
                Directory.Delete("Assets/Resources");
                if (File.Exists("Assets/Resources.meta"))
                    File.Delete("Assets/Resources.meta");
            }
            
            AssetDatabase.Refresh();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            const string PATH = "Assets/Resources/PurrHashes.json";
            Directory.CreateDirectory(Path.GetDirectoryName(PATH) ?? string.Empty);
            
            var hashes = Hasher.GetAllHashesAsText();
            File.WriteAllText(PATH, hashes);
            
            AssetDatabase.Refresh();
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            
            if (report == null)
                return;
            
            uint hash = Hasher.ActualHash(scene.path);
            
            var rootObjects = scene.GetRootGameObjects();
            for (uint i = 0; i < rootObjects.Length; i++)
            {
                var rootObj = rootObjects[i];
                var id = rootObj.AddComponent<SceneObjectIdentitfier>();
                id.order = hash + i;
            }
        }
    }
}
