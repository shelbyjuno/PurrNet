using System.Collections.Generic;
using UnityEngine;
using System.IO;
using PurrNet.Logging;
#if UNITY_EDITOR
using PurrNet.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace PurrNet
{
    [CreateAssetMenu(fileName = "NetworkPrefabs", menuName = "PurrNet/Network Prefabs", order = -201)]
    public class NetworkPrefabs : ScriptableObject
#if UNITY_EDITOR
        , IPreprocessBuildWithReport
#endif
    {
        public bool autoGenerate = true;
        public Object folder;
        public List<GameObject> prefabs = new();

#if UNITY_EDITOR
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (autoGenerate)
                Generate();
        }
        
        private void OnValidate()
        {
            if (autoGenerate)
                Generate();
        }
#endif
        
        public bool TryGetPrefabId(GameObject prefab, out int id)
        {
            id = prefabs.IndexOf(prefab);
            return id != -1;
        }
        
        public bool TryGetPrefab(int id, out GameObject prefab)
        {
            if (id < 0 || id >= prefabs.Count)
            {
                prefab = null;
                return false;
            }

            prefab = prefabs[id];
            return true;
        }
        
        public bool TryGetPrefabFromGuid(string guid, out int id)
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i].TryGetComponent<PrefabLink>(out var link) && link.MatchesGUID(guid))
                {
                    id = i;
                    return true;
                }
            }
            
            id = -1;
            return false;
        }
        
        public bool TryGetPrefabFromGuid(string guid, out GameObject prefab, out int id)
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i].TryGetComponent<PrefabLink>(out var link) && link.MatchesGUID(guid))
                {
                    prefab = prefabs[i];
                    id = i;
                    return true;
                }
            }
            
            prefab = null;
            id = -1;
            return false;
        }
        
#if UNITY_EDITOR
        private bool _generating;
        static readonly List<NetworkIdentity> _identities = new();
        static readonly List<PrefabLink> _links = new();
#endif

        /// <summary>
        /// Editor only method to generate network prefabs from a specified folder.
        /// </summary>
        public void Generate()
        {
        #if UNITY_EDITOR
            if (ApplicationContext.isClone)
                return;
            
            if (_generating) return;
            
            _generating = true;
            
            EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Checking existing...",  0f);
            if (folder == null)
            {
                if (autoGenerate)
                {
                    prefabs.Clear();
                    EditorUtility.SetDirty(this);
                }

                EditorUtility.ClearProgressBar();
                _generating = false;
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(folderPath))
            {
                folder = null;
                
                if (autoGenerate)
                {
                    prefabs.Clear();
                    EditorUtility.SetDirty(this);
                }
                
                EditorUtility.ClearProgressBar();
                _generating = false;
                return;
            }

            // Track paths of existing prefabs for quick lookup
            var existingPaths = new HashSet<string>();
            foreach (var prefab in prefabs)
            {
                if (prefab)
                {
                    existingPaths.Add(AssetDatabase.GetAssetPath(prefab));
                }
            }
            
            EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Finding paths...",  0.1f);

            List<GameObject> foundPrefabs = new();
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            for (var i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                
                if (prefab)
                {
                    EditorUtility.DisplayProgressBar("Getting Network Prefabs", $"Looking at {prefab.name}",  0.1f + 0.7f * ((i + 1f) / guids.Length));
                    
                    prefab.GetComponentsInChildren(true, _identities);

                    if (_identities.Count > 0)
                        foundPrefabs.Add(prefab);
                }
            }

            EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Sorting...",  0.9f);
            
            // Order by creation time
            foundPrefabs.Sort((a, b) =>
            {
                string pathA = AssetDatabase.GetAssetPath(a);
                string pathB = AssetDatabase.GetAssetPath(b);

                var fileInfoA = new FileInfo(pathA);
                var fileInfoB = new FileInfo(pathB);

                return fileInfoA.CreationTime.CompareTo(fileInfoB.CreationTime);
            });
            
            EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Removing invalid prefabs...",  0.95f);
            // Remove invalid or no longer existing prefabs
            prefabs.RemoveAll(prefab => !prefab || !File.Exists(AssetDatabase.GetAssetPath(prefab)));
            
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (!foundPrefabs.Contains(prefabs[i]))
                {
                    prefabs.RemoveAt(i);
                    i--;
                }
            }
            
            // Add new prefabs found in the folder to the list if they don't already exist
            foreach (var foundPrefab in foundPrefabs)
            {
                string foundPath = AssetDatabase.GetAssetPath(foundPrefab);
                if (!existingPaths.Contains(foundPath))
                {
                    prefabs.Add(foundPrefab);
                }
            }
            
            PostProcess();

            EditorUtility.SetDirty(this);
            EditorUtility.ClearProgressBar();
            
            _generating = false;

            //PurrLogger.Log($"{prefabs.Count} prefabs found in {folderPath}", new LogStyle( Color.green));
        #endif
        }

        public void PostProcess()
        {
#if UNITY_EDITOR
            if (ApplicationContext.isClone)
                return;
            
            for (int i = 0; i < prefabs.Count; ++i)
            {
                if (!prefabs[i])
                    continue;
                
                var assetPath = AssetDatabase.GetAssetPath(prefabs[i]);
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
                
                prefabContents.GetComponentsInChildren(true, _links);

                if (_links.Count > 0)
                {
                    if (_links[0].gameObject != prefabContents)
                    {
                        for (int j = 0; j < _links.Count; j++)
                            DestroyImmediate(_links[j]);
                        var link = prefabContents.AddComponent<PrefabLink>();
                        link.SetGUID(guid);
                        link.hideFlags = HideFlags.NotEditable;
                    }
                    else
                    {
                        for (int j = 1; j < _links.Count; j++)
                            DestroyImmediate(_links[j]);
                        _links[0].SetGUID(guid);
                        _links[0].hideFlags = HideFlags.NotEditable;
                    }
                }
                else
                {
                    var link = prefabContents.AddComponent<PrefabLink>();
                    link.SetGUID(guid);
                    link.hideFlags = HideFlags.NotEditable;
                }
                
                PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }
}
