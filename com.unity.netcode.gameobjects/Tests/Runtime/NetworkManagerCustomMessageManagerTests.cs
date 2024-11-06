using NUnit.Framework;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkManagerCustomMessageManagerTests
    {
        [Test]
        public void CustomMessageManagerAssigned()
        {
            var gameObject = new GameObject(nameof(CustomMessageManagerAssigned));
            var networkManager = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<DummyTransport>();

            networkManager.NetworkConfig = new NetworkConfig
            {
                // Set dummy transport that does nothing
                NetworkTransport = transport
            };

            CustomMessagingManager preManager = networkManager.CustomMessagingManager;

            // Start server to cause initialization
            networkManager.StartServer();

            Debug.Assert(preManager == null);
            Debug.Assert(networkManager.CustomMessagingManager != null);


            networkManager.Shutdown();
            Object.DestroyImmediate(gameObject);
        }
    }
}
