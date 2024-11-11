using JetBrains.Annotations;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine.PlayerLoop;

namespace PurrNet
{
    public struct RPCInfo
    {
        public PlayerID sender;
        public Connection senderConn;
        public bool asServer;
        
        [UsedByIL]
        public RPCSignature compileTimeSignature;
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
        public bool isStatic;
        public bool runLocally;
        public bool requireOwnership;
        public bool bufferLast;
        public bool requireServer;
        public bool excludeOwner;
        public bool excludeSender;
        public string rpcName;
        public float asyncTimeoutInSec;
        public PlayerID? targetPlayer;

        [UsedImplicitly]
        public static RPCSignature Make(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner, string name, bool isStatic, float asyncTimoutInSec)
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
                isStatic = isStatic,
                rpcName = name,
                asyncTimeoutInSec = asyncTimoutInSec
            };
        }
        
        [UsedImplicitly]
        public static RPCSignature MakeWithTarget(RPCType type, Channel channel, bool runLocally, bool requireOwnership, bool bufferLast, bool requireServer, bool excludeOwner, string name, bool isStatic, float asyncTimoutInSec, PlayerID playerID)
        {
            var rpc = Make(type, channel, runLocally, requireOwnership, bufferLast, requireServer, excludeOwner, name, isStatic, asyncTimoutInSec);
            rpc.targetPlayer = playerID;
            return rpc;
        }
    }
}