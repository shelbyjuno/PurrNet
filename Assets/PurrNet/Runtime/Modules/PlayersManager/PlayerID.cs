using System;
using PurrNet.Packets;

namespace PurrNet
{
    public readonly partial struct PlayerID : IAutoNetworkedData, IEquatable<PlayerID>
    {
        private uint _id { get; }

        public bool isBot { get; }
        
        public uint id => _id;

        public PlayerID(uint id, bool isBot)
        {
            _id = id;
            this.isBot = isBot;
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
    
    public readonly partial struct SceneID : IAutoNetworkedData, IEquatable<SceneID>
    {
        private ushort _id { get; }

        public ushort id => _id;

        public SceneID(ushort id)
        {
            _id = id;
        }

        public override string ToString()
        {
            return _id.ToString("000");
        }

        public override int GetHashCode()
        {
            return _id;
        }

        public bool Equals(SceneID other)
        {
            return _id == other._id;
        }

        public override bool Equals(object obj)
        {
            return obj is SceneID other && Equals(other);
        }
        
        public static bool operator ==(SceneID a, SceneID b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(SceneID a, SceneID b)
        {
            return a._id != b._id;
        }
    }
}
