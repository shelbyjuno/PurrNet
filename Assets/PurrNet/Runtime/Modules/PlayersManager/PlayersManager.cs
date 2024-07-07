using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using PurrNet.Transports;

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
        public Dictionary<Connection, PlayerID> connectionToPlayerId { get; }
        
        public PlayerSnapshotEvent(IDictionary<Connection, PlayerID> connectionToPlayerId)
        {
            this.connectionToPlayerId = new Dictionary<Connection, PlayerID>(connectionToPlayerId);
        }
    }
    
    public delegate void OnPlayerJoinedEvent(PlayerID player, bool asserver);
    
    public delegate void OnPlayerLeftEvent(PlayerID player, bool asserver);
    
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
        
        public event OnPlayerJoinedEvent onPrePlayerJoined;
        public event OnPlayerJoinedEvent onPlayerJoined;
        public event OnPlayerJoinedEvent onPostPlayerJoined;
        
        public event OnPlayerLeftEvent onPrePlayerLeft;
        public event OnPlayerLeftEvent onPlayerLeft;
        public event OnPlayerLeftEvent onPostPlayerLeft;

        private bool _asServer;

        private PlayersBroadcaster _playerBroadcaster;

        internal void SetBroadcaster(PlayersBroadcaster broadcaster)
        {
            _playerBroadcaster = broadcaster;
        }
        
        public void Send<T>(PlayerID player, T data, Channel method = Channel.ReliableOrdered) 
            => _playerBroadcaster.Send(player, data, method);

        public void Send<T>(IEnumerable<PlayerID> players, T data, Channel method = Channel.ReliableOrdered)
            => _playerBroadcaster.Send(players, data, method);
        
        public void SendToServer<T>(T data, Channel method = Channel.ReliableOrdered) 
            => _playerBroadcaster.SendToServer(data, method);
        
        public void SendToAll<T>(T data, Channel method = Channel.ReliableOrdered)
            => _playerBroadcaster.SendToAll(data, method);
        
        public void Unsubscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
            => _playerBroadcaster.Unsubscribe(callback);
        
        public void Subscribe<T>(PlayerBroadcastDelegate<T> callback) where T : new()
            => _playerBroadcaster.Subscribe(callback);

        public PlayersManager(NetworkManager nm, CookiesModule cookiesModule, BroadcastModule broadcaste)
        {
            _transport = nm.transport.transport;
            _cookiesModule = cookiesModule;
            _broadcastModule = broadcaste;
        }
        
        /// <summary>
        /// Try to get the connection of a playerId.
        /// For bots, this will always return false.
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="conn"></param>
        /// <returns>The network connection tied to this player</returns>
        public bool TryGetConnection(PlayerID playerId, out Connection conn)
        {
            if (playerId.isBot)
            {
                conn = default;
                return false;
            }
            
            return _playerToConnection.TryGetValue(playerId, out conn);
        }
        
        /// <summary>
        /// Try to get the playerId of a connection.
        /// </summary>
        public bool TryGetPlayer(Connection conn, out PlayerID playerId)
        {
            return _connectionToPlayerId.TryGetValue(conn, out playerId);
        }

        /// <summary>
        /// Check if a playerId is the local player.
        /// </summary>
        public bool IsLocalPlayer(PlayerID playerId)
        {
            return _localPlayerId == playerId;
        }

        /// <summary>
        /// Check if a playerId is a valid player.
        /// A valid player is a player that is connected to the server.
        /// </summary>
        public bool IsValidPlayer(PlayerID playerId)
        {
            return _connectedPlayers.Contains(playerId);
        }
        
        /// <summary>
        /// Create a new bot player and add it to the connected players list.
        /// </summary>
        /// <returns>The playerId of the new bot player</returns>
        public PlayerID CreateBot()
        {
            if (!_asServer)
            {
                throw new InvalidOperationException(PurrLogger.FormatMessage("Cannot create a bot from a client."));
            }
            
            var playerId = new PlayerID(++_playerIdCounter, true);
            RegisterPlayer(default, playerId);
            SendNewUserToAllClients(default, playerId);
            return playerId;
        }
        
        /// <summary>
        /// Kick a player from the server.
        /// If the user has a connection, it will be closed.
        /// </summary>
        /// <param name="playerId"></param>
        public void KickPlayer(PlayerID playerId)
        {
            if (_playerToConnection.TryGetValue(playerId, out var conn))
                _transport.CloseConnection(conn);
            UnregisterPlayer(playerId);
            SendUserLeftToAllClients(playerId);
        }
        
        public void Enable(bool asServer)
        {
            _asServer = asServer;
            
            if (asServer)
            {
                _broadcastModule.Subscribe<ClientLoginRequest>(OnClientLoginRequest, true);
            }
            else
            {
                _broadcastModule.Subscribe<ServerLoginResponse>(OnClientLoginResponse, false);
                _broadcastModule.Subscribe<PlayerSnapshotEvent>(OnPlayerSnapshotEvent, false);
                _broadcastModule.Subscribe<PlayerJoinedEvent>(OnPlayerJoinedEvent, false);
                _broadcastModule.Subscribe<PlayerLeftEvent>(OnPlayerLeftEvent, false);
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
            foreach (var (key, pid) in data.connectionToPlayerId)
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
                PurrLogger.LogError("Client connected using a cookie from an already connected player; closing their connection.");
                return;
            }
            
            _broadcastModule.Send(conn, new ServerLoginResponse(playerId));

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
            _broadcastModule.Send(conn, new PlayerSnapshotEvent(_connectionToPlayerId));
        }

        private void RegisterPlayer(Connection conn, PlayerID player)
        {
            _connectedPlayers.Add(player);

            if (conn.isValid)
            {
                _connectionToPlayerId.Add(conn, player);
                _playerToConnection.Add(player, conn);
            }
            
            onPrePlayerJoined?.Invoke(player, _asServer);
            onPlayerJoined?.Invoke(player, _asServer);
            onPostPlayerJoined?.Invoke(player, _asServer);
        }
        
        private void UnregisterPlayer(Connection conn)
        {
            if (!_connectionToPlayerId.TryGetValue(conn, out var player))
                return;
            
            _connectedPlayers.Remove(player);
            _playerToConnection.Remove(player);
            _connectionToPlayerId.Remove(conn);
            
            onPrePlayerLeft?.Invoke(player, _asServer);
            onPlayerLeft?.Invoke(player, _asServer);
            onPostPlayerLeft?.Invoke(player, _asServer);
        }
        
        private void UnregisterPlayer(PlayerID playerId)
        {
            if (_playerToConnection.TryGetValue(playerId, out var conn))
                _connectionToPlayerId.Remove(conn);
            _connectedPlayers.Remove(playerId);
            _playerToConnection.Remove(playerId);
            
            onPrePlayerLeft?.Invoke(playerId, _asServer);
            onPlayerLeft?.Invoke(playerId, _asServer);
            onPostPlayerLeft?.Invoke(playerId, _asServer);
        }

        public void Disable(bool asServer) { }
        
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
