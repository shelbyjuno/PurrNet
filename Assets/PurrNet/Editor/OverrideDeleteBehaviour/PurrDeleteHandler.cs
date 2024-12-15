using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    public static class PurrDeleteHandler
    {
        public static void CustomDeleteLogic(Object[] objectsToDelete)
        {
            // Example: Display a confirmation dialog
            bool confirmDelete = EditorUtility.DisplayDialog(
                "Delete Confirmation",
                $"Are you sure you want to delete {objectsToDelete.Length} object(s)?",
                "Yes", "No");

            if (confirmDelete)
            {
                // Perform custom deletion logic
                foreach (var obj in objectsToDelete)
                    Undo.DestroyObjectImmediate(obj);
            }
        }
    }
}