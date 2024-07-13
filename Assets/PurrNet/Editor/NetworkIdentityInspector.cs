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

            if (!identity)
            {
                EditorGUILayout.LabelField("Invalid identity");
                return;
            }

            if (identity.isSpawned)
            {
                EditorGUILayout.LabelField("Identity", identity.id.ToString());
                
                EditorGUILayout.LabelField("Prefab Id", identity.prefabId == -1 ? 
                    "None" : identity.prefabId.ToString());
                
                EditorGUILayout.LabelField("Owner Id",identity.owner.HasValue ? 
                    identity.owner.Value.ToString() : 
                    "None"
                );
            }
            else
            {
                EditorGUILayout.LabelField("Currently not spawned");
            }
        }
    }
}
