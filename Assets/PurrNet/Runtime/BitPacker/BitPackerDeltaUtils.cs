using PurrNet.Packing;

namespace PurrNet
{
    public static class BitPackerDeltaUtils
    {
        /*[RuntimeInitializeOnLoadMethod]
        static void Test()
        {
            using var origin = BitPackerPool.Get();
            
            Packer<long>.Write(origin, 69);
            Packer<long>.Write(origin, 42);
            Packer<long>.Write(origin, 6942);
            Packer<long>.Write(origin, 666);
            Packer<long>.Write(origin, 666);
            Packer<long>.Write(origin, 666);
            Packer<long>.Write(origin, 666);
            Packer<long>.Write(origin, 666);
            
            using var target = BitPackerPool.Get();
            
            Packer<long>.Write(target, 69);
            Packer<long>.Write(target, 6942);
            Packer<long>.Write(target, 666);
            Packer<long>.Write(target, 666);
            Packer<long>.Write(target, 666);
            Packer<long>.Write(target, 666);
            Packer<long>.Write(target, 667);
            
            using var delta = BitPackerPool.Get();
            
            CreateDelta(origin, target, delta);
            Debug.Log(delta.ToByteData().ToString());

            using var result = BitPackerPool.Get();
            
            ApplyDelta(origin, delta, result);
            
            Debug.Log(target.ToByteData().ToString());
            Debug.Log(result.ToByteData().ToString());
        }*/
        
        public static void CreateDelta(BitPacker origin, BitPacker target, BitPacker result)
        {
            result.ResetPositionAndMode(false);
            
            var o = origin.ToByteData().span;
            var t = target.ToByteData().span;
            
            Fossil.Delta.Create(o, t, result);
        }
        
        public static void ApplyDelta(BitPacker origin, BitPacker delta, BitPacker result)
        {
            result.ResetPositionAndMode(false);
            
            var o = origin.ToByteData().span;
            var t = delta.ToByteData().span;
            
            delta.ResetPositionAndMode(true);
            
            Fossil.Delta.Apply(o, delta, t, result);
        }
    }
}
