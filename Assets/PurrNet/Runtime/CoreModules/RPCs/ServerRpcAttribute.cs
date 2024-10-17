using System;
using PurrNet.Transports;

namespace PurrNet
{
    public class ServerRpcAttribute : Attribute
    {
        public ServerRpcAttribute(Channel channel = Channel.ReliableOrdered, bool runLocally = false, bool requireOwnership = true) {  }
    }
}
