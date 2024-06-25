using System;
using Rabsi.Transports;
using Rabsi.Utils;
using UnityEngine;

namespace Rabsi
{
    [Flags]
    public enum StartFlags
    {
        Editor = 1,
        Clone = 2,
        ClientBuild = 4,
        ServerBuild = 8
    }
    
    public sealed class NetworkManager : MonoBehaviour
    {
        [Header("Auto Start Settings")]
        [SerializeField] private StartFlags _startServerFlags = StartFlags.ServerBuild | StartFlags.Editor;
        [SerializeField] private StartFlags _startClientFlags = StartFlags.ClientBuild | StartFlags.Editor | StartFlags.Clone;
        
        [Header("Network Settings")]
        [SerializeField] private GenericTransport _transport;
        
        public GenericTransport transport
        {
            get => _transport;
            set => _transport = value;
        }
        
        public bool shouldAutoStartServer => ShouldStart(_startServerFlags);
        public bool shouldAutoStartClient => ShouldStart(_startClientFlags);
        
        public bool isServer => _transport.transport.listenerState == ConnectionState.Connected;
        
        public bool isClient => _transport.transport.clientState == ConnectionState.Connected;
        
        public bool isHost => isServer && isClient;
        
        public bool isServerOnly => isServer && !isClient;
        
        public bool isClientOnly => !isServer && isClient;

        private void Awake()
        {
            Application.runInBackground = true;
        }
        
        static bool ShouldStart(StartFlags flags)
        {
            return (flags.HasFlag(StartFlags.Editor) && ApplicationContext.isMainEditor) ||
                   (flags.HasFlag(StartFlags.Clone) && ApplicationContext.isClone) ||
                   (flags.HasFlag(StartFlags.ClientBuild) && ApplicationContext.isClientBuild) ||
                   (flags.HasFlag(StartFlags.ServerBuild) && ApplicationContext.isServerBuild);
        }

        private void Start()
        {
            bool shouldStartServer = ShouldStart(_startServerFlags);
            bool shouldStartClient = ShouldStart(_startClientFlags);

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
