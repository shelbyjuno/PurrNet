using PurrNet.Pooling;

namespace PurrNet.Packing
{
    public class BitStreamPool : GenericPool<BitStream>
    {
        private static readonly BitStreamPool _instance;
        
        static BitStreamPool() => _instance = new BitStreamPool();

        static BitStream Factory() => new();

        static void Reset(BitStream list) => list.ResetPosition();
        
        public BitStreamPool() : base(Factory, Reset) { }
        
        public static BitStream Get(bool readMode = false)
        {
            var packer = _instance.Allocate();
            packer.ResetMode(readMode);
            return packer;
        }

        public static void Free(BitStream stream)
        {
            _instance.Delete(stream);
        }
    }
}