using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    public class NetworkIdentityInspector : UnityEditor.Editor
    {
        private bool settingsFoldoutVisible = false;
        private GUIStyle boldFoldoutStyle; 

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

            serializedObject.Update();
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

            HandleSettings(identity);
            HandleOptionalRules(identity);
            HandleStatus(identity);
            
            serializedObject.ApplyModifiedProperties();
        }

        private void HandleSettings(NetworkIdentity identity)
        {
            if (identity.isSpawned)
                return;
            
            settingsFoldoutVisible = EditorGUILayout.Foldout(settingsFoldoutVisible, "Network Identity Settings", true, boldFoldoutStyle);

            if (settingsFoldoutVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Test");
                EditorGUI.indentLevel--;
            }
        }

        private void HandleOptionalRules(NetworkIdentity identity)
        {
            if (identity.isSpawned)
                return;
            
            string prefKey = $"NetworkIdentityInspector_OptionalRulesFoldout_{identity.GetInstanceID()}";
            bool foldoutVisible = EditorPrefs.GetBool(prefKey, false);
            
            EditorGUI.BeginChangeCheck();
            foldoutVisible = EditorGUILayout.Foldout(foldoutVisible, "Optional Network Rules", true, boldFoldoutStyle);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(prefKey, foldoutVisible);
            }

            if (foldoutVisible)
            {
                EditorGUI.indentLevel++;

                DrawRulesSection("Spawn Rules", "optionalSpawnRules", new[]
                {
                    ("spawnAuth", "Spawn Auth"),
                    ("despawnAuth", "Despawn Auth"),
                    ("defaultOwner", "Default Owner"),
                    ("propagateOwnership", "Propagate Ownership"),
                    ("despawnIfOwnerDisconnects", "Despawn If Owner Disconnects")
                });

                DrawRulesSection("Ownership Rules", "optionalOwnershipRules", new[]
                {
                    ("assignAuth", "Assign Auth"),
                    ("transferAuth", "Transfer Auth"),
                    ("removeAuth", "Remove Auth")
                });

                DrawRulesSection("Network Identity Rules", "optionalIdentityRules", new[]
                {
                    ("syncComponentActive", "Sync Component Active"),
                    ("syncComponentAuth", "Sync Component Auth"),
                    ("syncGameObjectActive", "Sync GameObject Active"),
                    ("syncGameObjectActiveAuth", "Sync GameObject Active Auth")
                });

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
                    if (valueProp.propertyType == SerializedPropertyType.Enum)
                        valueProp.enumValueIndex = selectedIndex - 1;
                    else if (valueProp.propertyType == SerializedPropertyType.Boolean)
                        valueProp.boolValue = options[selectedIndex] == "True";
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private string[] GetOptionsForProperty(SerializedProperty property)
        {
            string[] options;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Enum:
                    options = new string[] { "Default" }.Concat(property.enumNames).ToArray();
                    break;
                case SerializedPropertyType.Boolean:
                    options = new string[] { "Default", "True", "False" };
                    break;
                default:
                    options = new string[] { "Default", "Custom" };
                    break;
            }
            return options;
        }

        private void HandleStatus(NetworkIdentity identity)
        {
            if (identity.isSpawned)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {identity.id}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Prefab ID: {(identity.prefabId == -1 ? "None" : identity.prefabId.ToString())}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Owner ID: {(identity.owner.HasValue ? identity.owner.Value.ToString() : "None")}");
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}