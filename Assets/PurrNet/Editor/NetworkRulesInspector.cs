using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkRules))]
    public class NetworkRulesInspector : UnityEditor.Editor
    {
        private SerializedProperty spawnAuth;
        private SerializedProperty defaultOwner;
        private SerializedProperty ownershipTransfer;
        private SerializedProperty syncParentAuth;
        
        private void OnEnable()
        {
            spawnAuth = serializedObject.FindProperty("spawnAuth");
            defaultOwner = serializedObject.FindProperty("defaultOwner");
            ownershipTransfer = serializedObject.FindProperty("ownershipTransferAuth");
            syncParentAuth = serializedObject.FindProperty("syncParentAuth");
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label("Network Rules", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            const string description = "This asset is used to set the default rules of a Network manager. " +
                                       "Modifying these rules will change how things act over the network. " +
                                       "Default.";

            GUILayout.Label(description, DescriptionStyle());
    
            GUILayout.Space(10);
            GUILayout.Label("Settings", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            serializedObject.Update();
            
            DrawSettings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSettings()
        {
            EditorGUILayout.PropertyField(spawnAuth, new GUIContent("Spawn"));
            
            EditorGUILayout.PropertyField(defaultOwner, new GUIContent("Default owner"));
            
            EditorGUILayout.PropertyField(ownershipTransfer, new GUIContent("Ownership Transfer"));
            
            EditorGUILayout.PropertyField(syncParentAuth, new GUIContent("Sync Parent"));
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
