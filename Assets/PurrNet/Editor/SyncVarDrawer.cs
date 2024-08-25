using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomPropertyDrawer(typeof(SyncVar<>))]
    public class SyncVarDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't indent child fields
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var value = property.FindPropertyRelative("_value");

            if (value == null)
            {
                EditorGUI.LabelField(position, "SyncVar is not initialized.");
                return;
            }
            else
            {
                GUI.enabled = !Application.isPlaying;
                EditorGUI.PropertyField(position, value, GUIContent.none);
                GUI.enabled = true;
            }

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}
