using PurrNet.Packing;
using UnityEngine;

namespace PurrNet
{
    public static class DeltaPackFloats
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            DeltaPacker<float>.Register(WriteSingle, ReadSingle);
            DeltaPacker<double>.Register(WriteDouble, ReadDouble);
            DeltaPacker<Half>.Register(WriteHalf, ReadHalf);
        }

        private static void WriteHalf(BitPacker packer, Half oldvalue, Half newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);
            
            if (hasChanged)
            {
                var diff = newvalue.Value - oldvalue.Value;
                Packer<PackedInt>.Write(packer, diff);
            }
        }

        private static void ReadHalf(BitPacker packer, Half oldvalue, ref Half value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                PackedInt packed = default;
                Packer<PackedInt>.Read(packer, ref packed);
                value = new Half(oldvalue.Value + packed.value);
            }
        }

        private static unsafe void WriteDouble(BitPacker packer, double oldvalue, double newvalue)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            bool hasChanged = oldvalue != newvalue;
            
            Packer<bool>.Write(packer, hasChanged);
            
            if (hasChanged)
            {
                ulong oldBits = *(ulong*)&oldvalue;
                ulong newBits = *(ulong*)&newvalue;
                long diff = (long)(newBits - oldBits);
                Packer<PackedLong>.Write(packer, diff);
            }
        }

        private static unsafe void ReadDouble(BitPacker packer, double oldvalue, ref double value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                PackedLong packed = default;
                Packer<PackedLong>.Read(packer, ref packed);
                ulong oldBits = *(ulong*)&oldvalue;
                ulong newBits = (ulong)((long)oldBits + packed.value);
                value = *(double*)&newBits;
            }
        }

        private static unsafe void WriteSingle(BitPacker packer, float oldvalue, float newvalue)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                uint oldBits = *(uint*)&oldvalue;
                uint newBits = *(uint*)&newvalue;
                long diff = (long)newBits - oldBits;
                Packer<PackedLong>.Write(packer, diff);
            }
        }

        private static unsafe void ReadSingle(BitPacker packer, float oldvalue, ref float value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                PackedLong packed = default;
                Packer<PackedLong>.Read(packer, ref packed);
                uint oldBits = *(uint*)&oldvalue;
                uint newBits = (uint)(oldBits + packed.value);
                value = *(float*)&newBits;
            }
        }
    }
}
