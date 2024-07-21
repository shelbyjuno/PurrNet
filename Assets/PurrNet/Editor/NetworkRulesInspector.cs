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
        private SerializedProperty syncVarAuth;
        private SerializedProperty clientRpcAuth;
        private SerializedProperty fullObjectOwnership;
        
        private void OnEnable()
        {
            spawnAuth = serializedObject.FindProperty("spawnAuth");
            defaultOwner = serializedObject.FindProperty("defaultOwner");
            ownershipTransfer = serializedObject.FindProperty("ownershipTransferAuth");
            clientRpcAuth = serializedObject.FindProperty("clientRpcAuth");
            syncParentAuth = serializedObject.FindProperty("syncParentAuth");
            syncVarAuth = serializedObject.FindProperty("syncVarAuth");
            fullObjectOwnership = serializedObject.FindProperty("fullObjectOwnership");
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label("Network Rules", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            const string description = "This asset is used to set the default rules of a Network manager. " +
                                       "Modifying these rules will change how things act over the network. ";

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

            EditorGUILayout.PropertyField(fullObjectOwnership);
            
            EditorGUILayout.PropertyField(syncVarAuth, new GUIContent("Sync Var"));
            
            EditorGUILayout.PropertyField(clientRpcAuth, new GUIContent("Client Rpcs"));
            
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
