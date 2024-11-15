using UnityEngine;

namespace PurrNet.Packing
{
    internal static class PackInt32
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Packer.Register<int>(Write, Read);
        }
        
        static void Write(BitPacker packer, int value)
        {
            packer.WriteBits((ulong)value, 32);
        }

        static void Read(BitPacker packer, ref int value)
        {
            value = (int)packer.ReadBits(32);
        }
    }
    
    internal static class PackIData
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Packer.Register<IData>(Write, Read);
        }
        
        static void Write(BitPacker packer, IData value)
        {
            Packer<int>.Write(packer, 69);
            value.Write(packer);
        }

        static void Read(BitPacker packer, ref IData value)
        {
            int val = 0;
            Packer<int>.Read(packer, ref val);
            value.Read(packer);
        }
    }
    
    internal static class PackISimpleData
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Packer.Register<ISimpleData>(Write, Read);
        }
        
        static void Write(BitPacker packer, ISimpleData value)
        {
            value.Write(packer);
        }

        static void Read(BitPacker packer, ref ISimpleData value)
        {
            value.Read(packer);
        }
    }
}
