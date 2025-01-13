using System;
using JetBrains.Annotations;

namespace PurrNet
{
    public struct NetworkID : IEquatable<NetworkID>
    {
        [UsedImplicitly] private PlayerID _scope;
        [UsedImplicitly] private int _id;
        
        public int id => _id;

        public PlayerID scope => _scope;
        
        public NetworkID(NetworkID baseId, int offset)
        {
            _id = baseId._id + offset;
            _scope = baseId._scope;
        }
        
        public NetworkID(int id, PlayerID scope = default)
        {
            _id = id;
            _scope = scope;
        }
        
        public override string ToString()
        {
            return $"{_scope}:{_id}";
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_scope, _id);
        }

        public bool Equals(NetworkID other)
        {
            return _scope == other._scope && _id == other._id;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkID other && Equals(other);
        }
        
        public static bool operator ==(NetworkID a, NetworkID b)
        {
            return a.Equals(b);
        }
        
        public static bool operator !=(NetworkID a, NetworkID b)
        {
            return !a.Equals(b);
        }
    }
}