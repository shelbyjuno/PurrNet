using UnityEngine;

namespace PurrNet.Transports
{
    public abstract class GenericTransport : MonoBehaviour
    {
        public abstract bool isSupported { get; }
        
        public abstract ITransport transport { get; }
        
        public abstract void StartClient();
        
        public abstract void StartServer();
        
        public void StopClient() => transport.Disconnect();
        
        public void StopServer() => transport.StopListening();
    }
}
