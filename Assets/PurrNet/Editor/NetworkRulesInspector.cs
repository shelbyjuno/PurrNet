using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkRules))]
    public class NetworkRulesInspector : UnityEditor.Editor
    {
        private SerializedProperty _defaultSpawnRules;
        private SerializedProperty _defaultOwnershipRules;
        private SerializedProperty _defaultIdentityRules;
        private SerializedProperty _defaultTransformRules;
        
        private void OnEnable()
        {
            _defaultSpawnRules = serializedObject.FindProperty("_defaultSpawnRules");
            _defaultOwnershipRules = serializedObject.FindProperty("_defaultOwnershipRules");
            _defaultIdentityRules = serializedObject.FindProperty("_defaultIdentityRules");
            _defaultTransformRules = serializedObject.FindProperty("_defaultTransformRules");
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label("Network Rules", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            const string description = "This asset is used to set the default rules of a Network manager. " +
                                       "Modifying these rules will change how things act over the network. ";

            GUILayout.Label(description, DescriptionStyle());
            GUILayout.Space(10);
            
            DrawDefaultInspector();
        }

        void DrawBoolRight(SerializedProperty property, string label)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
        }
        
        private static GUIStyle DescriptionStyle()
        {
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };

            return headerStyle; 
        }
    }
}
