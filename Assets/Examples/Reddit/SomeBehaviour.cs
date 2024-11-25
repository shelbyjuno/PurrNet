using PurrNet;
using UnityEngine;

public class SomeNetworkedData
{
    private int _data;
    public int data;
    public object random;
}

public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            SimpleRPC(new SomeNetworkedData
            {
                data = 42,
                random = "Hello, World!"
            }, "WTF");
        }
    }
    
    [ServerRpc(requireOwnership: false)]
    private void SimpleRPC(SomeNetworkedData data, object wtf)
    {
        if (data == null)
        {
            Debug.LogError("SimpleRPC: data is null!");
            return;
        }
        Debug.Log($"SimpleRPC: {data.data}, {data.random.GetType()} {data.random}, {wtf}");
    }
}
