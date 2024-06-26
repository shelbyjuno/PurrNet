using System;
using Rabsi.Packets;

namespace Rabsi
{
    public readonly struct PlayerID : INetworkedData, IEquatable<PlayerID>
    {
        private readonly uint id;
        
        public PlayerID(uint id)
        {
            this.id = id;
        }
        
        public override int GetHashCode()
        {
            return (int)id;
        }
        
        public bool Equals(PlayerID other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerID other && Equals(other);
        }
        
        public static bool operator ==(PlayerID a, PlayerID b)
        {
            return a.id == b.id;
        }

        public static bool operator !=(PlayerID a, PlayerID b)
        {
            return a.id != b.id;
        }
    }
}
