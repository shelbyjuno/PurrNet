using System;
using System.Collections.Generic;
using PurrNet.Packets;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Modules
{
    public partial struct ClientLoginRequest : IAutoNetworkedData
    {
        public string join { get; }
        
        public ClientLoginRequest(string join)
        {
            this.join = join;
        }
    }
    
    public partial struct ServerLoginResponse : IAutoNetworkedData
    {
        public PlayerID playerId { get; }

        public ServerLoginResponse(PlayerID playerId)
        {
            this.playerId = playerId;
        }
    }
    
    public partial struct PlayerJoinedEvent : IAutoNetworkedData
    {
        public PlayerID playerId { get; }
        public Connection connection { get; }
        
        public PlayerJoinedEvent(PlayerID playerId, Connection connection)
        {
            this.playerId = playerId;
            this.connection = connection;
        }
    }
    
    public partial struct PlayerLeftEvent : IAutoNetworkedData
    {
        public PlayerID playerId { get; }
        
        public PlayerLeftEvent(PlayerID playerId)
        {
            this.playerId = playerId;
        }
    }
    
    public partial struct PlayerSnapshotEvent : IAutoNetworkedData
    {
        public Dictionary<Connection, PlayerID> _connectionToPlayerId { get; }
        
        public PlayerSnapshotEvent(Dictionary<Connection, PlayerID> connectionToPlayerId)
        {
            _connectionToPlayerId = connectionToPlayerId;
        }
    }
    
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private readonly CookiesModule _cookiesModule;
        private readonly BroadcastModule _broadcastModule;
        private readonly ITransport _transport;

        private readonly Dictionary<string, PlayerID> _cookieToPlayerId = new();
        private uint _playerIdCounter;
        
        private readonly Dictionary<Connection, PlayerID> _connectionToPlayerId = new();
        private readonly Dictionary<PlayerID, Connection> _playerToConnection = new();
        private readonly List<PlayerID> _connectedPlayers = new();
        private PlayerID? _localPlayerId;
        
        public List<PlayerID> connectedPlayers => _connectedPlayers;
        public PlayerID? localPlayerId => _localPlayerId;

        private bool _asServer;

        public PlayersManager(NetworkManager nm, CookiesModule cookiesModule, BroadcastModule broadcaste)
        {
            _transport = nm.transport.transport;
            _cookiesModule = cookiesModule;
            _broadcastModule = broadcaste;
        }

        public bool IsLocalPlayer(PlayerID playerId)
        {
            return _localPlayerId == playerId;
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            if (asServer)
            {
                _broadcastModule.RegisterCallback<ClientLoginRequest>(OnClientLoginRequest, true);
            }
            else
            {
                _broadcastModule.RegisterCallback<ServerLoginResponse>(OnClientLoginResponse, false);
                _broadcastModule.RegisterCallback<PlayerSnapshotEvent>(OnPlayerSnapshotEvent, false);
                _broadcastModule.RegisterCallback<PlayerJoinedEvent>(OnPlayerJoinedEvent, false);
                _broadcastModule.RegisterCallback<PlayerLeftEvent>(OnPlayerLeftEvent, false);
            }
        }

        private void OnPlayerJoinedEvent(Connection conn, PlayerJoinedEvent data, bool asserver)
        {
            RegisterPlayer(data.connection, data.playerId);
        }

        private void OnPlayerLeftEvent(Connection conn, PlayerLeftEvent data, bool asserver)
        {
            UnregisterPlayer(data.playerId);
        }

        private void OnPlayerSnapshotEvent(Connection conn, PlayerSnapshotEvent data, bool asserver)
        {
            foreach (var (key, pid) in _connectionToPlayerId)
                RegisterPlayer(key, pid);
        }

        private void OnClientLoginResponse(Connection conn, ServerLoginResponse data, bool asServer)
        {
            _localPlayerId = data.playerId;
        }

        private void OnClientLoginRequest(Connection conn, ClientLoginRequest data, bool asserver)
        {
            if (!_cookieToPlayerId.TryGetValue(data.join, out var playerId))
            {
                playerId = new PlayerID(++_playerIdCounter, false);
                _cookieToPlayerId.Add(data.join, playerId);
            }
            
            if (_connectedPlayers.Contains(playerId))
            {
                // Player is already connected?
                _transport.CloseConnection(conn);
                Debug.LogError("Client connected using a cookie from an already connected player; closing their connection.");
                return;
            }
            
            _broadcastModule.SendToClient(conn, new ServerLoginResponse(playerId));

            SendSnapshotToClient(conn);
            RegisterPlayer(conn, playerId);
            SendNewUserToAllClients(conn, playerId);
        }
        
        private void SendNewUserToAllClients(Connection conn, PlayerID playerId)
        {
            _broadcastModule.SendToAll(new PlayerJoinedEvent(playerId, conn));
        }
        
        private void SendUserLeftToAllClients(PlayerID playerId)
        {
            _broadcastModule.SendToAll(new PlayerLeftEvent(playerId));
        }
        
        private void SendSnapshotToClient(Connection conn)
        {
            _broadcastModule.SendToClient(conn, new PlayerSnapshotEvent(_connectionToPlayerId));
        }

        private void RegisterPlayer(Connection conn, PlayerID player)
        {
            _connectedPlayers.Add(player);

            if (conn.isValid)
            {
                _connectionToPlayerId.Add(conn, player);
                _playerToConnection.Add(player, conn);
            }
        }
        
        private void UnregisterPlayer(Connection conn)
        {
            if (!_connectionToPlayerId.TryGetValue(conn, out var player))
                return;
            
            _connectedPlayers.Remove(player);
            _playerToConnection.Remove(player);
            _connectionToPlayerId.Remove(conn);
        }
        
        private void UnregisterPlayer(PlayerID playerId)
        {
            if (_playerToConnection.TryGetValue(playerId, out var conn))
                _connectionToPlayerId.Remove(conn);
            _connectedPlayers.Remove(playerId);
            _playerToConnection.Remove(playerId);
        }

        public void Disable(bool asServer)
        {
            if (asServer)
            {
                _broadcastModule.UnregisterCallback<ClientLoginRequest>(OnClientLoginRequest, true);
            }
            else
            {
                _broadcastModule.UnregisterCallback<ServerLoginResponse>(OnClientLoginResponse, false);
            }
        }
        
        public void OnConnected(Connection conn, bool asServer)
        {
            if (asServer) return;
            
            // Generate a new session cookie or get the existing one and send it to the server
            var cookie = _cookiesModule.GetOrSet("client_connection_session", Guid.NewGuid().ToString(), false);
            _broadcastModule.SendToServer(new ClientLoginRequest(cookie), Channel.ReliableUnordered);
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            if (!asServer) return;
            
            if (_connectionToPlayerId.TryGetValue(conn, out var playerId))
                SendUserLeftToAllClients(playerId);

            UnregisterPlayer(conn);
        }
    }
}
