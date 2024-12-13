using System;
using System.Collections.Generic;
using System.Text;
using PurrNet.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Modules
{
    public readonly struct TransformIdentityPair
    {
        public readonly Transform transform;
        public readonly NetworkIdentity identity;
        
        public TransformIdentityPair(Transform transform, NetworkIdentity identity)
        {
            this.transform = transform;
            this.identity = identity;
        }
    }
    
    public struct GameObjectRuntimePair : IDisposable
    {
        public readonly Transform parent;
        public readonly NetworkIdentity identity;
        public DisposableList<TransformIdentityPair> children;
        
        public GameObjectRuntimePair(Transform parent, NetworkIdentity identity, DisposableList<TransformIdentityPair> children)
        {
            this.parent = parent;
            this.identity = identity;
            this.children = children;
        }

        public void Dispose()
        {
            children.Dispose();
        }
    }
    
    public readonly struct GameObjectFrameworkPiece
    {
        public readonly PrefabPieceID pid;
        public readonly int childCount;
        public readonly int relativeDepth;
        public readonly int siblingIndex;
        
        public GameObjectFrameworkPiece(PrefabPieceID pid, int childCount, int relativeDepth, int siblingIndex)
        {
            this.pid = pid;
            this.childCount = childCount;
            this.relativeDepth = relativeDepth;
            this.siblingIndex = siblingIndex;
        }

        public override string ToString()
        {
            return $"{{ PID: {pid.prefabId}, Depth: {relativeDepth}, Sibling: {siblingIndex} }}";
        }
    }

    public struct GameObjectPrototype : IDisposable
    {
        public DisposableList<GameObjectFrameworkPiece> framework;

        public void Dispose()
        {
            framework.Dispose();
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("GameObjectPrototype: {\n    ");
            for (int i = 0; i < framework.Count; i++)
            {
                builder.Append(framework[i]);
                if (i < framework.Count - 1)
                    builder.Append("\n    ");
            }
            builder.Append("\n}");
            return builder.ToString();
        }
    }
    
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

        private static void GetSiblingDepth(Transform parent, Transform transform, out int depth, out int siblingIndex)
        {
            depth = 0;
            siblingIndex = 0;
            
            if (parent == null)
                return;
            
            while (transform != parent)
            {
                depth++;
                siblingIndex = transform.GetSiblingIndex();
                transform = transform.parent;
            }
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
                
                GetSiblingDepth(current.parent, current.identity.transform, out var depth, out var siblingIndex);

                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    var childPair = GetRuntimePair(current.identity.transform, child.transform, child.identity);
                    queue.Enqueue(childPair);
                }
                
                var pid = new PrefabPieceID(current.identity.prefabId, current.identity.depthIndex, current.identity.siblingIndex);
                var piece = new GameObjectFrameworkPiece(pid, children.Count, depth, siblingIndex);
                
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