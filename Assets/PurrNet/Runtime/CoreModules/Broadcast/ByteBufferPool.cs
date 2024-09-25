using PurrNet.Pooling;

namespace PurrNet.Packets
{
    public class ByteBufferPool : GenericPool<ByteBuffer>
    {
        static readonly ByteBufferPool _pool;
        
        static ByteBufferPool()
        {
            _pool = new ByteBufferPool();
        }
        
        static ByteBuffer Create()
        {
            return new ByteBuffer();
        }
        
        static void Reset(ByteBuffer buffer)
        {
            buffer.Clear();
        }

        public ByteBufferPool() : base(Create, Reset) { }

        public static ByteBuffer Alloc()
        {
            return _pool.Allocate();
        }
        
        public static void Free(ByteBuffer buffer)
        {
            _pool.Delete(buffer);
        }
    }
}
