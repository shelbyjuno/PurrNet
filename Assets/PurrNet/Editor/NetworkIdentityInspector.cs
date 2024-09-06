using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    public class NetworkIdentityInspector : UnityEditor.Editor
    {
        private bool settingsFoldoutVisible;
        private GUIStyle boldFoldoutStyle; 
        
        private SerializedProperty _networkRules;
        
        private void OnEnable()
        {
            _networkRules = serializedObject.FindProperty("_networkRules");
        }

        private void SetStyle()
        {
            boldFoldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (boldFoldoutStyle == null)
            {
                SetStyle(); 
                return;
            }

            GUILayout.Space(10);
            
            var identity = (NetworkIdentity)target;
            
            if (!identity)
            {
                EditorGUILayout.LabelField("Invalid identity");
                return;
            }

            HandleOverrides(identity);
            HandleStatus(identity);
            
            serializedObject.ApplyModifiedProperties();
        }

        private void HandleOverrides(NetworkIdentity identity)
        {
            if (identity.isSpawned)
                return;
            
            string prefKey = $"NetworkIdentityInspector_OptionalRulesFoldout_{identity.GetInstanceID()}";
            bool foldoutVisible = SessionState.GetBool(prefKey, false);
            
            EditorGUI.BeginChangeCheck();
            foldoutVisible = EditorGUILayout.Foldout(foldoutVisible, "Optional Network Rules", true, boldFoldoutStyle);
            if (EditorGUI.EndChangeCheck())
            {
                SessionState.SetBool(prefKey, foldoutVisible);
            }

            if (foldoutVisible)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_networkRules, new GUIContent("Rules Override"));

                EditorGUI.indentLevel--;
            }
        }

        private void DrawRulesSection(string sectionTitle, string propertyName, (string propertyName, string label)[] properties)
        {
            EditorGUILayout.LabelField(sectionTitle, EditorStyles.boldLabel);
            SerializedProperty rulesProp = serializedObject.FindProperty(propertyName);
            foreach (var (propName, label) in properties)
            {
                DrawOptionalProperty(rulesProp.FindPropertyRelative(propName), label);
            }
            EditorGUILayout.Space();
        }

        private void DrawOptionalProperty(SerializedProperty property, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));

            SerializedProperty overriddenProp = property.FindPropertyRelative("overridden");
            SerializedProperty valueProp = property.FindPropertyRelative("value");

            string[] options = GetOptionsForProperty(valueProp);

            int selectedIndex = overriddenProp.boolValue ? Array.IndexOf(options, valueProp.propertyType == SerializedPropertyType.Enum ? valueProp.enumNames[valueProp.enumValueIndex] : valueProp.boolValue.ToString()) : 0;
            
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(selectedIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                overriddenProp.boolValue = selectedIndex != 0;
                if (overriddenProp.boolValue)
                {
                    switch (valueProp.propertyType)
                    {
                        case SerializedPropertyType.Enum:
                            valueProp.enumValueIndex = selectedIndex - 1;
                            break;
                        case SerializedPropertyType.Boolean:
                            valueProp.boolValue = options[selectedIndex] == "True";
                            break;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string[] GetOptionsForProperty(SerializedProperty property)
        {
            string[] options = property.propertyType switch
            {
                SerializedPropertyType.Enum => new[] { "Default" }.Concat(property.enumNames).ToArray(),
                SerializedPropertyType.Boolean => new[] { "Default", "True", "False" },
                _ => new[] { "Default", "Custom" }
            };
            return options;
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
        }
    }
}