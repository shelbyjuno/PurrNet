using System;
using System.Collections.Generic;
using System.Reflection;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Packing
{
    public delegate void WriteFunc<in T>(BitStream stream, T value);
        
    public delegate void ReadFunc<T>(BitStream stream, ref T value);
    
    public static class Packer<T>
    {
        static readonly WriteFunc<T> _write;
        static readonly ReadFunc<T> _read;

        static Packer()
        {
            if (Packer.TryGetReader<T>(out var reader))
            {
                _read = reader.ReadData;
            }
            else throw new Exception($"No reader found for type '{typeof(T)}'.");

            
            if (Packer.TryGetWriter<T>(out var writer))
            {
                _write = writer.WriteData;
            }
            else throw new Exception($"No writer found for type '{typeof(T)}'.");
        }
        
        public static void Write(BitStream stream, T value)
        {
            _write.Invoke(stream, value);
        }
        
        public static void Read(BitStream stream, ref T value)
        {
            _read.Invoke(stream, ref value);
        }
    }
    
    public static class Packer
    {
        internal readonly unsafe struct PackerHelper
        {
            readonly void* funcPtr;
            
            public PackerHelper(MethodInfo funcPtr)
            {
                this.funcPtr = funcPtr.MethodHandle.GetFunctionPointer().ToPointer();
            }
            
            public void WriteData<T>(BitStream stream, T value)
            {
                try
                {
                    ((delegate* managed<BitStream, T, void>)funcPtr)(stream, value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            
            public void ReadData<T>(BitStream stream, ref T value)
            {
                try
                {
                    ((delegate* managed<BitStream, ref T, void>)funcPtr)(stream, ref value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
        
        static readonly Dictionary<Type, PackerHelper> _writers = new ();
        static readonly Dictionary<Type, PackerHelper> _readers = new ();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Clear();
        }

        private static void Clear()
        {
            _writers.Clear();
            _readers.Clear();
        }

        public static void RegisterWriter<T>(WriteFunc<T> a)
        {
            if (_writers.TryAdd(typeof(T), new PackerHelper(a.Method)))
                return;
            
            PurrLogger.LogError($"Writer for type '{typeof(T)}' already exists and cannot be overwritten.");
        }
        
        public static void RegisterReader<T>(ReadFunc<T> b)
        {
            if (_readers.TryAdd(typeof(T), new PackerHelper(b.Method)))
                return;
            
            PurrLogger.LogError($"Reader for type '{typeof(T)}' already exists and cannot be overwritten.");
        }
        
        internal static bool TryGetReader<T>(out PackerHelper helper)
        {
            if (_readers.TryGetValue(typeof(T), out helper))
                return true;
            return false;
        }
        
        internal static bool TryGetWriter<T>(out PackerHelper helper)
        {
            if (_writers.TryGetValue(typeof(T), out helper))
                return true;
            return false;
        }
        
        public static void Write<T>(BitStream stream, T value)
        {
            if (_writers.TryGetValue(typeof(T), out var helper))
                helper.WriteData(stream, value);
            else PurrLogger.LogError($"No packer found for type '{typeof(T)}'.");
        }
        
        public static void Read<T>(BitStream stream, ref T value)
        {
            if (_readers.TryGetValue(typeof(T), out var helper))
                helper.ReadData(stream, ref value);
            else PurrLogger.LogError($"No packer found for type '{typeof(T)}'.");
        }
    }
}
