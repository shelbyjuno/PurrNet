using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [InitializeOnLoad]
    public class CustomSceneViewDelete
    {
        static CustomSceneViewDelete()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            var currentEvent = Event.current;

            // Check if Delete or Backspace is pressed
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode is KeyCode.Delete or KeyCode.Backspace)
            {
                // Get selected objects
                var selectedObjects = Selection.objects;

                if (selectedObjects.Length > 0)
                {
                    PurrDeleteHandler.CustomDeleteLogic(selectedObjects);
                    currentEvent.Use();
                }
            }
        }
    }
}
