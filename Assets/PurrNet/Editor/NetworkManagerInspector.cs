using PurrNet.Transports;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerInspector : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;

        private SerializedProperty _startServerFlags;
        private SerializedProperty _startClientFlags;
        
        private SerializedProperty _cookieScope;
        
        private SerializedProperty _networkPrefabs;
        private SerializedProperty _networkRules;
        private SerializedProperty _transport;
        private SerializedProperty _tickRate;
        
        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");

            _startServerFlags = serializedObject.FindProperty("_startServerFlags");
            _startClientFlags = serializedObject.FindProperty("_startClientFlags");
            
            _cookieScope = serializedObject.FindProperty("_cookieScope");
            
            _networkPrefabs = serializedObject.FindProperty("_networkPrefabs");
            _networkRules = serializedObject.FindProperty("_networkRules");
            _transport = serializedObject.FindProperty("_transport");
            _tickRate = serializedObject.FindProperty("_tickRate");
        }

        public override void OnInspectorGUI()
        {
            var networkManager = (NetworkManager)target;

            if (networkManager.networkRules == null)
            {
                GUILayout.Label("Network Rules", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                const string description = "Set the network rules of your network manager. " +
                                           "This can be changed later. ";

                GUILayout.Label(description, new GUIStyle(GUI.skin.label) { wordWrap = true });
                
                GUILayout.Space(10);
                
                GUI.backgroundColor = Color.yellow;
                EditorGUILayout.PropertyField(_networkRules, new GUIContent("Network Rules"));
                serializedObject.ApplyModifiedProperties();
                return;
            }
                
            bool willStartServer = networkManager.shouldAutoStartServer;
            bool willStartClient = networkManager.shouldAutoStartClient;
            string status = willStartClient && willStartServer ? "HOST" : willStartClient ? "CLIENT" : willStartServer ? "SERVER" : "NONE";

            GUI.enabled = false;
            EditorGUILayout.PropertyField(_scriptProp, true);
            GUI.enabled = true;
            
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

            if (networkManager.isClient || networkManager.isServer)
                GUI.enabled = false;
            
            EditorGUILayout.PropertyField(_transport);
            EditorGUILayout.PropertyField(_networkPrefabs);
            EditorGUILayout.PropertyField(_networkRules);
            
            GUI.enabled = true;

            if (networkManager.serverState != ConnectionState.Disconnected || networkManager.clientState != ConnectionState.Disconnected)
                GUI.enabled = false;

            RenderTickSlider();
            
            GUI.enabled = true;
            
            serializedObject.ApplyModifiedProperties();
        }

        private void RenderTickSlider()
        {
            // serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(_tickRate, 1, 128, new GUIContent("Tick Rate"));
            if (EditorGUI.EndChangeCheck())
            {
                Time.fixedDeltaTime = 1f / _tickRate.intValue;
                AssetDatabase.SaveAssets();
            }
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