using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Utils;

namespace PurrNet
{
    public static class PackGenericObject
    {
        [UsedByIL]
        public static void WriteObject(this BitStream stream, object value)
        {
            PurrLogger.Log("WriteObject");
            bool isNull = value == null;
            
            stream.Write(isNull);
            
            if (isNull)
                return;

            var hash = Hasher.GetStableHashU32(value.GetType());
            
            stream.Write(hash);
            
            Packer.Write(stream, value);
        }

        [UsedByIL]
        public static void ReadObject(this BitStream stream, ref object value)
        {
            PurrLogger.Log("ReadObject");

            bool isNull = false;
            
            stream.Read(ref isNull);
            
            if (isNull)
            {
                value = null;
                return;
            }
            
            uint hash = 0;
            
            stream.Read(ref hash);
            
            if (!Hasher.TryGetType(hash, out var type))
                return;
            
            Packer.Read(stream, type, ref value);
        }
    }
}
