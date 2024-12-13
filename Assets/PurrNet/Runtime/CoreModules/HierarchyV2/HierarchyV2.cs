using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Pooling;
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
        
        private readonly NetworkManager _manager;
        private readonly bool _asServer;
        private readonly SceneID _sceneId;
        private readonly Scene _scene;
        private readonly ScenePlayersModule _players;
        private readonly VisilityV2 _visibility;
        
        private HierarchyPool _prefabPool;
        private readonly HierarchyPool _scenePool;

        private static readonly Dictionary<IPrefabProvider, HierarchyPool> _pools = new();
        
        public bool areSceneObjectsReady { get; private set; }
        
        public HierarchyV2(NetworkManager manager, SceneID sceneId, Scene scene, ScenePlayersModule players, IPrefabProvider prefabs, bool asServer)
        {
            _manager = manager;
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
            SetupSceneObjects(scene);
            
            areSceneObjectsReady = true;
        }

        private void SetupSceneObjects(Scene scene)
        {
            var allSceneIdentities = ListPool<NetworkIdentity>.Instantiate();
            SceneObjectsModule.GetSceneIdentities(scene, allSceneIdentities);
            
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            var count = allSceneIdentities.Count;
            for (int i = 0; i < count; i++)
            {
                var identity = allSceneIdentities[i];
                var root = identity.GetRootIdentity();
                
                identity.SetIsSceneObject(true);

                if (!roots.Add(root))
                    continue;
                
                var children = ListPool<NetworkIdentity>.Instantiate();
                root.GetComponentsInChildren(true, children);
                
                var cc = children.Count;
                var pid = -i - 2;
                var rootDepth = root.transform.GetTransformDepth();
                
                for (int j = 0; j < cc; j++)
                {
                    var child = children[j];
                    var trs = child.transform;
                    int depth = trs.GetTransformDepth() - rootDepth;
                    child.PreparePrefabInfo(pid, trs.GetSiblingIndex(), depth, true);
                }

                SpawnSceneObject(children);
                ListPool<NetworkIdentity>.Destroy(children);
            }
            
            ListPool<NetworkIdentity>.Destroy(allSceneIdentities);
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
        
        [UsedImplicitly]
        public void Spawn(PrefabLink prefabLink)
        {
            PurrLogger.Log($"Auto spawning {prefabLink.prefabGuid} in scene {_sceneId}");
        }

        private ushort _sceneObjectSpawnCount;
        
        private void SpawnSceneObject(List<NetworkIdentity> children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.isSceneObject)
                {
                    var id = new NetworkID(default, _sceneObjectSpawnCount++);
                    child.SetIdentity(_manager, _sceneId, id, _asServer);
                }
            }
        }
        
        public void Despawn(GameObject gameObject)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            gameObject.GetComponentsInChildren(true, children);

            for (var i = 0; i < children.Count; i++)
                children[i].ResetIdentity();

            ListPool<NetworkIdentity>.Destroy(children);
            
            if (gameObject.TryGetComponent<NetworkIdentity>(out var id) && id.isSceneObject)
            {
                _scenePool.PutBackInPool(gameObject);
            }
            else
            {
                _prefabPool.PutBackInPool(gameObject);
            }
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