using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    public static class PurrDeleteHandler
    {
        static List<GameObject> GetAllNetworkedObjects(Object[] objectsToDelete)
        {
            var networkedObjects = new List<GameObject>();
            
            foreach (var obj in objectsToDelete)
            {
                if (obj is GameObject go)
                {
                    if (go.GetComponentInChildren<NetworkIdentity>())
                        networkedObjects.Add(go);
                }
            }
            
            return networkedObjects;
        }
        
        public static bool CustomDeleteLogic(Object[] objectsToDelete)
        {
            var networkedObjects = GetAllNetworkedObjects(objectsToDelete);

            // if nothing network related just do normal delete
            if (networkedObjects.Count == 0)
                return false;
            
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

            return true;
        }
    }
}