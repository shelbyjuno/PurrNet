using UnityEngine;
using UnityEditor;

namespace PurrNet.Utils
{
    public class HelpLinkAttribute : PropertyAttribute
    {
        public readonly string url;

        public HelpLinkAttribute(string docsExtension)
        {
            url = docsExtension;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HelpLinkAttribute))]
    public class HelpLinkDrawer : PropertyDrawer
    {
        private const float IconWidth = 20f;
        private static GUIContent iconContent;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (iconContent == null)
            {
                iconContent = EditorGUIUtility.IconContent("_Help");
            }

            Rect iconRect = new Rect(position.x, position.y, IconWidth, EditorGUIUtility.singleLineHeight);
            Rect propertyRect = new Rect(position.x + IconWidth, position.y, position.width - IconWidth, position.height);

            if (GUI.Button(iconRect, iconContent, GUIStyle.none))
            {
                HelpLinkAttribute helpLink = attribute as HelpLinkAttribute;
                Application.OpenURL("https://purrnet.gitbook.io/docs/" + helpLink.url);
            }

            EditorGUI.PropertyField(propertyRect, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
}