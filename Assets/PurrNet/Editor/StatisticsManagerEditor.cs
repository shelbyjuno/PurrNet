using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(StatisticsManager), true)]
    public class StatisticsManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _checkRate;

        private void OnEnable()
        {
            _checkRate = serializedObject.FindProperty("checkRate");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var statisticsManager = (StatisticsManager)target;
            
            var scriptProp = serializedObject.FindProperty("m_Script");

            GUI.enabled = false;
            EditorGUILayout.PropertyField(scriptProp, true);
            GUI.enabled = true;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Collection Settings", EditorStyles.boldLabel);

            statisticsManager.checkRate = EditorGUILayout.Slider("Check Rate In Seconds", statisticsManager.checkRate, 0.05f, 1f);
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Statistics Preview", EditorStyles.boldLabel);

            RenderStatistics(statisticsManager);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void RenderStatistics(StatisticsManager statisticsManager)
        {
            serializedObject.Update();

            if (!statisticsManager.connectedServer && !statisticsManager.connectedClient)
            {
                EditorGUILayout.LabelField("Awaiting connection");  
                return;
            }

            if (statisticsManager.connectedClient)
            {
                GUILayout.BeginHorizontal();
                DrawLed(GetPingStatus(statisticsManager));
                EditorGUILayout.LabelField("Ping", $"{statisticsManager.ping}ms");
                GUILayout.EndHorizontal();
            
                GUILayout.BeginHorizontal();
                DrawLed(GetJitterStatus(statisticsManager));
                EditorGUILayout.LabelField("Jitter", $"{statisticsManager.jitter}ms");
                GUILayout.EndHorizontal();
            
                GUILayout.BeginHorizontal();
                DrawLed(GetPacketLossStatus(statisticsManager));
                EditorGUILayout.LabelField("Packet Loss", $"{statisticsManager.packetLoss}%");
                GUILayout.EndHorizontal();
            }
            
            GUILayout.BeginHorizontal();
            DrawLed(statisticsManager.upload > 0 ? Status.green : Status.yellow);
            EditorGUILayout.LabelField("Upload", $"{statisticsManager.upload}KB/s");
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            DrawLed(statisticsManager.download > 0 ? Status.green : Status.yellow);
            EditorGUILayout.LabelField("Download", $"{statisticsManager.download}KB/s");
            GUILayout.EndHorizontal();
        }

        private static Status GetPingStatus(StatisticsManager statisticsManager)
        {
            return statisticsManager.ping switch
            {
                < 50 => Status.green,
                < 100 => Status.yellow,
                < 200 => Status.orange,
                _ => Status.red
            };
        }
        
        private Status GetJitterStatus(StatisticsManager statisticsManager)
        {
            if(statisticsManager.jitter < 10)
                return Status.green;
            if (statisticsManager.jitter < 20)
                return Status.yellow;
            if (statisticsManager.jitter < 40)
                return Status.orange;
            return Status.red;
        }
        
        private Status GetPacketLossStatus(StatisticsManager statisticsManager)
        {
            if(statisticsManager.packetLoss < 11)
                return Status.green;
            if (statisticsManager.packetLoss < 21)
                return Status.yellow;
            if (statisticsManager.packetLoss < 31)
                return Status.orange;
            return Status.red;
        }
        
        static void DrawLed(Status status)
        {
            var white = Texture2D.whiteTexture;
            var color = status switch
            {
                Status.green => Color.green,
                Status.yellow => Color.yellow,
                Status.orange => new Color(1, 0.5f, 0),
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
        
        private GUIStyle HeaderStyle()
        {
            GUIStyle headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 30,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            return headerStyle;
        }
        
        private GUIStyle FolderStyle()
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState()
                {
                    textColor = Color.white
                }
            };
            return labelStyle;
        }

        private enum Status
        {
            green,
            yellow,
            orange,
            red
        }
    }
}
