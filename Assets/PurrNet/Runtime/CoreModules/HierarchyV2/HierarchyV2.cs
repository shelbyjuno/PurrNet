using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public class HierarchyV2
    {
        private readonly NetworkManager _manager;
        private readonly bool _asServer;
        private readonly SceneID _sceneId;
        private readonly Scene _scene;
        private readonly ScenePlayersModule _players;
        private readonly VisilityV2 _visibility;
        
        private readonly HierarchyPool _scenePool;
        private readonly HierarchyPool _prefabsPool;
        
        public bool areSceneObjectsReady { get; private set; }
        
        public HierarchyV2(NetworkManager manager, SceneID sceneId, Scene scene, ScenePlayersModule players, IPrefabProvider prefabs, bool asServer)
        {
            _manager = manager;
            _sceneId = sceneId;
            _scene = scene;
            _players = players;
            _visibility = new VisilityV2();
            _asServer = asServer;
            
            _scenePool = NetworkPoolManager.GetScenePool(sceneId);
            _prefabsPool = NetworkPoolManager.GetPool(prefabs);
            
            SetupSceneObjects(scene);
        }

        private void SetupSceneObjects(Scene scene)
        {
            if (_manager.TryGetModule<HierarchyFactory>(!_asServer, out var factory) &&
                factory.TryGetHierarchy(_sceneId, out var other))
            {
                if (other.areSceneObjectsReady)
                {
                    areSceneObjectsReady = true;
                    return;
                }
            }
            
            if (areSceneObjectsReady)
                return;
            
            var allSceneIdentities = ListPool<NetworkIdentity>.Instantiate();
            SceneObjectsModule.GetSceneIdentities(scene, allSceneIdentities);
            
            var roots = HashSetPool<NetworkIdentity>.Instantiate();

            var count = allSceneIdentities.Count;
            for (int i = 0; i < count; i++)
            {
                var identity = allSceneIdentities[i];
                var root = identity.GetRootIdentity();
                
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
                    child.PreparePrefabInfo(pid, trs.GetSiblingIndex(), depth, true, true);
                }

                SpawnSceneObject(children);
                ListPool<NetworkIdentity>.Destroy(children);
            }
            
            ListPool<NetworkIdentity>.Destroy(allSceneIdentities);
            areSceneObjectsReady = true;
        }

        public void Enable()
        {
            PurrNetGameObjectUtils.onGameObjectCreated += OnGameObjectCreated;
            _visibility.Enable();
        }

        public void Disable()
        {
            PurrNetGameObjectUtils.onGameObjectCreated -= OnGameObjectCreated;
            _visibility.Disable();
        }

        private void OnGameObjectCreated(GameObject obj)
        {
            if (!_asServer && _manager.isServer)
                return;
            
            if (obj.scene.handle != _scene.handle)
                return;

            var root = obj.GetComponentInChildren<NetworkIdentity>();
            
            if (!root)
                return;
            
            PurrLogger.Log($"OnGameObjectCreated: {root.prefabId}", obj);
        }

        [UsedImplicitly]
        public void Spawn(GameObject gameObject)
        {
            if (!gameObject.TryGetComponent<NetworkIdentity>(out var id))
            {
                PurrLogger.LogError($"Failed to spawn object '{gameObject.name}'. No NetworkIdentity found.", gameObject);
                return;
            }
            
            
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
                    child.SetIdentity(_manager, this, _sceneId, id, _asServer);
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
                _prefabsPool.PutBackInPool(gameObject);
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
            var pool = prototype.isScenePrototype ? _scenePool : _prefabsPool;

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