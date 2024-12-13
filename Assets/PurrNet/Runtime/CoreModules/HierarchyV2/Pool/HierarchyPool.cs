using System.Collections.Generic;
using PurrNet.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    public class HierarchyPool
    {
        private readonly Dictionary<PrefabPieceID, DisposableList<GameObject>> _pool = new ();
        
        public void Warmup(GameObject prefab)
        {
            var copy = Object.Instantiate(prefab);
            
            var children = ListPool<NetworkIdentity>.Instantiate();
            var pidSet = HashSetPool<PrefabPieceID>.Instantiate();
            
            copy.SetActive(false);
            copy.GetComponentsInChildren(true, children);
            
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var pid = new PrefabPieceID(child.prefabId, child.depthIndex, child.siblingIndex);
                
                if (!pidSet.Add(pid)) continue;
                
                if (!_pool.TryGetValue(pid, out var list))
                {
                    list = new DisposableList<GameObject>(16);
                    _pool.Add(pid, list);
                }
                
                child.transform.SetParent(null, false);
                list.Add(child.gameObject);
            }
            
            ListPool<NetworkIdentity>.Destroy(children);
            HashSetPool<PrefabPieceID>.Destroy(pidSet);
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
    }
}