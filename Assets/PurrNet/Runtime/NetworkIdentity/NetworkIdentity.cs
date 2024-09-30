using System;
using System.Reflection;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public delegate void OnRootChanged(NetworkIdentity identity, NetworkIdentity oldRoot, NetworkIdentity newRoot);

    [DefaultExecutionOrder(-1000)]
    public partial class NetworkIdentity : MonoBehaviour
    {
        /// <summary>
        /// The prefab id of this object;
        /// Is -1 if scene object.
        /// </summary>
        public int prefabId { get; private set; } = -1;

        [UsedImplicitly]
        public ushort prefabOffset { get; private set; }

        [UsedImplicitly]
        public int siblingIndex { get; private set; } = -1;

        /// <summary>
        /// Network id of this object.
        /// </summary>
        public NetworkID? id => idServer ?? idClient;
        
        /// <summary>
        /// Scene id of this object.
        /// </summary>
        public SceneID sceneId { get; private set; }
        
        /// <summary>
        /// Is spawned over the network.
        /// </summary>
        public bool isSpawned => id.HasValue;
        
        public bool isSceneObject => isSpawned && prefabId == -1;

        public bool isServer => networkManager.isServer;
        
        public bool isClient => networkManager.isClient;
        
        public bool isHost => networkManager.isHost;
        
        public bool isOwner => isSpawned && localPlayer.HasValue && owner == localPlayer;
        
        public bool hasOwner => owner.HasValue;
        
        public bool hasConnectedOwner => owner.HasValue && networkManager.TryGetModule<PlayersManager>(isServer, out var module) && module.IsPlayerConnected(owner.Value);

        internal PlayerID? internalOwnerServer;
        internal PlayerID? internalOwnerClient;
        
        private TickManager _serverTickManager;
        private TickManager _clientTickManager;

        private NetworkID? idServer;
        private NetworkID? idClient;
        
        /// <summary>
        /// Returns the owner of this object.
        /// It will return the owner of the closest parent object.
        /// If you can, cache this value for performance.
        /// </summary>
        public PlayerID? owner => internalOwnerServer ?? internalOwnerClient;
        
        public NetworkManager networkManager { get; private set; }
        
        public PlayerID? localPlayer => isSpawned && networkManager.TryGetModule<PlayersManager>(false, out var module) && module.localPlayerId.HasValue 
            ? module.localPlayerId.Value : null;
        
        internal event OnRootChanged onRootChanged;
        internal event Action<NetworkIdentity> onRemoved;
        internal event Action<NetworkIdentity, bool> onEnabledChanged;
        internal event Action<NetworkIdentity, bool> onActivatedChanged;
        
        private bool _lastEnabledState;
        private GameObjectEvents _events;
        private GameObject _gameObject;
        private NetworkIdentity _root;
        
        private NetworkIdentity GetRootIdentity()
        {
            var lastKnown = this;
            var current = transform.parent;

            while (current)
            {
                if (current.TryGetComponent(out NetworkIdentity identity))
                    lastKnown = identity;
                
                current = current.parent;
            }
            
            return lastKnown;
        }

        private IServerSceneEvents _serverSceneEvents;
        
        private void InternalOnSpawn(bool asServer)
        {
            if (asServer)
            {
                _serverTickManager = networkManager.GetModule<TickManager>(true);
                _serverTickManager.onTick += ServerTick;
            }
            else
            {
                _clientTickManager = networkManager.GetModule<TickManager>(false);
                _clientTickManager.onTick += ClientTick;
            }
            
            if (networkManager.TryGetModule<PlayersManager>(asServer, out var players))
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (this is IPlayerEvents events)
                {
                    players.onPlayerJoined += events.OnPlayerConnected;
                    players.onPlayerLeft += events.OnPlayerDisconnected;
                }
                
                if (networkManager.TryGetModule<ScenePlayersModule>(asServer, out var scenePlayers))
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (this is IServerSceneEvents sceneEvents)
                    {
                        _serverSceneEvents = sceneEvents;
                        scenePlayers.onPlayerJoinedScene += OnServerJoinedScene;
                        scenePlayers.onPlayerLeftScene += OnServerLeftScene;
                    }
                }
            }
        }
        
        private void InternalOnDespawn(bool asServer)
        {
            if (asServer)
            {
                _serverTickManager.onTick -= ServerTick;
            }
            else _clientTickManager.onTick -= ClientTick;
            
            if (networkManager.TryGetModule<PlayersManager>(asServer, out var players))
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (this is IPlayerEvents events)
                {
                    players.onPlayerJoined -= events.OnPlayerConnected;
                    players.onPlayerLeft -= events.OnPlayerDisconnected;
                }
                
                if (networkManager.TryGetModule<ScenePlayersModule>(asServer, out var scenePlayers))
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (_serverSceneEvents != null)
                    {
                        scenePlayers.onPlayerJoinedScene -= OnServerJoinedScene;
                        scenePlayers.onPlayerLeftScene -= OnServerLeftScene;
                    }
                }
            }
        }
        
        void OnServerJoinedScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene == sceneId)
                _serverSceneEvents?.OnPlayerJoinedScene(player);
        }
        
        void OnServerLeftScene(PlayerID player, SceneID scene, bool asserver)
        {
            if (scene == sceneId)
                _serverSceneEvents?.OnPlayerLeftScene(player);
        }

        private void InternalOnServerTick()
        {
            var root = GetRootIdentity();

            if (root != _root)
            {
                var oldRoot = _root;
                _root = root;
                onRootChanged?.Invoke(this, oldRoot, root);
            }
        }
        
        private void ClientTick()
        {
            OnTick(_clientTickManager.tickDelta, false);
        }

        private void ServerTick()
        {
            InternalOnServerTick();
            OnTick(_serverTickManager.tickDelta, true);
        }

        internal PlayerID? GetOwner(bool asServer) => asServer ? internalOwnerServer : internalOwnerClient;

        internal bool IsSpawned(bool asServer) => asServer ? idServer.HasValue : idClient.HasValue;

        protected virtual void OnSpawned() { }
        
        protected virtual void OnDespawned() { }
        
        protected virtual void OnSpawned(bool asServer) { }
        
        protected virtual void OnTick(float delta, bool asServer) {}

        protected virtual void OnInitializeModules() { }
        
        protected virtual void OnDespawned(bool asServer) { }

        protected virtual void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) { }

        protected virtual void OnOwnerDisconnected(PlayerID ownerId, bool asServer) { }

        protected virtual void OnOwnerConnected(PlayerID ownerId, bool asServer) { }

        public bool IsNotOwnerPredicate(PlayerID player)
        {
            return player != owner;
        }
        
        private void OnActivated(bool active)
        {
            if (_ignoreNextActivation)
            {
                _ignoreNextActivation = false;
                return;
            }
            
            onActivatedChanged?.Invoke(this, active);
        }

        public virtual void OnEnable()
        {
            UpdateEnabledState();
        }
        
        public virtual void OnDisable()
        {
            UpdateEnabledState();
        }

        internal void UpdateEnabledState()
        {
            if (_lastEnabledState != enabled)
            {
                if (_ignoreNextEnable)
                     _ignoreNextEnable = false;
                else onEnabledChanged?.Invoke(this, enabled);

                _lastEnabledState = enabled;
            }
        }

        private void CallInitMethods()
        {
            var type = GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name.EndsWith("_CodeGen_Initialize"))
                    m.Invoke(this, Array.Empty<object>());
            }
        }

        internal void PostSetIdentity()
        {
            if (_pendingOwnershipRequest.HasValue)
            {
                GiveOwnershipInternal(_pendingOwnershipRequest.Value);
                _pendingOwnershipRequest = null;
            }
        }
        
        internal void SetIdentity(NetworkManager manager, SceneID scene, int pid, int siblingIdx, NetworkID identityId, ushort offset, bool asServer)
        {
            Hasher.PrepareType(GetType());
            
            networkManager = manager;
            sceneId = scene;
            prefabId = pid;
            siblingIndex = siblingIdx;
            prefabOffset = offset;

            bool wasAlreadySpawned = isSpawned;
            
            if (asServer)
                 idServer = identityId;
            else idClient = identityId;

            if (asServer)
                 internalOwnerServer = null;
            else internalOwnerClient = null;
            
            _lastEnabledState = enabled;
            _gameObject = gameObject;
            
            if (!_gameObject.TryGetComponent(out _events))
            {
                _events = _gameObject.AddComponent<GameObjectEvents>();
                _events.InternalAwake();
                _events.hideFlags = HideFlags.HideInInspector;
                _events.onActivatedChanged += OnActivated;
                _events.Register(this);
            }

            if (!wasAlreadySpawned)
            {
                OnInitializeModules();
                CallInitMethods();
            }
        }

        private bool _ignoreNextDestroy;
        
        public void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        private PlayerID? _pendingOwnershipRequest;
        
        public void GiveOwnership(PlayerID player)
        {
            if (!networkManager)
            {
                _pendingOwnershipRequest = player;
                return;
            }
            
            GiveOwnershipInternal(player);
        }
        
        private void GiveOwnershipInternal(PlayerID player)
        {
            if (!networkManager)
            {
                PurrLogger.LogError("Trying to give ownership to " + player + " but identity isn't spawned.", this);
                return;
            }
            
            if (networkManager.TryGetModule(networkManager.isServer, out GlobalOwnershipModule module))
            {
                module.GiveOwnership(this, player);
            }
            else PurrLogger.LogError("Failed to get ownership module.", this);
        }
        
        public void RemoveOwnership()
        {
            if (networkManager.TryGetModule(networkManager.isServer, out GlobalOwnershipModule module))
            {
                module.RemoveOwnership(this);
            }
            else PurrLogger.LogError("Failed to get ownership module.");
        }
        
        protected virtual void OnDestroy()
        {
            if (_events)
                _events.Unregister(this);
            
            if (_ignoreNextDestroy)
            {
                _ignoreNextDestroy = false;
                return;
            }
            
            if (ApplicationContext.isQuitting)
                return;

            onRemoved?.Invoke(this);
        }
        
        private bool _ignoreNextActivation;
        private bool _ignoreNextEnable;

        internal void IgnoreNextActivationCallback()
        {
            _ignoreNextActivation = true;
        }
        
        internal void IgnoreNextEnableCallback()
        {
            _ignoreNextEnable = true;
        }
        
        private int _spawnedCount;
        
        internal void TriggerSpawnEvent(bool asServer)
        {
            InternalOnSpawn(asServer);
            OnSpawned(asServer);

            for (int i = 0; i < _modules.Count; i++)
                _modules[i].OnSpawn(asServer);

            if (_spawnedCount == 0)
            {
                OnSpawned();

                for (int i = 0; i < _modules.Count; i++)
                    _modules[i].OnSpawn();
            }
            
            _spawnedCount++;
        }

        internal void TriggerDespawnEvent(bool asServer)
        {
            if (!IsSpawned(asServer)) return;

            InternalOnDespawn(asServer);
            OnDespawned(asServer);

            for (int i = 0; i < _modules.Count; i++)
                _modules[i].OnDespawned(asServer);
            
            if (asServer)
                 idServer = null;
            else idClient = null;
            
            _spawnedCount--;

            if (_spawnedCount == 0)
            {
                OnDespawned();

                for (int i = 0; i < _modules.Count; i++)
                    _modules[i].OnDespawned();

                _modules.Clear();
            }
        }

        internal void TriggerOnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) 
        {
            OnOwnerChanged(oldOwner, newOwner, asServer);
        }

        internal void TriggerOnOwnerDisconnected(PlayerID ownerId, bool asServer)
        {
            OnOwnerDisconnected(ownerId, asServer);
        }

        internal void TriggerOnOwnerReconnected(PlayerID ownerId, bool asServer)
        {
            OnOwnerConnected(ownerId, asServer);
        }
    }
}
