using System;
using PurrNet.Transports;

namespace PurrNet
{
    public class ServerRPCAttribute : Attribute
    {
        public ServerRPCAttribute() { }
        
        public ServerRPCAttribute(Channel channel = Channel.ReliableOrdered, bool runLocally = false, bool requireOwnership = true) {  }
    }
}
