using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Octokit;
using PurrNet.Logging;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    public struct PurrNetEntry
    {
        public string name;
        public string url;
        public string query;
        public string fragment;

        public override string ToString()
        {
            return $"{name} -> {url}{query}{fragment}";
        }
    }
    
    public class PurrNetBranchManager : EditorWindow
    {
        [MenuItem("Tools/PurrNet/Version Manager")]
        public static void ShowWindow()
        {
            GetWindow<PurrNetBranchManager>("PurrNet Version Manager");
        }
        
        static readonly Uri repositoryUrl = new Uri("https://github.com/BlenMiner/PurrNet");
        
        private GitHubClient _client;
        private PurrNetEntry? _purrnetEntry;
        
        private readonly List<Branch> _branches = new List<Branch>();
        private readonly List<Release> _releases = new List<Release>();

        static bool TryGetPurrnetEntry(out PurrNetEntry entry)
        {
            const string PATH = "Packages/manifest.json";
            var manifestString = File.ReadAllText(PATH);
            
            var manifest = JObject.Parse(manifestString);

            if (manifest.TryGetValue("purrnet", out var purrnet))
            {
                var value = purrnet.Value<string>();
                var url = new Uri(value);
                entry = new PurrNetEntry
                {
                    name = "purrnet",
                    url = $"{url.Scheme}://{url.Host}/{url.AbsolutePath}",
                    query = url.Query,
                    fragment = url.Fragment
                };
                return true;
            }
            
            entry = default;
            return false;
        }
        
        private void OnEnable()
        {
            if (TryGetPurrnetEntry(out var entry))
                 _purrnetEntry = entry;
            else _purrnetEntry = null;
            
            _client ??= new GitHubClient(new ProductHeaderValue("purrnet"), repositoryUrl);
            
            RefreshBranches();
            RefreshReleases();
        }
        
        private bool _isRefreshingBranches;

        private async void RefreshBranches()
        {
            try
            {
                if (_isRefreshingBranches)
                    return;
                
                _isRefreshingBranches = true;
                
                var branches = await _client.Repository.Branch.GetAll("BlenMiner", "PurrNet");
                _branches.Clear();

                foreach (var branch in branches)
                    _branches.Add(branch);
                
                _isRefreshingBranches = false;
            }
            catch (Exception e)
            {
                _isRefreshingBranches = false;
                PurrLogger.LogError(e.Message);
            }
        }
        
        private bool _isRefreshingReleases;
        
        private async void RefreshReleases()
        {
            try
            {
                if (_isRefreshingReleases)
                    return;
                
                _isRefreshingReleases = true;
                
                var releases = await _client.Repository.Release.GetAll("BlenMiner", "PurrNet", new ApiOptions
                {
                    PageCount = 1,
                    PageSize = 10
                });
                
                _releases.Clear();

                foreach (var release in releases)
                    _releases.Add(release);
                
                _isRefreshingReleases = false;
            }
            catch (Exception e)
            {
                _isRefreshingReleases = false;
                PurrLogger.LogError(e.Message);
            }
        }

        private void OnGUI()
        {
            if (_isRefreshingBranches)
                GUI.enabled = false;
            GUILayout.Label($"Branches ({_branches.Count}):");

            foreach (var b in _branches)
            {
                GUILayout.Label(b.Name);
            }
            
            if (GUILayout.Button("Refresh Branches"))
            {
                RefreshBranches();
            }
            GUI.enabled = true;
            
            if (_isRefreshingReleases)
                GUI.enabled = false;
            GUILayout.Label($"Releases ({_releases.Count}):");

            foreach (var b in _releases)
            {
                GUILayout.Label(b.Name);
            }
            
            if (GUILayout.Button("Refresh Releases"))
            {
                RefreshReleases();
            }
            GUI.enabled = true;
        }
    }
}
