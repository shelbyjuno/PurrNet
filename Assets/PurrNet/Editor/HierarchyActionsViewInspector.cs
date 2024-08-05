using UnityEditor;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(HierarchyActionsView), true)]
    public class HierarchyActionsViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            var view = (HierarchyActionsView)target;
            
            if (!view)
            {
                EditorGUILayout.LabelField("Invalid view");
                return;
            }
            
            string actions = view.GetActions();
            
            if (string.IsNullOrEmpty(actions))
            {
                EditorGUILayout.LabelField("No actions");
                return;
            }
            
            // draw with multiple lines
            EditorGUILayout.TextArea(actions);

            Repaint();
        }
    }
}
