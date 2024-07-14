using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PurrNet
{
    [CreateAssetMenu(fileName = "NetworkRules", menuName = "PurrNet/Network Rules", order = -201)]
    public class NetworkRules : ScriptableObject
    {
        public ConnectionAuth spawnAuth;
        public DefaultOwner defaultOwner;
        public ConnectionAuth syncVarAuth;
        public ConnectionAuth observersRpcAuth;
        public ConnectionAuth ownershipTransferAuth;
        public ActionAuth syncParentAuth;
        
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