using System;
using PurrNet.Transports;

namespace PurrNet
{
    public abstract class RPCAttribute : Attribute
    {
        public bool runLocally { get; private set; }
        
        public Channel channel { get; private set; }

        protected RPCAttribute(bool runLocally, Channel channel)
        {
            this.runLocally = runLocally;
            this.channel = channel;
        }
    }
    
    public class ServerRPCAttribute : RPCAttribute
    {
        public bool? requireOwnership { get; private set; }
        
        public ServerRPCAttribute() : base(false, Channel.ReliableOrdered) { }
        
        public ServerRPCAttribute(bool runLocally = false, Channel channel = Channel.ReliableOrdered, bool? requireOwnership = null) : base(runLocally, channel)
        {
            this.requireOwnership = requireOwnership;
        }
    }
}
