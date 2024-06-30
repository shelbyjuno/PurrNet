using System;
using System.Buffers;
using PurrNet.Transports;

namespace PurrNet.Packets
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
        
        public void Write(Span<byte> data)
        {
            EnsureCapacity(data.Length);
            
            for (int i = 0; i < data.Length; i++)
                _buffer[pointer + i] = data[i];
            
            pointer += data.Length;
        }

        public void Write(ByteData data)
        {
            EnsureCapacity(data.length);
            
            for (int i = 0; i < data.length; i++)
                _buffer[pointer + i] = data.data[i + data.offset];
            
            pointer += data.length;
        }
        
        public byte ReadByte()
        {
            return _buffer[pointer++];
        }

        public void Write(byte data)
        {
            EnsureCapacity(1);
            _buffer[pointer] = data;
            pointer++;
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
