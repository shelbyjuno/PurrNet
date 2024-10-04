using System;
using JetBrains.Annotations;
using PurrNet.Packets;

namespace PurrNet
{
    public struct GenericRPCHeader
    {
        public NetworkStream stream;
        public uint hash;
        public Type[] types;
        public object[] values;
        public RPCInfo info;
        
        [UsedImplicitly]
        public void SetPlayerId(PlayerID player, int index)
        {
            values[index] = player;
        }
        
        [UsedImplicitly]
        public void SetInfo(int index)
        {
            values[index] = info;
        }
        
        [UsedImplicitly]
        public void Read(int genericIndex, int index)
        {
            object value = default;
            stream.Serialize(types[genericIndex], ref value);
            values[index] = value;
        }
        
        [UsedImplicitly]
        public void Read<T>(int index)
        {
            T value = default;
            stream.Serialize(ref value);
            values[index] = value;
        }
    }
}