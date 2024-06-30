using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class NetworkIdentity : MonoBehaviour
    {
        public int prefabId { get; private set; } = -1;

        public int prefabOffset { get; private set; } = -1;

        public int identity { get; private set; } = -1;
        
        public bool isValid => identity != -1;

        internal SpawnPrefabMessage GetSpawnMessage(int childrenCount)
        {
            var trs = transform;
            
            return new SpawnPrefabMessage
            {
                prefabId = prefabId,
                prefabOffset = prefabOffset,
                rootIdentityId = identity,
                childrenCount = childrenCount,
                position = trs.position,
                rotation = trs.rotation,
                scale = trs.localScale
            };
        }
        
        internal SpawnPrefabMessage GetSpawnMessage()
        {
            var trs = transform;
            gameObject.GetComponentsInChildren(true, SpawnManager._identitiesCache);
            
            return new SpawnPrefabMessage
            {
                prefabId = prefabId,
                prefabOffset = prefabOffset,
                rootIdentityId = identity,
                childrenCount = SpawnManager._identitiesCache.Count,
                position = trs.position,
                rotation = trs.rotation,
                scale = trs.localScale
            };
        }
        
        // ReSharper disable once ParameterHidesMember
        internal void SetIdentity(int prefabId, int prefabOffset, int value)
        {
            this.prefabId = prefabId;
            this.prefabOffset = prefabOffset;
            identity = value;
        }
    }
}
