using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackIData
    {
        [UsedByIL]
        public static void Write(this BitStream stream, INetworkedData value)
        {
            value.Write(stream);
        }

        [UsedByIL]
        public static void Read(this BitStream stream, ref INetworkedData value)
        {
            value.Read(stream);
        }
    }
}
