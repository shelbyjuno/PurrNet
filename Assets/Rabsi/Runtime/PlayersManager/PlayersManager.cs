using Rabsi.Packets;
using Rabsi.Transports;
using UnityEngine;

namespace Rabsi.Modules
{
    public partial struct TestMessage : IAutoNetworkedData
    { 
        public string test;
        public Vector3 pos;
        public PlayerID pid;
    }
    
    public partial struct TestMessageB : INetworkedData
    {
        public string test;
        public Vector3 pos;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref test);
            packer.Serialize(ref pos);
        }
    }
    
    public struct TestMessageC
    { 
        public Vector3 pos;
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
            {
                _broadcastModule.RegisterCallback<TestMessage>(OnReceivedString, false);
                _broadcastModule.RegisterCallback<TestMessageB>(OnReceivedBString, false);
                _broadcastModule.RegisterCallback<TestMessageC>(OnReceivedCString, false);
            }
        }

        private void OnReceivedCString(Connection conn, TestMessageC data, bool asserver)
        {
            Debug.Log($"Received string: {data.pos} from {conn} as {(asserver ? "server" : "client")}");
        }

        private void OnReceivedBString(Connection conn, TestMessageB data, bool asserver)
        {
            Debug.Log($"Received string: {data.test}, {data.pos} from {conn} as {(asserver ? "server" : "client")}");
        }

        private void OnReceivedString(Connection conn, TestMessage data, bool asserver)
        {
            Debug.Log($"Received string: {data.test}, {data.pos}, {data.pid} from {conn} as {(asserver ? "server" : "client")}");
        }

        public void Disable(bool asServer)
        {
            if (!asServer)
            {
                _broadcastModule.UnregisterCallback<TestMessage>(OnReceivedString, false);
                _broadcastModule.UnregisterCallback<TestMessageB>(OnReceivedBString, false);
                _broadcastModule.UnregisterCallback<TestMessageC>(OnReceivedCString, false);
            }
        }
        
        public void OnConnected(Connection conn, bool asServer)
        {
            if (!asServer) return;
            
            _broadcastModule.SendToClient(conn, new TestMessage
            {
                test = "69.42f",
                pos = new Vector3(1, 2, 3),
                pid = new PlayerID(69)
            });
            
            _broadcastModule.SendToClient(conn, new TestMessageB
            {
                test = "69.42f",
                pos = new Vector3(1, 2, 3)
            });
            
            _broadcastModule.SendToClient(conn, new TestMessageC
            {
                pos = new Vector3(1, 2, 3)
            });
        }

        public void OnDisconnected(Connection conn, bool asServer)
        {
            
        }
    }
}
