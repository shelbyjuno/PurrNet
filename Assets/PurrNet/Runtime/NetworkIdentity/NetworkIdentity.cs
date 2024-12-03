using System;
using System.Collections.Generic;
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
        /// Network id of this object. Holds more information than the ObjectId
        /// </summary>
        public NetworkID? id => idServer ?? idClient;
        
        /// <summary>
        /// Unique ObjectId of this object
        /// </summary>
        public int ObjectId => id?.id ?? -1;
        
        /// <summary>
        /// Scene id of this object.
        /// </summary>
        public SceneID sceneId { get; private set; }
        
        /// <summary>
        /// Is spawned over the network.
        /// </summary>
        public bool isSpawned => _isSpawnedServer || _isSpawnedClient;
        
        public bool isSceneObject => prefabId <= -1;

        public bool isServer => isSpawned && networkManager.isServer;
        
        public bool isClient => isSpawned && networkManager.isClient;
        
        public bool isHost => isSpawned && networkManager.isHost;
        
        public bool isOwner => isSpawned && isClient && localPlayer.HasValue && owner == localPlayer;
        
        public bool hasOwner => owner.HasValue;
        
        protected int _autoSpawnCalledFrame;
        
        Queue<Action> _onSpawnedQueue;

        /// <summary>
        /// Returns if you can control this object.
        /// If the object has an owner, it will return if you are the owner.
        /// If the object doesn't have an owner, it will return if you are the server.
        /// </summary>
        [UsedImplicitly]
        public bool isController => isSpawned && (hasConnectedOwner ? isOwner : isServer);
        
        /// <summary>
        /// Returns if you can control this object.
        /// If ownerHasAuthority is true, it will return true if you are the owner.
        /// If ownerHasAuthority is false, it will return true if you are the server.
        /// Otherwise, similar to isController.
        /// </summary>
        /// <param name="ownerHasAuthority">Should owner be controller or is it server only</param>
        /// <returns>Can you control this identity</returns>
        [UsedImplicitly]
        public bool IsController(bool ownerHasAuthority) => ownerHasAuthority ? isController : isServer;
        
        public bool hasConnectedOwner => owner.HasValue && networkManager.TryGetModule<PlayersManager>(isServer, out var module) && module.IsPlayerConnected(owner.Value);

        internal PlayerID? internalOwnerServer;
        internal PlayerID? internalOwnerClient;
        
        private TickManager _serverTickManager;
        private TickManager _clientTickManager;

        private bool _isSpawnedClient;
        private bool _isSpawnedServer;
        
        public NetworkID? idServer;
        public NetworkID? idClient;
        
        /// <summary>
        /// Returns the owner of this object.
        /// It will return the owner of the closest parent object.
        /// If you can, cache this value for performance.
        /// </summary>
        public PlayerID? owner => internalOwnerServer ?? internalOwnerClient;
        
        public NetworkManager networkManager { get; private set; }
        
        /// <summary>
        /// The cached value of the local player.
        /// </summary>
        public PlayerID? localPlayer { get; private set; }
        
        /// <summary>
        /// Returns the local player if it exists.
        /// Defaults to default(PlayerID) if it doesn't exist.
        /// </summary>
        [UsedByIL]
        public PlayerID localPlayerForced => localPlayer ?? default;
        
        public event OnRootChanged onRootChanged;
        public event Action<NetworkIdentity> onFlush;
        public event Action<NetworkIdentity> onRemoved;
        public event Action<NetworkIdentity, bool> onEnabledChanged;
        public event Action<NetworkIdentity, bool> onActivatedChanged;
        
        private bool _lastEnabledState;
        private GameObjectEvents _events;
        private GameObject _gameObject;
        
        public ISet<PlayerID> observers
        {
            get
            {
                if (!root || !root.isSpawned || !root.id.HasValue)
                    return VisibilityFactory.EMPTY_OBSERVERS;
                
                if (!networkManager.TryGetModule<VisibilityFactory>(true, out var factory))
                    return VisibilityFactory.EMPTY_OBSERVERS;
                
                if (factory.TryGetObservers(sceneId, root.id.Value, out var result))
                    return result;
                
                return VisibilityFactory.EMPTY_OBSERVERS;
            }
        }

        /// <summary>
        /// The root identity is the topmost parent that has a NetworkIdentity.
        /// </summary>
        public NetworkIdentity root { get; private set; }
        
        public void QueueOnSpawned(Action action)
        {
            _onSpawnedQueue ??= new Queue<Action>();
            _onSpawnedQueue.Enqueue(action);
        }
        
        public NetworkIdentity GetRootIdentity()
        {
            var lastKnown = gameObject.GetComponent<NetworkIdentity>();
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
        private int onTickCount;
        private ITick _ticker;
        
        private readonly List<ITick> _tickables = new ();
        
        private void InternalOnSpawn(bool asServer)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (_ticker == null && this is ITick ticker)
                _ticker = ticker;
            
            if (asServer)
            {
                _serverTickManager = networkManager.GetModule<TickManager>(true);
                _serverTickManager.onTick += ServerTick;
            }
            else if (_ticker != null || _tickables.Count > 0)
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
                if (_serverTickManager != null)
                    _serverTickManager.onTick -= ServerTick;
            }
            else if (_ticker != null || _tickables.Count > 0) 
            {
                if (_clientTickManager != null)
                    _clientTickManager.onTick -= ClientTick;
            }

            if (!networkManager.TryGetModule<PlayersManager>(asServer, out var players)) return;
            
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (this is IPlayerEvents events)
            {
                players.onPlayerJoined -= events.OnPlayerConnected;
                players.onPlayerLeft -= events.OnPlayerDisconnected;
            }

            if (!networkManager.TryGetModule<ScenePlayersModule>(asServer, out var scenePlayers)) return;
            
            if (_serverSceneEvents == null) return;
            
            scenePlayers.onPlayerJoinedScene -= OnServerJoinedScene;
            scenePlayers.onPlayerLeftScene -= OnServerLeftScene;
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
            var rootId = GetRootIdentity();

            if (rootId != root)
            {
                var oldRoot = root;
                root = rootId;
                onRootChanged?.Invoke(this, oldRoot, rootId);
            }
        }
        
        private void ClientTick()
        {
            _ticker?.OnTick(_clientTickManager.tickDelta);

            for (var i = 0; i < _tickables.Count; i++)
            {
                var ticker = _tickables[i];
                ticker.OnTick(_clientTickManager.tickDelta);
            }
        }

        private void ServerTick()
        {
            InternalOnServerTick();

            if (!isClient)
            {
                _ticker?.OnTick(_serverTickManager.tickDelta);
                for (var i = 0; i < _tickables.Count; i++)
                {
                    var ticker = _tickables[i];
                    ticker.OnTick(_serverTickManager.tickDelta);
                }
            }
        }

        internal PlayerID? GetOwner(bool asServer) => asServer ? internalOwnerServer : internalOwnerClient;

        [UsedImplicitly]
        public bool IsSpawned(bool asServer) => asServer ? _isSpawnedServer : _isSpawnedClient;

        protected virtual void OnSpawned() { }
        
        protected virtual void OnDespawned() { }
        
        protected virtual void OnSpawned(bool asServer) { }
        
        protected virtual void OnInitializeModules() { }
        
        protected virtual void OnDespawned(bool asServer) { }

        protected virtual void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) { }

        protected virtual void OnOwnerDisconnected(PlayerID ownerId, bool asServer) { }

        protected virtual void OnOwnerConnected(PlayerID ownerId, bool asServer) { }
        
        /// <summary>
        /// Called when an observer is added.
        /// Server only.
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnObserverAdded(PlayerID player) { }
        
        /// <summary>
        /// Called when an observer is removed.
        /// Server only.
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnObserverRemoved(PlayerID player) { }

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
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name.EndsWith("_CodeGen_Initialize"))
                    m.Invoke(this, Array.Empty<object>());
            }
        }

        internal void PostSetIdentity()
        {
            if (!_pendingOwnershipRequest.HasValue) return;
            
            GiveOwnershipInternal(_pendingOwnershipRequest.Value);
            _pendingOwnershipRequest = null;
        }

        /// <summary>
        /// The layer of this object. Avoids gameObject.layer.
        /// Only available when spawned.
        /// </summary>
        public int layer { get; private set; }
        
        internal void SetIdentity(NetworkManager manager, SceneID scene, int pid, int siblingIdx, NetworkID identityId, ushort offset, bool asServer)
        {
            layer = gameObject.layer;
            networkManager = manager;
            sceneId = scene;
            prefabId = pid;
            siblingIndex = siblingIdx;
            prefabOffset = offset;

            if (!localPlayer.HasValue && networkManager.TryGetModule<PlayersManager>(asServer, out var playersManager))
                localPlayer = playersManager.localPlayerId;

            bool wasAlreadySpawned = isSpawned;

            if (asServer)
            {
                _isSpawnedServer = true;
                idServer = identityId;
            }
            else
            {
                _isSpawnedClient = true;
                idClient = identityId;
            }

            if (asServer)
            {
                internalOwnerServer = null;
                InternalOnServerTick();
            }
            else
            {
                internalOwnerClient = null;
            }
            
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
                _modules.Clear();
                _externalModulesView.Clear();
                _moduleId = 0;
                
                OnInitializeModules();
                CallInitMethods();

                foreach (var module in _externalModulesView)
                    module.OnInitializeModules();
                
                _tickables.Clear();
                RegisterEvents();
            }
            
            if (_visitiblityRules && !_visitiblityRules.isInitialized)
            {
                _visitiblityRules = Instantiate(_visitiblityRules);
                _visitiblityRules.Setup(manager);
            }
        }

        private bool _ignoreNextDestroy;
        
        internal void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        internal void ResetIgnoreNextDestroy()
        {
            _ignoreNextDestroy = false;
        }
        
        private PlayerID? _pendingOwnershipRequest;
        
        /// <summary>
        /// Gives ownership of this object to the player.
        /// </summary>
        /// <param name="player">PlayerID to give ownership to</param>
        /// <param name="silent">Dont log any errors if in silent mode</param>
        public void GiveOwnership(PlayerID player, bool silent = false)
        {
            if (!networkManager)
            {
                _pendingOwnershipRequest = player;
                return;
            }
            
            GiveOwnershipInternal(player, silent);
        }
        
        /// <summary>
        /// Spawns the object over the network.
        /// The gameobject must contain a PrefabLink component in order to spawn.
        /// Errors will be logged if something goes wrong.
        /// </summary>
        /// <param name="asServer">Weather to spawn from the prespective of the server or the client</param>
        public void Spawn(bool asServer)
        {
            if (isSpawned)
                return;
            
            if (!networkManager)
                return;
            
            if (networkManager.TryGetModule(networkManager.isServer, out HierarchyModule module))
            {
                module.Spawn(gameObject);
            }
            else PurrLogger.LogError("Failed to get spawn module.", this);
        }
        
        public void GiveOwnership(PlayerID? player, bool silent = false)
        {
            if (!player.HasValue)
            {
                RemoveOwnership();
                return;
            }

            var link = GetComponentInParent<PrefabLink>();
            
            if (!networkManager || (link && link._autoSpawnCalledFrame == Time.frameCount))
            {
                _pendingOwnershipRequest = player;
                return;
            }
            
            GiveOwnershipInternal(player.Value, silent);
        }
        
        private void GiveOwnershipInternal(PlayerID player, bool silent = false)
        {
            if (!networkManager)
            {
                PurrLogger.LogError("Trying to give ownership to " + player + " but identity isn't spawned.", this);
                return;
            }
            
            if (networkManager.TryGetModule(networkManager.isServer, out GlobalOwnershipModule module))
            {
                module.GiveOwnership(this, player, silent: silent);
            }
            else PurrLogger.LogError("Failed to get ownership module.", this);
        }
        
        public void RemoveOwnership()
        {
            if (!networkManager)
                return;
            
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
                
                TriggerDespawnEvent(true);
                TriggerDespawnEvent(false);
                return;
            }
            
            if (ApplicationContext.isQuitting)
                return;

            onRemoved?.Invoke(this);
            
            TriggerDespawnEvent(true);
            TriggerDespawnEvent(false);

            _ticker = null;
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
        
        internal void ResetIgnoreNextActivation()
        {
            _ignoreNextActivation = false;
        }
        
        internal void ResetIgnoreNextEnable()
        {
            _ignoreNextEnable = false;
        }
        
        private int _spawnedCount;
        
        internal void TriggerSpawnEvent(bool asServer)
        {
            InternalOnSpawn(asServer);
            OnSpawned(asServer);

            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnSpawn(asServer);

            if (_spawnedCount == 0)
            {
                while (_onSpawnedQueue != null && _onSpawnedQueue.Count > 0)
                    _onSpawnedQueue.Dequeue().Invoke();

                OnSpawned();

                for (int i = 0; i < _externalModulesView.Count; i++)
                    _externalModulesView[i].OnSpawn();
            }
            
            _spawnedCount++;
        }

        internal void TriggerDespawnEvent(bool asServer)
        {
            if (!IsSpawned(asServer)) return;

            InternalOnDespawn(asServer);
            OnDespawned(asServer);

            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnDespawned(asServer);

            if (asServer)
            {
                _isSpawnedServer = false;
            }
            else
            {
                _isSpawnedClient = false;
            }

            _spawnedCount--;

            if (_spawnedCount == 0)
            {
                OnDespawned();
                
                for (int i = 0; i < _externalModulesView.Count; i++)
                    _externalModulesView[i].OnDespawned();

                _externalModulesView.Clear();
                _modules.Clear();
            }
        }

        internal void TriggerOnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) 
        {
            OnOwnerChanged(oldOwner, newOwner, asServer);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnOwnerChanged(oldOwner, newOwner, asServer);
        }

        internal void TriggerOnOwnerDisconnected(PlayerID ownerId, bool asServer)
        {
            OnOwnerDisconnected(ownerId, asServer);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnOwnerDisconnected(ownerId, asServer);
        }

        internal void TriggerOnOwnerReconnected(PlayerID ownerId, bool asServer)
        {
            OnOwnerConnected(ownerId, asServer);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnOwnerConnected(ownerId, asServer);
        }

        public void TriggerOnObserverAdded(PlayerID target)
        {
            OnObserverAdded(target);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnObserverAdded(target);
        }

        public void TriggerOnObserverRemoved(PlayerID target)
        {
            OnObserverRemoved(target);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnObserverRemoved(target);
        }

        internal void SetPendingOwnershipRequest(PlayerID playersLocalPlayerId)
        {
            _pendingOwnershipRequest = playersLocalPlayerId;
        }

        internal void SetIsSpawned(bool value, bool asServer)
        {
            if (asServer)
                 _isSpawnedServer = value;
            else _isSpawnedClient = value;
        }

        /// <summary>
        /// Sends all the queued actions to the server.
        /// For example an auto-spawn or destroying a networked gameobject.
        /// Without flushing your RPC could be sent before the auto-spawn or destroy.
        /// Same for parent changes, etc.
        /// </summary>
        public void FlushHierarchyActions()
        {
            onFlush?.Invoke(this);
        }
    }
}
