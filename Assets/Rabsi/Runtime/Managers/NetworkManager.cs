using System;
using System.Collections.Generic;
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

    internal readonly struct ModulesCollection
    {
        private readonly List<INetworkModule> _modules;
        private readonly List<IConnectionListener> _connectionListeners;
        private readonly List<IConnectionStateListener> _connectionStateListeners;
        private readonly List<IDataListener> _dataListeners;

        private readonly NetworkManager _manager;
        private readonly bool _asServer;
        
        public ModulesCollection(NetworkManager manager, bool asServer)
        {
            _modules = new List<INetworkModule>();
            _connectionListeners = new List<IConnectionListener>();
            _connectionStateListeners = new List<IConnectionStateListener>();
            _dataListeners = new List<IDataListener>();
            _manager = manager;
            _asServer = asServer;
        }
        
        public void RegisterModules()
        {
            UnregisterModules();
            
            NetworkManager.RegisterModules(this, _asServer);
            _modules.Sort((moduleA, moduleB) => moduleA.priority.CompareTo(moduleB.priority));

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Setup(_manager);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IConnectionListener connectionListener)
                    _connectionListeners.Add(connectionListener);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IConnectionStateListener connectionStateListener)
                    _connectionStateListeners.Add(connectionStateListener);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IDataListener dataListener)
                    _dataListeners.Add(dataListener);
            }
        }
        
        public void OnNewConnection(Connection conn, bool asserver)
        {
            for (int i = 0; i < _connectionListeners.Count; i++)
                _connectionListeners[i].OnConnected(conn, asserver);
        }

        public void OnLostConnection(Connection conn, bool asserver)
        {
            for (int i = 0; i < _connectionListeners.Count; i++)
                _connectionListeners[i].OnDisconnected(conn, asserver);
        }

        public void OnDataReceived(Connection conn, ByteData data, bool asserver)
        {
            for (int i = 0; i < _dataListeners.Count; i++)
                _dataListeners[i].OnDataReceived(conn, data, asserver);
        }

        public void OnConnectionState(ConnectionState state, bool asserver)
        {
            for (int i = 0; i < _connectionStateListeners.Count; i++)
                _connectionStateListeners[i].OnConnectionState(state, asserver);
        }
        
        public void UnregisterModules()
        {
            _modules.Clear();
            _connectionListeners.Clear();
            _connectionStateListeners.Clear();
            _dataListeners.Clear();
        }

        public void AddModule(INetworkModule module)
        {
            _modules.Add(module);
        }
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
            Application.runInBackground = true;

            if (!_subscribed)
                transport = _transport;
            
            _serverModules = new ModulesCollection(this, true);
            _clientModules = new ModulesCollection(this, false);
        }
        
        internal static void RegisterModules(ModulesCollection modules, bool asServer)
        {
            modules.AddModule(new PlayersManager());
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

        public void StopServer() => _transport.StopServer();

        public void StopClient() => _transport.StopClient();
    }
}
