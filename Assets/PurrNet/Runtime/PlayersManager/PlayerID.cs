using System;
using PurrNet.Packets;

namespace PurrNet
{
    public partial struct PlayerID : IAutoNetworkedData, IEquatable<PlayerID>
    {
        private uint _id;
        
        public PlayerID(uint id)
        {
            _id = id;
        }

        public override string ToString()
        {
            return _id.ToString("000");
        }

        public override int GetHashCode()
        {
            return (int)_id;
        }

        public bool Equals(PlayerID other)
        {
            return _id == other._id;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerID other && Equals(other);
        }
        
        public static bool operator ==(PlayerID a, PlayerID b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(PlayerID a, PlayerID b)
        {
            return a._id != b._id;
        }
    }
}
