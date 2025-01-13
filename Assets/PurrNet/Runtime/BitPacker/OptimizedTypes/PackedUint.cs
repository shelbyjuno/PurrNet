using System;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet.Packing
{
    [Serializable]
    public struct PackedUint
    {
        public uint value;
        
        public PackedUint(uint value)
        {
            this.value = value;
        }
        
        public static implicit operator PackedUint(uint value) => new PackedUint(value);
        
        public static implicit operator uint(PackedUint value) => value.value;
    }
    
    [Serializable]
    public struct PackedInt
    {
        public int value;
        
        public PackedInt(int value)
        {
            this.value = value;
        }
        
        public static implicit operator PackedInt(int value) => new PackedInt(value);
        
        public static implicit operator int(PackedInt value) => value.value;
    }

    [Serializable]
    public struct PackedUshort
    {
        public ushort value;
        
        public PackedUshort(ushort value)
        {
            this.value = value;
        }
        
        public static implicit operator PackedUshort(ushort value) => new PackedUshort(value);
        
        public static implicit operator ushort(PackedUshort value) => value.value;
    }

    public static class PackedUintSerializer
    {
        public static uint ZigzagEncode (int i) => (uint)(((ulong)i >> 31) ^ ((ulong)i << 1));
        
        public static int ZigzagDecode (uint i) => (int)(((long)i >> 1) ^ -((long)i & 1));
        
        static int CountLeadingZeroBits(uint value)
        {
            if (value == 0) return 32; // Special case for zero

            int count = 0;
            if ((value & 0xFFFF0000) == 0) { count += 16; value <<= 16; }
            if ((value & 0xFF000000) == 0) { count += 8; value <<= 8; }
            if ((value & 0xF0000000) == 0) { count += 4; value <<= 4; }
            if ((value & 0xC0000000) == 0) { count += 2; value <<= 2; }
            if ((value & 0x80000000) == 0) { count += 1; }

            return count;
        }
        
        const int PREFIX_BITS = 2;
        const int MAX_COUNT = 1 << PREFIX_BITS;
        const int CHUNK = 8;
        
        [UsedByIL]
        public static void Write(BitPacker packer, PackedUint value)
        {
            int trailingZeroes = CountLeadingZeroBits(value.value);
            int emptyChunks = trailingZeroes / CHUNK;
            int fullBytes = Mathf.Clamp(MAX_COUNT - emptyChunks, 1, 4);
            packer.WriteBits((ulong)(fullBytes - 1), PREFIX_BITS);
            byte numberBits = (byte)(fullBytes * CHUNK);
            packer.WriteBits(value.value, numberBits);
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref PackedUint value)
        {
            var fullBytes = packer.ReadBits(PREFIX_BITS) + 1;
            int emptyChunks = MAX_COUNT - (int)fullBytes;
            byte numberBits = (byte)(32 - emptyChunks * CHUNK);
            value = new PackedUint((uint)packer.ReadBits(numberBits));
        }
        
        [UsedByIL]
        public static void Write(BitPacker packer, PackedInt value)
        {
            var packed = new PackedUint(ZigzagEncode(value.value));
            Write(packer, packed);
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref PackedInt value)
        {
            PackedUint packed = default;
            Read(packer, ref packed);
            value = new PackedInt(ZigzagDecode(packed.value));
        }
        
        const int USHORT_PREFIX_BITS = 3;
        const int USHORT_MAX_COUNT = 1 << USHORT_PREFIX_BITS;
        const int USHORT_CHUNK = 2;
        
        [UsedByIL]
        public static void Write(BitPacker packer, PackedUshort value)
        {
            int trailingZeroes = CountLeadingZeroBits(value.value);
            int adjustedZeroes = Math.Max(0, trailingZeroes - 16);
            int fullChunks = Mathf.Max(1, USHORT_MAX_COUNT - adjustedZeroes / USHORT_CHUNK);
            
            packer.WriteBits((ulong)(fullChunks - 1), USHORT_PREFIX_BITS);
            byte numberBits = (byte)(fullChunks * USHORT_CHUNK);
            packer.WriteBits(value.value, numberBits);
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref PackedUshort value)
        {
            var fullBytes = packer.ReadBits(USHORT_PREFIX_BITS) + 1;
            int emptyChunks = USHORT_MAX_COUNT - (int)fullBytes;
            byte numberBits = (byte)(16 - emptyChunks * USHORT_CHUNK);

            value = new PackedUshort((ushort)packer.ReadBits(numberBits));
        }
    }
}
