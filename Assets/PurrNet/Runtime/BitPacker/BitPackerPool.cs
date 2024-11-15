using PurrNet.Pooling;

namespace PurrNet.Packing
{
    public class BitPackerPool : GenericPool<BitPacker>
    {
        private static readonly BitPackerPool _instance;
        
        static BitPackerPool() => _instance = new BitPackerPool();

        static BitPacker Factory() => new();

        static void Reset(BitPacker list) => list.ResetPosition();
        
        public BitPackerPool() : base(Factory, Reset) { }
        
        public static BitPacker Instantiate(bool readMode)
        {
            var packer = _instance.Allocate();
            packer.ResetMode(readMode);
            return packer;
        }

        public static void Destroy(BitPacker packer)
        {
            _instance.Delete(packer);
        }
    }
}