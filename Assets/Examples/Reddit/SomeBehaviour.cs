using PurrNet;
using PurrNet.Logging;

public class SomeBehaviour : PurrMonoBehaviour
{
    public override void Subscribe(NetworkManager manager, bool asServer)
    {
        PurrLogger.Log($"Subscribed to {manager} as {(asServer ? "server" : "client")}");
    }

    public override void Unsubscribe(NetworkManager manager, bool asServer)
    {
        PurrLogger.Log($"Unsubscribed from {manager} as {(asServer ? "server" : "client")}");
    }
}