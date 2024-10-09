using UnityEngine;

namespace PurrNet
{
    public abstract class NetworkVisibilityRule : ScriptableObject, INetworkVisibilityRule
    {
        protected NetworkManager manager;
        
        public void Setup(NetworkManager nmanager)
        {
            manager = nmanager;
        }

        public abstract int complexity { get; }
        
        public abstract bool HasVisiblity(PlayerID playerId, NetworkIdentity identity);
    }
}