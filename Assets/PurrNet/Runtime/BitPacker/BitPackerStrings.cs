namespace PurrNet.Packing
{
    public partial class BitPacker
    {
        public void Write(string value)
        {
            int strLen = value.Length;
            
            Write(strLen);
            
            for (int i = 0; i < strLen; i++)
                Write(value[i]);
        }
        
        public void Read(ref string value)
        {
            int strLen = 0;
            
            Read(ref strLen);
            
            var chars = new char[strLen];

            for (int i = 0; i < strLen; i++)
            {
                Read(ref chars[i]);
            }
            
            value = new string(chars);
        }
        
        public void Write(char value)
        {
            WriteBits(value, 8);
        }
        
        public void Read(ref char value)
        {
            value = (char)ReadBits(8);
        }
    }
}
