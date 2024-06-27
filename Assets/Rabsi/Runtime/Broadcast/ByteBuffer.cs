using System;
using System.Buffers;
using Rabsi.Transports;

namespace Rabsi.Packets
{
    public interface IByteBuffer : IBufferWriter<byte>
    {
        int pointer { get; }

        void Write(byte data);
    }
    
    public class ByteBuffer : IByteBuffer
    {
        private byte[] _buffer;

        public int pointer { get; private set; }
        
        public ByteBuffer(int initialCapacity = 2048)
        {
            _buffer = new byte[initialCapacity];
            pointer = 0;
        }
        
        public void Clear()
        {
            pointer = 0;
        }

        public void Write(ByteData data)
        {
            EnsureCapacity(data.length);
            data.data.CopyTo(_buffer.AsSpan(pointer));
            pointer += data.length;
        }
        
        public byte ReadByte()
        {
            return _buffer[pointer++];
        }

        public void Write(byte data)
        {
            EnsureCapacity(1);
            _buffer[pointer++] = data;
        }

        public void Advance(int count)
        {
            EnsureCapacity(count);
            pointer += count;
        }
        
        public ByteData ToByteData()
        {
            return new ByteData(_buffer, 0, pointer);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(pointer);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(pointer);
        }
        
        public Span<byte> GetSpanWithSize(int targetSize)
        {
            EnsureCapacity(targetSize);
            return _buffer.AsSpan(pointer, targetSize);
        }
        
        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                return;

            if (_buffer.Length - pointer < sizeHint)
            {
                int newSize = Math.Max(_buffer.Length * 2, pointer + sizeHint);
                Array.Resize(ref _buffer, newSize);
            }
        }

        public void ResetPointer()
        {
            pointer = 0;
        }
    }
}
