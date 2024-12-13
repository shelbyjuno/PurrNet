using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    public class HierarchyPool : IDisposable
    {
        private readonly Dictionary<PrefabPieceID, Queue<GameObject>> _pool = new ();
        
        private readonly Transform _parent;
        
        [UsedImplicitly]
        private readonly IPrefabProvider _prefabs;
        
        public HierarchyPool(Transform parent, IPrefabProvider prefabs)
        {
            _parent = parent;
            _prefabs = prefabs;
        }
        
        public void Warmup(GameObject prefab)
        {
            var copy = Object.Instantiate(prefab, _parent);

            PutBackInPool(copy);
        }

        public void PutBackInPool(GameObject target)
        {
            var children = ListPool<NetworkIdentity>.Instantiate();
            var pidSet = HashSetPool<PrefabPieceID>.Instantiate();
            
            target.GetComponentsInChildren(true, children);
            
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var pid = new PrefabPieceID(child.prefabId, child.depthIndex, child.siblingIndex);
                
                if (!pidSet.Add(pid)) continue;
                
                if (!_pool.TryGetValue(pid, out var queue))
                {
                    queue = QueuePool<GameObject>.Instantiate();
                    _pool.Add(pid, queue);
                }
                
                child.gameObject.SetActive(false);
                child.transform.SetParent(null, false);
                queue.Enqueue(child.gameObject);
            }
            
            ListPool<NetworkIdentity>.Destroy(children);
            HashSetPool<PrefabPieceID>.Destroy(pidSet);
        }
        
        public bool TryGetFromPool(PrefabPieceID pid, Transform parent, out GameObject instance)
        {
            if (!_pool.TryGetValue(pid, out var list))
            {
                instance = null;
                return false;
            }
            
            switch (list.Count)
            {
                case 0:
                    instance = null;
                    return false;
                case 1:
                    // make a copy of the object
                    instance = Object.Instantiate(list.Peek(), parent);
                    return true;
                default:
                    return list.TryDequeue(out instance);
            }
        }

        private static DisposableList<int> GetSiblingDepth(Transform parent, Transform transform)
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
        
        public static GameObjectPrototype GetFramework(Transform transform)
        {
            var framework = new DisposableList<GameObjectFrameworkPiece>(16);
            
            if (!transform.TryGetComponent<NetworkIdentity>(out var rootId))
                return new GameObjectPrototype { framework = framework };

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
                
                var pid = new PrefabPieceID(current.identity.prefabId, current.identity.depthIndex, current.identity.siblingIndex);
                var piece = new GameObjectFrameworkPiece(
                    pid,
                    current.identity.id ?? default,
                    children.Count,
                    GetSiblingDepth(current.parent, current.identity.transform)
                );
                framework.Add(piece);
            }
            
            QueuePool<GameObjectRuntimePair>.Destroy(queue);
            return new GameObjectPrototype { framework = framework };
        }

        public bool TryBuildPrototype(GameObjectPrototype prototype, out GameObject result)
        {
            if (prototype.framework.Count == 0)
            {
                result = null;
                return false;
            }
            
            return TryBuildPrototypeHelper(prototype, null, 0, 1, out result);
        }
        
        private bool TryBuildPrototypeHelper(GameObjectPrototype prototype, Transform parent, int currentIdx, int childrenStartIdx, out GameObject result)
        {
            var framework = prototype.framework;
            var current = framework[currentIdx];
            int childCount = current.childCount;
            
            if (!TryGetFromPool(current.pid, _parent, out var instance))
            {
                PurrLogger.LogError($"Failed to get object from pool: {current.pid}");
                result = null;
                return false;
            }

            if (parent)
            {
                WalkThePath(parent, instance.transform, current.inversedRelativePath);

                // todo: depends on the prefab, it might be disabled in there
                instance.SetActive(true);
            }
            
            for (var j = 0; j < childCount; j++)
            {
                var child = framework[childrenStartIdx + j];
                TryBuildPrototypeHelper(prototype, instance.transform, childrenStartIdx + j, childrenStartIdx + child.childCount, out _);
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
                var sibling = parent.GetChild(siblingIndex);
                parent = sibling;
            }
            
            instance.SetParent(parent, false);
            instance.SetSiblingIndex(inversedPath[0]);
        }

        private static GameObjectRuntimePair GetRuntimePair(Transform parent, Transform transform, NetworkIdentity rootId)
        {
            var children = new DisposableList<TransformIdentityPair>(16);
            var pair = new GameObjectRuntimePair(parent, rootId, children);
            
            GetDirectChildren(transform, children);
            return pair;
        }

        private static void GetDirectChildren(Transform root, DisposableList<TransformIdentityPair> children)
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
        }
    }
}