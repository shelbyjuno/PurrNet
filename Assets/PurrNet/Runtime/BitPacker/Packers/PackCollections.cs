using System;
using System.Collections.Generic;
using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackCollections
    {
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
        public static void WriteList<T>(this BitStream stream, IList<T> value)
        {
            if (value == null)
            {
                Packer<bool>.Write(stream, false);
                return;
            }
            
            Packer<bool>.Write(stream, true);

            int length = value.Count;
            stream.WriteInteger(length, 31);
            
            for (int i = 0; i < length; i++)
                Packer<T>.Write(stream, value[i]);
        }

        [UsedByIL]
        public static void ReadList<T>(this BitStream stream, ref List<T> value)
        {
            bool hasValue = default;
            stream.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            stream.ReadInteger(ref length, 31);
            
            if (value == null)
                 value = new List<T>((int)length);
            else value.Clear();
            
            for (int i = 0; i < length; i++)
            {
                T item = default;
                Packer<T>.Read(stream, ref item);
                value.Add(item);
            }
        }

        [UsedByIL]
        public static void ReadArray<T>(this BitStream stream, ref T[] value)
        {
            bool hasValue = default;
            stream.Read(ref hasValue);
            
            if (!hasValue)
            {
                value = null;
                return;
            }
            
            long length = default;
            
            stream.ReadInteger(ref length, 31);
            
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
                Packer<T>.Read(stream, ref value[i]);
        }
    }
}