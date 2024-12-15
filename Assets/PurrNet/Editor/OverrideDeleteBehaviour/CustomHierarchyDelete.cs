using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [InitializeOnLoad]
    public class CustomHierarchyDelete
    {
        static CustomHierarchyDelete()
        {
            // Subscribe to the Hierarchy GUI callback
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceid, Rect selectionrect)
        {
            var currentEvent = Event.current;

            // Check if Delete or Backspace is pressed
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode is KeyCode.Delete or KeyCode.Backspace)
            {
                // Get the selected objects in the hierarchy
                var selectedObjects = Selection.objects;

                if (selectedObjects.Length > 0)
                {
                    if (PurrDeleteHandler.CustomDeleteLogic(selectedObjects))
                        currentEvent.Use();
                }
            }
        }
    }
}
