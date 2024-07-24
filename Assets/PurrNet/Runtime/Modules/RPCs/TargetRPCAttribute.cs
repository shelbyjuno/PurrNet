using System;
using PurrNet.Transports;

namespace PurrNet
{
    public class TargetRPCAttribute : Attribute
    {
        public TargetRPCAttribute()  { }
        
        public TargetRPCAttribute(Channel channel = Channel.ReliableOrdered, bool runLocally = false, bool bufferLast = false, bool requireServer = true) { }
    }
}