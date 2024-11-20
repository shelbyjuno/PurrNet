namespace PurrNet.Packing
{
    public interface IAutoNetworkedData { }
    
    public interface INetworkedData
    {
        void Write(BitPacker packer);

        void Read(BitPacker packer);
    }
}
