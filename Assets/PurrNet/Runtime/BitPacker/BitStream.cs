using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Transports;

namespace PurrNet.Packing
{
    [UsedImplicitly]
    public partial class BitStream : IDisposable
    {
        private byte[] _buffer;
        private int _positionInBits;
        private bool _isReading;

        public int length
        {
            get
            {
                int pos = _positionInBits / 8;
                int len = pos + (_positionInBits % 8 == 0 ? 0 : 1);
                return len;
            }
        }
        
        public bool isReading => _isReading;
        
        public bool isWriting => !_isReading;
        
        public BitStream(int initialSize = 1024)
        {
            _buffer = new byte[initialSize];
        }

        public void Dispose()
        {
            BitPackerPool.Destroy(this);
        }
        
        public ByteData ToByteData()
        {
            return new ByteData(_buffer, 0, length);
        }
        
        public void ResetPosition()
        {
            _positionInBits = 0;
        }
        
        public void ResetMode(bool readMode)
        {
            _isReading = readMode;
        }
        
        public void ResetPositionAndMode(bool readMode)
        {
            _positionInBits = 0;
            _isReading = readMode;
        }
        
        private void EnsureBitsExist(int bits)
        {
            int targetPos = (_positionInBits + bits) / 8;

            if (targetPos >= _buffer.Length)
            {
                if (_isReading)
                    throw new IndexOutOfRangeException("Not enough bits in the buffer.");
                Array.Resize(ref _buffer, _buffer.Length * 2);
            }
        }
        
        public void WriteBits(ulong data, byte bits)
        {
            EnsureBitsExist(bits);
            
            if (bits > 64)
                throw new ArgumentOutOfRangeException(nameof(bits), "Cannot write more than 64 bits at a time.");
            
            int bitsLeft = bits;

            while (bitsLeft > 0)
            {
                int bytePos = _positionInBits / 8;
                int bitOffset = _positionInBits % 8;
                int bitsToWrite = Math.Min(bitsLeft, 8 - bitOffset);

                byte mask = (byte)((1 << bitsToWrite) - 1);
                byte value = (byte)((data >> (bits - bitsLeft)) & mask);

                _buffer[bytePos] &= (byte)~(mask << bitOffset); // Clear the bits to be written
                _buffer[bytePos] |= (byte)(value << bitOffset); // Set the bits

                bitsLeft -= bitsToWrite;
                _positionInBits += bitsToWrite;
            }
        }

        public ulong ReadBits(byte bits)
        {
            if (bits > 64)
                throw new ArgumentOutOfRangeException(nameof(bits), "Cannot read more than 64 bits at a time.");
            
            ulong result = 0;
            int bitsLeft = bits;

            while (bitsLeft > 0)
            {
                int bytePos = _positionInBits / 8;
                int bitOffset = _positionInBits % 8;
                int bitsToRead = Math.Min(bitsLeft, 8 - bitOffset);

                byte mask = (byte)((1 << bitsToRead) - 1);
                byte value = (byte)((_buffer[bytePos] >> bitOffset) & mask);

                result |= (ulong)value << (bits - bitsLeft);

                bitsLeft -= bitsToRead;
                _positionInBits += bitsToRead;
            }

            return result;
        }
        
        public void Write(IData data)
        {
            data.Write(this);
        }
        
        public void Read(ref IData data)
        {
            data.Read(this);
        }
        
        public void Write(ISimpleData data)
        {
            data.Pack(this);
        }
        
        public void Read(ref ISimpleData data)
        {
            data.Pack(this);
        }

        public void ReadBytes(IList<byte> bytes)
        {
            EnsureBitsExist(bytes.Count * 8);
            
            for (int i = 0; i < bytes.Count; i++)
                bytes[i] = (byte)ReadBits(8);
        }
        
        public void WriteBytes(IReadOnlyList<byte> bytes)
        {
            EnsureBitsExist(bytes.Count * 8);
            
            for (int i = 0; i < bytes.Count; i++)
                WriteBits(bytes[i], 8);
        }
    }
}
