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

        internal SpawnPrefabMessage GetSpawnMessage()
        {
            var trs = transform;
            
            return new SpawnPrefabMessage
            {
                prefabId = prefabId,
                prefabOffset = prefabOffset,
                childrenCount = 0,
                position = trs.position,
                rotation = trs.rotation,
                scale = trs.localScale
            };
        }
        
        internal void SetIdentity(int prefabId, int prefabOffset, int value)
        {
            this.prefabId = prefabId;
            this.prefabOffset = prefabOffset;
            identity = value;
        }
    }
}
