using UnityEngine;

namespace Rabsi.Transports
{
    public class TestTransport : MonoBehaviour
    {
        [SerializeField] GenericTransport _generic;
        
        ITransport _transport;

        private void Awake()
        {
            _transport = _generic.transport;
            
            if (!Application.runInBackground)
                Application.runInBackground = true;
        }
        
        [ContextMenu("Start Server")]
        public void StartServer()
        {
            _generic.Listen();
        }
        
        [ContextMenu("Stop Server")]
        public void StopServer()
        {
            _generic.transport.StopListening();
        }
        
        [ContextMenu("Connect Client")]
        public void Connect()
        {
            _generic.Connect();
        }
        
        [ContextMenu("Disconnect Client")]
        public void Disconnect()
        {
            _generic.transport.Disconnect();
        }

        void OnTransportOnonConnectionState(ConnectionState conn, bool asServer)
        {
            Debug.Log($"State is {conn} for {(asServer ? "server" : "client")}");
        }

        void OnTransportOnonDisconnected(Connection conn, bool asServer)
        {
            Debug.Log($"Disconnected {conn} on {(asServer ? "server" : "client")}");
        }

        void OnTransportOnonConnected(Connection conn, bool asServer)
        {
            Debug.Log($"Connected {conn} on {(asServer ? "server" : "client")}");
        }
        
        private void OnEnable()
        {
            _transport.onDataReceived += OnData;
            _transport.onConnected += OnTransportOnonConnected;
            _transport.onDisconnected += OnTransportOnonDisconnected;
            _transport.onConnectionState += OnTransportOnonConnectionState;
        }
        
        private void OnDisable()
        {
            _transport.onDataReceived -= OnData;
            _transport.onConnected -= OnTransportOnonConnected;
            _transport.onDisconnected -= OnTransportOnonDisconnected;
            _transport.onConnectionState -= OnTransportOnonConnectionState;
        }

        private static void OnData(Connection conn, ByteData data, bool asserver)
        {
            Debug.Log($"Received data as {(asserver ? "server" : "client")}: {data.length}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                _transport.SendToServer(new ByteData(data));
            }
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x5 };
                for (var i = 0; i < _transport.connections.Count; i++)
                    _transport.SendToClient(_transport.connections[i], new ByteData(data));
            }
        }
    }
}
