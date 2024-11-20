﻿using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackIntegers
    {
        [UsedByIL]
        public static void Write(this BitPacker packer, int value)
        {
            packer.WriteBits((ulong)value, 32);
        }

        [UsedByIL]
        public static void Read(this BitPacker packer, ref int value)
        {
            value = (int)packer.ReadBits(32);
        }
        
        [UsedByIL]
        public static void Write(this BitPacker packer, short value)
        {
            packer.WriteBits((ulong)value, 16);
        }

        [UsedByIL]
        public static void Read(this BitPacker packer, ref short value)
        {
            value = (short)packer.ReadBits(16);
        }
        
        [UsedByIL]
        public static void Write(this BitPacker packer, sbyte value)
        {
            packer.WriteBits((ulong)value, 16);
        }

        [UsedByIL]
        public static void Read(this BitPacker packer, ref sbyte value)
        {
            value = (sbyte)packer.ReadBits(16);
        }
        
        [UsedByIL]
        public static void Write(this BitPacker packer, bool value)
        {
            packer.WriteBits(value ? (ulong)1 : 0, 1);
        }

        [UsedByIL]
        public static void Read(this BitPacker packer, ref bool value)
        {
            value = packer.ReadBits(1) == 1;
        }
    }
}