using UnityEngine;

namespace PurrNet
{
    public class NetworkTransform : NetworkIdentity
    {
        [SerializeField] private bool _syncParent = true;
    }
}
