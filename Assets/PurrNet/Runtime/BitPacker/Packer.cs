using System;
using System.Collections.Generic;
using System.Reflection;
using PurrNet.Logging;

namespace PurrNet.Packing
{
    public delegate void WriteFunc<in T>(BitStream stream, T value);
        
    public delegate void ReadFunc<T>(BitStream stream, ref T value);
    
    public static class Packer<T>
    {
        static WriteFunc<T> _write;
        static ReadFunc<T> _read;

        public static void RegisterWriter(WriteFunc<T> a)
        {
            if (_write != null)
            {
                PurrLogger.LogError($"Writer for type '{typeof(T)}' is already registered.");
                return;
            }
            
            Packer.RegisterWriter(typeof(T), a.Method);
            _write = a;
        }
        
        public static void RegisterReader(ReadFunc<T> b)
        {
            if (_read != null)
            {
                PurrLogger.LogError($"Reader for type '{typeof(T)}' is already registered.");
                return;
            }
            
            Packer.RegisterReader(typeof(T), b.Method);
            _read = b;
        }
        
        public static void RegisterWriterSilent(WriteFunc<T> a)
        {
            Packer.RegisterWriterSilent(typeof(T), a.Method);
            _write = a;
        }
        
        public static void RegisterReaderSilent(ReadFunc<T> b)
        {
            Packer.RegisterReaderSilent(typeof(T), b.Method);
            _read = b;
        }
        
        public static void Write(BitStream stream, T value)
        {
            try
            {
                _write(stream, value);
            }
            catch (Exception e)
            {
                PurrLogger.LogError($"Failed to write value of type '{typeof(T)}'.\n{e.Message}\n{e.StackTrace}");
            }
        }
        
        public static void Read(BitStream stream, ref T value)
        {
            try
            {
                _read(stream, ref value);
            }
            catch (Exception e)
            {
                PurrLogger.LogError($"Failed to read value of type '{typeof(T)}'.\n{e.Message}\n{e.StackTrace}");
            }
        }
    }

    public static class Packer
    {
        static readonly Dictionary<Type, MethodInfo> _writeMethods = new ();
        static readonly Dictionary<Type, MethodInfo> _readMethods = new ();
        
        public static void RegisterWriter(Type type, MethodInfo method)
        {
            if (!_writeMethods.TryAdd(type, method))
                PurrLogger.LogError($"Writer for type '{type}' is already registered.");
        }
        
        public static void RegisterReader(Type type, MethodInfo method)
        {
            if (!_readMethods.TryAdd(type, method))
                PurrLogger.LogError($"Reader for type '{type}' is already registered.");
        }
        
        public static void RegisterWriterSilent(Type type, MethodInfo method)
        {
            _writeMethods[type] = method;
        }
        
        public static void RegisterReaderSilent(Type type, MethodInfo method)
        {
            _readMethods[type] = method;
        }
        
        static readonly object[] _args = new object[2];
        
        public static void Write(BitStream stream, object value)
        {
            var type = value.GetType();
            
            if (!_writeMethods.TryGetValue(type, out var method))
            {
                PurrLogger.LogError($"No writer for type '{type}' is registered.");
                return;
            }
            
            try
            {
                _args[0] = stream;
                _args[1] = value;
                method.Invoke(null, _args);
            }
            catch (Exception e)
            {
                PurrLogger.LogError($"Failed to write value of type '{type}'.\n{e.Message}\n{e.StackTrace}");
            }
        }
        
        public static void Read(BitStream stream, Type type, ref object value)
        {
            if (!_readMethods.TryGetValue(type, out var method))
            {
                PurrLogger.LogError($"No reader for type '{type}' is registered.");
                return;
            }
            
            try
            {
                _args[0] = stream;
                _args[1] = value;
                method.Invoke(null, _args);
            }
            catch (Exception e)
            {
                PurrLogger.LogError($"Failed to read value of type '{type}'.\n{e.Message}\n{e.StackTrace}");
            }
        }
    }
}
