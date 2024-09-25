using System.Collections.Generic;

namespace PurrNet.Pooling
{
    public class ListPool<T> : GenericPool<List<T>>
    {
        private static readonly ListPool<T> _instance;

        static ListPool() => _instance = new ListPool<T>();

        static List<T> Factory() => new();

        static void Reset(List<T> list) => list.Clear();

        public ListPool() : base(Factory, Reset) { }
        
        public static int GetCount() => _instance.count;

        public static List<T> Instantiate() => _instance.Allocate();

        public static void Destroy(List<T> list) => _instance.Delete(list);
    }
    
    public class HashSetPool<T> : GenericPool<HashSet<T>>
    {
        private static readonly HashSetPool<T> _instance;

        static HashSetPool()
        {
            _instance = new HashSetPool<T>();
        }
        
        static HashSet<T> Factory()
        {
            return new HashSet<T>();
        }
        
        static void Reset(HashSet<T> list)
        {
            list.Clear();
        }
        
        public HashSetPool() : base(Factory, Reset) { }
        
        public static int GetCount()
        {
            return _instance.count;
        }
        
        public static HashSet<T> Instantiate()
        {
            return _instance.Allocate();
        }
        
        public static void Destroy(HashSet<T> list)
        {
            _instance.Delete(list);
        }
    }
}
