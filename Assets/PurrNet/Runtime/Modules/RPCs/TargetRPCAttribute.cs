using PurrNet.Transports;

namespace PurrNet
{
    public class TargetRPCAttribute : RPCAttribute
    {
        public bool? requireServer { get; private set; }
        
        public bool bufferLast { get; private set; }
        
        public TargetRPCAttribute() : base(false, Channel.ReliableOrdered) { }
        
        public TargetRPCAttribute(bool runLocally = false, bool bufferLast = false, Channel channel = Channel.ReliableOrdered, bool? requireServer = null) : base(runLocally, channel)
        {
            this.requireServer = requireServer;
            this.bufferLast = bufferLast;
        }
    }
}