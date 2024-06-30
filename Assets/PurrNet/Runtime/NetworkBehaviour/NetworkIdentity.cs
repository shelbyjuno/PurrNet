using UnityEngine;

namespace PurrNet
{
    public class NetworkIdentity : MonoBehaviour
    {
        private int _identity;
        
        public bool isValid => _identity != 0;
        
        public int identity => _identity;
        
        internal void SetIdentity(int value)
        {
            _identity = value;
        }
    }
}
