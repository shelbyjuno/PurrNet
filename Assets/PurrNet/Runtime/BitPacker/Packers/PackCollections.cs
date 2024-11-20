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