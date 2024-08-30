using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PurrNet;

namespace Purrnet.Tests
{
    public class ConnectionTests
    {
        public static NetworkManager BuildNetworkManager()
        {
            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();

            return manager;
        }

        [UnityTest]
        public IEnumerator ConnectionTestsWithEnumeratorPasses()
        {
            var networkManager = BuildNetworkManager();

            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
    }
}
