using PurrNet;
using System.Threading.Tasks;
using UnityEngine;

public class SomeBehaviour : NetworkBehaviour
{
    private async void Update()
    {
        bool myResult = false;

        if (Input.GetKeyDown(KeyCode.X))
        {
            myResult = await MyAwaitableRpc(1);
            Debug.Log($"MyResult: {myResult}");
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            myResult = await MyAwaitableRpc(0);
            Debug.Log($"MyResult: {myResult}");
        }
    }

    [ServerRpc(requireOwnership:false)]
    public static void MyStaticRpc(string message)
    {
        Debug.Log(message);
    }

    [ServerRpc(requireOwnership:false)]
    private void MyGenericRpc<T>(T data)
    {
        Debug.Log($"Received generic data: {data} | Type: {typeof(T)}");
    }

    [ServerRpc(requireOwnership:false)]
    private async Task<bool> MyAwaitableRpc(int myInput)
    {
        await Task.Delay(1000);

        return myInput > 0;
    }
}