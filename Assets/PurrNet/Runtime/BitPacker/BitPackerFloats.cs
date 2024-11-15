using System;

namespace PurrNet.Packing
{
    public partial class BitPacker : IPack<Half>, IPack<float>, IPack<double>
    {
        public void Write(Half half)
        {
            Write(half.Value);
        }
        
        public void Read(ref Half half)
        {
            Read(ref half.Value);
        }
        
        public void Write(float data)
        {
            WriteBits((ulong)BitConverter.SingleToInt32Bits(data), 32);
        }
        
        public void Read(ref float data)
        {
            data = BitConverter.Int32BitsToSingle((int)ReadBits(32));
        }
        
        public void Write(double data)
        {
            WriteBits((ulong)BitConverter.DoubleToInt64Bits(data), 64);
        }
        
        public void Read(ref double data)
        {
            data = BitConverter.Int64BitsToDouble((long)ReadBits(64));
        }
    }
}
