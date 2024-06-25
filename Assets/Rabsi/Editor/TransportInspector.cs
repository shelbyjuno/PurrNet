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
            var color = state switch
            {
                ConnectionState.Connecting => Color.yellow,
                ConnectionState.Connected => Color.green,
                ConnectionState.Disconnecting => new Color(1, 0.5f, 0),
                _ => Color.red
            };

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            var rect = GUILayoutUtility.GetLastRect();
            rect.height = EditorGUIUtility.singleLineHeight;
            
            const float padding = 5;
            
            rect.x += padding;
            rect.y += padding;
            
            rect.width -= padding * 2;
            rect.height -= padding * 2;
            
            GUI.DrawTexture(rect, white, ScaleMode.StretchToFill, true, 1f, color, 0, 10f);
        }
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

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
        }
    }
}
