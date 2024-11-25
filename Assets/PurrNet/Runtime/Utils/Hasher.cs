using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet.Utils
{
    public class Hasher
    {
        private const uint FNV_offset_basis32 = 2166136261;
        private const uint FNV_prime32 = 16777619;
        
        private const ulong FNV_offset_basis64 = 14695981039346656037;
        private const ulong FNV_prime64 = 1099511628211;
        
        static readonly Dictionary<Type, uint> _hashes = new ();
        static readonly Dictionary<uint, Type> _decoder = new ();
        
        static readonly Dictionary<Type, ulong> _hashes_64 = new ();
        static readonly Dictionary<ulong, Type> _decoder_64 = new ();
        
        public static Type ResolveType(uint hash)
        {
            if (_decoder.TryGetValue(hash, out var type))
                return type;
            
            throw new InvalidOperationException(
                PurrLogger.FormatMessage($"Type with hash '{hash}' not found.")
            );
        }
        
        public static bool TryGetType(uint hash, out Type type)
        {
            return _decoder.TryGetValue(hash, out type);
        }
        
        public static bool TryGetType(ulong hash, out Type type)
        {
            return _decoder_64.TryGetValue(hash, out type);
        }

        [UsedImplicitly]
        public static uint PrepareType(Type type)
        {
            if (_hashes.TryGetValue(type, out var hash))
                return hash;
            
            var value = GetStableHashU32(type.FullName);
            _hashes.Add(type, value);
            
            if (_decoder.TryGetValue(value, out var otherType))
            {
                throw new InvalidOperationException(
                    PurrLogger.FormatMessage($"Hash of '{type.FullName}' is already registered with a different type '{otherType.FullName}'.")
                );
            }
            
            _decoder.Add(value, type);
            
            return value;
        }
        
        static ulong PrepareType_64(Type type)
        {
            if (_hashes_64.TryGetValue(type, out var hash))
                return hash;
            
            var value = GetStableHashU64(type.FullName);
            _hashes_64.Add(type, value);
            
            if (_decoder_64.TryGetValue(value, out var otherType))
            {
                throw new InvalidOperationException(
                    PurrLogger.FormatMessage($"Hash of '{type.FullName}' is already registered with a different type '{otherType.FullName}'.")
                );
            }
            
            _decoder_64.Add(value, type);
            
            return value;
        }
        
        [UsedByIL]
        public static void PrepareType<T>() => PrepareType(typeof(T));

        public static uint GetStableHashU32(Type type)
        {
            return _hashes.TryGetValue(type, out var hash) ? hash : throw new InvalidOperationException(
                PurrLogger.FormatMessage($"Type '{type.FullName}' is not registered.")
            );
        }
        
        public static uint GetStableHashU32<T>()
        {
            var type = typeof(T);
            return _hashes.TryGetValue(type, out var hash) ? hash : throw new InvalidOperationException(
                PurrLogger.FormatMessage($"Type '{type.FullName}' is not registered.")
            );
        }
        
        static uint GetStableHashU32(string txt)
        {
            unchecked
            {
                uint hash = FNV_offset_basis32;
                for (int i = 0; i < txt.Length; i++)
                {
                    uint ch = txt[i];
                    hash *= FNV_prime32;
                    hash ^= ch;
                }

                return hash;
            }
        }
        
        public static ulong GetStableHashU64(Type type)
        {
            return _hashes_64.TryGetValue(type, out var hash) ? hash : PrepareType_64(type);
        }
        
        static ulong GetStableHashU64(string txt)
        {
            unchecked
            {
                ulong hash = FNV_offset_basis64;
                for (int i = 0; i < txt.Length; i++)
                {
                    ulong ch = txt[i];
                    hash *= FNV_prime64;
                    hash ^= ch;
                }

                return hash;
            }
        }
    }
}
