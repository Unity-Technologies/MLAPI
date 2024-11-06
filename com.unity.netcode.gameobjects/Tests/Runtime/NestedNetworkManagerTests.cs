using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NestedNetworkManagerTests
    {
        [Test]
        public void CheckNestedNetworkManager()
        {
            var parent = new GameObject("ParentObject");
            var networkManagerObject = new GameObject(nameof(CheckNestedNetworkManager));

            var unityTransport = networkManagerObject.AddComponent<UnityTransport>();
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig() { NetworkTransport = unityTransport };

            // Make our NetworkManager's GameObject nested
            networkManagerObject.transform.parent = parent.transform;

            // Generate the error message we are expecting to see
            var messageToCheck = NetworkManager.GenerateNestedNetworkManagerMessage(networkManagerObject.transform);

            // Trap for the nested NetworkManager exception
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, messageToCheck);
#else
            LogAssert.Expect(LogType.Exception, $"Exception: {messageToCheck}");
#endif

            // Clean up
            Object.Destroy(parent);
        }
    }
}
