using System;
using PurrNet.Transports;

namespace PurrNet
{
    public class ObserversRpcAttribute : Attribute
    {
        public ObserversRpcAttribute(Channel channel = Channel.ReliableOrdered, 
            bool runLocally = false,
            bool bufferLast = false,
            bool requireServer = true, 
            bool excludeOwner = false,
            bool excludeSender = true,
            float asyncTimeoutInSec = 5f) { }
    }
}
