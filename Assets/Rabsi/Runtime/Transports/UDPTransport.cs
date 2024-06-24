using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

namespace Rabsi.Transports
{
    public class UDPTransport : GenericTransport, ITransport, INetLogger
    {
        [Header("Server Settings")]
        [SerializeField] private ushort _serverPort = 5000;
        [SerializeField] private int _maxConnections = 100;

        [Header("Client Settings")]
        [SerializeField] private string _address = "127.0.0.1";
        
        public event OnConnected onConnected;
        public event OnDisconnected onDisconnected;
        public event OnDataReceived onDataReceived;
        public event OnConnectionState onConnectionState;

        public string address { get => _address; set => _address = value; }
        
        public ushort serverPort { get => _serverPort; set => _serverPort = value; }
        
        public int maxConnections { get => _maxConnections; set => _maxConnections = value; }
        
        public IReadOnlyList<Connection> connections => _connections;

        private EventBasedNetListener _clientListener;
        private EventBasedNetListener _serverListener;
        
        private NetManager _client;
        private NetManager _server;

        public ConnectionState clientState { get; private set; } = ConnectionState.Disconnected;
        
        public ConnectionState listenerState { get; private set; } = ConnectionState.Disconnected;

        readonly List<Connection> _connections = new ();

        public override bool isSupported => Application.platform != RuntimePlatform.WebGLPlayer;
        
        public override ITransport transport => this;

        private void Awake()
        {
            NetDebug.Logger = this;
        }

        private void OnEnable()
        {
            _clientListener = new EventBasedNetListener();
            _serverListener = new EventBasedNetListener();
            
            _client = new NetManager(_clientListener)
            {
                UnconnectedMessagesEnabled = true
            };
            
            _server = new NetManager(_serverListener)
            {
                UnconnectedMessagesEnabled = true
            };

            _client.Start();

            _clientListener.PeerConnectedEvent += OnClientConnected;
            _clientListener.PeerDisconnectedEvent += OnClientDisconnected;
            _clientListener.NetworkReceiveEvent += OnClientData;
            
            _serverListener.ConnectionRequestEvent += OnServerConnectionRequest;
            _serverListener.PeerConnectedEvent += OnServerConnected;
            _serverListener.PeerDisconnectedEvent += OnServerDisconnected;
            _serverListener.NetworkReceiveEvent += OnServerData;
        }

        private void OnServerData(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
        {
            var data = new ByteData(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
            onDataReceived?.Invoke(new Connection(peer.Id), data, true);
            reader.Recycle();
        }

        private void OnClientData(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
        {
            var data = new ByteData(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
            onDataReceived?.Invoke(new Connection(peer.Id), data, false);
            reader.Recycle();
        }

        private void OnServerConnectionRequest(ConnectionRequest request)
        {
            if (_server.ConnectedPeersCount < _maxConnections)
                 request.AcceptIfKey("Rabsi");
            else request.Reject();
        }

        public override void Listen()
        {
            Listen(_serverPort);
        }

        public override void Connect()
        {
            Connect(_address, _serverPort);
        }

        private void OnClientDisconnected(NetPeer peer, DisconnectInfo disconnectinfo)
        {
            if (clientState is not ConnectionState.Connected)
                return;
            
            clientState = ConnectionState.Disconnected;
            TriggerConnectionStateEvent(false);
            onDisconnected?.Invoke(new Connection(peer.Id), false);
        }

        private void OnClientConnected(NetPeer peer)
        {
            var conn = new Connection(peer.Id);
            clientState = ConnectionState.Connected;
            TriggerConnectionStateEvent(false);
            onConnected?.Invoke(conn, false);
        }
        
        private void OnServerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo)
        {
            var conn = new Connection(peer.Id);
            
            for (int i = 0; i < _connections.Count; i++)
            {
                if (_connections[i] == conn)
                {
                    _connections.RemoveAt(i);
                    break;
                }
            }
            
            onDisconnected?.Invoke(conn, true);
        }

        private void OnServerConnected(NetPeer peer)
        {
            var conn = new Connection(peer.Id);
            _connections.Add(conn);
            onConnected?.Invoke(conn, true);
        }

        private void FixedUpdate()
        {
            _server.PollEvents();
            _client.PollEvents();
        }

        public void Connect(string ip, ushort port)
        {
            if (clientState == ConnectionState.Connected)
                return;
            
            clientState = ConnectionState.Connecting;
            TriggerConnectionStateEvent(false);
            _client.Connect(ip, port, "Rabsi");
            TriggerConnectionStateEvent(false);
        }

        public void Disconnect()
        {
            if (clientState is not (ConnectionState.Connected or ConnectionState.Connecting))
                return;
            
            clientState = ConnectionState.Disconnecting;
            TriggerConnectionStateEvent(false);

            _client.DisconnectAll();
            
            clientState = ConnectionState.Disconnected;
            TriggerConnectionStateEvent(false);
        }

        public void Listen(ushort port)
        {
            NetDebug.Logger = this;

            if (listenerState is ConnectionState.Disconnected or ConnectionState.Disconnecting)
            {
                listenerState = ConnectionState.Connecting;
                TriggerConnectionStateEvent(true);

                _server.Start(port);
                
                listenerState = ConnectionState.Connected;
                TriggerConnectionStateEvent(true);
            }
        }

        public void StopListening()
        {
            if (listenerState is ConnectionState.Connected or ConnectionState.Connecting)
            {
                listenerState = ConnectionState.Disconnecting;
                TriggerConnectionStateEvent(true);
                
                _server.Stop();
                
                listenerState = ConnectionState.Disconnected;
                TriggerConnectionStateEvent(true);
                
                _connections.Clear();
            }
        }

        public void SendToClient(Connection target, ByteData data, Channel method = Channel.Unreliable)
        {
            if (listenerState is not ConnectionState.Connected)
                return;
            
            if (!target.isValid)
                return;
            
            var deliveryMethod = (DeliveryMethod)(byte)method;
            var peer = _server.GetPeerById(target.connectionId);
            
            peer?.Send(data.data, data.offset, data.length, deliveryMethod);
        }
        
        public void SendToServer(ByteData data, Channel method = Channel.Unreliable)
        {
            if (clientState != ConnectionState.Connected)
                return;
            
            var deliveryMethod = (DeliveryMethod)(byte)method;
            _client.SendToAll(data.data, data.offset, data.length, deliveryMethod);
        }

        private void OnDisable()
        {
            _client.Stop();
            _server.Stop();

            TriggerConnectionStateEvent(true);
            TriggerConnectionStateEvent(false);

            _connections.Clear();
        }

        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            switch (level)
            {
                case NetLogLevel.Trace:
                    Debug.LogFormat(str, args);
                    break;
                case NetLogLevel.Info:
                    Debug.LogFormat(str, args);
                    break;
                case NetLogLevel.Warning:
                    Debug.LogWarningFormat(str, args);
                    break;
                case NetLogLevel.Error:
                    Debug.LogErrorFormat(str, args);
                    break;
            }
        }
        
        ConnectionState _prevClientState = ConnectionState.Disconnected;
        ConnectionState _prevServerState = ConnectionState.Disconnected;
        
        private void TriggerConnectionStateEvent(bool asServer)
        {
            if (asServer)
            {
                if (_prevServerState != listenerState)
                {
                    onConnectionState?.Invoke(listenerState, true);
                    _prevServerState = listenerState;
                }
            }
            else
            {
                if (_prevClientState != clientState)
                {
                    onConnectionState?.Invoke(clientState, false);
                    _prevClientState = clientState;
                }
            }
        }
    }
}
