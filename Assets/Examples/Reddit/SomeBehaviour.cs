using System.Collections;
using System.IO;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

public class SomeBehaviour : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            var assetPath = new DirectoryInfo(".").Name;
            StartCoroutine(SendAndWaitConfirmation<string>(assetPath));
            
            StartCoroutine(CoroutineTest(1));
        }
    }
    
    /*private void SendAndWaitConfirmationTest<T>(T value)
    {
        CoroutineTestReall(value);
        CoroutineTestReall(value);
    }*/
    
    private IEnumerator SendAndWaitConfirmation<T>(T value)
    {
        yield return CoroutineTestReal(value);
        yield return CoroutineTest(value);
        Debug.Log("Server received the message!");
    }
    
    private IEnumerator SendAndWaitConfirmation(string value)
    {
        yield return CoroutineTest(value);
        Debug.Log("Server received the message!");
    }
    
    [ServerRpc(requireOwnership: false)]
    private IEnumerator CoroutineTest<T>(T value)
    {
        Debug.Log("CoroutineTest");
        yield return new WaitForSeconds(1);
        Debug.Log(value);
    }
    
    private static IEnumerator CoroutineTestReal<T>(T value)
    {
        Debug.Log("CoroutineTest");
        yield return new WaitForSeconds(1);
        Debug.Log(value);
    }
    
    [ServerRpc(requireOwnership: false)]
    private void CoroutineTestReall(string value)
    {
        Debug.Log(value);
    }
    
    private void CoroutineTestReall<T>(T value)
    {
        Debug.Log(value);
    }
}
