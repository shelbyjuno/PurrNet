using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections;
using PurrNet;

public class TemplateProjectCopier : EditorWindow
{
    private string templateFolderPath = "Assets/PurrNet/Examples/Template";
    private string newProjectName = "";
    private string newFolderPath = "";
    private bool projectCopied = false;

    [MenuItem("Tools/PurrNet/Create New Example Project")]
    public static void ShowWindow()
    {
        GetWindow<TemplateProjectCopier>("Create New Example Project");
    }

    private void OnGUI()
    {
        GUILayout.Label("Create New Example Project", EditorStyles.boldLabel);

        templateFolderPath = EditorGUILayout.TextField("Template Folder Path", templateFolderPath);
        newProjectName = EditorGUILayout.TextField("New Project Name", newProjectName);

        GUI.enabled = !string.IsNullOrEmpty(newProjectName) && !projectCopied;
        if (GUILayout.Button("Step 1: Copy Project"))
        {
            CopyProject();
        }
        GUI.enabled = projectCopied;
        if (GUILayout.Button("Step 2: Update Assets"))
        {
            UpdateAssets();
        }
        GUI.enabled = true;
    }

    private void CopyProject()
    {
        newFolderPath = Path.Combine(Path.GetDirectoryName(templateFolderPath), newProjectName);

        // Step 1: Perform all file system operations
        CopyAndUpdateFiles(templateFolderPath, newFolderPath);

        // Refresh the asset database to recognize the new files
        AssetDatabase.Refresh();

        projectCopied = true;
    }

    private void UpdateAssets()
    {
        if (!projectCopied)
        {
            EditorUtility.DisplayDialog("Error", "Please copy the project first.", "OK");
            return;
        }

        // Step 2: Perform all Unity asset operations
        EditorCoroutineUtility.StartCoroutine(UpdateUnityAssetsCoroutine(newFolderPath), this);
    }

    private IEnumerator UpdateUnityAssetsCoroutine(string folderPath)
    {
        while (EditorApplication.isCompiling)
            yield return null;

        yield return new EditorCoroutineUtility.EditorWaitForSeconds(1);

        RenameTemplateFiles(folderPath);
        UpdateAssetReferences(folderPath);
        UpdatePrefabReferences(folderPath);
        UpdateSceneReferences(folderPath);

        EditorUtility.DisplayDialog("Success", $"New project '{newProjectName}' created and updated successfully! Please make sure the PlayerSpawner is populated with a new player.", "OK");
    }

    private void CopyAndUpdateFiles(string sourcePath, string destPath)
    {
        CopyFolder(sourcePath, destPath);
        UpdateScriptNamespaces(destPath);
        RenameTemplateFiles(destPath);
    }

    private void CopyFolder(string sourcePath, string destPath)
    {
        if (Directory.Exists(destPath))
        {
            Directory.Delete(destPath, true);
        }
        FileUtil.CopyFileOrDirectory(sourcePath, destPath);
    }

