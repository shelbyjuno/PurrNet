using PurrNet.Modules;
using PurrNet.Packing;

namespace PurrNet
{
    public static class PackNetworkIdentity
    {
        [UsedByIL]
        public static void RegisterIdentity<T>() where T : NetworkIdentity
        {
            Packer<T>.RegisterWriter(WriteIdentity);
            Packer<T>.RegisterReader(ReadIdentity);
        }
        
        [UsedByIL]
        public static void WriteIdentity<T>(this BitPacker packer, T value) where T : NetworkIdentity
        {
            if (value == null || !value.id.HasValue)
            {
                Packer<bool>.Write(packer, false);
                return;
            }

            Packer<bool>.Write(packer, true);
            Packer<NetworkID>.Write(packer, value.id.Value);
            Packer<SceneID>.Write(packer, value.sceneId);
        }

        [UsedByIL]
        public static void ReadIdentity<T>(this BitPacker packer, ref T value) where T : NetworkIdentity
        {
            bool hasValue = false;
            
            Packer<bool>.Read(packer, ref hasValue);

            if (!hasValue)
            {
                value = null;
                return;
            }
            
            NetworkID id = default;
            SceneID sceneId = default;
            
            Packer<NetworkID>.Read(packer, ref id);
            Packer<SceneID>.Read(packer, ref sceneId);
            
            var networkManager = NetworkManager.main;
            
            if (!networkManager)
            {
                value = null;
                return;
            }

            if (!networkManager.TryGetModule<HierarchyModule>(networkManager.isServer, out var module) ||
                !module.TryGetIdentity(sceneId, id, out var result) || result is not T identity)
            {
                value = null;
                return;
            }
            
            value = identity;
        }
    }
}