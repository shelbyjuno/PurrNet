using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    public class PurrNetBranchManager : EditorWindow
    {
        [MenuItem("Tools/PurrNet/Version Manager")]
        public static void ShowWindow()
        {
            GetWindow<PurrNetBranchManager>("PurrNet Version Manager");
        }
        
        private void OnGUI()
        {
            if (GUILayout.Button("Switch to Main Branch"))
            {
            }
            
            if (GUILayout.Button("Switch to Dev Branch"))
            {
            }
        }
    }
}
