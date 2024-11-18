using PurrNet.Modules;
using PurrNet.Packing;

namespace PurrNet
{
    public static class PackNetworkIdentity
    {
        [UsedByIL]
        public static void RegisterIdentity<T>() where T : NetworkIdentity
        {
            Packer.RegisterWriterSilent<T>(WriteIdentity);
            Packer.RegisterReaderSilent<T>(ReadIdentity);
        }

        [UsedByIL]
        public static void WriteIdentity(this BitStream stream, NetworkIdentity value)
        {
            if (value == null || !value.id.HasValue)
            {
                Packer<bool>.Write(stream, false);
                return;
            }

            Packer.Write(stream, true);
            Packer.Write(stream, value.id.Value);
            Packer.Write(stream, value.sceneId);
        }

        [UsedByIL]
        public static void ReadIdentity<T>(this BitStream stream, ref T value) where T : NetworkIdentity
        {
            bool hasValue = false;
            stream.Read(ref hasValue);

            if (!hasValue)
            {
                value = null;
                return;
            }
            
            NetworkID id = default;
            SceneID sceneId = default;
            
            Packer<NetworkID>.Read(stream, ref id);
            Packer<SceneID>.Read(stream, ref sceneId);
        }
    }
}