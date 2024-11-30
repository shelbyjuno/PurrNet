using UnityEngine;
using UnityEditor;

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
                // Find the scene asset by searching for the stored scene name
                string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (scene.name == property.stringValue)
                    {
                        sceneObj = scene;
                        break;
                    }
                }
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
                string sceneName = newScene.name;
                if (sceneName != property.stringValue)
                {
                    property.stringValue = sceneName;
                }
            }
            else if (sceneObj != null)
            {
                property.stringValue = "";
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}