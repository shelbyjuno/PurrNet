using Rabsi.Transports;
using Rabsi.Utils;
using UnityEngine;

namespace Rabsi
{
    public sealed class NetworkManager : MonoBehaviour
    {
        [Header("Editor Settings")]
        [Tooltip("Automatically start the server when running in the editor. Excluding clones.")]
        [SerializeField] private bool _startServerInEditor = true;
        [Tooltip("Automatically start the client when running in the editor. Excluding clones.")]
        [SerializeField] private bool _startClientInEditor = true;
        
        [Header("Clones Settings")]
        [Tooltip("Automatically start the client in cloned instances.")]
        [SerializeField] private bool _startClientInClones = true;
        
        [Header("Build Settings")]
        [Tooltip("Automatically start the client in non server builds.")]
        [SerializeField] private bool _startClientInBuilds = true;
        [Tooltip("Automatically start the server in server builds.")]
        [SerializeField] private bool _startServerInServerBuilds = true;
        
        [Header("Network Settings")]
        [SerializeField] private GenericTransport _transport;
        
        public GenericTransport transport
        {
            get => _transport;
            set => _transport = value;
        }

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private void Start()
        {
            bool shouldStartServer = ApplicationContext.isServerBuild && _startServerInServerBuilds ||
                                     ApplicationContext.isMainEditor && _startServerInEditor;

            bool shouldStartClient = ApplicationContext.isMainEditor && _startClientInEditor ||
                                     ApplicationContext.isClone && _startClientInClones ||
                                     ApplicationContext.isClientBuild && _startClientInBuilds;

            if (shouldStartServer)
                StartServer();
            
            if (shouldStartClient)
                StartClient();
        }

        public void StartServer()
        {
            _transport.Listen();
        }
        
        public void StartClient()
        {
            _transport.Connect();
        }
    }
}
