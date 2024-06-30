using System.Collections.Generic;

namespace PurrNet.Packets
{
    public static class ByteBufferPool
    {
        static readonly Queue<ByteBuffer> _pool = new ();
        
        public static ByteBuffer Alloc()
        {
            return _pool.Count > 0 ? _pool.Dequeue() : new ByteBuffer();
        }
        
        public static void Free(ByteBuffer buffer)
        {
            buffer.Clear();
            _pool.Enqueue(buffer);
        }
    }
}
