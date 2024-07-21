#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using JetBrains.Annotations;
using PurrNet.Logging;
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
        public static NetworkManager main { get; private set; }
        
        [Header("Auto Start Settings")]
        [SerializeField] private StartFlags _startServerFlags = StartFlags.ServerBuild | StartFlags.Editor;
        [SerializeField] private StartFlags _startClientFlags = StartFlags.ClientBuild | StartFlags.Editor | StartFlags.Clone;
        
        [Header("Persistence Settings")]
        [SerializeField] private CookieScope _cookieScope = CookieScope.LiveWithProcess;

        [Header("Network Settings")]
        [SerializeField] private GenericTransport _transport;
        [SerializeField] private NetworkPrefabs _networkPrefabs;
        [SerializeField] private NetworkRules _networkRules;
        [SerializeField] private int _tickRate = 20;
        
        /// <summary>
        /// Occurs when the server connection state changes.
        /// </summary>
        public event Action<ConnectionState> onServerConnectionState;
        
        /// <summary>
        /// Occurs when the client connection state changes.
        /// </summary>
        public event Action<ConnectionState> onClientConnectionState;

        [NotNull]
        public GenericTransport transport
        {
            get => _transport;
            set
            {
                if (_transport)
                {
                    if (serverState != ConnectionState.Disconnected ||
                        clientState != ConnectionState.Disconnected)
                    {
                        throw new InvalidOperationException(PurrLogger.FormatMessage("Cannot change transport while it is being used."));
                    }
                    
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

        public bool shouldAutoStartServer => transport && ShouldStart(_startServerFlags);
        public bool shouldAutoStartClient => transport && ShouldStart(_startClientFlags);

        private bool _isCleaningClient;
        private bool _isCleaningServer;
        
        public ConnectionState serverState
        {
            get
            {
                var state = !_transport ? ConnectionState.Disconnected : _transport.transport.listenerState;
                return state == ConnectionState.Disconnected && _isCleaningServer ? ConnectionState.Disconnecting : state;
            }
        }

        public ConnectionState clientState
        {
            get
            {
                var state = !_transport ? ConnectionState.Disconnected : _transport.transport.clientState;
                return state == ConnectionState.Disconnected && _isCleaningClient ? ConnectionState.Disconnecting : state;
            }
        }

        public bool isServer => _transport.transport.listenerState == ConnectionState.Connected;
        
        public bool isClient => _transport.transport.clientState == ConnectionState.Connected;
        
        public bool isOffline => !isServer && !isClient;

        public bool isPlannedHost => ShouldStart(_startServerFlags) && ShouldStart(_startClientFlags);

        public bool isHost => isServer && isClient;
        
        public bool isServerOnly => isServer && !isClient;
        
        public bool isClientOnly => !isServer && isClient;
        
        public NetworkRules networkRules => _networkRules;
        
        private ModulesCollection _serverModules;
        private ModulesCollection _clientModules;
        
        private bool _subscribed;
        
        public static void SetMainInstance(NetworkManager instance)
        {
            if (instance)
                main = instance;
        }

        private void Awake()
        {
            if (!main)
                main = this;
            
            Application.runInBackground = true;
            
            if(_networkPrefabs.autoGenerate)
                _networkPrefabs.Generate();
            _networkPrefabs.PostProcess();

            if (!_subscribed)
                transport = _transport;
            
            _serverModules = new ModulesCollection(this, true);
            _clientModules = new ModulesCollection(this, false);
        }

        private void Reset()
        {
            if (TryGetComponent(out GenericTransport _) || transport)
                return;
            transport = gameObject.AddComponent<UDPTransport>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            float tickRate = 1f / _tickRate;
            
            if (Time.fixedDeltaTime == tickRate)
                return;
            
            Time.fixedDeltaTime = tickRate;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif

        public T GetModule<T>(bool asServer) where T : INetworkModule
        {
            if (TryGetModule(out T module, asServer))
                return module;
            
            throw new InvalidOperationException(PurrLogger.FormatMessage($"Module {typeof(T).Name} not found - asServer : {asServer}."));
        }

        public bool TryGetModule<T>(out T module, bool asServer) where T : INetworkModule
        {
            switch (asServer)
            {
                case true when !isServer:
                case false when !isClient:
                    module = default;
                    return false;
            }

            return asServer ?
                _serverModules.TryGetModule(out module) :
                _clientModules.TryGetModule(out module);
        }
        
        internal void RegisterModules(ModulesCollection modules, bool asServer)
        {
            var tickManager = new TickManager(_tickRate);
            var broadcastModule = new BroadcastModule(this, asServer);
            var networkCookies = new CookiesModule(_cookieScope);
            
            var playersManager = new PlayersManager(this, networkCookies, broadcastModule);
            var playersBroadcast = new PlayersBroadcaster(broadcastModule, playersManager);

            var scenesModule = new ScenesModule(this, playersManager);
            var scenePlayersModule = new ScenePlayersModule(scenesModule, playersManager);
            
            var hierarchyModule = new HierarchyModule(this, scenesModule, playersManager, scenePlayersModule, _networkPrefabs);
            var ownershipModule = new GlobalOwnershipModule(hierarchyModule, playersManager, scenePlayersModule, scenesModule);
            
            var rpcModule = new RPCModule(playersManager, hierarchyModule);

            scenesModule.SetScenePlayers(scenePlayersModule);
            playersManager.SetBroadcaster(playersBroadcast);
            
            modules.AddModule(tickManager);
            modules.AddModule(broadcastModule);
            modules.AddModule(networkCookies);
            modules.AddModule(playersManager);
            modules.AddModule(playersBroadcast);
            modules.AddModule(scenesModule);
            modules.AddModule(scenePlayersModule);
            modules.AddModule(ownershipModule);
            modules.AddModule(rpcModule);
            modules.AddModule(hierarchyModule);
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
            bool shouldStartServer = transport && ShouldStart(_startServerFlags);
            bool shouldStartClient = transport && ShouldStart(_startClientFlags);
            
            if (shouldStartServer)
                StartServer();
            
            if (shouldStartClient)
                StartClient();
        }

        private void Update()
        {
            _serverModules.TriggerOnUpdate();
            _clientModules.TriggerOnUpdate();
        }

        private void FixedUpdate()
        {
            if (serverState == ConnectionState.Connected)
                _serverModules.TriggerOnFixedUpdate();
            
            if (clientState == ConnectionState.Connected)
                _clientModules.TriggerOnFixedUpdate();

            if (_isCleaningClient && _clientModules.Cleanup())
            {
                _clientModules.UnregisterModules();
                _isCleaningClient = false;
            }

            if (_isCleaningServer && _serverModules.Cleanup())
            {
                _serverModules.UnregisterModules();
                _isCleaningServer = false;
            }
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
            if (!_transport)
                PurrLogger.Throw<InvalidOperationException>("Transport is not set (null).");
            _serverModules.RegisterModules();
            _transport.StartServer();
        }
        
        public void StartClient()
        {
            if (!_transport)
                PurrLogger.Throw<InvalidOperationException>("Transport is not set (null).");
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
            
            if (asserver)
                 onServerConnectionState?.Invoke(state);
            else onClientConnectionState?.Invoke(state);

            if (state == ConnectionState.Disconnected)
            {
                switch (asserver)
                {
                    case false:
                        _isCleaningClient = true;
                        break;
                    case true:
                        _isCleaningServer = true;
                        break;
                }
            }
        }
        
        public bool TryGetModule<T>(bool asServer, out T module) where T : INetworkModule
        {
            return asServer ? 
                _serverModules.TryGetModule(out module) : 
                _clientModules.TryGetModule(out module);
        }

        public void StopServer() => _transport.StopServer();

        public void StopClient() => _transport.StopClient();

        public GameObject GetPrefabFromGuid(string guid)
        {
            for (int i = 0; i < _networkPrefabs.prefabs.Count; i++)
            {
                if (_networkPrefabs.prefabs[i].TryGetComponent<PrefabLink>(out var link) && link.MatchesGUID(guid))
                    return _networkPrefabs.prefabs[i];
            }
            
            return null;
        }
    }
}
