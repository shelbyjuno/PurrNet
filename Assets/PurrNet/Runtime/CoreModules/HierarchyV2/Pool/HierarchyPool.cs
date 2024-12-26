using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet.Modules
{
    public readonly struct PoolPair
    {
        public readonly HierarchyPool scenePool;
        public readonly HierarchyPool prefabPool;
        
        public PoolPair(HierarchyPool scenePool, HierarchyPool prefabPool)
        {
            this.scenePool = scenePool;
            this.prefabPool = prefabPool;
        }
    }
    
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

        private void Warmup(NetworkPrefabs.PrefabData prefabData, int pid)
        {
            var copy = UnityProxy.InstantiateDirectly(prefabData.prefab, _parent);
            NetworkManager.SetupPrefabInfo(copy, pid, prefabData.pool, false, -1);
            
            if (!_prefabPrototypes.ContainsKey(prefabData.prefab))
            {
                var prototype = GetFullPrototype(copy.transform);
                _prefabPrototypes.Add(prefabData.prefab, prototype);
            }
            
            PutBackInPool(copy, true);
        }
        
        public static void PutBackInPool(PoolPair pool, GameObject target, bool tagName = false)
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
                var pair = pid.prefabId >= 0 ? pool.prefabPool : pool.scenePool;

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
                if (!pair._pool.TryGetValue(pid, out var queue))
                {
                    queue = QueuePool<GameObject>.Instantiate();
                    pair._pool.Add(pid, queue);
                }
                
                // put the object in the queue
                child.gameObject.SetActive(false);
                child.transform.SetParent(pair._parent, false);
                
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

        private static bool TryGetFromPool(PoolPair pair, PrefabPieceID pid, out GameObject instance)
        {
            var pool = pid.prefabId >= 0 ? pair.prefabPool : pair.scenePool;
            
            if (!pool._pool.TryGetValue(pid, out var queue))
            {
                pool.Warmup(pid);

                if (!pool._pool.TryGetValue(pid, out queue))
                {
                    instance = null;
                    return false;
                }
            }

            if (queue.Count == 0)
            {
                pool.Warmup(pid);
                
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

        private static void SetEnabledStates(GameObject go, NetworkID baseNid, List<NetworkIdentity> createdNids, DisposableList<bool> enabledStates)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();

            go.GetComponents(children);

            if (children.Count != enabledStates.Count)
            {
                PurrLogger.LogError(
                    $"Mismatched enabled states count, expected {children.Count} but got {enabledStates.Count} on {go}", go);
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                child.enabled = i < enabledStates.Count && enabledStates[i];
                
                createdNids?.Add(child);
                child.SetID(new NetworkID(baseNid, i));
            }

            ListPool<NetworkIdentity>.Destroy(children);
        }
        
        public static bool TryGetPrefabPrototype(GameObject prefab, out GameObjectPrototype prototype)
        {
            return _prefabPrototypes.TryGetValue(prefab, out prototype);
        }
        
        public static bool TryGetPrototype(Transform transform, PlayerID scope, List<NetworkIdentity> allChildren, out GameObjectPrototype prototype)
        {
            var framework = new DisposableList<GameObjectFrameworkPiece>(16);
            
            if (!transform.TryGetComponent<NetworkIdentity>(out var rootId))
            {
                prototype = default;
                return false;
            }

            var rootPair = new TransformIdentityPair(transform, rootId);
            if (!rootPair.HasObserver(scope, allChildren))
            {
                prototype = default;
                return false;
            }

            var queue = QueuePool<GameObjectRuntimePair>.Instantiate();
            var pair = GetRuntimePair(null, rootId);

            queue.Enqueue(pair);

            while (queue.Count > 0)
            {
                using var current = queue.Dequeue();
                var children = current.children;
                int actualChildCount = 0;

                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    
                    if (child.HasObserver(scope, allChildren))
                    {
                        var childPair = GetRuntimePair(current.identity.transform, child.identity);
                        queue.Enqueue(childPair);

                        ++actualChildCount;
                    }
                }

                var trs = current.identity.transform;

                var pid = new PrefabPieceID(current.identity.prefabId, current.identity.depthIndex, current.identity.siblingIndex);
                var piece = new GameObjectFrameworkPiece(
                    pid,
                    current.identity.id ?? default,
                    actualChildCount,
                    current.identity.gameObject.activeSelf,
                    current.identity.invertedPathToNearestParent,
                    GetEnabledStates(trs)
                );
                framework.Add(piece);
            }

            QueuePool<GameObjectRuntimePair>.Destroy(queue);
            prototype = new GameObjectPrototype(transform.localPosition, transform.localRotation, framework);
            return true;
        }

        public static GameObjectPrototype GetFullPrototype(Transform transform)
        {
            var framework = new DisposableList<GameObjectFrameworkPiece>(16);
            if (!transform.TryGetComponent<NetworkIdentity>(out var rootId))
                return new GameObjectPrototype(transform.localPosition, transform.localRotation, framework);

            var queue = QueuePool<GameObjectRuntimePair>.Instantiate();
            var pair = GetRuntimePair(null, rootId);

            queue.Enqueue(pair);

            while (queue.Count > 0)
            {
                using var current = queue.Dequeue();
                var children = current.children;

                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    var childPair = GetRuntimePair(current.identity.transform, child.identity);
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
                    current.identity.invertedPathToNearestParent,
                    GetEnabledStates(trs)
                );
                framework.Add(piece);
            }

            QueuePool<GameObjectRuntimePair>.Destroy(queue);
            
            return new GameObjectPrototype(transform.localPosition, transform.localRotation, framework);
        }

        public static bool TryBuildPrototype(PoolPair pair, GameObjectPrototype prototype, List<NetworkIdentity> createdNids, out GameObject result, out bool shouldBeActive)
        {
            try
            {
                if (prototype.framework.Count == 0)
                {
                    result = null;
                    shouldBeActive = false;
                    return false;
                }

                if (TryBuildPrototypeHelper(pair, prototype, createdNids, null, 0, 1, out result,
                        out shouldBeActive))
                {
                    var resultTrs = result.transform;
                    resultTrs.localPosition = prototype.position;
                    resultTrs.localRotation = prototype.rotation;
                    return true;
                }
                
                return false;
            }
            catch (
#if PURRNET_DEBUG_POOLING
                System.Exception e
#endif
            )
            {
#if PURRNET_DEBUG_POOLING
                PurrLogger.LogError($"Build prototype exception: {e.Message}\n{e.StackTrace}");
#endif
                result = null;
                shouldBeActive = false;
                return false;
            }
        }

        private static bool TryBuildPrototypeHelper(PoolPair pair, GameObjectPrototype prototype, List<NetworkIdentity> createdNids, Transform parent, int currentIdx,
            int childrenStartIdx, out GameObject result, out bool shouldBeActive)
        {
            var framework = prototype.framework;
            var current = framework[currentIdx];
            var childCount = current.childCount;

            if (!TryGetFromPool(pair, current.pid, out var instance))
            {
                result = null;
                shouldBeActive = false;
                return false;
            }

            var trs = instance.transform;
            shouldBeActive = current.isActive;

            SetEnabledStates(instance, current.id, createdNids, current.enabled);

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
                    pair,
                    prototype,
                    createdNids,
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

        private static void WalkThePath(Transform parent, Transform instance, int[] inversedPath)
        {
            if (inversedPath.Length == 0)
            {
                instance.SetParent(parent, false);
                return;
            }

            int len = inversedPath.Length;
            for (var i = len - 1; i >= 1; i--)
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
                targetSiblingIndex = parent.childCount;
            
            instance.SetSiblingIndex(targetSiblingIndex);
        }

        private static GameObjectRuntimePair GetRuntimePair(Transform parent, NetworkIdentity rootId)
        {
            var children = new DisposableList<TransformIdentityPair>(rootId.directChildren.Count);
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