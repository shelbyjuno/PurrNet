using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using PurrNet.Logging;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace PurrNet.Editor
{
    public class AddonLibrary : EditorWindow
    {
        private static List<Addon> _addons = new();
        private static List<UnityWebRequest> _imageRequests = new();

        private static bool _fetchedAddons;
        private UnityWebRequest _request;
        private Vector2 scrollViewPosition;
        private Texture2D defaultIcon;

        private static int imageWidth = 100;
        private static int sectionOneWidth = 250;
        private static int sectionTwoWidth = 100;
        
        [MenuItem("Window/PurrNet Addon Library")]
        public static void ShowWindow()
        {
            _fetchedAddons = false;
            _imageRequests.Clear();
            
            int width = (imageWidth + sectionOneWidth + sectionTwoWidth) * 2;
            var window = GetWindow<AddonLibrary>("PurrNet Addon Library");
            window.minSize = new Vector2(width, 300);
            window.maxSize = new Vector2(width, 9999);
            window.LoadDefaultIcon();
        }

        private void OnGUI()
        {
            if (_addons.Count <= 0 && !_fetchedAddons)
            {
                HandleGettingAddons();
                HandleWaiting("Populating addons...");
                return;
            }

            foreach (var imageRequest in _imageRequests)
            {
                if (!imageRequest.isDone)
                {
                    if (imageRequest.result == UnityWebRequest.Result.ConnectionError || imageRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        HandleError(imageRequest.result.ToString());
                        return;
                    }
                }
            }

            foreach (var request in _imageRequests)
            {
                if (!request.isDone)
                {
                    HandleWaiting("Loading images...");
                    return;
                }
            }
            
            scrollViewPosition = EditorGUILayout.BeginScrollView(scrollViewPosition);
            HandleAddons();
            EditorGUILayout.EndScrollView();
        }

        private void LoadDefaultIcon()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string directory = System.IO.Path.GetDirectoryName(scriptPath);
            string relativePath = System.IO.Path.Combine(directory, "Editor Default Resources", "Pebbles.png");
            defaultIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        }

        private void HandleGettingAddons()
        {
            if (_request == null)
            {
                _request = UnityWebRequest.Get("https://pebblesgames.com/wp-content/PurrNet/PurrNetAddons.json");
                _request.SendWebRequest();
            }
            else if (_request.isDone)
            {
                if (_request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(_request.error);
                }
                else
                {
                    string json = _request.downloadHandler.text;
                    AddonsWrapper wrapper = JsonUtility.FromJson<AddonsWrapper>(json);
                    if (wrapper == null || wrapper.addons == null)
                    {
                        HandleError("Failed to parse JSON");
                        return;
                    }

                    if (wrapper.addons.Count <= 0)
                    {
                        HandleError("No addons found");
                        return;
                    }

                    _addons.Clear();

                    foreach (var addon in wrapper.addons)
                    {
                        // Synchronously download the image
                        var imageRequest = UnityWebRequestTexture.GetTexture(addon.imageUrl);
                        imageRequest.SendWebRequest();
                        _imageRequests.Add(imageRequest);
                        addon.icon = defaultIcon;
                        _addons.Add(addon);
                    }

                    _fetchedAddons = true;
                }
            }
        }

        private void HandleAddons()
        {
            for (var i = 0; i < _addons.Count; i += 2)
            {
                EditorGUILayout.BeginHorizontal(); // Begin a new row

                for (var j = 0; j < 2; j++)
                {
                    if (i + j < _addons.Count)
                    {
                        var addon = _addons[i + j]; 
                        EditorGUILayout.BeginVertical("box");

                        EditorGUILayout.BeginHorizontal();

                        if (_imageRequests.Count > i + j && _imageRequests[i + j].isDone && _imageRequests[i + j].result == UnityWebRequest.Result.Success)
                            addon.icon = DownloadHandlerTexture.GetContent(_imageRequests[i + j]);

                        GUILayout.Label(addon.icon, GUILayout.Width(imageWidth), GUILayout.Height(imageWidth));

                        EditorGUILayout.BeginVertical(GUILayout.MaxWidth(sectionOneWidth));
                        EditorGUILayout.LabelField(addon.name, EditorStyles.boldLabel, GUILayout.MaxWidth(sectionOneWidth));
                        EditorGUILayout.LabelField(addon.description, GUILayout.MaxWidth(sectionOneWidth));
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical(GUILayout.MaxWidth(sectionTwoWidth));
                        EditorGUILayout.LabelField($"Version {addon.version}", GUILayout.MaxWidth(sectionTwoWidth));
                        EditorGUILayout.LabelField("Author:", GUILayout.MaxWidth(sectionTwoWidth));
                        EditorGUILayout.LabelField(addon.author, GUILayout.MaxWidth(sectionTwoWidth));
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.EndHorizontal();

                        if (ExistsInProject(addon))
                        {
                            GUIStyle redButtonStyle = new GUIStyle(GUI.skin.button);
                            redButtonStyle.normal.textColor = Color.red;
                            if (GUILayout.Button("Remove", redButtonStyle))
                            {
                                RemoveAddon(addon);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Install"))
                            {
                                AddAddon(addon);
                            }
                        }
                        

                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(10);
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(10);
            }
        }

        private void AddAddon(Addon addon)
        {
            // Implement the logic to update the manifest file with the given gitUrl
            if (addon.asManifest)
                AddAddon_Manifest(addon);
            else
                AddAddon_Assets(addon);
        }

        private void RemoveAddon(Addon addon)
        {
            
        }

        private void HandleWaiting(string message)
        {
            GUILayout.BeginVertical("Box");
            GUILayout.Label(message);
            GUILayout.EndVertical();
        }
        
        private void HandleError(string error)
        {
            EditorGUILayout.HelpBox("Failed to fetch addons: " + error, MessageType.Error);
        }

        private bool ExistsInProject(Addon addon)
        {
            return false;
        }
        
        private void AddAddon_Manifest(Addon addon)
        {
            string manifestPath = "Packages/manifest.json";
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var dependencies = manifest["dependencies"] as JObject;

            string parsedName = addon.name.Replace(" ", "").ToLower();
            dependencies["com.purrnet."+parsedName] = addon.projectUrl;
            File.WriteAllText(manifestPath, manifest.ToString());
        }
        
        private void AddAddon_Assets(Addon addon)
        {
            string parsedName = addon.name.Replace(" ", "").ToLower();
            string tempPath = Path.Combine(Path.GetTempPath(), parsedName + ".unitypackage");

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(addon.projectUrl, tempPath);
            }

            if (File.Exists(tempPath))
            {
                AssetDatabase.ImportPackage(tempPath, false);
                File.Delete(tempPath);
            }
            else
            {
                PurrLogger.LogError($"Couldn't get the {addon.name} package and install it."); 
            }
        }

        [System.Serializable]
        private class Addon
        {
            public string name;
            public string description;
            public string version;
            public string author;
            public bool asManifest;
            public string projectUrl;
            public string imageUrl;
            public Texture2D icon;
        }

        [System.Serializable]
        private class AddonsWrapper
        {
            public List<Addon> addons;
        }
    }
}
