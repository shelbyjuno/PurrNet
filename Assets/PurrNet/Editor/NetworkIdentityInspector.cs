using System;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    public class NetworkIdentityInspector : UnityEditor.Editor
    {
        private bool settingsFoldoutVisible = false, statusFoldoutVisible = false;
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
            
            HandleStatus(identity);
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

        private void HandleStatus(NetworkIdentity identity)
        {
            statusFoldoutVisible = EditorGUILayout.Foldout(statusFoldoutVisible, "Status", true, boldFoldoutStyle);

            if (!statusFoldoutVisible)
                return;
            if (identity.isSpawned)
            {
                EditorGUILayout.LabelField("Identity", identity.id.ToString());

                EditorGUILayout.LabelField("Prefab Id", identity.prefabId == -1 ? "None" : identity.prefabId.ToString());

                EditorGUILayout.LabelField("Owner Id", identity.owner.HasValue ? identity.owner.Value.ToString() : "None"
                );
            }
            else
            {
                EditorGUILayout.LabelField("Currently not spawned");
            }
        }
    }
}