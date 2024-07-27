using JetBrains.Annotations;
using PurrNet.Transports;

namespace PurrNet
{
    public struct RPCInfo
    {
        public PlayerID sender;
    }
    
    public enum RPCType
    {
        ServerRPC,
        ObserversRPC,
        TargetRPC
    }
    
    public struct RPCDetails
    {
        public RPCType type;
        public Channel channel;
        public bool runLocally;
        public bool requireOwnership;
        public bool bufferLast;
        public bool requireServer;
        public bool excludeOwner;
        public PlayerID? targetPlayer;

        [UsedImplicitly]
        public static RPCDetails Make(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner)
        {
            return new RPCDetails
            {
                type = type,
                channel = channel,
                runLocally = runLocally,
                requireOwnership = requireOwnership,
                bufferLast = bufferLast,
                requireServer = requireServer,
                excludeOwner = excludeOwner,
                targetPlayer = null
            };
        }
        
        [UsedImplicitly]
        public static RPCDetails MakeWithTarget(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner, PlayerID playerID)
        {
            return new RPCDetails
            {
                type = type,
                channel = channel,
                runLocally = runLocally,
                requireOwnership = requireOwnership,
                bufferLast = bufferLast,
                requireServer = requireServer,
                excludeOwner = excludeOwner,
                targetPlayer = playerID
            };
        }
    }
}