using UnityEngine;

namespace PurrNet.Transports
{
    public abstract class GenericTransport : MonoBehaviour
    {
        public abstract bool isSupported { get; }
        
        public abstract ITransport transport { get; }
        
        bool TryGetNetworkManager(NetworkManager manager, out NetworkManager networkManager)
        {
            if (manager)
            {
                networkManager = manager;
                return true;
            }
            
            if (TryGetComponent(out networkManager))
                return true;
            
            var parentNm = GetComponentInParent<NetworkManager>();
            
            if (parentNm)
            {
                networkManager = parentNm;
                return true;
            }
            
            var childNm = GetComponentInChildren<NetworkManager>();
            
            if (childNm)
            {
                networkManager = childNm;
                return true;
            }
            
            if (NetworkManager.main)
            {
                networkManager = NetworkManager.main;
                return true;
            }
            
            networkManager = null;
            return false;
        }

        [ContextMenu("Start Server")]
        public void StartServer(NetworkManager manager = null)
        {
            if (TryGetNetworkManager(manager, out var networkManager))
                networkManager.InternalRegisterServerModules();
            StartServerInternal();
        }
        
        [ContextMenu("Stop Server")]
        public void StopServer()
        {
            StopServerInternal();
        }
        
        [ContextMenu("Start Client")]
        public void StartClient(NetworkManager manager = null)
        {
            if (TryGetNetworkManager(manager, out var networkManager))
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
