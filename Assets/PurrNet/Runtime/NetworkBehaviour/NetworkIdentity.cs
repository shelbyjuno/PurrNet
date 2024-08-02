using System;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    [DefaultExecutionOrder(-1000)]
    public partial class NetworkIdentity : MonoBehaviour
    {
        /// <summary>
        /// The prefab id of this object;
        /// Is -1 if scene object.
        /// </summary>
        public int prefabId { get; private set; } = -1;
        
        /// <summary>
        /// Network id of this object.
        /// </summary>
        public NetworkID? id { get; private set; }
        
        /// <summary>
        /// Scene id of this object.
        /// </summary>
        public SceneID sceneId { get; private set; }
        
        /// <summary>
        /// Is spawned over the network.
        /// </summary>
        public bool isSpawned => id.HasValue;

        public bool isServer => isSpawned && networkManager.isServer;
        
        public bool isClient => isSpawned && networkManager.isClient;
        
        public bool isHost => isSpawned && networkManager.isHost;
        
        public bool isOwner => owner == localPlayer;
        
        public bool hasOwner => owner.HasValue;

        internal PlayerID? internalOwnerServer;
        internal PlayerID? internalOwnerClient;
        
        /// <summary>
        /// Returns the owner of this object.
        /// It will return the owner of the closest parent object.
        /// If you can, cache this value for performance.
        /// </summary>
        public PlayerID? owner => internalOwnerServer ?? internalOwnerClient;
        
        public NetworkManager networkManager { get; private set; }
        
        public PlayerID localPlayer => isSpawned && networkManager.TryGetModule<PlayersManager>(false, out var module) && module.localPlayerId.HasValue 
            ? module.localPlayerId.Value : default;
        
        internal event Action<NetworkIdentity> onRemoved;
        internal event Action<NetworkIdentity, bool> onEnabledChanged;
        internal event Action<NetworkIdentity, bool> onActivatedChanged;
        
        private bool _lastEnabledState;
        private GameObjectEvents _events;
        private GameObject _gameObject;

        protected virtual void OnSpawned(bool asServer) { }
        
        protected virtual void OnDespawned(bool asServer) { }
        
        private bool IsNotOwnerPredicate(PlayerID player)
        {
            return player != owner;
        }
        
        [UsedByIL]
        protected void SendRPC(RPCPacket packet, RPCSignature signature)
        {
            if (!isSpawned)
            {
                PurrLogger.LogError($"Trying to send RPC from '{name}' which is not spawned.", this);
                return;
            }

            if (!networkManager.TryGetModule<RPCModule>(networkManager.isServer, out var module))
            {
                PurrLogger.LogError("Failed to get RPC module.", this);
                return;
            }
            
            if (signature.requireOwnership && !isOwner)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{name}' without ownership.", this);
                return;
            }
            
            if (signature.requireServer && !networkManager.isServer)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{name}' without server.", this);
                return;
            }
            
            module.AppendToBufferedRPCs(packet, signature);

            Func<PlayerID, bool> predicate = null;
            
            if (signature.excludeOwner)
                predicate = IsNotOwnerPredicate;

            switch (signature.type)
            {
                case RPCType.ServerRPC: SendToServer(packet, signature.channel); break;
                case RPCType.ObserversRPC: SendToObservers(packet, predicate, signature.channel); break;
                case RPCType.TargetRPC: SendToTarget(signature.targetPlayer!.Value, packet, signature.channel); break;
            }
        }
        
        [UsedByIL]
        protected bool ValidateReceivingRPC(RPCInfo info, RPCSignature signature, bool asServer)
        {
            if (signature.requireOwnership && info.sender != owner)
            {
                PurrLogger.LogError($"Sender '{info.sender}' of RPC '{signature.rpcName}' from '{name}' is not the owner. Aborting RPC call.", this);
                return false;
            }

            if (signature.type == RPCType.ServerRPC && !asServer)
            {
                PurrLogger.LogError($"Trying to receive server RPC '{signature.rpcName}' from '{name}' on client. Aborting RPC call.", this);
                return false;
            }

            return true;
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

        public virtual void OnDisable()
        {
            UpdateEnabledState();
        }
        
        internal void SetIdentity(NetworkManager manager, SceneID scene, int pid, NetworkID identityId, bool asServer)
        {
            networkManager = manager;

            Hasher.PrepareType(GetType());

            sceneId = scene;
            prefabId = pid;
            id = identityId;

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
        }

        private bool _ignoreNextDestroy;
        
        public void IgnoreNextDestroyCallback()
        {
            _ignoreNextDestroy = true;
        }
        
        public void GiveOwnership(PlayerID player)
        {
            if (!networkManager)
            {
                PurrLogger.LogError($"Trying to give ownership to '{name}' which is not spawned.", this);
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

            if (isSpawned)
            {
                if (networkManager.isHost)
                {
                    OnDespawned(true);
                    OnDespawned(false);
                }
                else
                {
                    OnDespawned(networkManager.isServer);
                }
            }
            
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
        
        internal void TriggetSpawnEvent(bool asServer)
        {
            OnSpawned(asServer);
        }

        internal void TriggetClientSpawnEvent()
        {
            TriggetSpawnEvent(false);
        }
        
        internal void TriggetClientDespawnEvent()
        {
            OnDespawned(false);
        }
    }
}
