using Rabsi.Transports;
using UnityEditor;
using UnityEngine;

namespace Rabsi.Editor
{
    [CustomEditor(typeof(GenericTransport), true)]
    public class TransportInspector : UnityEditor.Editor
    {
        static void DrawLed(ConnectionState state)
        {
            var white = Texture2D.whiteTexture;
            GUI.color = state switch
            {
                ConnectionState.Connecting => Color.yellow,
                ConnectionState.Connected => Color.green,
                ConnectionState.Disconnecting => new Color(1, 0.5f, 0),
                _ => Color.red
            };
            
            GUILayout.Label(white, GUILayout.Width(20), GUILayout.Height(20));
            GUI.color = Color.white;
        }
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (!Application.isPlaying)
                return;

            var generic = (GenericTransport)target;
            var transport = generic.transport;

            if (!generic.isSupported)
            {
                EditorGUILayout.HelpBox("Transport is not supported on this platform", MessageType.Info);
                return;
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Protocol Status", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            DrawLed(transport.listenerState);
            EditorGUILayout.LabelField("Listening");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawLed(transport.clientState);
            EditorGUILayout.LabelField("Connected");
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Server Options", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Start Server"))
                generic.Listen();
            
            if (GUILayout.Button("Stop Server"))
                transport.StopListening();
            
            GUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("Client Options", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Connect"))
                generic.Connect();
            
            if (GUILayout.Button("Disconnect"))
                transport.Disconnect();
            
            GUILayout.EndHorizontal();
        }
    }
}
