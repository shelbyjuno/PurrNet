using System;
using UnityEngine;

namespace PurrNet
{
    [CreateAssetMenu(fileName = "NetworkRules", menuName = "PurrNet/Network Rules", order = -201)]
    public class NetworkRules : ScriptableObject
    {
        [Tooltip("Who can spawn identities")]
        public ConnectionAuth spawnAuth;
        [Tooltip("Who gains ownership upon spawning of the identity")]
        public DefaultOwner defaultOwner;
        [Tooltip("If ownership should transfer to all identities of the GameObject")]
        public bool fullObjectOwnership;
        [Tooltip("Who can modify syncvars")]
        public ConnectionAuth syncVarAuth;
        [Tooltip("Who can send ObserversRpc and TargetRpc")]
        public ConnectionAuth clientRpcAuth;
        [Tooltip("Who can transfer ownership of objects")]
        public ConnectionAuth ownershipTransferAuth;
        [Tooltip("Who can synchronize parent nesting of objects")]
        public ActionAuth syncParentAuth;

        public bool despawnOnDisconnect = true;
        public bool syncComponentActive = true;
        public bool syncGameObjectActive = true;
        
        [Flags]
        public enum ActionAuth
        {
            None = 0, // Only the server can do the action
            Server = 1, // Only the server can do the action
            Owner = 2, // Owner can do the action
            Observer = 4 // Anyone can do the action
        }

        public enum ConnectionAuth
        {
            Server,
            Everyone
        }

        public enum DefaultOwner
        {
            None = 0, // Use the default owner setting of the network manager
            Server = 1, // Server if is client, otherwise None
            Spawner = 2 // Client that spawns it (only works for unsafe spawning)
        }
    }
}