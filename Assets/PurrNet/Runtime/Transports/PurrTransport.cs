using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JamesFrowen.SimpleWeb;
using PurrNet.Logging;
using PurrNet.Packing;
using UnityEngine;

namespace PurrNet.Transports
{
    public class PurrTransport : GenericTransport, ITransport
    {
        enum SERVER_PACKET_TYPE : byte
        {
            SERVER_CLIENT_CONNECTED = 0,
            SERVER_CLIENT_DISCONNECTED = 1,
            SERVER_CLIENT_DATA = 2,
            SERVER_AUTHENTICATED = 3,
            SERVER_AUTHENTICATION_FAILED = 4
        }
    
        enum HOST_PACKET_TYPE : byte
        {
            SEND_ALL = 0,
            SEND_ONE = 1,
            SEND_MANY = 2,
            SEND_ALL_EXCEPT = 3,
            SEND_ALL_EXCEPT_MANY = 4
        }
        
        [Serializable]
        private struct ClientAuthenticate
        {
            public string roomName;
            public string clientSecret;
        }
        
        [SerializeField] private string _roomName;
        
        public override bool isSupported => true;
        
        public override ITransport transport => this;
        
        public event OnConnected onConnected;
        public event OnDisconnected onDisconnected;
        public event OnDataReceived onDataReceived;
        public event OnDataSent onDataSent;
        public event OnConnectionState onConnectionState;
        
        private ConnectionState _listenerState = ConnectionState.Disconnected;
        private ConnectionState _clientState = ConnectionState.Disconnected;
        
        public ConnectionState listenerState
        {
            get => _listenerState;
            set
            {
                if (_listenerState == value)
                    return;
                
                _listenerState = value;
                onConnectionState?.Invoke(value, true);
            }
        }

        public ConnectionState clientState
        {
            get => _clientState;
            set
            {
                if (_clientState == value)
                    return;
                
                _clientState = value;
                onConnectionState?.Invoke(value, false);
            }
        }
        
        public IReadOnlyList<Connection> connections => new [] { new Connection(0) };

        private void Reset()
        {
            _roomName = Guid.NewGuid().ToString().Replace("-", "");
        }

        protected override void StartClientInternal()
        {
            
        }

        readonly List<CancellationTokenSource> _cancellationTokenSourcesServer = new ();
        readonly List<CancellationTokenSource> _cancellationTokenSourcesClient = new ();
        
        private void CancelAll(bool asServer)
        {
            var sources = asServer ? _cancellationTokenSourcesServer : _cancellationTokenSourcesClient;
            for (var i = 0; i < sources.Count; i++)
                sources[i].Cancel();
            sources.Clear();
        }
        
        private void AddCancellation(CancellationTokenSource token, bool asServer)
        {
            if (asServer)
                 _cancellationTokenSourcesServer.Add(token);
            else _cancellationTokenSourcesClient.Add(token);
        }

        private SimpleWebClient _server;
        private SimpleWebClient _client;
        private HostJoinInfo _hostJoinInfo;
        readonly TcpConfig _tcpConfig = new (noDelay: false, sendTimeout: 5000, receiveTimeout: 20000);

        protected override async void StartServerInternal()
        {
            try
            {
                if (listenerState != ConnectionState.Disconnected)
                    StopListening();
                
                listenerState = ConnectionState.Connecting;
                
                _server = SimpleWebClient.Create(ushort.MaxValue, 5000, _tcpConfig);
                
                _server.onConnect += OnHostConnected;
                _server.onData += OnHostData;
                _server.onDisconnect += OnHostDisconnected;
                
                Log.level = Log.Levels.verbose;
                
                try
                {
                    var token = new CancellationTokenSource();
                    AddCancellation(token, true);

                    var relayServer = await PurrTransportUtils.GetRelayServerAsync();

                    if (token.IsCancellationRequested)
                        return;

                    _hostJoinInfo = await PurrTransportUtils.AllocWS(relayServer, _roomName);
                    
                    var builder = new UriBuilder
                    {
                        Scheme = _hostJoinInfo.ssl ? "wss" : "ws",
                        Host = relayServer.host,
                        Port = _hostJoinInfo.port,
                        Query = string.Empty,
                        Path = string.Empty
                    };
                    
                    _server.Connect(builder.Uri);
                }
                catch (Exception e)
                {
                    StopListening();
                    PurrLogger.LogWarning(e.Message[(e.Message.IndexOf('\n') + 1)..]);
                }
            }
            catch (Exception e)
            {
                StopListening();
                PurrLogger.LogException(e.Message);
            }
        }

        private void OnHostData(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count == 0)
                return;
            
            var type = (SERVER_PACKET_TYPE)data.Array[data.Offset];

            switch (type)
            {
                case SERVER_PACKET_TYPE.SERVER_AUTHENTICATED:
                    listenerState = ConnectionState.Connected;
                    break;
                case SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED:
                    PurrLogger.LogError("Authentication to relay failed");
                    StopListening();
                    break;
                default:
                    PurrLogger.Log(type.ToString());
                    break;
            }
        }

        private void OnHostConnected()
        {
            ClientAuthenticate authenticate = new ()
            {
                roomName = _roomName,
                clientSecret = _hostJoinInfo.secret
            };
            
            string json = JsonUtility.ToJson(authenticate);
            var data = Encoding.UTF8.GetBytes(json);
            
            _server.Send(data);
        }

        private void OnHostDisconnected()
        {
            StopListening();
        }

        public void Listen(ushort port)
        {
            throw new System.NotImplementedException();
        }

        public void StopListening()
        {
            CancelAll(true);

            if (_server != null)
            {
                _server.onConnect -= OnHostConnected;
                _server.onData -= OnHostData;
                _server.onDisconnect -= OnHostDisconnected;
                _server.Disconnect();
            }

            _server = null;
            
            if (listenerState is ConnectionState.Connecting or ConnectionState.Connected)
                listenerState = ConnectionState.Disconnecting;
            listenerState = ConnectionState.Disconnected;
        }

        public void Connect(string ip, ushort port)
        {
            throw new System.NotImplementedException();
        }

        public void Disconnect()
        {
            CancelAll(false);
        }
        
        public void RaiseDataReceived(Connection conn, ByteData data, bool asServer)
        {
            throw new System.NotImplementedException();
        }

        public void RaiseDataSent(Connection conn, ByteData data, bool asServer)
        {
            throw new System.NotImplementedException();
        }

        public void SendToClient(Connection target, ByteData data, Channel method = Channel.ReliableOrdered)
        {
            throw new System.NotImplementedException();
        }

        public void SendToServer(ByteData data, Channel method = Channel.ReliableOrdered)
        {
            throw new System.NotImplementedException();
        }

        public void CloseConnection(Connection conn)
        {
            throw new System.NotImplementedException();
        }

        public void UpdateEvents(float delta)
        {
            _server?.ProcessMessageQueue();
        }
    }
}