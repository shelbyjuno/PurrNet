using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackIData
    {
        [UsedByIL]
        public static void Write(this BitPacker packer, INetworkedData value)
        {
            value.Write(packer);
        }

        [UsedByIL]
        public static void Read(this BitPacker packer, ref INetworkedData value)
        {
            value.Read(packer);
        }
    }
}
