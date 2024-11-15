using System;

namespace PurrNet.Packing
{
    public partial class BitPacker
    {
        public void Write(bool data)
        {
            WriteBits(data ? 1u : 0u, 1);
        }
        
        public void Read(ref bool data)
        {
            data = ReadBits(1) == 1;
        }


        public void Write(byte data)
        {
            WriteBits(data, 8);
        }
        
        public void Read(ref byte data)
        {
            data = (byte)ReadBits(8);
        }

        public void Write(sbyte data)
        {
            WriteBits((ulong)data, 8);
        }
        
        public void Read(ref sbyte data)
        {
            data = (sbyte)ReadBits(8);
        }

        public void Write(short data)
        {
            WriteBits((ulong)data, 16);
        }
        
        public void Read(ref short data)
        {
            data = (short)ReadBits(16);
        }

        public void Write(ushort data)
        {
            WriteBits(data, 16);
        }
        
        public void Read(ref ushort data)
        {
            data = (ushort)ReadBits(16);
        }
        
        public void Write(int data)
        {
            WriteBits((ulong)data, 32);
        }
        
        public void Read(ref int data)
        {
            data = (int)ReadBits(32);
        }
        
        public void Write(uint data)
        {
            WriteBits(data, 32);
        }
        
        public void Read(ref uint data)
        {
            data = (uint)ReadBits(32);
        }
        
        public void Write(long data)
        {
            WriteBits((ulong)data, 64);
        }
        
        public void Read(ref long data)
        {
            data = (long)ReadBits(64);
        }

        public void Pack(ref long data, long minValue, long maxValue = long.MaxValue)
        {
            // Ensure min/max values are in the correct order
            if (maxValue < minValue)
                (minValue, maxValue) = (maxValue, minValue);

            if (minValue == long.MinValue && maxValue == long.MaxValue)
            {
                if (_isReading)
                    data = (long)ReadBits(64);
                else
                    WriteBits((ulong)data, 64);
                return;
            }

            // Calculate the range and bits needed
            var range = (ulong)(maxValue - minValue);
            var bitsNeeded = (int)Math.Ceiling(Math.Log(range + 1, 2));

            // Ensure sufficient space in the buffer
            EnsureBitsExist(bitsNeeded);

            if (_isReading)
            {
                var result = ReadBits((byte)bitsNeeded);
                data = (long)result + minValue;
            }
            else
            {
                WriteBits((ulong)(data - minValue), (byte)bitsNeeded);
            }
        }

        public void Write(ulong data)
        {
            WriteBits(data, 64);
        }
        
        public void Read(ref ulong data)
        {
            data = ReadBits(64);
        }

        public void Pack(ref ulong data, ulong minValue, ulong maxValue = ulong.MaxValue)
        {
            // Ensure min/max values are in the correct order
            if (maxValue < minValue)
                (minValue, maxValue) = (maxValue, minValue);

            if (minValue == ulong.MinValue && maxValue == ulong.MaxValue)
            {
                if (_isReading)
                    data = ReadBits(64);
                else
                    WriteBits(data, 64);
                return;
            }

            // Calculate the range and bits needed
            var range = maxValue - minValue;
            var bitsNeeded = (int)Math.Ceiling(Math.Log(range + 1, 2));

            // Ensure sufficient space in the buffer
            EnsureBitsExist(bitsNeeded);

            if (_isReading)
            {
                var result = ReadBits((byte)bitsNeeded);
                data = result + minValue;
            }
            else
            {
                WriteBits(data - minValue, (byte)bitsNeeded);
            }
        }
    }
}