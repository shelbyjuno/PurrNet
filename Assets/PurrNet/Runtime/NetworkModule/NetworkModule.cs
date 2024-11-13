using System;
using System.Reflection;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;

namespace PurrNet
{
    public class NetworkModule
    {
        protected NetworkIdentity parent { get; private set; }
        
        public string name { get; private set; }

        private byte index { get; set; } = 255;

        protected NetworkManager networkManager => parent ? parent.networkManager : null;
        
        protected bool isSceneObject => parent && parent.isSceneObject;
        
        protected bool isOwner => parent && parent.isOwner;
        
        protected bool isClient => parent && parent.isClient;

        protected bool isServer => parent && parent.isServer;
        
        protected bool isHost => parent && parent.isHost;
        
        protected bool isSpawned => parent && parent.isSpawned;
        
        protected bool hasOwner => parent.hasOwner;
        
        protected bool hasConnectedOwner => parent && parent.hasConnectedOwner;
        
        protected PlayerID? localPlayer => parent ? parent.localPlayer : null;
        
        protected PlayerID localPlayerForced => parent ? parent.localPlayerForced : default;
        
        protected PlayerID? owner => parent ? parent.owner : null;

        public virtual void OnSpawn() { }

        public virtual void OnSpawn(bool asServer) { }

        public virtual void OnDespawned() { }
        
        public virtual void OnDespawned(bool asServer) { }

        /// <summary>
        /// Called when an observer is added.
        /// Server only.
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnObserverAdded(PlayerID player) { }
        
        /// <summary>
        /// Called when an observer is removed.
        /// Server only.
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnObserverRemoved(PlayerID player) { }
        
        public virtual void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) { }

        public virtual void OnOwnerDisconnected(PlayerID ownerId, bool asServer) { }

        public virtual void OnOwnerConnected(PlayerID ownerId, bool asServer) { }
        
        public void SetComponentParent(NetworkIdentity p, byte i, string moduleName)
        {
            parent = p;
            index = i;
            name = moduleName;
        }

        [UsedByIL]
        protected void SendRPC(ChildRPCPacket packet, RPCSignature signature)
        {
            if (!parent)
            {
                if (signature.channel is Channel.ReliableOrdered or Channel.ReliableSequenced)
                    PurrLogger.LogError($"Trying to send RPC from '{GetType().Name}' which is not initialized.");
                return;
            }

            if (!parent.isSpawned)
            {
                if (signature.channel is Channel.ReliableOrdered or Channel.ReliableSequenced)
                    PurrLogger.LogError($"Trying to send RPC from '{parent.name}' which is not spawned.", parent);
                return;
            }

            var nm = parent.networkManager;

            if (!nm.TryGetModule<RPCModule>(nm.isServer, out var module))
            {
                PurrLogger.LogError("Failed to get RPC module.", parent);
                return;
            }

            if (signature.requireOwnership && !parent.isOwner)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{parent.name}' without ownership.",
                    parent);
                return;
            }

            if (signature.requireServer && !nm.isServer)
            {
                PurrLogger.LogError($"Trying to send RPC '{signature.rpcName}' from '{parent.name}' without server.",
                    parent);
                return;
            }

            module.AppendToBufferedRPCs(packet, signature);

            Func<PlayerID, bool> predicate = null;
            
            if (signature.excludeOwner)
                predicate = parent.IsNotOwnerPredicate;

            switch (signature.type)
            {
                case RPCType.ServerRPC: parent.SendToServer(packet, signature.channel); break;
                case RPCType.ObserversRPC:
                {
                    if (isServer)
                        parent.SendToObservers(packet, predicate, signature.channel);
                    else parent.SendToServer(packet, signature.channel);
                    break;
                }
                case RPCType.TargetRPC: 
                    if (isServer)
                        parent.SendToTarget(signature.targetPlayer!.Value, packet, signature.channel);
                    else parent.SendToServer(packet, signature.channel);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
        [UsedByIL]
        protected bool ValidateReceivingRPC(RPCInfo info, RPCSignature signature, IRpc data, bool asServer)
        {
            return parent && parent.ValidateReceivingRPC(info, signature, data, asServer);
        }
        
        [UsedByIL]
        protected object CallGeneric(string methodName, GenericRPCHeader rpcHeader)
        {
            var key = new NetworkIdentity.InstanceGenericKey(methodName, GetType(), rpcHeader.types);
            
            if (!NetworkIdentity.genericMethods.TryGetValue(key, out var gmethod))
            {
                var method = GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                gmethod = method?.MakeGenericMethod(rpcHeader.types);
                
                NetworkIdentity.genericMethods.Add(key, gmethod);
            }
        
            if (gmethod == null)
            {
                PurrLogger.LogError($"Calling generic RPC failed. Method '{methodName}' not found.");
                return null;
            }

            return gmethod.Invoke(this, rpcHeader.values);
        }
        
        [UsedByIL]
        protected ChildRPCPacket BuildRPC(byte rpcId, NetworkStream data)
        {
            var rpc = new ChildRPCPacket
            {
                networkId = parent.id!.Value,
                sceneId = parent.sceneId,
                childId = index,
                rpcId = rpcId,
                data = data.buffer.ToByteData(),
                senderId = RPCModule.GetLocalPlayer(networkManager)
            };

            return rpc;
        }
    }
}
