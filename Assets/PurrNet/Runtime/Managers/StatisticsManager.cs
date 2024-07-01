using System.Collections.Generic;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    [RequireComponent(typeof(NetworkManager))]
    public class StatisticsManager : MonoBehaviour
    {
        [Range(0.05f, 1f)] public float checkRate = 0.33f;
        
        public int ping = 0;
        public int jitter = 0;
        public int packetLoss = 0;
        public int upload = 0;
        public int download = 0;
        
        private NetworkManager _networkManager;
        private PlayersBroadcaster _playersClientBroadcaster;
        private PlayersBroadcaster _playersServerBroadcaster;
        private TickManager _tickManager;
        public bool connectedServer;
        public bool connectedClient;
        
        // Ping stuff
        private Queue<float> _pingHistory = new();
        private uint _lastPingSendTick;
        
        // Packet loss stuff
        private int _packetsToSendPerSec = 10;
        private List<float> _receivedPacketTimes = new();
        private uint _lastPacketSendTick;
        
        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
                return;
            
            _networkManager.onServerConnectionState += OnServerConnectionState;
            _networkManager.onClientConnectionState += OnClientConnectionState;
        }

        private void OnServerConnectionState(ConnectionState state)
        {
            connectedServer = state == ConnectionState.Connected;
            
            if (state != ConnectionState.Connected)
                return;

            _playersServerBroadcaster = _networkManager.GetModule<PlayersBroadcaster>(true);
            _playersServerBroadcaster.Subscribe<PingMessage>(ReceivePing);
            _playersServerBroadcaster.Subscribe<PacketMessage>(ReceivePacket);
            _networkManager.GetModule<TickManager>(true).OnTick += OnServerTick;
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
            _networkManager.GetModule<TickManager>(true).OnTick += OnClientTick;
            
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
            if (_lastPingSendTick + _tickManager.TimeToTick(checkRate) > _tickManager.Tick)
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

            var oldPing = ping;
            ping = Mathf.Max(0, Mathf.FloorToInt((Time.time - _pingHistory.Dequeue()) * 1000) - 1000/_tickManager.TickRate * 2);
            jitter = Mathf.Abs(ping - oldPing);
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
            
            packetLoss = 100 - Mathf.FloorToInt((_receivedPacketTimes.Count / (float)_packetsToSendPerSec) * 100);
            if (_tickManager.Tick < 10 * _tickManager.TickRate || packetLoss < 0)
                packetLoss = 0;
            
            _receivedPacketTimes.Add(Time.time);
        }

        private struct PingMessage
        {
            
        }

        private struct PacketMessage
        {
            
        }
    }
}
