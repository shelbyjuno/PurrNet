using System;
using JetBrains.Annotations;

namespace PurrNet.Packing
{
    public partial class BitStream
    {
        [UsedImplicitly]
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

        [UsedImplicitly]
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