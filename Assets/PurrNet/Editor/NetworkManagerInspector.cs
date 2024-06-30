using PurrNet.Transports;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerInspector : UnityEditor.Editor
    {
        private SerializedProperty _startServerFlags;
        private SerializedProperty _startClientFlags;
        
        private SerializedProperty _cookieScope;
        
        private SerializedProperty _networkPrefabs;
        private SerializedProperty _transport;
        
        private void OnEnable()
        {
            _startServerFlags = serializedObject.FindProperty("_startServerFlags");
            _startClientFlags = serializedObject.FindProperty("_startClientFlags");
            
            _cookieScope = serializedObject.FindProperty("_cookieScope");
            
            _networkPrefabs = serializedObject.FindProperty("_networkPrefabs");
            _transport = serializedObject.FindProperty("_transport");
        }

        public override void OnInspectorGUI()
        {
            var networkManager = (NetworkManager)target;

            bool willStartServer = networkManager.shouldAutoStartServer;
            bool willStartClient = networkManager.shouldAutoStartClient;
            string status = willStartClient && willStartServer ? "HOST" : willStartClient ? "CLIENT" : willStartServer ? "SERVER" : "NONE";

            GUI.color = willStartClient && willStartServer ? Color.green : willStartClient ? Color.blue : willStartServer ? Color.red : Color.white;
            GUILayout.BeginVertical("box");
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"During play mode this instance will start as a <b>{status}</b>", new GUIStyle(GUI.skin.label) {richText = true});
            GUILayout.EndVertical();
            
            EditorGUILayout.PropertyField(_startServerFlags);
            EditorGUILayout.PropertyField(_startClientFlags);

            if (Application.isPlaying)
                RenderStartStopButtons(networkManager);

            EditorGUILayout.PropertyField(_cookieScope);
            EditorGUILayout.PropertyField(_networkPrefabs);

            if (networkManager.serverState != ConnectionState.Disconnected || networkManager.clientState != ConnectionState.Disconnected)
                GUI.enabled = false;
            
            EditorGUILayout.PropertyField(_transport);
            GUI.enabled = true;
            
            serializedObject.ApplyModifiedProperties();
        }

        private static void RenderStartStopButtons(NetworkManager networkManager)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = Application.isPlaying;

            switch (networkManager.serverState)
            {
                case ConnectionState.Disconnected:
                {
                    GUI.color = Color.white;
                    if (GUILayout.Button("Start Server", GUILayout.Width(10), GUILayout.ExpandWidth(true)))
                        networkManager.StartServer();
                    break;
                }
                case ConnectionState.Disconnecting:
                {
                    GUI.color = new Color(1f, 0.5f, 0f);
                    GUI.enabled = false;
                    GUILayout.Button("Stopping Server", GUILayout.Width(10), GUILayout.ExpandWidth(true));
                    break;
                }
                case ConnectionState.Connecting:
                {
                    GUI.color = Color.yellow;
                    GUI.enabled = false;
                    GUILayout.Button("Starting Server", GUILayout.Width(10), GUILayout.ExpandWidth(true));
                    break;
                }
                case ConnectionState.Connected:
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button("Stop Server", GUILayout.Width(10), GUILayout.ExpandWidth(true)))
                        networkManager.StopServer();
                    break;
                }
            }

            GUI.enabled = Application.isPlaying;

            switch (networkManager.clientState)
            {
                case ConnectionState.Disconnected:
                {
                    GUI.color = Color.white;
                    if (GUILayout.Button("Start Client", GUILayout.Width(10), GUILayout.ExpandWidth(true)))
                        networkManager.StartClient();
                    break;
                }
                case ConnectionState.Disconnecting:
                {
                    GUI.color = new Color(1f, 0.5f, 0f);
                    GUI.enabled = false;
                    GUILayout.Button("Stopping Client", GUILayout.Width(10), GUILayout.ExpandWidth(true));
                    break;
                }
                case ConnectionState.Connecting:
                {
                    GUI.color = Color.yellow;
                    GUI.enabled = false;
                    GUILayout.Button("Starting Client", GUILayout.Width(10), GUILayout.ExpandWidth(true));
                    break;
                }
                case ConnectionState.Connected:
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button("Stop Client", GUILayout.Width(10), GUILayout.ExpandWidth(true)))
                        networkManager.StopClient();
                    break;
                }
            }

            GUI.color = Color.white;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}