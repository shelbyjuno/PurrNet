using UnityEngine;

namespace PurrNet.Transports
{
    public abstract class GenericTransport : MonoBehaviour
    {
        public abstract bool isSupported { get; }
        
        public abstract ITransport transport { get; }
        
        internal abstract void StartClient();
        
        internal abstract void StartServer();
        
        internal void StopClient() => transport.Disconnect();
        
        internal void StopServer() => transport.StopListening();
    }
}
