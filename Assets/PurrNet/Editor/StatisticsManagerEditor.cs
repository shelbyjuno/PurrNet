using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(StatisticsManager), true)]
    public class StatisticsManagerEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var statisticsManager = (StatisticsManager)target;

            GUILayout.Box("Statistics manager", HeaderStyle(), GUILayout.ExpandWidth(true));
            GUILayout.Space(13);

            statisticsManager.checkInterval = EditorGUILayout.Slider("Check Interval", statisticsManager.checkInterval, 0.05f, 1f);
            
            var scriptProp = serializedObject.FindProperty("m_Script");

            GUI.enabled = false;
            EditorGUILayout.PropertyField(scriptProp, true);
            GUI.enabled = true;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Collection Settings", EditorStyles.boldLabel);

            statisticsManager.checkInterval = EditorGUILayout.Slider("Check Rate In Seconds", statisticsManager.checkInterval, 0.05f, 1f);
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Statistics Preview", EditorStyles.boldLabel);

            RenderStatistics(statisticsManager);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void RenderStatistics(StatisticsManager statisticsManager)
        {
            serializedObject.Update();

            if (!statisticsManager.ConnectedServer && !statisticsManager.ConnectedClient)
            {
                EditorGUILayout.LabelField("Awaiting connection");  
                return;
            }

            if (statisticsManager.ConnectedClient)
            {
                GUILayout.BeginHorizontal();
                DrawLed(GetPingStatus(statisticsManager));
                EditorGUILayout.LabelField($"Ping:");
                EditorGUILayout.LabelField($"{statisticsManager.Ping}ms");
                GUILayout.EndHorizontal();
            
                GUILayout.BeginHorizontal();
                DrawLed(GetJitterStatus(statisticsManager));
                EditorGUILayout.LabelField($"Jitter:");
                EditorGUILayout.LabelField($"{statisticsManager.Jitter}ms");
                GUILayout.EndHorizontal();
            
                GUILayout.BeginHorizontal();
                DrawLed(GetPacketLossStatus(statisticsManager));
                EditorGUILayout.LabelField($"Packet Loss:");
                EditorGUILayout.LabelField($"{statisticsManager.PacketLoss}%");
                GUILayout.EndHorizontal();
            }
            
            GUILayout.BeginHorizontal();
            DrawLed(Status.green);
            EditorGUILayout.LabelField($"Upload:");
            EditorGUILayout.LabelField($"{statisticsManager.Upload}KB/s");
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            DrawLed(Status.green);
            EditorGUILayout.LabelField($"Download:");
            EditorGUILayout.LabelField($"{statisticsManager.Download}KB/s");
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
            if(statisticsManager.Jitter < 10)
                return Status.green;
            if (statisticsManager.Jitter < 20)
                return Status.yellow;
            if (statisticsManager.Jitter < 40)
                return Status.orange;
            return Status.red;
        }
        
        private Status GetPacketLossStatus(StatisticsManager statisticsManager)
        {
            if(statisticsManager.PacketLoss < 11)
                return Status.green;
            if (statisticsManager.PacketLoss < 21)
                return Status.yellow;
            if (statisticsManager.PacketLoss < 31)
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

        private enum Status
        {
            green,
            yellow,
            orange,
            red
        }
    }
}
