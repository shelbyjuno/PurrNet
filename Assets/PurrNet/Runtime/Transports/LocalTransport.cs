using System.Collections.Generic;

namespace PurrNet.Transports
{
    public class LocalTransport : GenericTransport, ITransport
    {
        public event OnConnected onConnected;
        public event OnDisconnected onDisconnected;
        public event OnDataReceived onDataReceived;
        public event OnDataSent onDataSent;
        public event OnConnectionState onConnectionState;

        public IReadOnlyList<Connection> connections => new [] { new Connection(0) };
        
        public override bool isSupported => true;
        public override ITransport transport => this;

        public ConnectionState listenerState { get; private set; } = ConnectionState.Disconnected;
        
        public ConnectionState clientState { get; private set; } = ConnectionState.Disconnected;

        public void Listen(ushort port)
        {
            listenerState = ConnectionState.Connecting;
            TriggerConnectionStateEvent(true);
            
            listenerState = ConnectionState.Connected;
            TriggerConnectionStateEvent(true);
            
            if (clientState == ConnectionState.Connecting)
            {
                clientState = ConnectionState.Connected;
                TriggerConnectionStateEvent(false);
                
                onConnected?.Invoke(new Connection(0), true);
                onConnected?.Invoke(new Connection(0), false);
            }
        }

        internal override void StartServer()
        {
            Listen(default);
        }
        
        public void RaiseDataReceived(Connection conn, ByteData data, bool asServer)
        {
            onDataReceived?.Invoke(conn, data, asServer);
        }

        public void RaiseDataSent(Connection conn, ByteData data, bool asServer)
        {
            onDataSent?.Invoke(conn, data, asServer);
        }

        public void StopListening()
        {
            listenerState = ConnectionState.Disconnecting;
            TriggerConnectionStateEvent(true);
            
            listenerState = ConnectionState.Disconnected;
            TriggerConnectionStateEvent(true);
            
            if (clientState == ConnectionState.Connected)
                Disconnect();
        }


        public void Connect(string up, ushort port)
        {
            if (clientState == ConnectionState.Connected) 
                return;
            
            clientState = ConnectionState.Connecting;
            TriggerConnectionStateEvent(false);

            if (listenerState == ConnectionState.Connected)
            {
                clientState = ConnectionState.Connected;
                TriggerConnectionStateEvent(false);
                
                onConnected?.Invoke(new Connection(0), true);
                onConnected?.Invoke(new Connection(0), false);
            }
        }

        internal override void StartClient()
        {
            Connect(default, default);
        }

        public void Disconnect()
        {
            switch (clientState)
            {
                case ConnectionState.Connecting:
                    clientState = ConnectionState.Disconnected;
                    TriggerConnectionStateEvent(false);
                    return;
                case ConnectionState.Disconnected:
                    return;
            }

            clientState = ConnectionState.Disconnecting;
            TriggerConnectionStateEvent(false);
            
            clientState = ConnectionState.Disconnected;
            TriggerConnectionStateEvent(false);

            var conn = new Connection(0);

            onDisconnected?.Invoke(conn, true);
            onDisconnected?.Invoke(conn, false);
        }
        
        public void SendToClient(Connection target, ByteData data, Channel method = Channel.Unreliable)
        {
            if (clientState != ConnectionState.Connected ||
                listenerState != ConnectionState.Connected)
                return;
            
            onDataReceived?.Invoke(target, data, false);
            RaiseDataSent(target, data, true);
        }

        public void SendToServer(ByteData data, Channel method = Channel.Unreliable)
        {
            if (clientState != ConnectionState.Connected ||
                listenerState != ConnectionState.Connected)
                return;
            
            var conn = new Connection(0);
            onDataReceived?.Invoke(conn, data, true);
            RaiseDataSent(default, data, false);
        }

        public void CloseConnection(Connection conn)
        {
            StopClient();
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
