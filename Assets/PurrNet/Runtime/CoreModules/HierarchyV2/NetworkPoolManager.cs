using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Modules
{
    public static class NetworkPoolManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearPools()
        {
            foreach (var pool in _pools.Values)
                pool.Dispose();
            
            foreach (var pool in _scenePools.Values)
                pool.Dispose();
            
            _pools.Clear();
            _scenePools.Clear();
        }
        
        private static readonly Dictionary<IPrefabProvider, HierarchyPool> _pools = new();
        private static readonly Dictionary<SceneID, HierarchyPool> _scenePools = new();

        public static HierarchyPool GetScenePool(SceneID scene)
        {
            if (_scenePools.TryGetValue(scene, out var pool))
                return pool;
            
            var poolParent = new GameObject($"PurrNetPool-{scene.ToString()}")
            {
#if PURRNET_DEBUG_POOLING
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
#else
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.HideAndDontSave
#endif
            };
            
            pool = new HierarchyPool(poolParent.transform);
            _scenePools.Add(scene, pool);
            return pool;
        }
        
        public static HierarchyPool GetPool(IPrefabProvider prefabs)
        {
            if (_pools.TryGetValue(prefabs, out var pool))
                return pool;
            
            var poolParent = new GameObject($"PurrNetPool-{_pools.Count}")
            {
#if PURRNET_DEBUG_POOLING
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
#else
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.HideAndDontSave
#endif
            };
            
            Object.DontDestroyOnLoad(poolParent);
            pool = new HierarchyPool(poolParent.transform, prefabs);
            _pools.Add(prefabs, pool);
            
            for (int i = 0 ; i < prefabs.allPrefabs.Count; i++)
            {
                var prefab = prefabs.allPrefabs[i];

                if (prefab.TryGetComponent<PrefabLink>(out var link) && link.usePooling)
                {
                    for (int j = 0; j < link.poolWarmupCount; j++)
                        pool.Warmup(prefab);
                }
            }
            
            return pool;
        }
        
        public static void RemovePool(IPrefabProvider prefabs)
        {
            if (_pools.Remove(prefabs, out var pool))
                pool.Dispose();
        }

        public static void RemovePool(SceneID scene)
        {
            if (_scenePools.Remove(scene, out var pool))
                pool.Dispose();
        }
    }
}