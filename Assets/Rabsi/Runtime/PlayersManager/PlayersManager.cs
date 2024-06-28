using System;
using System.Collections.Generic;
using Rabsi.Packets;
using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    public partial struct ClientLoginRequest : INetworkedData
    {
        public string join;
        public PlayerID playerId;

        public ClientLoginRequest(string join)
        {
            this.join = join;
            playerId = new(69);
        }
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref join);
            playerId.Serialize(packer);
        }
    }

    public partial struct ServerLoginResponse : INetworkedData
    {
        public PlayerID playerId;
        
        public ServerLoginResponse(PlayerID playerId)
        {
            this.playerId = playerId;
        }
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref playerId);
        }
    }
    
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private readonly CookiesModule _cookiesModule;
        private readonly BroadcastModule _broadcastModule;

        private readonly Dictionary<string, PlayerID> _cookieToPlayerId = new();
        private uint _playerIdCounter;

        public PlayersManager(CookiesModule cookiesModule, BroadcastModule broadcaste)
        {
            _cookiesModule = cookiesModule;
            _broadcastModule = broadcaste;
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
            Debug.Log("Received login response: " + data.playerId);
        }

        private void OnClientLoginRequest(Connection conn, ClientLoginRequest data, bool asserver)
        {
            if (_cookieToPlayerId.TryGetValue(data.join, out var playerId))
            {
                Debug.Log("Player already exists: " + playerId);
            }
            else
            {
                Debug.Log("Player does not exist, creating new player");
                playerId = new PlayerID(++_playerIdCounter);
                _cookieToPlayerId.Add(data.join, playerId);
            }
            
            _broadcastModule.SendToClient(conn, new ServerLoginResponse(playerId), Channel.ReliableUnordered);
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
            
        }
    }
}
