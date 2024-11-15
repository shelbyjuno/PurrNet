namespace PurrNet.Packing
{
    public interface IData
    {
        void Write(BitPacker packer);

        void Read(BitPacker packer);
    }
    
    public interface ISimpleData : IData
    {
        void Pack(BitPacker packer);
    }
}
