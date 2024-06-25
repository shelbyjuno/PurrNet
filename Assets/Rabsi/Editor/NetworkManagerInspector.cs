using UnityEditor;
using UnityEngine;

namespace Rabsi.Editor
{
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var networkManager = (NetworkManager)target;

            bool willStartServer = networkManager.shouldAutoStartServer;
            bool willStartClient = networkManager.shouldAutoStartClient;
            string status = willStartClient && willStartServer ? "HOST" : willStartClient ? "CLIENT" : willStartServer ? "SERVER" : "NONE";

            GUI.color = willStartClient && willStartServer ? Color.green : willStartClient ? Color.blue : willStartServer ? Color.red : Color.white;
            GUILayout.BeginVertical("box");
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"During play mode, this instance will start as a <b>{status}</b>", new GUIStyle(GUI.skin.label) {richText = true});
            GUILayout.EndVertical();
            
            base.OnInspectorGUI();
        }
    }
}