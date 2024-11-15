using System;
using System.Collections.Generic;
using System.Reflection;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Packing
{
    public delegate void WriteFunc<T>(BitPacker packer, T value);
        
    public delegate void ReadFunc<T>(BitPacker packer, ref T value);
    
    public static class Packer<T>
    {
        static readonly WriteFunc<T> _write;
        static readonly ReadFunc<T> _read;

        static Packer()
        {
            if (Packer.TryGetPacker<T>(out var helper))
            {
                _write = helper.WriteData;
                _read = helper.ReadData;
            }
            else throw new Exception($"No packer found for type '{typeof(T)}'.");
        }
        
        public static void Write(BitPacker packer, T value)
        {
            _write.Invoke(packer, value);
        }
        
        public static void Read(BitPacker packer, ref T value)
        {
            _read.Invoke(packer, ref value);
        }
    }
    
    public static class Packer
    {
        internal readonly struct PackerHelper
        {
            readonly IntPtr readerFuncPtr;
            readonly IntPtr writerFuncPtr;
            
            public PackerHelper(MethodInfo writerFuncPtr, MethodInfo readerFuncPtr)
            {
                this.readerFuncPtr = readerFuncPtr.MethodHandle.GetFunctionPointer();
                this.writerFuncPtr = writerFuncPtr.MethodHandle.GetFunctionPointer();
            }
            
            public void WriteData<T>(BitPacker packer, T value)
            {
                unsafe
                {
                    try
                    {
                        ((delegate* managed<BitPacker, T, void>)writerFuncPtr.ToPointer())(packer, value);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            
            public void ReadData<T>(BitPacker packer, ref T value)
            {
                unsafe
                {
                    try
                    {
                        ((delegate* managed<BitPacker, ref T, void>)readerFuncPtr.ToPointer())(packer, ref value);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }
        
        static readonly Dictionary<Type, PackerHelper> _packers = new ();
        
        public static void Register<T>(WriteFunc<T> a, ReadFunc<T> b)
        {
            if (_packers.TryAdd(typeof(T), new PackerHelper(a.Method, b.Method)))
                return;
            
            PurrLogger.LogError($"Packer for type '{typeof(T)}' already exists and cannot be overwritten.");
        }
        
        internal static bool TryGetPacker<T>(out PackerHelper helper)
        {
            if (_packers.TryGetValue(typeof(T), out helper))
                return true;
            return false;
        }
        
        public static void Write<T>(BitPacker packer, T value)
        {
            if (_packers.TryGetValue(typeof(T), out var helper))
                helper.WriteData(packer, value);
            else PurrLogger.LogError($"No packer found for type '{typeof(T)}'.");
        }
        
        public static void Read<T>(BitPacker packer, ref T value)
        {
            if (_packers.TryGetValue(typeof(T), out var helper))
                helper.ReadData(packer, ref value);
            else PurrLogger.LogError($"No packer found for type '{typeof(T)}'.");
        }
    }
}
