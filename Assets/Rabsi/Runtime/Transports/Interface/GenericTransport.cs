using UnityEngine;

namespace Rabsi.Transports
{
    public abstract class GenericTransport : MonoBehaviour
    {
        public abstract bool isSupported { get; }
        
        public abstract ITransport transport { get; }
        
        public abstract void Connect();
        
        public abstract void Listen();
    }
}
