using System;
using System.Collections.Generic;
using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackCollections
    {
        [UsedByIL]
        public static void RegisterNullable<T>() where T : struct
        {
            Packer<T?>.RegisterWriter(WriteNullable);
            Packer<T?>.RegisterReader(ReadNullable);
        }

        private static void WriteNullable<T>(BitPacker packer, T? value) where T : struct
        {
            if (!value.HasValue)
            {
                Packer<bool>.Write(packer, false);
                return;
            }
            
            Packer<bool>.Write(packer, true);
            Packer<T>.Write(packer, value.Value);
        }
        
        private static void ReadNullable<T>(BitPacker packer, ref T? value) where T : struct
        {
            bool hasValue = default;
            packer.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            T val = default;
            Packer<T>.Read(packer, ref val);
            value = val;
        }


        [UsedByIL]
        public static void RegisterDictionary<TKey, TValue>()
        {
            Packer<Dictionary<TKey, TValue>>.RegisterWriter(WriteDictionary);
            Packer<Dictionary<TKey, TValue>>.RegisterReader(ReadDictionary);
        }

        private static void WriteDictionary<K, V>(BitPacker packer, Dictionary<K, V> value)
        {
            if (value == null)
            {
                Packer<bool>.Write(packer, false);
                return;
            }
            
            Packer<bool>.Write(packer, true);

            int length = value.Count;
            packer.WriteInteger(length, 31);
            
            foreach (var pair in value)
            {
                Packer<K>.Write(packer, pair.Key);
                Packer<V>.Write(packer, pair.Value);
            }
        }

        private static void ReadDictionary<K, V>(BitPacker packer, ref Dictionary<K, V> value)
        {
            bool hasValue = default;
            packer.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            packer.ReadInteger(ref length, 31);
            
            if (value == null)
                value = new Dictionary<K, V>((int)length);
            else value.Clear();
            
            for (int i = 0; i < length; i++)
            {
                K key = default;
                V val = default;
                Packer<K>.Read(packer, ref key);
                Packer<V>.Read(packer, ref val);
                value.Add(key, val);
            }
        }

        [UsedByIL]
        public static void RegisterHashSet<T>()
        {
            Packer<HashSet<T>>.RegisterWriter(WriteCollection);
            Packer<HashSet<T>>.RegisterReader(ReadHashSet);
        }
        
        [UsedByIL]
        public static void RegisterList<T>()
        {
            Packer<List<T>>.RegisterWriter(WriteList);
            Packer<List<T>>.RegisterReader(ReadList);
        }
        
        [UsedByIL]
        public static void RegisterArray<T>()
        {
            Packer<T[]>.RegisterWriter(WriteList);
            Packer<T[]>.RegisterReader(ReadArray);
        }
        
        [UsedByIL]
        public static void WriteCollection<T>(this BitPacker packer, ICollection<T> value)
        {
            if (value == null)
            {
                Packer<bool>.Write(packer, false);
                return;
            }
            
            Packer<bool>.Write(packer, true);

            int length = value.Count;
            packer.WriteInteger(length, 31);

            foreach (var v in value)
                Packer<T>.Write(packer, v);
        }
        
        [UsedByIL]
        public static void ReadHashSet<T>(this BitPacker packer, ref HashSet<T> value)
        {
            bool hasValue = default;
            packer.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            packer.ReadInteger(ref length, 31);
            
            if (value == null)
                value = new HashSet<T>((int)length);
            else value.Clear();
            
            for (int i = 0; i < length; i++)
            {
                T item = default;
                Packer<T>.Read(packer, ref item);
                value.Add(item);
            }
        }
        
        [UsedByIL]
        public static void WriteList<T>(this BitPacker packer, IList<T> value)
        {
            if (value == null)
            {
                Packer<bool>.Write(packer, false);
                return;
            }
            
            Packer<bool>.Write(packer, true);

            int length = value.Count;
            packer.WriteInteger(length, 31);
            
            for (int i = 0; i < length; i++)
                Packer<T>.Write(packer, value[i]);
        }

        [UsedByIL]
        public static void ReadList<T>(this BitPacker packer, ref List<T> value)
        {
            bool hasValue = default;
            packer.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            packer.ReadInteger(ref length, 31);
            
            if (value == null)
                 value = new List<T>((int)length);
            else value.Clear();
            
            for (int i = 0; i < length; i++)
            {
                T item = default;
                Packer<T>.Read(packer, ref item);
                value.Add(item);
            }
        }

        [UsedByIL]
        public static void ReadArray<T>(this BitPacker packer, ref T[] value)
        {
            bool hasValue = default;
            packer.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            packer.ReadInteger(ref length, 31);
            
            if (length == -1)
            {
                value = null;
                return;
            }
            
            if (value == null)
                value = new T[length];
            else if (value.Length != length)
                Array.Resize(ref value, (int)length);
            
            for (int i = 0; i < length; i++)
                Packer<T>.Read(packer, ref value[i]);
        }
    }
}