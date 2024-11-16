using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackIData
    {
        [UsedByIL]
        public static void Write(this BitStream stream, IData value)
        {
            value.Write(stream);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref IData value)
        {
            value.Read(stream);
        }
    }
    
    public static class PackISimpleData
    {
        [UsedByIL]
        public static void Write(this BitStream stream, ISimpleData value)
        {
            value.Write(stream);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref ISimpleData value)
        {
            value.Read(stream);
        }
    }
}