    private void UpdateScriptNamespaces(string folderPath)
    {
        string[] csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            content = content.Replace("namespace PurrNet.Examples.Template", $"namespace PurrNet.Examples.{newProjectName}");
            File.WriteAllText(file, content);
        }
    }
    
    private void UpdateAssetReferences(string folderPath)
    {
        string[] allAssets = AssetDatabase.FindAssets("", new[] { folderPath });
        foreach (string guid in allAssets)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) continue;

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.GetIterator();

            bool modified = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                {
                    string referencePath = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                    if (!string.IsNullOrEmpty(referencePath) && referencePath.Contains("Template"))
                    {
                        string newPath = referencePath.Replace("Template", newProjectName);
                        Object newReference = AssetDatabase.LoadAssetAtPath<Object>(newPath);
                        if (newReference != null)
                        {
                            property.objectReferenceValue = newReference;
                            modified = true;
                        }
                    }
                }
            }

            if (modified)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void RenameTemplateFiles(string folderPath)
    {
        string[] allFiles = Directory.GetFiles(folderPath, "*Template*", SearchOption.AllDirectories);
        foreach (string file in allFiles)
        {
            string fileName = Path.GetFileName(file);
            string newFileName = fileName.Replace("Template", newProjectName);
            string newFilePath = Path.Combine(Path.GetDirectoryName(file), newFileName);
        
            AssetDatabase.RenameAsset(file, newFileName);

            string fileExtension = Path.GetExtension(newFilePath).ToLower();
            if (fileExtension == ".asset" || fileExtension == ".prefab")
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(newFilePath);
                if (asset != null)
                {
                    asset.name = Path.GetFileNameWithoutExtension(newFileName);
                    EditorUtility.SetDirty(asset);

                    if (fileExtension == ".prefab")
                    {
                        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(newFilePath);
                        prefabRoot.name = Path.GetFileNameWithoutExtension(newFileName);
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, newFilePath);
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            else if (fileExtension == ".cs" || fileExtension == ".txt")
            {
                string content = File.ReadAllText(newFilePath);
                content = content.Replace("Template", newProjectName);
                File.WriteAllText(newFilePath, content);
            }
        }
    
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private void UpdatePrefabReferences(string folderPath)
    {
        string[] prefabFiles = Directory.GetFiles(folderPath, "*.prefab", SearchOption.AllDirectories);
        foreach (string prefabPath in prefabFiles)
        {
            try
            {
                GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                bool modified = false;

                if (prefab.name.Contains("Template"))
                {
                    prefab.name = prefab.name.Replace("Template", newProjectName);
                    modified = true;
                }

                Component[] components = prefab.GetComponentsInChildren<Component>(true);
                foreach (Component component in components)
                {
                    if (component == null) continue;

                    MonoBehaviour monoBehaviour = component as MonoBehaviour;
                    if (monoBehaviour != null)
                    {
                        MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
                        if (script != null)
                        {
                            string scriptPath = AssetDatabase.GetAssetPath(script);
                            if (scriptPath.Contains("Template"))
                            {
                                string newScriptPath = scriptPath.Replace("Template", newProjectName);
                                MonoScript newScript = AssetDatabase.LoadAssetAtPath<MonoScript>(newScriptPath);
                                if (newScript != null)
                                {
                                    SerializedObject serializedObject = new SerializedObject(monoBehaviour);
                                    SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
                                    scriptProperty.objectReferenceValue = newScript;
                                    serializedObject.ApplyModifiedProperties();
                                    modified = true;
                                }
                            }
                        }
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating prefab {prefabPath}: {e.Message}");
            }
        }
    }
    
    private void UpdateSceneReferences(string folderPath)
    {
        string[] sceneFiles = Directory.GetFiles(folderPath, "*.unity", SearchOption.AllDirectories);
        foreach (string scenePath in sceneFiles)
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool sceneModified = false;

                var sceneRootObjects = scene.GetRootGameObjects();
                foreach (var obj in sceneRootObjects)
                {
                    if (UpdateGameObjectReferences(obj, folderPath))
                    {
                        sceneModified = true;
                    }
                }

                string networkPrefabsPath = Path.Combine(folderPath, $"{newProjectName}_Prefabs.asset");
                var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>(networkPrefabsPath);
                if (networkPrefabs != null)
                {
                    SerializedObject serializedPrefabs = new SerializedObject(networkPrefabs);
                    SerializedProperty folderProp = serializedPrefabs.FindProperty("folder");
                    if (folderProp != null)
                    {
                        string newPrefabsFolderPath = Path.Combine(folderPath, "Prefabs");
                        Object newFolder = AssetDatabase.LoadAssetAtPath<Object>(newPrefabsFolderPath);
                        if (newFolder != null)
                        {
                            folderProp.objectReferenceValue = newFolder;
                            serializedPrefabs.ApplyModifiedProperties();
                            sceneModified = true;
                        }
                        else
                        {
                            Debug.LogError($"Could not find new Prefabs folder at path: {newPrefabsFolderPath}");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Could not find NetworkPrefabs asset at path: {networkPrefabsPath}");
                }

                if (sceneModified)
                {
                    EditorSceneManager.SaveScene(scene);
                }
                EditorSceneManager.CloseScene(scene, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating scene {scenePath}: {e.Message}");
            }
        }
    }

    private bool UpdateGameObjectReferences(GameObject obj, string rootFolderPath)
    {
        bool modified = false;
        Component[] components = obj.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null) continue;

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("Template"))
                    {
                        string newPath = assetPath.Replace("Template", newProjectName);
                        Object newReference = AssetDatabase.LoadAssetAtPath<Object>(newPath);
                        if (newReference != null)
                        {
                            property.objectReferenceValue = newReference;
                            modified = true;
                        }
                    }
                }
            }

            if (modified)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        if (obj.TryGetComponent<NetworkManager>(out var networkManager))
        {
            SerializedObject serializedManager = new SerializedObject(networkManager);

            string networkRulesPath = Path.Combine(rootFolderPath, $"{newProjectName}_Rules.asset");
            string networkPrefabsPath = Path.Combine(rootFolderPath, $"{newProjectName}_Prefabs.asset");

            var networkRules = AssetDatabase.LoadAssetAtPath<NetworkRules>(networkRulesPath);
            if (networkRules != null)
            {
                SerializedProperty networkRulesProp = serializedManager.FindProperty("_networkRules");
                networkRulesProp.objectReferenceValue = networkRules;
                modified = true;
            }
            else
            {
                Debug.LogError($"Could not find NetworkRules asset at path: {networkRulesPath}");
            }

            var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>(networkPrefabsPath);
            if (networkPrefabs != null)
            {
                SerializedProperty networkPrefabsProp = serializedManager.FindProperty("_networkPrefabs");
                networkPrefabsProp.objectReferenceValue = networkPrefabs;
                modified = true;
            }
            else
            {
                Debug.LogError($"Could not find NetworkPrefabs asset at path: {networkPrefabsPath}");
            }

            if (modified)
            {
                serializedManager.ApplyModifiedProperties();
            }
        }

        foreach (Transform child in obj.transform)
        {
            if (UpdateGameObjectReferences(child.gameObject, rootFolderPath))
            {
                modified = true;
            }
        }

        return modified;
    }
}

// Helper class for EditorCoroutines
public static class EditorCoroutineUtility
{
    public class EditorWaitForSeconds : IEnumerator
    {
        private float _seconds;
        private double _done;
        public EditorWaitForSeconds(float seconds)
        {
            _seconds = seconds;
            _done = EditorApplication.timeSinceStartup + seconds;
        }
        public bool MoveNext()
        {
            return EditorApplication.timeSinceStartup < _done;
        }
        public void Reset() { }
        public object Current { get { return null; } }
    }

    public static void StartCoroutine(IEnumerator routine, object owner)
    {
        EditorApplication.update += Update;
        
        void Update()
        {
            try
            {
                if (!routine.MoveNext())
                {
                    EditorApplication.update -= Update;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.update -= Update;
            }
        }
    }
}