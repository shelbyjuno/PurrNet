using System.IO;
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
        }
    }
}
