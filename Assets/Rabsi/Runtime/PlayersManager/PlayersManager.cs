using MemoryPack;
using Rabsi.Packets;
using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    [MemoryPackable]
    public partial struct TestMessage : INetworkedData
    {
        public string test;
        public Vector3 pos;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref test);
            packer.Serialize(ref pos);
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
            Debug.Log($"Received string: {data.test}, {data.pos} from {conn} as {(asserver ? "server" : "client")}");
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
                test = "69.42f",
                pos = new Vector3(1, 2, 3)
            });
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            
        }
    }
}
