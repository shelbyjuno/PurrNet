using UnityEngine;

namespace PurrNet
{
    public class NetworkBehaviour : NetworkIdentity
    {
        [ServerRPC, ContextMenu("ServerRPCMethod3")]
        protected void ServerRPCMethod3()
        {
            // This method will be called on the server
            Debug.Log("NOPOOO 3"); 
        }
    }
}
