using PurrNet.Pooling;

namespace PurrNet.Packing
{
    public class BitPackerPool : GenericPool<BitStream>
    {
        private static readonly BitPackerPool _instance;
        
        static BitPackerPool() => _instance = new BitPackerPool();

        static BitStream Factory() => new();

        static void Reset(BitStream list) => list.ResetPosition();
        
        public BitPackerPool() : base(Factory, Reset) { }
        
        public static BitStream Instantiate(bool readMode)
        {
            var packer = _instance.Allocate();
            packer.ResetMode(readMode);
            return packer;
        }

        public static void Destroy(BitStream stream)
        {
            _instance.Delete(stream);
        }
    }
}