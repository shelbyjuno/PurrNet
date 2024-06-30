using UnityEngine;

namespace PurrNet.Modules
{
    public class SpawnManager : INetworkModule
    {
        private bool _asServer;

        public void Enable(bool asServer)
        {
            _asServer = asServer;
        }

        public void Disable(bool asServer) { }

        public void Spawn(GameObject gameObject)
        {
            
        }
    }
}
