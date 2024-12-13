using UnityEngine;

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
}