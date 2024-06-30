using System;
using JetBrains.Annotations;
using PurrNet.Modules;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
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
        [UsedImplicitly]
        public static NetworkManager lastInstance { get; private set; }
        
        [Header("Auto Start Settings")]
        [SerializeField] private StartFlags _startServerFlags = StartFlags.ServerBuild | StartFlags.Editor;
        [SerializeField] private StartFlags _startClientFlags = StartFlags.ClientBuild | StartFlags.Editor | StartFlags.Clone;
        
        [Header("Persistency Settings")]
        [SerializeField] private CookieScope _cookieScope = CookieScope.LiveWithProcess;

        [Header("Network Settings")]
        [SerializeField] private GenericTransport _transport;

        [NotNull]
        public GenericTransport transport
        {
            get => _transport;
            set
            {
                if (_transport)
                {
                    _transport.transport.onConnected -= OnNewConnection;
                    _transport.transport.onDisconnected -= OnLostConnection;
                    _transport.transport.onConnectionState -= OnConnectionState;
                    _transport.transport.onDataReceived -= OnDataReceived;
                }

                _transport = value;
                
                if (_transport)
                {
                    _transport.transport.onConnected += OnNewConnection;
                    _transport.transport.onDisconnected += OnLostConnection;
                    _transport.transport.onConnectionState += OnConnectionState;
                    _transport.transport.onDataReceived += OnDataReceived;
                    _subscribed = true;
                }
            }
        }

        public bool shouldAutoStartServer => ShouldStart(_startServerFlags);
        public bool shouldAutoStartClient => ShouldStart(_startClientFlags);
        
        public ConnectionState serverState => _transport.transport.listenerState;
        
        public ConnectionState clientState => _transport.transport.clientState;
        
        public bool isServer => _transport.transport.listenerState == ConnectionState.Connected;
        
        public bool isClient => _transport.transport.clientState == ConnectionState.Connected;
        
        public bool isHost => isServer && isClient;
        
        public bool isServerOnly => isServer && !isClient;
        
        public bool isClientOnly => !isServer && isClient;
        
        private ModulesCollection _serverModules;
        private ModulesCollection _clientModules;
        
        private bool _subscribed;

        private void Awake()
        {
            lastInstance = this;
            Application.runInBackground = true;

            if (!_subscribed)
                transport = _transport;
            
            _serverModules = new ModulesCollection(this, true);
            _clientModules = new ModulesCollection(this, false);
        }

        public T GetModule<T>(bool asServer) where T : INetworkModule
        {
            if (TryGetModule(out T module, asServer))
                return module;
            
            throw new InvalidOperationException($"Module {typeof(T).Name} not found.");
        }

        public bool TryGetModule<T>(out T module, bool asServer) where T : INetworkModule
        {
            return asServer ?
                _serverModules.TryGetModule(out module) :
                _clientModules.TryGetModule(out module);
        }
        
        internal void RegisterModules(ModulesCollection modules, bool asServer)
        {
            var broadcastModule = new BroadcastModule(this, asServer);
            var networkCookies = new CookiesModule(_cookieScope);
            var playersManager = new PlayersManager(this, networkCookies, broadcastModule);
            
            modules.AddModule(broadcastModule);
            modules.AddModule(networkCookies);
            modules.AddModule(playersManager);
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
        
        private void OnDestroy()
        {
            if (_transport)
            {
                StopClient();
                StopServer();
            }
        }

        public void StartServer()
        {
            _serverModules.RegisterModules();
            _transport.StartServer();
        }
        
        public void StartClient()
        {
            _clientModules.RegisterModules();
            _transport.StartClient();
        }

        private void OnNewConnection(Connection conn, bool asserver)
        {
            if (asserver)
                 _serverModules.OnNewConnection(conn, true);
            else _clientModules.OnNewConnection(conn, false);
        }

        private void OnLostConnection(Connection conn, bool asserver)
        {
            if (asserver)
                 _serverModules.OnLostConnection(conn, true);
            else _clientModules.OnLostConnection(conn, false);
        }

        private void OnDataReceived(Connection conn, ByteData data, bool asserver)
        {
            if (asserver)
                 _serverModules.OnDataReceived(conn, data, true);
            else _clientModules.OnDataReceived(conn, data, false);
        }

        private void OnConnectionState(ConnectionState state, bool asserver)
        {
            if (asserver)
                 _serverModules.OnConnectionState(state, true);
            else _clientModules.OnConnectionState(state, false);
        }
        
        public bool TryGetModule<T>(bool asServer, out T module) where T : INetworkModule
        {
            return asServer ? 
                _serverModules.TryGetModule(out module) : 
                _clientModules.TryGetModule(out module);
        }

        public void StopServer() => _transport.StopServer();

        public void StopClient() => _transport.StopClient();
    }
}
