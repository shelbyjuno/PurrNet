using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet.Utils
{
    public class Hasher
    {
        static readonly Dictionary<Type, uint> _hashes = new Dictionary<Type, uint>();
        static readonly Dictionary<uint, Type> _decoder = new Dictionary<uint, Type>();
        
        static uint _hashCounter;
        
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
        
        [UsedImplicitly]
        public static uint PrepareType(Type type)
        {
            if (_hashes.TryGetValue(type, out var hash))
                return hash;
            
            hash = _hashCounter++;
            _hashes[type] = hash;
            _decoder[hash] = type;
            
            return hash;
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
            return GetStableHashU32(typeof(T));
        }
    }
}
