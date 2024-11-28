using UnityEngine;

namespace PurrNet.Transports
{
    public abstract class GenericTransport : MonoBehaviour
    {
        public abstract bool isSupported { get; }
        
        public abstract ITransport transport { get; }

        [ContextMenu("Start Server")]
        public void StartServer()
        {
            if (TryGetComponent<NetworkManager>(out var networkManager))
                networkManager.InternalRegisterServerModules();
            StartServerInternal();
        }
        
        [ContextMenu("Stop Server")]
        public void StopServer()
        {
            StopServerInternal();
        }
        
        [ContextMenu("Start Client")]
        public void StartClient()
        {
            if (TryGetComponent<NetworkManager>(out var networkManager))
                networkManager.InternalRegisterClientModules();
            StartClientInternal();
        }

        [ContextMenu("Stop Client")]
        public void StopClient()
        {
            StopClientInternal();
        }

        internal void StartClientInternalOnly()
        {
            StartClientInternal();
        }

        protected abstract void StartClientInternal();

        protected abstract void StartServerInternal();

        protected void StopClientInternal() => transport.Disconnect();

        protected void StopServerInternal() => transport.StopListening();
    }
}
