using System;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class NetworkIdentity : MonoBehaviour
    {
        public int prefabId { get; private set; } = -1;

        public int id { get; private set; } = -1;

        public bool isValid => id != -1;
        
        internal event Action<NetworkIdentity> onDestroy; 

        internal SpawnPrefabMessage GetSpawnMessage(int childrenCount)
        {
            var trs = transform;
            
            return new SpawnPrefabMessage
            {
                prefabId = prefabId,
                rootIdentityId = id,
                childrenCount = childrenCount,
                position = trs.position,
                rotation = trs.rotation,
                scale = trs.localScale
            };
        }
        
        internal void SetIdentity(int pid, int identityId)
        {
            prefabId = pid;
            id = identityId;
        }

        protected virtual void OnDestroy()
        {
            onDestroy?.Invoke(this);
        }
    }
}
