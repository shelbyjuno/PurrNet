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
    
    public struct GameObjectFrameworkPiece : IDisposable
    {
        public readonly PrefabPieceID pid;
        public readonly NetworkID id;
        public readonly int childCount;
        public DisposableList<int> inversedRelativePath;
        
        public GameObjectFrameworkPiece(PrefabPieceID pid, NetworkID id, int childCount, DisposableList<int> path)
        {
            this.pid = pid;
            this.id = id;
            this.childCount = childCount;
            this.inversedRelativePath = path;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("GameObjectFrameworkPiece: { ");
            builder.Append("Nid: ");
            builder.Append(id);
            builder.Append(", childCount: ");
            builder.Append(childCount);
            builder.Append(", Path: ");
            for (int i = 0; i < inversedRelativePath.Count; i++)
            {
                builder.Append(inversedRelativePath[i]);
                if (i < inversedRelativePath.Count - 1)
                    builder.Append(" <- ");
            }
            builder.Append(" }");
            return builder.ToString();
        }

        public void Dispose()
        {
            inversedRelativePath.Dispose();
        }
    }

    public struct GameObjectPrototype : IDisposable
    {
        public DisposableList<GameObjectFrameworkPiece> framework;

        public void Dispose()
        {
            for (var i = 0; i < framework.Count; i++)
            {
                var piece = framework[i];
                piece.Dispose();
            }

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