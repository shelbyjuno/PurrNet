using System;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public class TargetRpcAttribute : Attribute
    {
        [UsedByIL]
        public TargetRpcAttribute(
            Channel channel = Channel.ReliableOrdered, 
            bool runLocally = false, 
            bool bufferLast = false, 
            bool requireServer = true,
            float asyncTimeoutInSec = 5f) { }
    }
}