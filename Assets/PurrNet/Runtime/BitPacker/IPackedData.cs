namespace PurrNet.Packing
{
    public interface IData
    {
        void Write(BitStream stream);

        void Read(BitStream stream);
    }
    
    public interface ISimpleData : IData
    {
        void Pack(BitStream stream);
    }
}
