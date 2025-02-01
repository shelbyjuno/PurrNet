using PurrNet;
using PurrNet.Modules;
using PurrNet.Packing;

public class SomeBehaviour : NetworkBehaviour
{
    public static void ReadDelta(BitPacker stream, ChangeParentPacket oldValue, ref ChangeParentPacket value)
    {
        Packer.AreEqual(oldValue, value);
    }
}