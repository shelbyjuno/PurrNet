using System.Collections.Generic;
using PurrNet.Pooling;
using UnityEngine;

namespace PurrNet.Modules
{
    public struct GameObjectFrameworkPiece
    {
        public PrefabPieceID pid;
        public int childCount;
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
                    list = new DisposableList<GameObject>();
                    _pool.Add(pid, list);
                }
                
                child.transform.SetParent(null, false);
                list.Add(child.gameObject);
            }
            
            ListPool<NetworkIdentity>.Destroy(children);
            HashSetPool<PrefabPieceID>.Destroy(pidSet);
        }

        public void GetFramework(GameObject gameObject, IList<GameObjectFrameworkPiece> framework)
        {
            
        }
    }
}