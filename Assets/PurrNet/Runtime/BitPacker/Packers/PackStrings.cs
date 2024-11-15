using PurrNet.Modules;

namespace PurrNet.Packing
{
    public static class PackStrings
    {
        [UsedByIL]
        public static void Write(this BitStream stream, string value)
        {
            int strLen = value.Length;
            
            stream.Write(strLen);
            
            for (int i = 0; i < strLen; i++)
                stream.Write(value[i]);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref string value)
        {
            int strLen = 0;
            
            stream.Read(ref strLen);
            
            var chars = new char[strLen];

            for (int i = 0; i < strLen; i++)
            {
                stream.Read(ref chars[i]);
            }
            
            value = new string(chars);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, char value)
        {
            stream.WriteBits(value, 8);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref char value)
        {
            value = (char)stream.ReadBits(8);
        }
    }
}
