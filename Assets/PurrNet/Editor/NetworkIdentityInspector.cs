using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    [CanEditMultipleObjects]
#if TRI_INSPECTOR_PACKAGE
    public class NetworkIdentityInspector : TriInspector.Editors.TriEditor
#else
    public class NetworkIdentityInspector : UnityEditor.Editor
#endif
    {
        private SerializedProperty _networkRules;
        private SerializedProperty _visitiblityRules;
        
#if TRI_INSPECTOR_PACKAGE
        protected override void OnEnable()
#else
        protected virtual void OnEnable()
#endif
        {
#if TRI_INSPECTOR_PACKAGE
            base.OnEnable();
#endif
            try
            {
                _networkRules = serializedObject.FindProperty("_networkRules");
                _visitiblityRules = serializedObject.FindProperty("_visitiblityRules");
            }
            catch
            {
                // ignored
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            return null;
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

            var identities = targets.Length;
            var identity = (NetworkIdentity)target;
            
            if (!identity)
            {
                EditorGUILayout.LabelField("Invalid identity");
                return;
            }

            
            HandleOverrides(identity, identities > 1);
            HandleStatus(identity, identities > 1);
        }
        
        private bool _foldoutVisible;

        private void HandleOverrides(NetworkIdentity identity, bool multi)
        {
            if (multi || identity.isSpawned)
                GUI.enabled = false;
            
            string label = "Override Defaults";

            if (!multi)
            {
                bool isNetworkRulesOverridden = _networkRules.objectReferenceValue != null;
                bool isVisibilityRulesOverridden = _visitiblityRules.objectReferenceValue != null;

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
            }
            else
            {
                label += " (...)";
            }

            var old = GUI.enabled;
            GUI.enabled = !multi;
            _foldoutVisible = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutVisible, label);
            GUI.enabled = old;
            if (!multi && _foldoutVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_networkRules, new GUIContent("Permissions Override"));
                EditorGUILayout.PropertyField(_visitiblityRules, new GUIContent("Visibility Override"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private bool _debuggingVisible;
        private bool _observersVisible;

        private void HandleStatus(NetworkIdentity identity, bool multi)
        {
            if (multi)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField("...");
                EditorGUILayout.EndHorizontal();
            }
            else if (identity.isSpawned)
            {
                if (identity.isServer)
                {
                    var old = GUI.enabled;
                    GUI.enabled = true;
                    PrintObserversDropdown(identity);
                    GUI.enabled = old;
                }

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

        private void PrintObserversDropdown(NetworkIdentity identity)
        {
            _observersVisible = EditorGUILayout.BeginFoldoutHeaderGroup(_observersVisible, $"Observers ({identity.observers.Count})");

            if (_observersVisible)
            {
                EditorGUI.indentLevel++;
                foreach (var observer in identity.observers)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField(observer.ToString());
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}