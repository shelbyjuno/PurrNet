using System.Collections.Generic;

namespace PurrNet.Pooling
{
    public class ListPool<T> : GenericPool<List<T>>
    {
        private static readonly ListPool<T> _instance;

        static ListPool()
        {
            _instance = new ListPool<T>();
        }
        
        static List<T> Factory()
        {
            return new List<T>();
        }
        
        static void Reset(List<T> list)
        {
            list.Clear();
        }
        
        public ListPool() : base(Factory, Reset) { }
        
        public static int GetCount()
        {
            return _instance.count;
        }
        
        public static List<T> New()
        {
            return _instance.Allocate();
        }
        
        public static void Destroy(List<T> list)
        {
            _instance.Free(list);
        }
    }
}
