using Rabsi.Utils;
using UnityEditor;

namespace Rabsi.Editor
{
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (ClonesContext.isClone)
                EditorGUILayout.HelpBox("You are inside a cloned editor", MessageType.Warning);
            
            base.OnInspectorGUI();
        }
    }
}