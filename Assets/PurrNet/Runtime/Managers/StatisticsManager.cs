using System;
using System.Collections.Generic;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    [RequireComponent(typeof(NetworkManager))]
    public class StatisticsManager : MonoBehaviour
    {
        [Range(0.05f, 1f)] public float checkInterval = 0.33f;
        
        public int Ping { get; private set; }
        public int Jitter { get; private set; }
        public int PacketLoss { get; private set; }
        public float Upload { get; private set; }
        public float Download { get; private set; }
        
        private NetworkManager _networkManager;
        private PlayersBroadcaster _playersClientBroadcaster;
        private PlayersBroadcaster _playersServerBroadcaster;
        private TickManager _tickManager;
        public bool ConnectedServer { get; private set; }
        public bool ConnectedClient { get; private set; }
        
        // Ping stuff
        private Queue<float> _pingHistory = new();
        private uint _lastPingSendTick;
        
        // Packet loss stuff
        private int _packetsToSendPerSec = 10;
        private List<float> _receivedPacketTimes = new();
        private uint _lastPacketSendTick;
        
        // Download stuff
        private float _totalDataReceived;
        private float _lastDataCheckTime;
        
        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
                return;
            
            _networkManager.onServerConnectionState += OnServerConnectionState;
            _networkManager.onClientConnectionState += OnClientConnectionState;
        }

        private void Update()
        {
            if (Time.time - _lastDataCheckTime >= 1f)
            {
                Download = Mathf.Round((_totalDataReceived / 1024f) * 1000f) / 1000f;
                _totalDataReceived = 0;
                _lastDataCheckTime = Time.time;
            }
        }

        private void OnServerConnectionState(ConnectionState state)
        {
            ConnectedServer = state == ConnectionState.Connected;
            
            if (state != ConnectionState.Connected)
                return;

            _playersServerBroadcaster = _networkManager.GetModule<PlayersBroadcaster>(true);
            _playersServerBroadcaster.Subscribe<PingMessage>(ReceivePing);
            _playersServerBroadcaster.Subscribe<PacketMessage>(ReceivePacket);
            _networkManager.GetModule<TickManager>(true).OnTick += OnServerTick;
            _networkManager.transport.transport.onDataReceived += OnDataReceived;
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            ConnectedClient = state == ConnectionState.Connected;
            
            if (state != ConnectionState.Connected)
                return;
            
            _tickManager = _networkManager.GetModule<TickManager>(false);
            _playersClientBroadcaster = _networkManager.GetModule<PlayersBroadcaster>(false);
            _playersClientBroadcaster.Subscribe<PingMessage>(ReceivePing);
            _playersClientBroadcaster.Subscribe<PacketMessage>(ReceivePacket);
            _networkManager.GetModule<TickManager>(true).OnTick += OnClientTick;
            
            if(!ConnectedServer)
                _networkManager.transport.transport.onDataReceived += OnDataReceived;
            
            if(_tickManager.TickRate < _packetsToSendPerSec)
                _packetsToSendPerSec = (int)_tickManager.TickRate;
        }

        private void OnServerTick()
        {
            
        }
        
        private void OnClientTick()
        {
            HandlePingCheck();
            HandlePacketCheck();
        }

        private void HandlePingCheck()
        {
            if (_lastPingSendTick + _tickManager.TimeToTick(checkInterval) > _tickManager.Tick)
                return;
            
            _pingHistory.Enqueue(Time.time);
            SendPingCheck();
        }

        private void SendPingCheck()
        {
            _playersClientBroadcaster.SendToServer(new PingMessage(), Channel.ReliableUnordered);
            _lastPingSendTick = _tickManager.Tick;
        }

        private void ReceivePing(PlayerID sender, PingMessage msg, bool asServer)
        {
            if (asServer)
            {
                _playersServerBroadcaster.Send(sender, new PingMessage(), Channel.ReliableUnordered);
                return;
            }

            var oldPing = Ping;
            Ping = Mathf.Max(0, Mathf.FloorToInt((Time.time - _pingHistory.Dequeue()) * 1000) - 1000/_tickManager.TickRate * 2);
            Jitter = Mathf.Abs(Ping - oldPing);
        }

        private void HandlePacketCheck()
        {
            if (_receivedPacketTimes.Count > 0 && _receivedPacketTimes[0] < Time.time - 1)
                _receivedPacketTimes.RemoveAt(0);

            if (_lastPacketSendTick + _tickManager.TimeToTick(1f / _packetsToSendPerSec) > _tickManager.Tick)
                return;
            
            _lastPacketSendTick = _tickManager.Tick;
            _playersClientBroadcaster.SendToServer(new PacketMessage(), Channel.Unreliable);
        }

        private void ReceivePacket(PlayerID sender, PacketMessage msg, bool asServer)
        {
            if (asServer)
            {
                _playersServerBroadcaster.Send(sender, new PacketMessage(), Channel.Unreliable);
                return;
            }
            
            PacketLoss = 100 - Mathf.FloorToInt((_receivedPacketTimes.Count / (float)_packetsToSendPerSec) * 100);
            if (_tickManager.Tick < 10 * _tickManager.TickRate || PacketLoss < 0)
                PacketLoss = 0;
            
            _receivedPacketTimes.Add(Time.time);
        }

        private void OnDataReceived(Connection conn, ByteData data, bool asServer)
        {
            _totalDataReceived += data.length;
        }

        private struct PingMessage
        {
            
        }

        private struct PacketMessage
        {
            
        }
    }
}
