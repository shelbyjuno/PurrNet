#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PurrNet
{
    [CustomEditor(typeof(NetworkPrefabs))]
    public class NetworkPrefabsEditor : UnityEditor.Editor
    {
        private NetworkPrefabs networkPrefabs;
        private SerializedProperty prefabs;
        
        private void OnEnable()
        {
            networkPrefabs = (NetworkPrefabs)target;
            
            prefabs = serializedObject.FindProperty("prefabs");
            
            if (networkPrefabs.autoGenerate)
                networkPrefabs.Generate();
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label("Network Prefabs", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            const string description = "This asset is used to store any prefabs containing a Network Behaviour. " +
                                       "You can add prefabs to this asset manually or auto generate the references. " +
                                       "This list is used by the NetworkManager to spawn network prefabs.";

            GUILayout.Label(description, DescriptionStyle());

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("folder"), new GUIContent("Folder"));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(networkPrefabs);
            }

            GUILayout.BeginHorizontal();

            if (networkPrefabs.autoGenerate)
            {
                GUI.color = Color.green;
            }

            if (GUILayout.Button(networkPrefabs.autoGenerate ? "Auto generate: Enabled" : "Auto generate: Disabled",
                GUILayout.Width(1), GUILayout.ExpandWidth(true)))
            {
                networkPrefabs.autoGenerate = !networkPrefabs.autoGenerate;

                if (networkPrefabs.autoGenerate)
                {
                    networkPrefabs.Generate();
                    serializedObject.ApplyModifiedProperties(); // Update the serialized object after generating
                    prefabs = serializedObject.FindProperty("prefabs");
                }

                EditorUtility.SetDirty(networkPrefabs);
            }
            
            GUI.color = Color.white;
            if (networkPrefabs.networkOnly)
            {
                GUI.color = Color.green;
            }

            if (GUILayout.Button(networkPrefabs.networkOnly ? "Networked only: Enabled" : "Networked only: Disabled",
                    GUILayout.Width(1), GUILayout.ExpandWidth(true)))
            {
                networkPrefabs.networkOnly = !networkPrefabs.networkOnly;
                
                if (networkPrefabs.autoGenerate)
                {
                    networkPrefabs.Generate();
                    serializedObject.ApplyModifiedProperties(); // Update the serialized object after generating
                    prefabs = serializedObject.FindProperty("prefabs");
                }
                EditorUtility.SetDirty(networkPrefabs);
            }

            GUILayout.EndHorizontal();

            GUI.color = Color.white;

            if (GUILayout.Button("Generate", GUILayout.Width(1), GUILayout.ExpandWidth(true)))
            {
                //serializedObject.Update(); // Update the serialized object after generating
                networkPrefabs.Generate();
                serializedObject.ApplyModifiedProperties();
                prefabs = serializedObject.FindProperty("prefabs");
            }
            
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(networkPrefabs.autoGenerate);
            EditorGUILayout.PropertyField(prefabs, true);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(networkPrefabs);
            }
        }


        private static GUIStyle DescriptionStyle()
        {
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };

            return headerStyle; 
        }
        
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private GUIStyle GenerateButtonStyle(bool toggle)
        {
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = new GUIStyleState()
                {
                    textColor = toggle ? Color.green : Color.white
                }
            };

            return buttonStyle;
        }

        private GUIStyle FolderStyle()
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState()
                {
                    textColor = Color.white
                }
            };
            return labelStyle;
        }
    }
}
#endif
