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
    
    public struct RPCSignature
    {
        public RPCType type;
        public Channel channel;
        public bool runLocally;
        public bool requireOwnership;
        public bool bufferLast;
        public bool requireServer;
        public bool excludeOwner;
        public string rpcName;
        public PlayerID? targetPlayer;

        [UsedImplicitly]
        public static RPCSignature Make(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner, string name)
        {
            return new RPCSignature
            {
                type = type,
                channel = channel,
                runLocally = runLocally,
                requireOwnership = requireOwnership,
                bufferLast = bufferLast,
                requireServer = requireServer,
                excludeOwner = excludeOwner,
                targetPlayer = null,
                rpcName = name
            };
        }
        
        [UsedImplicitly]
        public static RPCSignature MakeWithTarget(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner, string name, PlayerID playerID)
        {
            return new RPCSignature
            {
                type = type,
                channel = channel,
                runLocally = runLocally,
                requireOwnership = requireOwnership,
                bufferLast = bufferLast,
                requireServer = requireServer,
                excludeOwner = excludeOwner,
                rpcName = name,
                targetPlayer = playerID
            };
        }
    }
}