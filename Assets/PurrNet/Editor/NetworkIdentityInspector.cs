using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    public class NetworkIdentityInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Network Identity Status", EditorStyles.boldLabel);
            
            var identity = (NetworkIdentity)target;

            if (identity.isValid)
            {
                EditorGUILayout.LabelField("Identity",identity.identity.ToString());

                EditorGUILayout.LabelField("Prefab Id",
                    identity.prefabOffset > 0
                        ? $"{identity.prefabId} (+{identity.prefabOffset})"
                        : $"{identity.prefabId}");
            }
            else
            {
                EditorGUILayout.LabelField("Currently not spawned");
            }
        }
    }
}
