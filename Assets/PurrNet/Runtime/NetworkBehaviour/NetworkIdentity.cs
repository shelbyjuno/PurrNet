using UnityEngine;

namespace PurrNet
{
    public class NetworkIdentity : MonoBehaviour
    {
        private uint _identity;
        
        public bool isValid => _identity != 0;
        
        public uint identity => _identity;
        
        internal void SetIdentity(uint identity)
        {
            _identity = identity;
        }
    }
}
