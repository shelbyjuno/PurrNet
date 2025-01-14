using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackNetworkID
    {
        [UsedByIL]
        public static void Write(BitPacker packer, SceneID value)
        {
            Packer<PackedUshort>.Write(packer, new PackedUshort(value.id));
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref SceneID value)
        {
            PackedUshort id = default;
            Packer<PackedUshort>.Read(packer, ref id);
            value = new SceneID(id);
        }
        
        [UsedByIL]
        public static void Write(BitPacker packer, NetworkID value)
        {
            Packer<PackedInt>.Write(packer, new PackedInt(value.id));
            Packer<PlayerID>.Write(packer, value.scope);
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref NetworkID value)
        {
            PackedInt id = default;
            PlayerID scope = default;
            
            Packer<PackedInt>.Read(packer, ref id);
            Packer<PlayerID>.Read(packer, ref scope);
            
            value = new NetworkID(id, scope);
        }
        
        [UsedByIL]
        public static void Write(BitPacker packer, PlayerID value)
        {
            Packer<PackedUshort>.Write(packer, new PackedUshort(value.id));
            Packer<bool>.Write(packer, value.isBot);
        }

        [UsedByIL]
        public static void Read(BitPacker packer, ref PlayerID value)
        {
            PackedUshort id = default;
            bool isBot = default;
            
            Packer<PackedUshort>.Read(packer, ref id);
            Packer<bool>.Read(packer, ref isBot);
            
            value = new PlayerID(id, isBot);
        }
    }
}
