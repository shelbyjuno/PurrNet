using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackIntegers
    {
        [UsedByIL]
        public static void Write(this BitStream stream, int value)
        {
            stream.WriteBits((ulong)value, 32);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref int value)
        {
            value = (int)stream.ReadBits(32);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, short value)
        {
            stream.WriteBits((ulong)value, 16);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref short value)
        {
            value = (short)stream.ReadBits(16);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, sbyte value)
        {
            stream.WriteBits((ulong)value, 16);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref sbyte value)
        {
            value = (sbyte)stream.ReadBits(16);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, bool value)
        {
            stream.WriteBits(value ? (ulong)1 : 0, 1);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref bool value)
        {
            value = stream.ReadBits(1) == 1;
        }
    }
}