#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace PurrNet
{
    [CustomEditor(typeof(NetworkPrefabs))]
    public class NetworkPrefabsEditor : UnityEditor.Editor
    {
        private NetworkPrefabs networkPrefabs;
        private SerializedProperty prefabs;
        private bool? allPoolingState = null;
        private ReorderableList reorderableList;
        
        private const float POOL_TOGGLE_WIDTH = 45f;
        private const float SPACING = 8f;
        private const float REORDERABLE_LIST_BUTTON_WIDTH = 25f;
        
        private void OnEnable()
        {
            networkPrefabs = (NetworkPrefabs)target;
            prefabs = serializedObject.FindProperty("prefabs");
            
            if (networkPrefabs.autoGenerate)
                networkPrefabs.Generate();
                
            UpdateAllPoolingState();
            SetupReorderableList();
        }

        private void SetupReorderableList()
        {
            reorderableList = new ReorderableList(serializedObject, prefabs, true, true, true, true);
            
            reorderableList.drawHeaderCallback = (Rect rect) =>
            {
                rect.width -= REORDERABLE_LIST_BUTTON_WIDTH;
                
                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y, rect.width - POOL_TOGGLE_WIDTH - SPACING, rect.height),
                    "Prefabs",
                    EditorStyles.boldLabel
                );
                
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = !allPoolingState.HasValue;
                
                bool newAllPooling = EditorGUI.ToggleLeft(
                    new Rect(rect.x + rect.width - POOL_TOGGLE_WIDTH, rect.y, POOL_TOGGLE_WIDTH, rect.height),
                    "Pool",
                    allPoolingState ?? false
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    for (int i = 0; i < prefabs.arraySize; i++)
                    {
                        prefabs.GetArrayElementAtIndex(i).FindPropertyRelative("pool").boolValue = newAllPooling;
                    }
                    allPoolingState = newAllPooling;
                    serializedObject.ApplyModifiedProperties();
                }
                EditorGUI.showMixedValue = false;
            };

            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = prefabs.GetArrayElementAtIndex(index);
                SerializedProperty prefabProp = element.FindPropertyRelative("prefab");
                SerializedProperty poolProp = element.FindPropertyRelative("pool");

                rect.width -= REORDERABLE_LIST_BUTTON_WIDTH;
                
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width - POOL_TOGGLE_WIDTH - SPACING, rect.height),
                    prefabProp,
                    GUIContent.none
                );
                
                EditorGUI.BeginChangeCheck();
                poolProp.boolValue = EditorGUI.Toggle(
                    new Rect(rect.x + rect.width - POOL_TOGGLE_WIDTH, rect.y, POOL_TOGGLE_WIDTH, rect.height),
                    poolProp.boolValue
                );
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateAllPoolingState();
                    serializedObject.ApplyModifiedProperties();
                }
            };

            reorderableList.onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Empty Entry"), false, () =>
                {
                    int index = list.count;
                    list.serializedProperty.arraySize++;
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("prefab").objectReferenceValue = null;
                    element.FindPropertyRelative("pool").boolValue = networkPrefabs.defaultPooling;
                    serializedObject.ApplyModifiedProperties();
                    UpdateAllPoolingState();
                });
                
                menu.AddItem(new GUIContent("Add Selected Prefabs"), false, () =>
                {
                    bool addedAny = false;
                    foreach (var obj in Selection.gameObjects)
                    {
                        if (PrefabUtility.IsPartOfPrefabAsset(obj))
                        {
                            addedAny = true;
                            int index = list.count;
                            list.serializedProperty.arraySize++;
                            var element = list.serializedProperty.GetArrayElementAtIndex(index);
                            element.FindPropertyRelative("prefab").objectReferenceValue = obj;
                            element.FindPropertyRelative("pool").boolValue = networkPrefabs.defaultPooling;
                        }
                    }
                    if (addedAny)
                    {
                        serializedObject.ApplyModifiedProperties();
                        UpdateAllPoolingState();
                    }
                });
                
                menu.ShowAsContext();
            };
        }

        private void UpdateAllPoolingState()
        {
            if (prefabs.arraySize == 0)
            {
                allPoolingState = null;
                return;
            }

            bool firstState = prefabs.GetArrayElementAtIndex(0).FindPropertyRelative("pool").boolValue;
            allPoolingState = firstState;

            for (int i = 1; i < prefabs.arraySize; i++)
            {
                if (prefabs.GetArrayElementAtIndex(i).FindPropertyRelative("pool").boolValue != firstState)
                {
                    allPoolingState = null;
                    return;
                }
            }
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

            // Generation Settings
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("folder"), new GUIContent("Folder"));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(networkPrefabs);
            }

            // Toggle buttons row
            GUILayout.BeginHorizontal();

            DrawToggleButton("Auto generate", ref networkPrefabs.autoGenerate);
            DrawToggleButton("Networked only", ref networkPrefabs.networkOnly);
            DrawToggleButton("Default pooling", ref networkPrefabs.defaultPooling);

            GUILayout.EndHorizontal();

            if (GUILayout.Button("Generate", GUILayout.Width(1), GUILayout.ExpandWidth(true)))
            {
                networkPrefabs.Generate();
                serializedObject.ApplyModifiedProperties();
                prefabs = serializedObject.FindProperty("prefabs");
                UpdateAllPoolingState();
            }
            
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(networkPrefabs.autoGenerate);
            reorderableList.DoLayoutList();
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(networkPrefabs);
            }
        }

        private void DrawToggleButton(string label, ref bool value)
        {
            GUI.color = value ? Color.green : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(1), GUILayout.ExpandWidth(true)))
            {
                value = !value;
                if (networkPrefabs.autoGenerate)
                {
                    networkPrefabs.Generate();
                    serializedObject.ApplyModifiedProperties();
                    prefabs = serializedObject.FindProperty("prefabs");
                    UpdateAllPoolingState();
                }
                EditorUtility.SetDirty(networkPrefabs);
            }
            GUI.color = Color.white;
        }

        private static GUIStyle DescriptionStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
        }
    }
}
#endif