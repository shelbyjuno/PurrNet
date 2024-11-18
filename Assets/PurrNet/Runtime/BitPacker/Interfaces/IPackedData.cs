namespace PurrNet.Packing
{
    public interface IAutoNetworkedData { }
    
    public interface INetworkedData
    {
        void Write(BitStream stream);

        void Read(BitStream stream);
    }
}
