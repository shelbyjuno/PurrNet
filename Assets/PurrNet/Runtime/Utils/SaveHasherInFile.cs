using System.IO;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public class SaveHasherInFile : MonoBehaviour
    {
        private void Start()
        {
            var hashes = Resources.Load<TextAsset>($"PurrHashes");
            if (hashes == null)
                return;
            
            File.WriteAllText("hashes.txt", hashes.text);

            var hashesRuntume = Hasher.GetAllHashesAsText();
            File.WriteAllText("myhashes.txt", hashesRuntume);
        }
    }
}
