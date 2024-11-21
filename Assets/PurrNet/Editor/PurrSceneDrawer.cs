using UnityEngine;
using UnityEditor;
using System;

namespace PurrNet
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(PurrSceneAttribute))]
    public class PurrSceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "PurrScene attribute can only be used with string fields");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            SceneAsset sceneObj = null;
            if (!string.IsNullOrEmpty(property.stringValue))
            {
                sceneObj = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            }

            var newScene = EditorGUI.ObjectField(
                position,
                label,
                sceneObj,
                typeof(SceneAsset),
                false
            ) as SceneAsset;

            if (newScene != null)
            {
                string newPath = AssetDatabase.GetAssetPath(newScene);
                if (newPath != property.stringValue)
                {
                    property.stringValue = newPath;
                }
            }
            else if (sceneObj != null)
            {
                property.stringValue = "";
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif