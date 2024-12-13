using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public interface IPrefabProvider
    {
        IReadOnlyList<GameObject> allPrefabs { get; }
        
        GameObject GetPrefabFromGuid(string guid);

        bool TryGetPrefab(int id, out GameObject prefab);
        
        bool TryGetPrefab(int id, int offset, out GameObject prefab);

        bool TryGetPrefabID(string guid, out int id);
    }

    public abstract class PrefabProviderScriptable : ScriptableObject, IPrefabProvider
    {
        public abstract IReadOnlyList<GameObject> allPrefabs { get; }

        public abstract GameObject GetPrefabFromGuid(string guid);

        public abstract bool TryGetPrefab(int id, out GameObject prefab);
        
        public abstract bool TryGetPrefab(int id, int offset, out GameObject prefab);

        public abstract bool TryGetPrefabID(string guid, out int id);
    }
}
