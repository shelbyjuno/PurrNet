using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public class HierarchyV2
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearPools()
        {
            _pools.Clear();
        }
        
        private readonly bool _asServer;
        private readonly SceneID _sceneId;
        private readonly Scene _scene;
        private readonly ScenePlayersModule _players;
        private readonly VisilityV2 _visibility;
        
        private HierarchyPool _prefabPool;
        private readonly HierarchyPool _scenePool;

        private static readonly Dictionary<IPrefabProvider, HierarchyPool> _pools = new();
        
        public HierarchyV2(SceneID sceneId, Scene scene, ScenePlayersModule players, IPrefabProvider prefabs, bool asServer)
        {
            _sceneId = sceneId;
            _scene = scene;
            _players = players;
            _visibility = new VisilityV2();
            _asServer = asServer;
            
            var gameObject = new GameObject($"PurrNetPool-SceneObjects-{sceneId}")
            {
#if PURRNET_DEBUG_POOLING
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
#else
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.HideAndDontSave
#endif
            };
            
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            _scenePool = new HierarchyPool(gameObject.transform, prefabs);
            
            SetupPrefabPool(prefabs);
        }

        private void SetupPrefabPool(IPrefabProvider prefabs)
        {
            if (_pools.TryGetValue(prefabs, out _prefabPool))
                return;
            
            var poolParent = new GameObject($"PurrNetPool-{_pools.Count}")
            {
#if PURRNET_DEBUG_POOLING
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
#else
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.HideAndDontSave
#endif
            };
            
            Object.DontDestroyOnLoad(poolParent);
            _prefabPool = new HierarchyPool(poolParent.transform, prefabs);
            _pools.Add(prefabs, _prefabPool);
            
            for (int i = 0 ; i < prefabs.allPrefabs.Count; i++)
            {
                var prefab = prefabs.allPrefabs[i];

                if (prefab.TryGetComponent<PrefabLink>(out var link) && link.usePooling)
                {
                    for (int j = 0; j < link.poolWarmupCount; j++)
                        _prefabPool.Warmup(prefab);
                }
            }
        }

        public void Enable()
        {
            _visibility.Enable();
        }

        public void Disable()
        {
            _visibility.Disable();
        }

        public void Spawn(GameObject gameObject)
        {
        }
        
        public void PreNetworkMessages()
        {
            _visibility.EvaluateAll();
        }

        public void PostNetworkMessages()
        {
            
        }

        public void CreatePrototype(GameObjectPrototype prototype)
        {
            PrefabLink.StartIgnoreAutoSpawn();
            var pool = prototype.isScenePrototype ? _scenePool : _prefabPool;

            if (!pool.TryBuildPrototype(prototype, out var result, out var shouldActivate))
            {
                PurrLogger.LogError("Failed to create prototype");
                PrefabLink.StopIgnoreAutoSpawn();
                return;
            }
            
            result.transform.SetParent(null, false);
            
            SceneManager.MoveGameObjectToScene(result, _scene);
            
            if (shouldActivate)
                result.SetActive(true);
            PrefabLink.StopIgnoreAutoSpawn();
        }
    }
}