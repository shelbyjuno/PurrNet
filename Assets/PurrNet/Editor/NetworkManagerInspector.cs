using System.Collections.Generic;
using PurrNet.Transports;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerInspector : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;

        private SerializedProperty _startServerFlags;
        private SerializedProperty _startClientFlags;
        private SerializedProperty _stopPlayingOnDisconnect;
        
        private SerializedProperty _cookieScope;

        private SerializedProperty _dontDestroyOnLoad;
        private SerializedProperty _networkPrefabs;
        private SerializedProperty _networkRules;
        private SerializedProperty _authenticator;
        private SerializedProperty _transport;
        private SerializedProperty _tickRate;
        private SerializedProperty _visibilityRules;
        
        private bool _showStatusFoldout = true;
        private bool _showPlayersFoldout;
        private readonly Dictionary<object, bool> _playerFoldouts = new Dictionary<object, bool>();

        
        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");

            _startServerFlags = serializedObject.FindProperty("_startServerFlags");
            _startClientFlags = serializedObject.FindProperty("_startClientFlags");
            _stopPlayingOnDisconnect = serializedObject.FindProperty("_stopPlayingOnDisconnect");
            
            _cookieScope = serializedObject.FindProperty("_cookieScope");
            
            _dontDestroyOnLoad = serializedObject.FindProperty("_dontDestroyOnLoad");
            _networkPrefabs = serializedObject.FindProperty("_networkPrefabs");
            _networkRules = serializedObject.FindProperty("_networkRules");
            _transport = serializedObject.FindProperty("_transport");
            _tickRate = serializedObject.FindProperty("_tickRate");
            _visibilityRules = serializedObject.FindProperty("_visibilityRules");
            _authenticator = serializedObject.FindProperty("_authenticator");
            
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
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

            if (networkManager && networkManager.isClient || networkManager.isServer)
                GUI.enabled = false;
            
            EditorGUILayout.PropertyField(_dontDestroyOnLoad);
            EditorGUILayout.PropertyField(_transport);
            DrawNetworkPrefabs();
            EditorGUILayout.PropertyField(_networkRules);
            EditorGUILayout.PropertyField(_visibilityRules);
            EditorGUILayout.PropertyField(_authenticator);

            GUI.enabled = true;

            if (networkManager.serverState != ConnectionState.Disconnected || networkManager.clientState != ConnectionState.Disconnected)
                GUI.enabled = false;

            RenderTickSlider();
            
            EditorGUILayout.PropertyField(_stopPlayingOnDisconnect);

            GUI.enabled = true;
            
            _showStatusFoldout = EditorGUILayout.Foldout(_showStatusFoldout, "Status");
            if (_showStatusFoldout)
            {
                DrawStatusFoldout(networkManager);
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNetworkPrefabs()
        {
            EditorGUILayout.BeginHorizontal();
            Color originalBgColor = GUI.backgroundColor;
            if (_networkPrefabs.objectReferenceValue == null)
            {
                GUI.backgroundColor = Color.yellow;
            }
            EditorGUILayout.PropertyField(_networkPrefabs);
            GUI.backgroundColor = originalBgColor;

            if (_networkPrefabs.objectReferenceValue == null)
            {
                if (GUILayout.Button("New", GUILayout.Width(50)))
                {
                    CreateNewNetworkPrefabs();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreateNewNetworkPrefabs()
        {
            string folderPath = "Assets";
            Object prefabsFolder = null;
            string[] prefabsFolders = AssetDatabase.FindAssets("t:Folder Prefabs");
    
            foreach (string guid in prefabsFolders)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if ((path.ToLower().EndsWith("/prefabs") || path.ToLower().EndsWith("/_prefabs")) && 
                    path.Split('/').Length == 2)
                {
                    folderPath = path;
                    prefabsFolder = AssetDatabase.LoadAssetAtPath<Object>(path);
                    break;
                }
            }

            var networkPrefabs = ScriptableObject.CreateInstance<NetworkPrefabs>();
    
            if (prefabsFolder != null)
            {
                networkPrefabs.folder = prefabsFolder;
            }
    
            string assetPath = $"{folderPath}/NetworkPrefabs.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
    
            AssetDatabase.CreateAsset(networkPrefabs, assetPath);
            AssetDatabase.SaveAssets();
    
            _networkPrefabs.objectReferenceValue = networkPrefabs;
            serializedObject.ApplyModifiedProperties();
    
            EditorGUIUtility.PingObject(networkPrefabs);
        }

        private void RenderTickSlider()
        {
            // serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(_tickRate, 1, 128, new GUIContent("Tick Rate"));
            /*if (EditorGUI.EndChangeCheck())
            {
                Time.fixedDeltaTime = 1f / _tickRate.intValue;
                AssetDatabase.SaveAssets();
            }*/
        }
        
        private void DrawStatusFoldout(NetworkManager networkManager)
        {
            if (!networkManager.isServer && !networkManager.isClient)
                return;

            if (networkManager.players == null)
                return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Server State:", networkManager.serverState.ToString());
            EditorGUILayout.LabelField("Client State:", networkManager.clientState.ToString());
            EditorGUILayout.LabelField("Player Count:", networkManager.playerCount.ToString());

            var players = networkManager.players;
            if (players != null)
            {
                _showPlayersFoldout = EditorGUILayout.Foldout(_showPlayersFoldout, $"Players ({players.Count})");
                if (_showPlayersFoldout)
                {
                    foreach (var playerId in players)
                    {
                        EditorGUI.indentLevel++;
                        if (!_playerFoldouts.ContainsKey(playerId))
                            _playerFoldouts[playerId] = false;

                        _playerFoldouts[playerId] = EditorGUILayout.Foldout(_playerFoldouts[playerId], $"Player: {playerId}");
                        if (_playerFoldouts[playerId])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.LabelField("Owned Objects:", networkManager.GetAllPlayerOwnedIds(playerId, networkManager.isServer).Count.ToString());

                            if (!networkManager.isServer)
                            {
                                EditorGUI.indentLevel--;
                                EditorGUI.indentLevel--;
                                continue;
                            }
                            
                            if (networkManager.TryGetPlayerScenes(playerId, out var scenes) && scenes.Length > 0)
                            {
                                EditorGUILayout.LabelField("Scenes (SceneId):");
                                foreach (var sceneId in scenes)
                                {
                                    if (!networkManager.TryGetScene(sceneId, out var scene))
                                        continue;
                                    
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.LabelField($"- {scene.name} ({sceneId})");
                                    EditorGUI.indentLevel--;
                                }
                            }
                            else
                            {
                                EditorGUILayout.LabelField("Scenes:", "None");
                            }

                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("No players connected.");
            }

            EditorGUILayout.EndVertical();
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