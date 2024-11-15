using UnityEngine;

namespace PurrNet.Packing
{
    internal static class PackIData
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Packer.Register<IData>(Write, Read);
        }
        
        static void Write(BitPacker packer, IData value)
        {
            value.Write(packer);
        }

        static void Read(BitPacker packer, ref IData value)
        {
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
