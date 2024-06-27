using MemoryPack;
using Rabsi.Packets;
using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    [MemoryPackable]
    public partial struct TestMessage : INetworkedData
    {
        public string message;
        public Vector3 pos;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref message);
        }
    }
    
    public class PlayersManager : INetworkModule, IConnectionListener
    {
        private readonly CookiesModule _cookiesModule;
        private readonly BroadcastModule _broadcastModule;

        public PlayersManager(CookiesModule cookiesModule, BroadcastModule broadcaste)
        {
            _cookiesModule = cookiesModule;
            _broadcastModule = broadcaste;
        }

        public void Enable(bool asServer)
        {
            if (!asServer)
                _broadcastModule.RegisterCallback<TestMessage>(OnReceivedString);
        }

        private void OnReceivedString(Connection conn, TestMessage data, bool asserver)
        {
            Debug.Log($"Received string: {data.message} from {conn} as {(asserver ? "server" : "client")}");
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
                _broadcastModule.UnregisterCallback<TestMessage>(OnReceivedString);
        }
        
        public void OnConnected(Connection conn, bool asServer)
        {
            if (!asServer) return;
            
            _broadcastModule.SendToClient(conn, new TestMessage
            {
                message = "Hello from server!",
                pos = new Vector3(1, 2, 3)
            });
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            
        }
    }
}
