using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet.Modules
{
    public class HierarchyPool
    {
        private readonly Dictionary<PrefabPieceID, Queue<GameObject>> _pool = new();

        private readonly Transform _parent;

        [UsedImplicitly] private readonly IPrefabProvider _prefabs;
        
        private static readonly Dictionary<GameObject, GameObjectPrototype> _prefabPrototypes = new();
        
        readonly HashSet<GameObject> _alreadyWarmedUp = new HashSet<GameObject>();

        public HierarchyPool(Transform parent, IPrefabProvider prefabs = null)
        {
            _parent = parent;
            _prefabs = prefabs;
        }

        /// <summary>
        /// Warmup all the prefabs that are marked as poolable.
        /// If a prefab was already warmed up, it will be skipped.
        /// </summary>
        public void Warmup()
        {
            if (_prefabs == null)
                return;
            
            for (int i = 0 ; i < _prefabs.allPrefabs.Count; i++)
            {
                var prefab = _prefabs.allPrefabs[i];

                if (prefab.pool && _alreadyWarmedUp.Add(prefab.prefab))
                {
                    for (int j = 0; j < prefab.warmupCount; j++)
                        Warmup(prefab, i);
                }
            }
        }

        public void Warmup(NetworkPrefabs.PrefabData prefabData, int pid)
        {
            var copy = UnityProxy.InstantiateDirectly(prefabData.prefab, _parent);
            NetworkManager.SetupPrefabInfo(copy, pid, prefabData.pool, false, -1);
            
            if (!_prefabPrototypes.ContainsKey(prefabData.prefab))
            {
                var prototype = GetPrototype(copy.transform);
                _prefabPrototypes.Add(prefabData.prefab, prototype);
            }
            
            PutBackInPool(copy, true);
        }
        
        public void PutBackInPool(GameObject target, bool tagName = false)
        {
            var toDestroy = ListPool<GameObject>.Instantiate();
            var children = ListPool<NetworkIdentity>.Instantiate();
            var pidSet = HashSetPool<PrefabPieceID>.Instantiate();

            target.GetComponentsInChildren(true, children);

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                if (!child)
                    continue;
                
                var pid = new PrefabPieceID(child.prefabId, child.depthIndex, child.siblingIndex);

                if (!pidSet.Add(pid)) continue;
                
                // check if we should pool this object or not
                if (!child.shouldBePooled)
                {
                    toDestroy.Add(child.gameObject);
                    continue;
                }
                
#if PURRNET_DEBUG_POOLING
                // set the tag
                if (tagName)
                    child.gameObject.name += "-Warmup";
#endif
                
                // get or create the queue
                if (!_pool.TryGetValue(pid, out var queue))
                {
                    queue = QueuePool<GameObject>.Instantiate();
                    _pool.Add(pid, queue);
                }
                
                // put the object in the queue
                child.gameObject.SetActive(false);
                child.transform.SetParent(_parent, false);
                
                queue.Enqueue(child.gameObject);
            }

            // destroy the objects that shouldn't be pooled
            for (var i = 0; i < toDestroy.Count; i++)
            {
                var id = toDestroy[i];
                if (id) UnityProxy.DestroyDirectly(id);
            }

            ListPool<GameObject>.Destroy(toDestroy);
            ListPool<NetworkIdentity>.Destroy(children);
            HashSetPool<PrefabPieceID>.Destroy(pidSet);
        }

        private bool TryGetFromPool(PrefabPieceID pid, out GameObject instance)
        {
            if (!_pool.TryGetValue(pid, out var queue))
            {
                Warmup(pid);

                if (!_pool.TryGetValue(pid, out queue))
                {
                    instance = null;
                    return false;
                }
            }

            if (queue.Count == 0)
            {
                Warmup(pid);
                
                if (queue.Count == 0)
                {
                    instance = null;
                    return false;
                }
            }

            return queue.TryDequeue(out instance);
        }

        private void Warmup(PrefabPieceID pid)
        {
            if (pid.prefabId >= 0 && _prefabs != null && _prefabs.TryGetPrefabData(pid.prefabId, out var prefab))
                Warmup(prefab, pid.prefabId);
        }

        public static DisposableList<int> GetInvPath(Transform parent, Transform transform)
        {
            var depth = new DisposableList<int>(16);
            var current = transform;

            if (parent == null)
                return depth;

            while (current != parent)
            {
                depth.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            return depth;
        }

        private static DisposableList<bool> GetEnabledStates(Transform transform)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            var enabled = new DisposableList<bool>(16);

            transform.GetComponents(children);

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                enabled.Add(child.enabled);
            }

            ListPool<NetworkIdentity>.Destroy(children);
            return enabled;
        }

        private static void SetEnabledStates(GameObject go, DisposableList<bool> enabledStates)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();

            go.GetComponents(children);

            if (children.Count != enabledStates.Count)
            {
                PurrLogger.LogError(
                    $"Mismatched enabled states count, expected {children.Count} but got {enabledStates.Count}");
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                child.enabled = enabledStates[i];
            }

            ListPool<NetworkIdentity>.Destroy(children);
        }
        
        public static bool TryGetPrefabPrototype(GameObject prefab, out GameObjectPrototype prototype)
        {
            return _prefabPrototypes.TryGetValue(prefab, out prototype);
        }

        public static GameObjectPrototype GetPrototype(Transform transform)
        {
            var framework = new DisposableList<GameObjectFrameworkPiece>(16);

            if (!transform.TryGetComponent<NetworkIdentity>(out var rootId))
                return new GameObjectPrototype { framework = framework, isScenePrototype = true };

            var queue = QueuePool<GameObjectRuntimePair>.Instantiate();
            var pair = GetRuntimePair(null, transform, rootId);

            queue.Enqueue(pair);

            while (queue.Count > 0)
            {
                using var current = queue.Dequeue();
                var children = current.children;

                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    var childPair = GetRuntimePair(current.identity.transform, child.transform, child.identity);
                    queue.Enqueue(childPair);
                }

                var trs = current.identity.transform;

                var pid = new PrefabPieceID(current.identity.prefabId, current.identity.depthIndex,
                    current.identity.siblingIndex);
                var piece = new GameObjectFrameworkPiece(
                    pid,
                    current.identity.id ?? default,
                    children.Count,
                    current.identity.gameObject.activeSelf,
                    GetInvPath(current.parent, trs),
                    GetEnabledStates(trs)
                );
                framework.Add(piece);
            }

            QueuePool<GameObjectRuntimePair>.Destroy(queue);
            return new GameObjectPrototype { framework = framework, isScenePrototype = rootId.isSceneObject };
        }

        public bool TryBuildPrototype(GameObjectPrototype prototype, out GameObject result, out bool shouldBeActive)
        {
            if (prototype.framework.Count == 0)
            {
                result = null;
                shouldBeActive = false;
                return false;
            }

            return TryBuildPrototypeHelper(prototype, null, 0, 1, out result, out shouldBeActive);
        }

        private bool TryBuildPrototypeHelper(GameObjectPrototype prototype, Transform parent, int currentIdx,
            int childrenStartIdx, out GameObject result, out bool shouldBeActive)
        {
            var framework = prototype.framework;
            var current = framework[currentIdx];
            var childCount = current.childCount;

            if (!TryGetFromPool(current.pid, out var instance))
            {
                result = null;
                shouldBeActive = false;
                return false;
            }

            var trs = instance.transform;
            shouldBeActive = current.isActive;

            SetEnabledStates(instance, current.enabled);

            if (parent)
            {
                WalkThePath(parent, trs, current.inversedRelativePath);
                instance.SetActive(shouldBeActive);
            }

            var nextChildIdx = childrenStartIdx + childCount;

            for (var j = 0; j < childCount; j++)
            {
                var childIdx = childrenStartIdx + j;
                var child = framework[childIdx];

                TryBuildPrototypeHelper(
                    prototype,
                    trs,
                    childIdx,
                    nextChildIdx,
                    out _,
                    out _);

                nextChildIdx += child.childCount;
            }

            result = instance;
            return true;
        }

        private static void WalkThePath(Transform parent, Transform instance, DisposableList<int> inversedPath)
        {
            if (inversedPath.Count == 0)
            {
                instance.SetParent(parent, false);
                return;
            }

            for (var i = inversedPath.Count - 1; i >= 1; i--)
            {
                var siblingIndex = inversedPath[i];
                
                if (parent.childCount <= siblingIndex)
                {
                    PurrLogger.LogError($"Parent {parent} doesn't have child with index {siblingIndex}");
                    return;
                }
                
                var sibling = parent.GetChild(siblingIndex);
                parent = sibling;
            }

            instance.SetParent(parent, false);
            
            var targetSiblingIndex = inversedPath[0];
            
            if (parent.childCount <= targetSiblingIndex)
            {
                PurrLogger.LogError($"Failed to set instance {instance} sibling index to {targetSiblingIndex}");
                return;
            }
            
            instance.SetSiblingIndex(targetSiblingIndex);
        }

        private static GameObjectRuntimePair GetRuntimePair(Transform parent, Transform transform,
            NetworkIdentity rootId)
        {
            var children = new DisposableList<TransformIdentityPair>(rootId.directChildren.Length);
            var pair = new GameObjectRuntimePair(parent, rootId, children);

            foreach (var c in rootId.directChildren)
                children.Add(new TransformIdentityPair(c.transform, c));
            return pair;
        }

        public static void GetDirectChildren(Transform root, DisposableList<TransformIdentityPair> children)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                GetDirectChildrenHelper(child, children);
            }
        }

        private static void GetDirectChildrenHelper(Transform root, DisposableList<TransformIdentityPair> children)
        {
            if (root.TryGetComponent<NetworkIdentity>(out var identity))
            {
                children.Add(new TransformIdentityPair(root, identity));
                return;
            }

            for (var i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                GetDirectChildrenHelper(child, children);
            }
        }

        public void Dispose()
        {
            foreach (var (_, queue) in _pool)
                QueuePool<GameObject>.Destroy(queue);
            _pool.Clear();
            
            if (_parent)
                UnityProxy.DestroyDirectly(_parent.gameObject);
        }
    }
}