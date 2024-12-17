using System.Collections.Generic;
using PurrNet.Modules;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class CustomDragAndDropHandler
{
    private static readonly HashSet<int> _beforeObjects = new ();
    private static readonly HashSet<int> _afterObjects = new ();
    private static readonly HashSet<int> _newObjects = new ();
    
    static int _lastDragDropEventFrame = -1;
    
    private static void TakeSnapShotOfHierarchy(HashSet<int> set)
    {
        set.Clear();
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allObjects.Length; i++)
        {
            var obj = allObjects[i];
            set.Add(obj.GetInstanceID());
        }
    }
    
    static CustomDragAndDropHandler()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
    } 

    private static void OnHierarchyItemGUI(int instanceid, Rect selectionrect)
    {
        bool isPlaying = Application.isPlaying;
            
        if (!isPlaying)
            return;
        
        switch (Event.current.type)
        {
            case EventType.DragPerform:
            {
                if (_lastDragDropEventFrame != Time.frameCount)
                {
                    TakeSnapShotOfHierarchy(_beforeObjects);
                    _lastDragDropEventFrame = Time.frameCount;
                }

                break;
            }
            case EventType.DragExited:
            {
                CheckNewInstantiations();
                break;
            }
        }
    }
    
    private static void OnSceneGUI(SceneView sceneView)
    {
        bool isPlaying = Application.isPlaying;
            
        if (!isPlaying)
            return;

        if (Event.current.type == EventType.DragExited)
        {
            foreach (var gos in Selection.gameObjects)
                PurrNetGameObjectUtils.NotifyGameObjectCreated(gos);
        }
    }
    
    private static void CheckNewInstantiations()
    {
        TakeSnapShotOfHierarchy(_afterObjects);
        _newObjects.Clear();
            
        foreach (var id in _afterObjects)
        {
            if (!_beforeObjects.Contains(id))
                _newObjects.Add(id);
        }

        if (_newObjects.Count > 0)
        {
            foreach (var id in _newObjects)
            {
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (go)
                {
                    bool isAnyParentInNewObjects = false;
                    
                    var trs = go.transform.parent;
                    
                    while (trs)
                    {
                        if (_newObjects.Contains(trs.gameObject.GetInstanceID()))
                        {
                            isAnyParentInNewObjects = true;
                            break;
                        }
                        
                        trs = trs.parent;
                    }
                    
                    if (!isAnyParentInNewObjects)
                        PurrNetGameObjectUtils.NotifyGameObjectCreated(go);
                }
            }
        }
                
        _beforeObjects.Clear();
        _beforeObjects.UnionWith(_afterObjects);
    }
}
