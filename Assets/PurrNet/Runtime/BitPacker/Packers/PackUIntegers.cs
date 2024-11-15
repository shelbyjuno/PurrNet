using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackUIntegers
    {
        [UsedByIL]
        public static void Write(this BitStream stream, uint value)
        {
            stream.WriteBits(value, 32);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref uint value)
        {
            value = (uint)stream.ReadBits(32);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, ushort value)
        {
            stream.WriteBits(value, 16);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref ushort value)
        {
            value = (ushort)stream.ReadBits(16);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, byte value)
        {
            stream.WriteBits(value, 16);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref byte value)
        {
            value = (byte)stream.ReadBits(16);
        }
    }
}