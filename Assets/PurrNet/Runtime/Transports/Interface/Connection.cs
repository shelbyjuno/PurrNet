using System;
using PurrNet.Packets;

namespace PurrNet.Transports
{
    public partial struct Connection : IEquatable<Connection>, IAutoNetworkedData
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(connectionId, isValid);
        }

        public int connectionId { get; private set; }
        
        public bool isValid { get; private set; }

        public Connection(int connectionId)
        {
            this.connectionId = connectionId;
            isValid = true;
        }
        
        public static bool operator ==(Connection a, Connection b)
        {
            return a.connectionId == b.connectionId;
        }
        
        public static bool operator !=(Connection a, Connection b)
        {
            return a.connectionId != b.connectionId;
        }
        
        public override bool Equals(object obj)
        {
            return obj is Connection other && Equals(other);
        }
        
        public bool Equals(Connection other)
        {
            return connectionId == other.connectionId && isValid == other.isValid;
        }

        public override string ToString()
        {
            return connectionId.ToString("000");
        }
    }
}
