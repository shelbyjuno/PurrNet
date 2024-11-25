using PurrNet;

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
    }
    
    /*[ObserversRpc(requireServer: false)]
    private void SimpleRPC<T>(SomeNetworkedData data, object wtf, T ftw)
    {
        if (data == null)
        {
            Debug.LogError("SimpleRPC: data is null!");
            return;
        }
        Debug.Log($"SimpleRPC: {data.data}, {data.random.GetType()} {data.random}, {wtf}, {ftw}");
    }*/
}
