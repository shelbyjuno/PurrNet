using System.Collections.Generic;
using System.Linq;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    [RequireComponent(typeof(NetworkManager))]
    public class StatisticsManager : MonoBehaviour
    {
        [Range(0.05f, 1f)] public float checkInterval = 0.33f;
        
        public int ping { get; private set; }
        public int jitter { get; private set; }
        public int packetLoss { get; private set; }
        public float upload { get; private set; }
        public float download { get; private set; }
        
        private NetworkManager _networkManager;
        private PlayersBroadcaster _playersClientBroadcaster;
        private PlayersBroadcaster _playersServerBroadcaster;
        private TickManager _tickManager;
        
        public bool connectedServer { get; private set; }
        
        public bool connectedClient { get; private set; }
        
        // Ping stuff
        private readonly Queue<float> _pingHistory = new();
        private readonly Queue<int> _pingStats = new();
        private uint _lastPingSendTick;
        
        // Packet loss stuff
        private int _packetsToSendPerSec = 10;
        private readonly List<float> _receivedPacketTimes = new();
        private uint _lastPacketSendTick;
        
        // Download stuff
        private float _totalDataReceived;
        private float _totalDataSent;
        private float _lastDataCheckTime;
        
        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
                return;
            
            _networkManager.onServerConnectionState += OnServerConnectionState;
            _networkManager.onClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            if (_networkManager)
            {
                _networkManager.transport.transport.onDataReceived -= OnDataReceived;
                _networkManager.transport.transport.onDataSent -= OnDataSent;
            }
            
            if (_playersServerBroadcaster != null)
            {
                _playersServerBroadcaster.Unsubscribe<PingMessage>(ReceivePing);
                _playersServerBroadcaster.Unsubscribe<PacketMessage>(ReceivePacket);
            }
            
            if (_playersClientBroadcaster != null)
            {
                if (_networkManager.TryGetModule(out TickManager tm, false))
                    tm.onTick -= OnClientTick;
                
                _playersClientBroadcaster.Unsubscribe<PingMessage>(ReceivePing);
                _playersClientBroadcaster.Unsubscribe<PacketMessage>(ReceivePacket);
            }
        }

        private void Update()
        {
            if (Time.time - _lastDataCheckTime >= 1f)
            {
                download = Mathf.Round((_totalDataReceived / 1024f) * 1000f) / 1000f;
                upload = Mathf.Round((_totalDataSent / 1024f) * 1000f) / 1000f;
                _totalDataReceived = 0;
                _totalDataSent = 0;
                _lastDataCheckTime = Time.time;
            }
        }

        private void OnServerConnectionState(ConnectionState state)
        {
            connectedServer = state == ConnectionState.Connected;
            
            if (state != ConnectionState.Connected)
                return;

            _playersServerBroadcaster = _networkManager.GetModule<PlayersBroadcaster>(true);
            _playersServerBroadcaster.Subscribe<PingMessage>(ReceivePing);
            _playersServerBroadcaster.Subscribe<PacketMessage>(ReceivePacket);
            _networkManager.transport.transport.onDataReceived += OnDataReceived;
            _networkManager.transport.transport.onDataSent += OnDataSent;
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            connectedClient = state == ConnectionState.Connected;
            
            if (state != ConnectionState.Connected)
                return;
            
            _tickManager = _networkManager.GetModule<TickManager>(false);
            _playersClientBroadcaster = _networkManager.GetModule<PlayersBroadcaster>(false);
            _playersClientBroadcaster.Subscribe<PingMessage>(ReceivePing);
            _playersClientBroadcaster.Subscribe<PacketMessage>(ReceivePacket);
            _tickManager.onTick += OnClientTick;

            if (!connectedServer)
            {
                _networkManager.transport.transport.onDataReceived += OnDataReceived;
                _networkManager.transport.transport.onDataSent += OnDataSent;
            }
            
            if(_tickManager.tickRate < _packetsToSendPerSec)
                _packetsToSendPerSec = _tickManager.tickRate; 
        }

        private void OnClientTick()
        {
            if (!gameObject.activeInHierarchy)
                return;
            
            HandlePingCheck();
            HandlePacketCheck();
        }

        private void HandlePingCheck()
        {
            if (_lastPingSendTick + _tickManager.TimeToTick(checkInterval) > _tickManager.tick)
                return;
            
            _pingHistory.Enqueue(Time.time);
            SendPingCheck();
        }

        private void SendPingCheck()
        {
            _playersClientBroadcaster.SendToServer(new PingMessage(), Channel.ReliableUnordered);
            _lastPingSendTick = _tickManager.tick;
        }

        private void ReceivePing(PlayerID sender, PingMessage msg, bool asServer)
        {
            if (asServer)
            {
                _playersServerBroadcaster.Send(sender, new PingMessage(), Channel.ReliableUnordered);
                return;
            }

            if(_tickManager.TickToTime((uint)_pingStats.Count) > 0.33f) //0.33f is the time for which we take the average
                _pingStats.Dequeue();
            _pingStats.Enqueue(Mathf.Max(0, Mathf.FloorToInt((Time.time - _pingHistory.Dequeue()) * 1000) - 1000/_tickManager.tickRate * 2));
            
            var oldPing = ping;
            ping = (int)_pingStats.Average();
            jitter = Mathf.Abs(ping - oldPing);
        }

        private void HandlePacketCheck()
        {
            if (_receivedPacketTimes.Count > 0 && _receivedPacketTimes[0] < Time.time - 1)
                _receivedPacketTimes.RemoveAt(0);

            if (_lastPacketSendTick + _tickManager.TimeToTick(1f / _packetsToSendPerSec) > _tickManager.tick)
                return;
            
            _lastPacketSendTick = _tickManager.tick;
            _playersClientBroadcaster.SendToServer(new PacketMessage(), Channel.Unreliable);
        }

        private void ReceivePacket(PlayerID sender, PacketMessage msg, bool asServer)
        {
            if (asServer)
            {
                _playersServerBroadcaster.Send(sender, new PacketMessage(), Channel.Unreliable);
                return;
            }
            
            packetLoss = 100 - Mathf.FloorToInt((_receivedPacketTimes.Count / (float)_packetsToSendPerSec) * 100);
            if (_tickManager.tick < 10 * _tickManager.tickRate || packetLoss < 0)
                packetLoss = 0;
            
            _receivedPacketTimes.Add(Time.time);
        }

        private void OnDataReceived(Connection conn, ByteData data, bool asServer)
        {
            _totalDataReceived += data.length;
        }

        private void OnDataSent(Connection conn, ByteData data, bool asServer)
        {
            _totalDataSent += data.length;
        }

        public struct PingMessage : Packing.IPackedAuto { }
 
        public struct PacketMessage : Packing.IPackedAuto { }
    }
}
