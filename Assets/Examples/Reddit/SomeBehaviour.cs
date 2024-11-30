using PurrNet;
using UnityEngine;

public class SomeBehaviour : PurrMonoBehaviour
{
    public override void Subscribe(NetworkManager manager, bool asServer)
    {
        Debug.Log($"Subscribed to {manager} as server: {asServer}");
    }

    public override void Unsubscribe(NetworkManager manager, bool asServer)
    {
        Debug.Log($"Unsubscribed from {manager} as server: {asServer}");
    }
}
