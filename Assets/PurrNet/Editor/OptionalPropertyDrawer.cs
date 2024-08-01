using UnityEngine;
using UnityEditor;

namespace PurrNet.Editor
{
    [CustomPropertyDrawer(typeof(NetworkIdentity.Optional<>))]
    public class OptionalPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var overriddenProp = property.FindPropertyRelative("overridden");
            var valueProp = property.FindPropertyRelative("value");

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var dropdownRect = new Rect(position.x, position.y, 100, position.height);
            var fieldRect = new Rect(position.x + 105, position.y, position.width - 105, position.height);

            string[] options = { "Default", "Override" };
            int selectedIndex = overriddenProp.boolValue ? 1 : 0;
            selectedIndex = EditorGUI.Popup(dropdownRect, selectedIndex, options);
            overriddenProp.boolValue = selectedIndex == 1;

            if (overriddenProp.boolValue)
            {
                EditorGUI.PropertyField(fieldRect, valueProp, GUIContent.none);
            }
            else
            {
                GUI.enabled = false;
                EditorGUI.PropertyField(fieldRect, valueProp, GUIContent.none);
                GUI.enabled = true;
            }

            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}