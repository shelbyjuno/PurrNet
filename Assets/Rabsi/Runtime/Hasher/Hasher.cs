using System;
using System.Collections.Generic;

namespace Rabsi.Utils
{
    public static class Hasher
    {
        private const uint FNV_offset_basis32 = 2166136261;
        private const uint FNV_prime32 = 16777619;
        
        static readonly Dictionary<Type, uint> _hashes = new ();
        static readonly Dictionary<uint, Type> _decoder = new ();
        
        static Type _lastType;
        static uint _lastHash;
        
        public static bool TryGetType(uint hash, out Type type)
        {
            return _decoder.TryGetValue(hash, out type);
        }

        public static uint GetStableHashU32(Type type)
        {
            if (_lastType == type)
                return _lastHash;

            if (_hashes.TryGetValue(type, out var hash))
            {
                _lastType = type;
                _lastHash = hash;
                return hash;
            }

            var value = GetStableHashU32(type.AssemblyQualifiedName);
            _hashes.Add(type, value);
            _decoder.Add(value, type);
            _lastType = type;
            _lastHash = value;
            return value;
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
    }
}
