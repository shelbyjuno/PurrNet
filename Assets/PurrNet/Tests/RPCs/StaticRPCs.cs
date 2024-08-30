using System.Collections;
using NUnit.Framework;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.TestTools;

namespace Purrnet.Tests
{
    public class StaticRPCs
    {
        private static bool _receivedServerRPC = false;
        private static int _receivedNumber = -1;

        [UnityTest]
        public IEnumerator SimpleCallWithoutArguments()
        {
            var nmTask = PurrTests.BuildHostUDP();

            while (!nmTask.IsCompleted)
                yield return null;

            var nm = nmTask.Result;

            _receivedServerRPC = false;
            ServerRPCExample();

            yield return PurrTests.WaitOrThrow(() => _receivedServerRPC);

            Assert.AreEqual(true, _receivedServerRPC);
        }

        [ServerRPC]
        private static void ServerRPCExample()
        {
            _receivedServerRPC = true;
        }

        [UnityTest]
        public IEnumerator SimpleCallWithArgument()
        {
            var nmTask = PurrTests.BuildHostUDP();

            while (!nmTask.IsCompleted)
                yield return null;

            var nm = nmTask.Result;

            _receivedNumber = -1;
            ServerRPCExample(69);

            yield return PurrTests.WaitOrThrow(() => _receivedNumber != -1);

            Assert.AreEqual(69, _receivedNumber);
        }

        [ServerRPC]
        private static void ServerRPCExample(int number)
        {
            _receivedNumber = number;
        }
    }
}
