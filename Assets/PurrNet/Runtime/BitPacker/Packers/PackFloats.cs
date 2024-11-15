using System;
using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackFloats
    {
        [UsedByIL]
        public static void Write(this BitStream stream, Half half)
        {
            stream.Write(half.Value);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Half half)
        {
            ushort data = default;
            stream.Read(ref data);
            half = new Half(data);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, float data)
        {
            stream.WriteBits((ulong)BitConverter.SingleToInt32Bits(data), 32);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref float data)
        {
            data = BitConverter.Int32BitsToSingle((int)stream.ReadBits(32));
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, double data)
        {
            stream.WriteBits((ulong)BitConverter.DoubleToInt64Bits(data), 64);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref double data)
        {
            data = BitConverter.Int64BitsToDouble((long)stream.ReadBits(64));
        }
    }
}
