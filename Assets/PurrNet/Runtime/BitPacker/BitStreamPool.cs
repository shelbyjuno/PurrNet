using PurrNet.Pooling;

namespace PurrNet.Packing
{
    public class BitStreamPool : GenericPool<BitPacker>
    {
        private static readonly BitStreamPool _instance;
        
        static BitStreamPool() => _instance = new BitStreamPool();

        static BitPacker Factory() => new();

        static void Reset(BitPacker list) => list.ResetPosition();
        
        public BitStreamPool() : base(Factory, Reset) { }
        
        public static BitPacker Get(bool readMode = false)
        {
            var packer = _instance.Allocate();
            packer.ResetMode(readMode);
            return packer;
        }

        public static void Free(BitPacker packer)
        {
            _instance.Delete(packer);
        }
    }
}