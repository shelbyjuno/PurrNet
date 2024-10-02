using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using PurrNet.Logging;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using PurrNet.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace PurrNet
{
    [CreateAssetMenu(fileName = "NetworkPrefabs", menuName = "PurrNet/Network Prefabs", order = -201)]
    public class NetworkPrefabs : PrefabProviderScriptable
#if UNITY_EDITOR
        , IPreprocessBuildWithReport
#endif
    {
        public bool autoGenerate = true;
        public bool networkOnly = true;
        public Object folder;
        public List<GameObject> prefabs = new();

#if UNITY_EDITOR
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (autoGenerate)
                Generate();
        }
#endif

        public override GameObject GetPrefabFromGuid(string guid)
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i].TryGetComponent<PrefabLink>(out var link) && link.MatchesGUID(guid))
                    return prefabs[i];
            }

            return null;
        }

        public override bool TryGetPrefab(int id, out GameObject prefab)
        {
            if (id < 0 || id >= prefabs.Count)
            {
                prefab = null;
                return false;
            }

            prefab = prefabs[id];
            return true;
        }

        public override bool TryGetPrefab(int id, int offset, out GameObject prefab)
        {
            if (!TryGetPrefab(id, out var root))
            {
                prefab = null;
                return false;
            }

            if (offset == 0)
            {
                prefab = root;
                return true;
            }

            root.GetComponentsInChildren(true, _identities);

            if (offset < 0 || offset >= _identities.Count)
            {
                prefab = null;
                return false;
            }

            prefab = _identities[offset].gameObject;
            return true;
        }

        public override bool TryGetPrefabID(string guid, out int id)
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

        static readonly List<NetworkIdentity> _identities = new();
#if UNITY_EDITOR
        private bool _generating;
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

            try
            {
                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Checking existing...", 0f);

                if (folder == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(folder)))
                {
                    EditorUtility.DisplayProgressBar("Getting Network Prefabs", "No folder found...", 0f);
                    if (autoGenerate && prefabs.Count > 0)
                    {
                        prefabs.Clear();
                        EditorUtility.SetDirty(this);
                    }

                    EditorUtility.ClearProgressBar();
                    _generating = false;
                    return;
                }

                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Found folder...", 0f);
                string folderPath = AssetDatabase.GetAssetPath(folder);

                if (string.IsNullOrEmpty(folderPath))
                {
                    EditorUtility.DisplayProgressBar("Getting Network Prefabs", "No folder path...", 0f);

                    if (autoGenerate && prefabs.Count > 0)
                    {
                        prefabs.Clear();
                        EditorUtility.SetDirty(this);
                    }

                    EditorUtility.ClearProgressBar();
                    _generating = false;
                    PurrLogger.LogError("Exiting Generate method early due to empty folder path.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Getting existing paths...", 0f);

                // Track paths of existing prefabs for quick lookup
                var existingPaths = new HashSet<string>();
                foreach (var prefab in prefabs)
                {
                    if (prefab)
                    {
                        existingPaths.Add(AssetDatabase.GetAssetPath(prefab));
                    }
                }

                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Finding paths...", 0.1f);

                List<GameObject> foundPrefabs = new();
                string[] guids = AssetDatabase.FindAssets("t:prefab", new[] { folderPath });
                for (var i = 0; i < guids.Length; i++)
                {
                    var guid = guids[i];
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                    if (prefab)
                    {
                        EditorUtility.DisplayProgressBar("Getting Network Prefabs", $"Looking at {prefab.name}",
                            0.1f + 0.7f * ((i + 1f) / guids.Length));

                        if (!networkOnly)
                        {
                            foundPrefabs.Add(prefab);
                            continue;
                        }

                        prefab.GetComponentsInChildren(true, _identities);

                        if (_identities.Count > 0)
                            foundPrefabs.Add(prefab);
                    }
                }

                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Sorting...", 0.9f);

                // Order by creation time
                foundPrefabs.Sort((a, b) =>
                {
                    string pathA = AssetDatabase.GetAssetPath(a);
                    string pathB = AssetDatabase.GetAssetPath(b);

                    var fileInfoA = new FileInfo(pathA);
                    var fileInfoB = new FileInfo(pathB);

                    return fileInfoA.CreationTime.CompareTo(fileInfoB.CreationTime);
                });

                EditorUtility.DisplayProgressBar("Getting Network Prefabs", "Removing invalid prefabs...", 0.95f);
                // Remove invalid or no longer existing prefabs
                int removed = prefabs.RemoveAll(prefab => !prefab || !File.Exists(AssetDatabase.GetAssetPath(prefab)));
                int added = 0;

                for (int i = 0; i < prefabs.Count; i++)
                {
                    if (!foundPrefabs.Contains(prefabs[i]))
                    {
                        prefabs.RemoveAt(i);
                        removed++;
                        i--;
                    }
                }

                // Add new prefabs found in the folder to the list if they don't already exist
                foreach (var foundPrefab in foundPrefabs)
                {
                    var foundPath = AssetDatabase.GetAssetPath(foundPrefab);
                    if (!existingPaths.Contains(foundPath))
                    {
                        prefabs.Add(foundPrefab);
                        added++;
                    }
                }

                PostProcess();

                if (removed > 0 || added > 0)
                    EditorUtility.SetDirty(this);
            }
            catch (Exception e)
            {
                PurrLogger.LogError($"An error occurred during prefab generation: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _generating = false;
            }
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

                if (!assetPath.EndsWith(".prefab"))
                    continue;
                
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
                
                bool isDirty = false;

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
                        isDirty = true;
                    }
                    else
                    {
                        if (_links.Count > 1)
                        {
                            isDirty = true;
                            for (int j = 1; j < _links.Count; j++)
                                DestroyImmediate(_links[j]);
                        }

                        if (_links[0].SetGUID(guid))
                        {
                            isDirty = true;
                            _links[0].hideFlags = HideFlags.NotEditable;
                        }
                    }
                }
                else
                {
                    var link = prefabContents.AddComponent<PrefabLink>();
                    link.SetGUID(guid);
                    link.hideFlags = HideFlags.NotEditable;
                    isDirty = true;
                }

                if (isDirty)
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }
}