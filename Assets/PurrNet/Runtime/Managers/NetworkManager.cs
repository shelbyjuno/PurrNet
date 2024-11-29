#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet
{
    [Flags]
    public enum StartFlags
    {
        /// <summary>
        /// No flags.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// The server should start in the editor.
        /// </summary>
        Editor = 1,
        
        /// <summary>
        /// The client should start in the editor.
        /// A clone is an editor instance that is not the main editor instance.
        /// For example when you use ParrelSync or other tools that create a clone of the editor.
        /// </summary>
        Clone = 2,
        
        /// <summary>
        /// A client build.
        /// It is a build that doesn't contain the UNITY_SERVER define.
        /// </summary>
        ClientBuild = 4,
        
        /// <summary>
        /// A server build.
        /// It is a build that contains the UNITY_SERVER define.
        /// The define is added automatically when doing a server build.
        /// </summary>
        ServerBuild = 8
    }
    
    [DefaultExecutionOrder(-999)]
    public sealed partial class NetworkManager : MonoBehaviour
    {
        /// <summary>
        /// The main instance of the network manager.
        /// </summary>
        [UsedImplicitly]
        public static NetworkManager main { get; private set; }
        
        [Header("Auto Start Settings")]
        [Tooltip("The flags to determine when the server should automatically start.")]
        [SerializeField] private StartFlags _startServerFlags = StartFlags.ServerBuild | StartFlags.Editor;
        [Tooltip("The flags to determine when the client should automatically start.")]
        [SerializeField] private StartFlags _startClientFlags = StartFlags.ClientBuild | StartFlags.Editor | StartFlags.Clone;
        
        [Header("Persistence Settings")]
        [PurrDocs("systems-and-modules/network-manager")]
        [SerializeField] private CookieScope _cookieScope = CookieScope.LiveWithProcess;

        [Header("Network Settings")]
        [Tooltip("Whether the network manager should not be destroyed on load. " +
                 "If true, the network manager will be moved to the DontDestroyOnLoad scene.")]
        [SerializeField] private bool _dontDestroyOnLoad = true;
        [PurrDocs("systems-and-modules/network-manager/transports")]
        [SerializeField] private GenericTransport _transport;
        [PurrDocs("systems-and-modules/network-manager/network-prefabs")]
        [SerializeField] private NetworkPrefabs _networkPrefabs;
        [PurrDocs("systems-and-modules/network-manager/network-rules")]
        [SerializeField] private NetworkRules _networkRules;
        [PurrDocs("systems-and-modules/network-manager/network-visibility")]
        [SerializeField] private NetworkVisibilityRuleSet _visibilityRules;
        [Tooltip("Number of target ticks per second.")]
        [SerializeField] private int _tickRate = 20;
        
        /// <summary>
        /// The local client connection.
        /// Null if the client is not connected.
        /// </summary>
        public Connection? localClientConnection { get; private set; }

        /// <summary>
        /// The cookie scope of the network manager.
        /// This is used to determine when the cookies should be cleared.
        /// This detemines the lifetime of the cookies which are used to remember connections and their PlayerID.
        /// </summary>
        public CookieScope cookieScope
        {
            get => _cookieScope;
            set
            {
                if (isOffline)
                    _cookieScope = value;
                else
                    PurrLogger.LogError("Failed to update cookie scope since a connection is active.");
            }
        }

        /// <summary>
        /// The start flags of the server.
        /// This is used to determine when the server should automatically start.
        /// </summary>
        public StartFlags startServerFlags { get => _startServerFlags; set => _startServerFlags = value; }

        /// <summary>
        /// The start flags of the client.
        /// This is used to determine when the client should automatically start.
        /// </summary>
        public StartFlags startClientFlags { get => _startClientFlags; set => _startClientFlags = value; }

        /// <summary>
        /// The prefab provider of the network manager.
        /// </summary>
        public IPrefabProvider prefabProvider { get; private set; }
        
        /// <summary>
        /// The visibility rules of the network manager.
        /// </summary>
        public NetworkVisibilityRuleSet visibilityRules => _visibilityRules;
        
        /// <summary>
        /// The original scene of the network manager.
        /// This is the scene the network manager was created in.
        /// </summary>
        public Scene originalScene { get; private set; }
        
        /// <summary>
        /// Occurs when the server connection state changes.
        /// </summary>
        public event Action<ConnectionState> onServerConnectionState;
        
        /// <summary>
        /// Occurs when the client connection state changes.
        /// </summary>
        public event Action<ConnectionState> onClientConnectionState;

        /// <summary>
        /// The transport of the network manager.
        /// This is the main transport used when starting the server or client.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when trying to change the transport while it is being used.</exception>
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

        /// <summary>
        /// Whether the server should automatically start.
        /// </summary>
        public bool shouldAutoStartServer => transport && ShouldStart(_startServerFlags);
        
        /// <summary>
        /// Whether the client should automatically start.
        /// </summary>
        public bool shouldAutoStartClient => transport && ShouldStart(_startClientFlags);

        private bool _isCleaningClient;
        private bool _isCleaningServer;
        
        /// <summary>
        /// The state of the server connection.
        /// This is based on the transport listener state.
        /// </summary>
        public ConnectionState serverState
        {
            get
            {
                var state = !_transport ? ConnectionState.Disconnected : _transport.transport.listenerState;
                return state == ConnectionState.Disconnected && _isCleaningServer ? ConnectionState.Disconnecting : state;
            }
        }

        /// <summary>
        /// The state of the client connection.
        /// This is based on the transport client state.
        /// </summary>
        public ConnectionState clientState
        {
            get
            {
                var state = !_transport ? ConnectionState.Disconnected : _transport.transport.clientState;
                return state == ConnectionState.Disconnected && _isCleaningClient ? ConnectionState.Disconnecting : state;
            }
        }

        /// <summary>
        /// Whether the network manager is a server.
        /// </summary>
        public bool isServer => _transport.transport.listenerState == ConnectionState.Connected;
        
        /// <summary>
        /// Whether the network manager is a client.
        /// </summary>
        public bool isClient => _transport.transport.clientState == ConnectionState.Connected;
        
        /// <summary>
        /// Whether the network manager is offline.
        /// Not a server or a client.
        /// </summary>
        public bool isOffline => !isServer && !isClient;

        /// <summary>
        /// Whether the network manager is a planned host.
        /// This is true even if the server or client is not yet connected or ready.
        /// </summary>
        public bool isPlannedHost => ShouldStart(_startServerFlags) && ShouldStart(_startClientFlags);

        /// <summary>
        /// Whether the network manager is a host.
        /// This is true only if the server and client are connected and ready.
        /// </summary>
        public bool isHost => isServer && isClient;
        
        /// <summary>
        /// Whether the network manager is a server only.
        /// </summary>
        public bool isServerOnly => isServer && !isClient;
        
        /// <summary>
        /// Whether the network manager is a client only.
        /// </summary>
        public bool isClientOnly => !isServer && isClient;
        
        /// <summary>
        /// The network rules of the network manager.
        /// </summary>
        public NetworkRules networkRules => _networkRules;
        
        private ModulesCollection _serverModules;
        private ModulesCollection _clientModules;
        
        private bool _subscribed;
        
        /// <summary>
        /// Sets the main instance of the network manager.
        /// This is used for convinience but also for static RPCs and other static functionality.
        /// </summary>
        /// <param name="instance">The instance to set as the main instance.</param>
        public static void SetMainInstance(NetworkManager instance)
        {
            if (instance)
                main = instance;
        }

        /// <summary>
        /// Sets the prefab provider.
        /// </summary>
        /// <param name="provider">The provider to set.</param>
        public void SetPrefabProvider(IPrefabProvider provider)
        {
            if (!isOffline)
            {
                PurrLogger.LogError("Failed to update prefab provider since a connection is active.");
                return;
            }

            prefabProvider = provider;
        }

        private void Awake()
        {
            if (main && main != this)
            {
                if (main.isOffline)
                {
                    Destroy(gameObject);
                }
                else
                {
                    Destroy(this);
                    return;
                }
            }
            
            if (!networkRules)
                throw new InvalidOperationException(PurrLogger.FormatMessage("NetworkRules is not set (null)."));

            originalScene = gameObject.scene;

            if (_visibilityRules)
            {
                var ogName = _visibilityRules.name;
                _visibilityRules = Instantiate(_visibilityRules);
                _visibilityRules.name = "Copy of " + ogName;
                _visibilityRules.Setup(this);
            }
            
            main = this;

            Time.fixedDeltaTime = 1f / _tickRate;
            Application.runInBackground = true;

            if (_networkPrefabs)
            {
                prefabProvider ??= _networkPrefabs;

                if (_networkPrefabs.autoGenerate)
                    _networkPrefabs.Generate();
                _networkPrefabs.PostProcess();
            }

            if (!_subscribed)
                transport = _transport;
            
            _serverModules = new ModulesCollection(this, true);
            _clientModules = new ModulesCollection(this, false);

            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
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
            if (!gameObject.scene.isLoaded)
                return;
            
            float tickRate = 1f / _tickRate;
            
            if (Mathf.Approximately(Time.fixedDeltaTime, tickRate))
                return;

            Time.fixedDeltaTime = tickRate;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif

        /// <summary>
        /// Gets the module of the given type.
        /// Throws an exception if the module is not found.
        /// </summary>
        /// <param name="asServer">Whether to get the server module or the client module.</param>
        /// <typeparam name="T">The type of the module.</typeparam>
        /// <returns>The module of the given type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the module is not found.</exception>
        public T GetModule<T>(bool asServer) where T : INetworkModule
        {
            if (TryGetModule(out T module, asServer))
                return module;
            
            throw new InvalidOperationException(PurrLogger.FormatMessage($"Module {typeof(T).Name} not found - asServer : {asServer}."));
        }

        /// <summary>
        /// Tries to get the module of the given type.
        /// </summary>
        /// <param name="module">The module if found, otherwise the default value of the type.</param>
        /// <param name="asServer">Whether to get the server module or the client module.</param>
        public bool TryGetModule<T>(out T module, bool asServer) where T : INetworkModule
        {
            return asServer ?
                _serverModules.TryGetModule(out module) :
                _clientModules.TryGetModule(out module);
        }
        
        /// <summary>
        /// Gets all the objects owned by the given player.
        /// This creates a new list every time it's called.
        /// So it's recommended to cache the result if you're going to use it multiple times.
        /// </summary>
        public List<NetworkIdentity> GetAllPlayerOwnedIds(PlayerID player, bool asServer)
        {
            var ownershipModule = GetModule<GlobalOwnershipModule>(asServer);
            return ownershipModule.GetAllPlayerOwnedIds(player);
        }
        
        /// <summary>
        /// Gets all the objects owned by the given player.
        /// Adds the result to the given list.
        /// </summary>
        public void GetAllPlayerOwnedIds(PlayerID player, bool asServer, List<NetworkIdentity> result)
        {
            var ownershipModule = GetModule<GlobalOwnershipModule>(asServer);
            ownershipModule.GetAllPlayerOwnedIds(player, result);
        }
        
        /// <summary>
        /// Gets the current player count.
        /// </summary>
        public int playerCount => GetModule<PlayersManager>(isServer).players.Count;
        
        /// <summary>
        /// Gets the current player list.
        /// This will be update every time a player joins or leaves.
        /// </summary>
        public IReadOnlyList<PlayerID> players => GetModule<PlayersManager>(isServer).players;
        
        /// <summary>
        /// Enumerates all the objects owned by the given player.
        /// </summary>
        /// <param name="player">The player to enumerate the objects of.</param>
        /// <param name="asServer">Whether to get the server module or the client module.</param>
        /// <returns>An enumerable of all the objects owned by the given player.</returns>
        public IEnumerable<NetworkIdentity> EnumerateAllPlayerOwnedIds(PlayerID player, bool asServer)
        {
            var ownershipModule = GetModule<GlobalOwnershipModule>(asServer);
            return ownershipModule.EnumerateAllPlayerOwnedIds(player);
        }
        
        /// <summary>
        /// Adds a visibility rule to the rule set.
        /// </summary>
        /// <param name="manager">The network manager to add the rule to.</param>
        /// <param name="rule">The rule to add.</param>
        public void AddVisibilityRule(NetworkManager manager, INetworkVisibilityRule rule)
        {
            _visibilityRules.AddRule(manager, rule);
        }

        /// <summary>
        /// Removes a visibility rule from the rule set.
        /// </summary>
        /// <param name="rule">The rule to remove.</param>
        public void RemoveVisibilityRule(INetworkVisibilityRule rule)
        {
            _visibilityRules.RemoveRule(rule);
        }
        
        /// <summary>
        /// The scene module of the network manager.
        /// Defaults to the server scene module if the server is active.
        /// Otherwise it defaults to the client scene module.
        /// </summary>
        public ScenesModule sceneModule => _serverSceneModule ?? _clientSceneModule;
        
        /// <summary>
        /// The players manager of the network manager.
        /// Defaults to the server players manager if the server is active.
        /// Otherwise it defaults to the client players manager.
        /// </summary>
        public PlayersManager playerModule => _serverPlayersManager ?? _clientPlayersManager;
        
        /// <summary>
        /// The tick manager of the network manager.
        /// Defaults to the server tick manager if the server is active.
        /// Otherwise it defaults to the client tick manager.
        /// </summary>
        public TickManager tickModule => _serverTickManager ?? _clientTickManager;
        
        /// <summary>
        /// The players broadcaster of the network manager.
        /// Defaults to the server players broadcaster if the server is active.
        /// Otherwise it defaults to the client players broadcaster.
        /// </summary>
        public PlayersBroadcaster broadcastModule => _serverPlayersBroadcast ?? _clientPlayersBroadcast;
        
        /// <summary>
        /// The local player of the network manager.
        /// If the local player is not set, this will return the default value of the player id.
        /// </summary>
        public PlayerID localPlayer => playerModule.localPlayerId ?? default;
        
        private ScenesModule _clientSceneModule;
        private ScenesModule _serverSceneModule;
        
        private PlayersManager _clientPlayersManager;
        private PlayersManager _serverPlayersManager;
        
        private TickManager _clientTickManager;
        private TickManager _serverTickManager;
        
        private PlayersBroadcaster _clientPlayersBroadcast;
        private PlayersBroadcaster _serverPlayersBroadcast;
        
        internal void RegisterModules(ModulesCollection modules, bool asServer)
        {
            var tickManager = new TickManager(_tickRate);
            
            if (asServer)
                _serverTickManager = tickManager;
            else _clientTickManager = tickManager;

            var connBroadcaster = new BroadcastModule(this, asServer);
            var networkCookies = new CookiesModule(_cookieScope);
            var playersManager = new PlayersManager(this, networkCookies, connBroadcaster);
            
            if (asServer)
                 _serverPlayersManager = playersManager;
            else _clientPlayersManager = playersManager;
            
            var playersBroadcast = new PlayersBroadcaster(connBroadcaster, playersManager);
            
            if (asServer)
                _serverPlayersBroadcast = playersBroadcast;
            else _clientPlayersBroadcast = playersBroadcast;

            var scenesModule = new ScenesModule(this, playersManager);
            
            if (asServer)
                 _serverSceneModule = scenesModule;
            else _clientSceneModule = scenesModule;

            var scenePlayersModule = new ScenePlayersModule(this, scenesModule, playersManager);
            
            var hierarchyModule = new HierarchyModule(this, scenesModule, playersManager, scenePlayersModule, prefabProvider);
            var visibilityFactory = new VisibilityFactory(this, playersManager, hierarchyModule, scenePlayersModule);
            var ownershipModule = new GlobalOwnershipModule(visibilityFactory, hierarchyModule, playersManager, scenePlayersModule, scenesModule);
            var rpcModule = new RPCModule(playersManager, visibilityFactory, hierarchyModule, ownershipModule, scenesModule);
            var rpcRequestResponseModule = new RpcRequestResponseModule(playersManager);
            
            hierarchyModule.SetVisibilityFactory(visibilityFactory);
            scenesModule.SetScenePlayers(scenePlayersModule);
            playersManager.SetBroadcaster(playersBroadcast);
            
            modules.AddModule(playersManager);
            modules.AddModule(playersBroadcast);
            modules.AddModule(tickManager);
            modules.AddModule(connBroadcaster);
            modules.AddModule(networkCookies);
            modules.AddModule(scenesModule);
            modules.AddModule(scenePlayersModule);
            
            modules.AddModule(hierarchyModule);
            modules.AddModule(visibilityFactory);
            modules.AddModule(ownershipModule);
            
            modules.AddModule(rpcModule);
            modules.AddModule(rpcRequestResponseModule);
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
            bool serverConnected = serverState == ConnectionState.Connected;
            bool clientConnected = clientState == ConnectionState.Connected;
            
            if (serverConnected)
                _serverModules.TriggerOnPreFixedUpdate();
            
            if (clientConnected)
                _clientModules.TriggerOnPreFixedUpdate();
            
            if (_transport)
                _transport.transport.UpdateEvents(Time.fixedDeltaTime);
            
            if (serverConnected)
                _serverModules.TriggerOnFixedUpdate();
            
            if (clientConnected)
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

        /// <summary>
        /// Starts the server.
        /// This will start the transport server.
        /// </summary>
        public void StartServer()
        {
            if (!_transport)
                PurrLogger.Throw<InvalidOperationException>("Transport is not set (null).");
            _transport.StartServer(this);
        }

        /// <summary>
        /// Internal method to register the server modules.
        /// Avoid calling this method directly if you're not sure what you're doing.
        /// </summary>
        public void InternalRegisterServerModules()
        {
            _serverModules.RegisterModules();
        }
        
        /// <summary>
        /// Internal method to register the client modules.
        /// Avoid calling this method directly if you're not sure what you're doing.
        /// </summary>
        public void InternalRegisterClientModules()
        {
            _clientModules.RegisterModules();
        }
        
        /// <summary>
        /// Starts the client.
        /// This will start the transport client.
        /// </summary>
        public void StartClient()
        {
            localClientConnection = null;
            if (!_transport)
                PurrLogger.Throw<InvalidOperationException>("Transport is not set (null).");
            _transport.StartClient(this);
        }

        private void OnNewConnection(Connection conn, bool asserver)
        {
            if (asserver)
                 _serverModules.OnNewConnection(conn, true);
            else
            {
                if (localClientConnection.HasValue)
                    PurrLogger.LogError($"A client connection already exists '{localClientConnection}', overwriting it with {conn}.");
                
                localClientConnection = conn;
                _clientModules.OnNewConnection(conn, false);
            }
        }

        private void OnLostConnection(Connection conn, DisconnectReason reason, bool asserver)
        {
            if (asserver)
                 _serverModules.OnLostConnection(conn, true);
            else
            {
                localClientConnection = null;
                _clientModules.OnLostConnection(conn, false);
            }
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
        
        /// <summary>
        /// Tries to get the module of the given type.
        /// </summary>
        /// <param name="asServer">Whether to get the server module or the client module.</param>
        /// <param name="module">The module if found, otherwise the default value of the type.</param>
        /// <typeparam name="T">The type of the module.</typeparam>
        /// <returns>Whether the module was found.</returns>
        public bool TryGetModule<T>(bool asServer, out T module) where T : INetworkModule
        {
            return asServer ? 
                _serverModules.TryGetModule(out module) : 
                _clientModules.TryGetModule(out module);
        }

        /// <summary>
        /// Stops the server.
        /// This will stop the transport server.
        /// </summary>
        public void StopServer() => _transport.StopServer();
        
        /// <summary>
        /// Stops the client.
        /// This will stop the transport client.
        /// </summary>
        public void StopClient() => _transport.StopClient();

        /// <summary>
        /// Gets the prefab from the given guid.
        /// </summary>
        /// <param name="guid">The guid of the prefab to get.</param>
        /// <returns>The prefab with the given guid.</returns>
        public GameObject GetPrefabFromGuid(string guid)
        {
            return _networkPrefabs.GetPrefabFromGuid(guid);
        }
    }
}
