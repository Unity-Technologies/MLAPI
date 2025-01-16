using System;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkManagerInitializedTests
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OnInitializedIsCalled(bool isServer)
        {
            var networkManagerObject = CreateNetworkManager(out var networkManager);

            var callbackHit = false;

            networkManager.OnInitialized += () => callbackHit = true;
            networkManager.Initialize(isServer);

            Assert.True(callbackHit, "OnInitialized callback was never triggered");

            // Clean up
            Object.Destroy(networkManagerObject);
        }

        [Test]
        public void OnInitializedShutsDownOnException()
        {
            var networkManagerObject = CreateNetworkManager(out var networkManager);

            networkManager.OnInitialized += () => throw new Exception();

            try
            {
                networkManager.StartServer();
            }
            catch (Exception)
            {
                // do nothing
            }

            Assert.True(networkManager.ShutdownInProgress, "Manager did not shutdown");

            // Clean up
            Object.Destroy(networkManagerObject);
        }

        private GameObject CreateNetworkManager(out NetworkManager networkManager)
        {
            var networkManagerObject = new GameObject(nameof(OnInitializedIsCalled));

            var unityTransport = networkManagerObject.AddComponent<UnityTransport>();
            networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig() { NetworkTransport = unityTransport };
            return networkManagerObject;
        }
    }
}
