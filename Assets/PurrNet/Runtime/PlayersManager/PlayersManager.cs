using System;
using System.Collections.Generic;
using PurrNet.Packets;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Modules
{
    public partial struct ClientLoginRequest : INetworkedData
    {
        public string join;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref join);
        }
    }
    
    public partial struct ServerLoginResponse : IAutoNetworkedData
    {
        public PlayerID playerId { get; set; }
    }
    
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private readonly CookiesModule _cookiesModule;
        private readonly BroadcastModule _broadcastModule;
        private readonly ITransport _transport;

        private readonly Dictionary<string, PlayerID> _cookieToPlayerId = new();
        private uint _playerIdCounter;
        
        private readonly Dictionary<Connection, PlayerID> _connectionToPlayerId = new();
        private readonly List<PlayerID> _connectedPlayers = new();
        private PlayerID? _localPlayerId;

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
            if (asServer)
            {
                _broadcastModule.RegisterCallback<ClientLoginRequest>(OnClientLoginRequest, true);
            }
            else
            {
                _broadcastModule.RegisterCallback<ServerLoginResponse>(OnClientLoginResponse, false);
            }
        }
        
        private void OnClientLoginResponse(Connection conn, ServerLoginResponse data, bool asServer)
        {
            _localPlayerId = data.playerId;
        }

        private void OnClientLoginRequest(Connection conn, ClientLoginRequest data, bool asserver)
        {
            if (!_cookieToPlayerId.TryGetValue(data.join, out var playerId))
            {
                playerId = new PlayerID(++_playerIdCounter);
                _cookieToPlayerId.Add(data.join, playerId);
            }
            
            if (_connectedPlayers.Contains(playerId))
            {
                // Player is already connected?
                _transport.CloseConnection(conn);
                Debug.LogError("Client connected using a cookie from an already connected player; closing their connection.");
                return;
            }
            
            RegisterPlayer(conn, playerId);
            
            _broadcastModule.SendToClient(conn, new ServerLoginResponse
            {
                playerId = playerId
            }, Channel.ReliableUnordered);
        }

        private void RegisterPlayer(Connection conn, PlayerID player)
        {
            _connectedPlayers.Add(player);
            _connectionToPlayerId.Add(conn, player);
        }
        
        private void UnregisterPlayer(Connection conn)
        {
            if (!_connectionToPlayerId.TryGetValue(conn, out var player)) return;
            
            _connectedPlayers.Remove(player);
            _connectionToPlayerId.Remove(conn);
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
            _broadcastModule.SendToServer(new ClientLoginRequest
            {
                join = cookie
            }, Channel.ReliableUnordered);
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            if (!asServer) return;
            
            UnregisterPlayer(conn);
        }
    }
}
