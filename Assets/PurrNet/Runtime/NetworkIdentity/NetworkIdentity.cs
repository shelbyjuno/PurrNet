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
        
        public NetworkID? GetNetworkID(bool asServer) => asServer ? idServer : idClient;
        
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
        
        public bool isOwner => isSpawned && localPlayer.HasValue && owner == localPlayer;
        
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
            
            root = lastKnown;
            return lastKnown;
        }

        private IServerSceneEvents _serverSceneEvents;
        private int onTickCount;
        private ITick _ticker;
        
        private readonly List<ITick> _tickables = new ();
        
        [ContextMenu("PurrNet/TakeOwnership")]
        private void TakeOwnership()
        {
            GiveOwnership(localPlayer);
        }
        
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
        
        void OnServerJoinedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (scene == sceneId)
                _serverSceneEvents?.OnPlayerJoinedScene(player);
        }
        
        void OnServerLeftScene(PlayerID player, SceneID scene, bool asServer)
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

        /// <summary>
        /// Called when this object is spawned
        /// This is only called once even if in host mode.
        /// </summary>
        protected virtual void OnSpawned() { }
        
        /// <summary>
        /// Called when this object is de-spawned.
        /// This is only called once even if in host mode.
        /// </summary>
        protected virtual void OnDespawned() { }
        
        /// <summary>
        /// Called when this object is spawned.
        /// This might be called twice times in host mode.
        /// Once for the server and once for the client.
        /// </summary>
        /// <param name="asServer">Is this on the server</param>
        protected virtual void OnSpawned(bool asServer) { }
        
        /// <summary>
        /// Called before the NetworkModules are initialized.
        /// You can use this to update their values before they are networked.
        /// </summary>
        protected virtual void OnInitializeModules() { }
        
        /// <summary>
        /// Called when this object is de-spawned.
        /// This might be called twice times in host mode.
        /// Once for the server and once for the client.
        /// </summary>
        /// <param name="asServer">Is this on the server</param>
        protected virtual void OnDespawned(bool asServer) { }

        /// <summary>
        /// Called when the owner of this object changes.
        /// </summary>
        /// <param name="oldOwner">The old owner of this object</param>
        /// <param name="newOwner">The new owner of this object</param>
        /// <param name="asServer">Is this on the server</param>
        protected virtual void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) { }

        /// <summary>
        /// Called when the owner of this object disconnects.
        /// Server only.
        /// </summary>
        /// <param name="ownerId">The current owner id</param>
        protected virtual void OnOwnerDisconnected(PlayerID ownerId) { }

        /// <summary>
        /// Called when the owner of this object reconnects.
        /// Server only.
        /// </summary>
        /// <param name="ownerId">The current owner id</param>
        protected virtual void OnOwnerReconnected(PlayerID ownerId) { }
        
        /// <summary>
        /// Called when an observer is added.
        /// Server only.
        /// </summary>
        /// <param name="player">The observer player id</param>
        protected virtual void OnObserverAdded(PlayerID player) { }
        
        /// <summary>
        /// Called when an observer is removed.
        /// Server only.
        /// </summary>
        /// <param name="player">The observer player id</param>
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
                var targetFirst = GetComponent<NetworkIdentity>();
                targetFirst.SetPendingOwnershipRequest(player);
                return;
            }
            
            ClearPendingRequest();
            GiveOwnershipInternal(player, silent);
        }

        private void ClearPendingRequest()
        {
            var targetFirst = GetComponent<NetworkIdentity>();
            targetFirst._pendingOwnershipRequest = null;
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
            
            ClearPendingRequest();
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
        
        public bool hasOwnerPended => _pendingOwnershipRequest.HasValue;

        internal void TriggerSpawnEvent(bool asServer)
        {
            InternalOnSpawn(asServer);
            OnSpawned(asServer);

            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnSpawn(asServer);

            if (_spawnedCount == 0)
            {
                while (_onSpawnedQueue is { Count: > 0 })
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
            
            _spawnedCount--;

            if (_spawnedCount == 0)
            {
                OnDespawned();
                
                for (int i = 0; i < _externalModulesView.Count; i++)
                    _externalModulesView[i].OnDespawned();
            }

            OnDespawned(asServer);
            

            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnDespawned(asServer);

            if (asServer)
                 _isSpawnedServer = false;
            else _isSpawnedClient = false;

            if (_spawnedCount == 0)
            {
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

        internal void TriggerOnOwnerDisconnected(PlayerID ownerId)
        {
            OnOwnerDisconnected(ownerId);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnOwnerDisconnected(ownerId);
        }

        internal void TriggerOnOwnerReconnected(PlayerID ownerId, bool asServer)
        {
            OnOwnerReconnected(ownerId);
            
            for (int i = 0; i < _externalModulesView.Count; i++)
                _externalModulesView[i].OnOwnerReconnected(ownerId);
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

        public void SetLocalOwner(bool asServer, PlayerID? actionOwner)
        {
            if (asServer)
                internalOwnerServer = actionOwner;
            else internalOwnerClient = actionOwner;
        }
    }
}
