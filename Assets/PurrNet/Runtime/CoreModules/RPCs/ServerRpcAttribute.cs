using System;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public class ServerRpcAttribute : Attribute
    {
        [UsedByIL]
        public ServerRpcAttribute(
            Channel channel = Channel.ReliableOrdered,
            bool runLocally = false, 
            bool requireOwnership = true,
            float asyncTimeoutInSec = 5f) {  }
    }
}
