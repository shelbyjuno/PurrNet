using System;
using PurrNet.Transports;

namespace PurrNet
{
    public class TargetRpcAttribute : Attribute
    {
        public TargetRpcAttribute(Channel channel = Channel.ReliableOrdered, bool runLocally = false, bool bufferLast = false, bool requireServer = true, float asyncTimeoutInSec = 5f) { }
    }
}