using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(PrefabLink), true)]
    public class PrefabLinkInspector : UnityEditor.Editor
    {
        private SerializedProperty _guid;
        
        private void OnEnable()
        {
            _guid = serializedObject.FindProperty("_guid");
        }

        public override void OnInspectorGUI()
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_guid);
            GUI.enabled = true;
        }
    }
}
