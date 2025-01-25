using PurrNet.Packing;
using UnityEngine;

namespace PurrNet.Packing
{
    public static class DeltaPackInteger
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            DeltaPacker<sbyte>.Register(WriteInt8, ReadInt8);
            DeltaPacker<byte>.Register(WriteUInt8, ReadUInt8);
            DeltaPacker<short>.Register(WriteInt16, ReadInt16);
            DeltaPacker<ushort>.Register(WriteUInt16, ReadUInt16);
            DeltaPacker<int>.Register(WriteInt32, ReadInt32);
            DeltaPacker<uint>.Register(WriteUInt32, ReadUInt32);
        }
        
        private static void WriteInt8(BitPacker packer, sbyte oldvalue, sbyte newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                short diff = (short)(newvalue - oldvalue);
                Packer<PackedShort>.Write(packer, diff);
            }
        }
        
        private static void ReadInt8(BitPacker packer, sbyte oldvalue, ref sbyte value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                PackedShort packed = default;
                Packer<PackedShort>.Read(packer, ref packed);
                value = (sbyte)(oldvalue + packed.value);
            }
        }
        
        private static void WriteUInt8(BitPacker packer, byte oldvalue, byte newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                short diff = (short)(newvalue - oldvalue);
                Packer<PackedShort>.Write(packer, diff);
            }
        }
        
        private static void ReadUInt8(BitPacker packer, byte oldvalue, ref byte value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);
            
            if (hasChanged)
            {
                PackedShort packed = default;
                Packer<PackedShort>.Read(packer, ref packed);
                value = (byte)(oldvalue + packed.value);
            }
        }
        
        private static void WriteInt16(BitPacker packer, short oldvalue, short newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                int diff = newvalue - oldvalue;
                Packer<PackedInt>.Write(packer, diff);
            }
        }
        
        private static void ReadInt16(BitPacker packer, short oldvalue, ref short value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);
            
            if (hasChanged)
            {
                PackedInt packed = default;
                Packer<PackedInt>.Read(packer, ref packed);
                value = (short)(oldvalue + packed.value);
            }
        }
        
        private static void WriteUInt16(BitPacker packer, ushort oldvalue, ushort newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                int diff = (int)((uint)newvalue - oldvalue);
                Packer<PackedInt>.Write(packer, diff);
            }
        }
        
        private static void ReadUInt16(BitPacker packer, ushort oldvalue, ref ushort value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);
            
            if (hasChanged)
            {
                PackedInt packed = default;
                Packer<PackedInt>.Read(packer, ref packed);
                value = (ushort)(oldvalue + packed.value);
            }
        }
        
        private static void WriteUInt32(BitPacker packer, uint oldvalue, uint newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                long diff = newvalue - (long)oldvalue;
                Packer<PackedLong>.Write(packer, diff);
            }
        }

        private static void ReadUInt32(BitPacker packer, uint oldvalue, ref uint value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);
            
            if (hasChanged)
            {
                PackedLong packed = default;
                Packer<PackedLong>.Read(packer, ref packed);
                value = (uint)(oldvalue + packed.value);
            }
        }

        private static void WriteInt32(BitPacker packer, int oldvalue, int newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);
            
            if (hasChanged)
            {
                long diff = newvalue - (long)oldvalue;
                Packer<PackedLong>.Write(packer, diff);
            }
        }

        private static void ReadInt32(BitPacker packer, int oldvalue, ref int value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);
            
            if (hasChanged)
            {
                PackedLong packed = default;
                Packer<PackedLong>.Read(packer, ref packed);
                value = (int)(oldvalue + packed.value);
            }
        }
    }
}
