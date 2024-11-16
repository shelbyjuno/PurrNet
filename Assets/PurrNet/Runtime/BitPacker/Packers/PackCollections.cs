using System;
using System.Collections.Generic;
using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackCollections
    {
        static void Read(BitStream stream, ref NetAnimatorAction value, int t)
        {
            byte value2 = default(byte);
            Packer<byte>.Read(stream, ref value2);
            value = (NetAnimatorAction)value2;
        }
        
        [UsedByIL]
        public static void RegisterList<T>()
        {
            Packer.RegisterWriter<List<T>>(WriteList);
            Packer.RegisterReader<List<T>>(ReadList);
        }
        
        [UsedByIL]
        public static void RegisterArray<T>()
        {
            Packer.RegisterWriter<T[]>(WriteList);
            Packer.RegisterReader<T[]>(ReadArray);
        }
        
        [UsedByIL]
        public static void WriteList<T>(this BitStream stream, IList<T> value)
        {
            if (value == null)
            {
                Packer<int>.Write(stream, -1);
                return;
            }
            
            int length = value.Count;
            Packer<int>.Write(stream, length);
            
            for (int i = 0; i < length; i++)
                Packer<T>.Write(stream, value[i]);
        }

        [UsedByIL]
        public static void ReadList<T>(this BitStream stream, ref List<T> value)
        {
            int length = default;
            
            Packer<int>.Read(stream, ref length);
            
            if (length == -1)
            {
                value = null;
                return;
            }
            
            if (value == null)
                 value = new List<T>(length);
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
            int length = default;
            
            Packer<int>.Read(stream, ref length);
            
            if (length == -1)
            {
                value = null;
                return;
            }
            
            if (value == null)
                value = new T[length];
            else if (value.Length != length)
                Array.Resize(ref value, length);
            
            for (int i = 0; i < length; i++)
                Packer<T>.Read(stream, ref value[i]);
        }
    }
}