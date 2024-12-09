using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    public delegate void RegisterEventsDelegate(NetworkManager manager, bool asServer);

    public interface IPurrEvents
    {
        void Subscribe(NetworkManager manager, bool asServer);
        
        void Unsubscribe(NetworkManager manager, bool asServer);
    }
    
    public delegate void OnSubscribeDelegate(NetworkManager manager, bool asServer);
    public delegate void OnSubscribeSimpleDelegate(NetworkManager manager);

    public sealed partial class NetworkManager
    {
        readonly List<RegisterEventsDelegate> _subscribeEvents = new ();
        readonly List<RegisterEventsDelegate> _unsubscribeEvents = new ();
        
        /// <summary>
        /// Event called when the network is started.
        /// This should be used to subscribe to network events.
        /// This is called twice, once for the server and once for the client.
        /// </summary>
        public event OnSubscribeDelegate onNetworkStarted;
        
        /// <summary>
        /// Event called when the network is shutdown.
        /// This should be used to unsubscribe from network events.
        /// This is called twice, once for the server and once for the client.
        /// </summary>
        public event OnSubscribeDelegate onNetworkShutdown;
        
        /// <summary>
        /// Event called when the network is started.
        /// This should be used to subscribe to network events.
        /// This is called once even if the network is started as both server and client.
        /// </summary>
        public event OnSubscribeSimpleDelegate onNetworkStartedSimple;
        
        /// <summary>
        /// Event called when the network is shutdown.
        /// This should be used to unsubscribe from network events.
        /// This is called once even if the network is started as both server and client.
        /// </summary>
        public event OnSubscribeSimpleDelegate onNetworkShutdownSimple;
        
        bool _isSubscribed;
        
        private void TriggerSubscribeEvents(bool asServer)
        {
            if (!_isSubscribed)
            {
                _isSubscribed = true;
                onNetworkStartedSimple?.Invoke(this);
            }
            
            onNetworkStarted?.Invoke(this, asServer);
            
            for (var i = 0; i < _subscribeEvents.Count; i++)
            {
                try
                {
                    _subscribeEvents[i](this, asServer);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
        
        private void TriggerUnsubscribeEvents(bool asServer)
        {
            if (_isSubscribed)
            {
                _isSubscribed = false;
                onNetworkShutdownSimple?.Invoke(this);
            }
            
            onNetworkShutdown?.Invoke(this, asServer);
            
            for (var i = 0; i < _unsubscribeEvents.Count; i++)
            {
                try
                {
                    _unsubscribeEvents[i](this, asServer);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
        
        /// <summary>
        /// Register events to be called when the manager is initialized.
        /// This ensures the modules are present and ready to be used.
        /// If the manager is already initialized the events will be called immediately.
        /// </summary>
        /// <param name="subscribe">Subscribe callback</param>
        /// <param name="unsubscribe">Unsubscribe callback</param>
        public void RegisterEvents(RegisterEventsDelegate subscribe, RegisterEventsDelegate unsubscribe)
        {
            if (subscribe != null)
            {           
                bool serverConnected = serverState == ConnectionState.Connected;
                bool clientConnected = clientState == ConnectionState.Connected;

                _subscribeEvents.Add(subscribe);
                
                if (serverConnected)
                    subscribe(this, true);
                
                if (clientConnected)
                    subscribe(this, false);
            }
            
            if (unsubscribe != null) 
                _unsubscribeEvents.Add(unsubscribe);
        }
        
        /// <summary>
        /// Unregister events from the manager.
        /// </summary>
        /// <param name="subscribe">Subscribe callback</param>
        /// <param name="unsubscribe">Unsubscribe callback</param>
        public void UnregisterEvents(RegisterEventsDelegate subscribe, RegisterEventsDelegate unsubscribe)
        {
            if (subscribe != null) 
                _subscribeEvents.Remove(subscribe);

            if (unsubscribe != null)
                _unsubscribeEvents.Remove(unsubscribe);
        }
        
        /// <summary>
        /// Register events to be called when the manager is initialized.
        /// This ensures the modules are present and ready to be used.
        /// If the manager is already initialized the events will be called immediately.
        /// </summary>
        /// <param name="events">Events to register</param>
        public void RegisterEvents(IPurrEvents events)
        {
            RegisterEvents(events.Subscribe, events.Unsubscribe);
        }
        
        /// <summary>
        /// Unregister events from the manager.
        /// </summary>
        /// <param name="events">Events to unregister</param>
        public void UnregisterEvents(IPurrEvents events)
        {
            UnregisterEvents(events.Subscribe, events.Unsubscribe);
        }
        
        [UsedImplicitly]
        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback, bool asServer)  where T : new()
        {
            var broadcaster = GetModule<PlayersBroadcaster>(asServer);
            broadcaster.Subscribe(callback);
        }
        
        [UsedImplicitly]
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback, bool asServer) where T : new()
        {
            var broadcaster = GetModule<PlayersBroadcaster>(asServer);
            broadcaster.Unsubscribe(callback);
        }

        [UsedImplicitly]
        public void Send<T>(PlayerID player, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.Send(player, data, method);
        }

        [UsedImplicitly]
        public void Send<T>(IEnumerable<PlayerID> playersCollection, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.Send(playersCollection, data, method);
        }
        
        [UsedImplicitly]
        public void SendToScene<T>(SceneID sceneId, T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            var scenePlayers = GetModule<ScenePlayersModule>(true);
            
            if (scenePlayers.TryGetPlayersInScene(sceneId, out var playersInScene))
                broadcaster.Send(playersInScene, data, method);
        }

        [UsedImplicitly]
        public void SendToServer<T>(T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(false);
            broadcaster.SendToServer(data, method);
        }

        [UsedImplicitly]
        public void SendToAll<T>(T data, Channel method = Channel.ReliableOrdered)
        {
            var broadcaster = GetModule<PlayersBroadcaster>(true);
            broadcaster.SendToAll(data, method);
        }
    }
}
