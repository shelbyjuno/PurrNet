using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    public class NetworkIdentityInspector : UnityEditor.Editor
    {
        private SerializedProperty _networkRules;
        private SerializedProperty _visitiblityRules;
        
        protected virtual void OnEnable()
        {
            _networkRules = serializedObject.FindProperty("_networkRules");
            _visitiblityRules = serializedObject.FindProperty("_visitiblityRules");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            DrawIdentityInspector();
            
            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawIdentityInspector()
        {
            GUILayout.Space(5);

            var identity = (NetworkIdentity)target;
            
            if (!identity)
            {
                EditorGUILayout.LabelField("Invalid identity");
                return;
            }
            
            HandleOverrides(identity);
            HandleStatus(identity);
        }
        
        private bool _foldoutVisible;

        private void HandleOverrides(NetworkIdentity identity)
        {
            if (identity.isSpawned)
                GUI.enabled = false;
            
            bool isNetworkRulesOverridden = _networkRules.objectReferenceValue != null;
            bool isVisibilityRulesOverridden = _visitiblityRules.objectReferenceValue != null;

            string label = "Override Defaults";
            int overridenCount = (isNetworkRulesOverridden ? 1 : 0) + (isVisibilityRulesOverridden ? 1 : 0);

            if (overridenCount > 0)
            {
                label += " (";

                if (isNetworkRulesOverridden)
                {
                    label += overridenCount > 1 ? "P," : "P";
                }
                
                if (isVisibilityRulesOverridden)
                    label += "V";
                
                label += ")";
            }

            var old = GUI.enabled;
            GUI.enabled = true;
            _foldoutVisible = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutVisible, label);
            GUI.enabled = old;
            if (_foldoutVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_networkRules, new GUIContent("Permissions Override"));
                EditorGUILayout.PropertyField(_visitiblityRules, new GUIContent("Visibility Override"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void HandleStatus(NetworkIdentity identity)
        {
            if (identity.isSpawned)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"ID: {identity.id}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Prefab ID: {(identity.prefabId == -1 ? "None" : identity.prefabId.ToString())}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Owner ID: {(identity.owner.HasValue ? identity.owner.Value.ToString() : "None")}");
                EditorGUILayout.EndHorizontal();
            }
            else if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField("Not Spawned");
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}