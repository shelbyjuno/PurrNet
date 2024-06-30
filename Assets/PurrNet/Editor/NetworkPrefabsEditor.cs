#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PurrNet
{
    [CustomEditor(typeof(NetworkPrefabs))]
    public class NetworkPrefabsEditor : UnityEditor.Editor
    {
        private NetworkPrefabs networkPrefabs;
        
        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            networkPrefabs = (NetworkPrefabs)target;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject != networkPrefabs)
                return;

            if (networkPrefabs.autoGenerate)
                networkPrefabs.Generate();
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Box("Network Prefabs", HeaderStyle(), GUILayout.ExpandWidth(true));
            string description = "This asset is used to store any prefabs containing a Network Behaviour. " +
                                 "You can add prefabs to this asset manually or auto generate the references. " +
                                 "This list is used by the NetworkManager to spawn network prefabs.";
            GUILayout.Box(description, new GUIStyle(GUI.skin.box) { wordWrap = true });

            GUILayout.Space(20);

            EditorGUILayout.LabelField("Project Folder", FolderStyle());

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("folder"), new GUIContent(""));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(networkPrefabs);
            }

            GUILayout.Space(10);

            if (GUILayout.Button(networkPrefabs.autoGenerate ? "Auto generate: Enabled" : "Auto generate: Disabled", GenerateButtonStyle(networkPrefabs.autoGenerate)))
            {
                networkPrefabs.autoGenerate = !networkPrefabs.autoGenerate;
                
                if(networkPrefabs.autoGenerate)
                    networkPrefabs.Generate();
                
                EditorUtility.SetDirty(networkPrefabs);
            }

            if (GUILayout.Button("Generate", new GUIStyle(GUI.skin.button) { fontSize = 15 }))
            {
                networkPrefabs.Generate();
            }

            GUIStyle backgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = EditorGUIUtility.isProSkin ? MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f)) : MakeTex(2, 2, new Color(0.8f, 0.8f, 0.8f, 1f)) }
            };

            EditorGUILayout.BeginVertical(backgroundStyle);
            EditorGUI.BeginDisabledGroup(networkPrefabs.autoGenerate);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabs"), new GUIContent("Prefabs"), true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(networkPrefabs);
            }
        }

        private GUIStyle HeaderStyle()
        {
            GUIStyle headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 30,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
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
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState()
                {
                    textColor = toggle ? Color.green : Color.red
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
