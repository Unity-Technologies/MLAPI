using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkManagerEventsTests
    {
        private NetworkManager m_ClientManager;
        private NetworkManager m_ServerManager;

        private NetworkManager m_NetworkManagerInstantiated;
        private bool m_Instantiated;
        private bool m_Destroyed;

        /// <summary>
        /// Validates the <see cref="NetworkManager.OnInstantiated"/> and <see cref="NetworkManager.OnDestroying"/> event notifications
        /// </summary>
        [UnityTest]
        public IEnumerator InstantiatedAndDestroyingNotifications()
        {
            NetworkManager.OnInstantiated += NetworkManager_OnInstantiated;
            NetworkManager.OnDestroying += NetworkManager_OnDestroying;
            var waitPeriod = new WaitForSeconds(0.01f);
            var prefab = new GameObject("InstantiateDestroy");
            var networkManagerPrefab = prefab.AddComponent<NetworkManager>();

            Assert.IsTrue(m_Instantiated, $"{nameof(NetworkManager)} prefab did not get instantiated event notification!");
            Assert.IsTrue(m_NetworkManagerInstantiated == networkManagerPrefab, $"{nameof(NetworkManager)} prefab parameter did not match!");

            m_Instantiated = false;
            m_NetworkManagerInstantiated = null;

            for (int i = 0; i < 3; i++)
            {
                var instance = Object.Instantiate(prefab);
                var networkManager = instance.GetComponent<NetworkManager>();
                Assert.IsTrue(m_Instantiated, $"{nameof(NetworkManager)} instance-{i} did not get instantiated event notification!");
                Assert.IsTrue(m_NetworkManagerInstantiated == networkManager, $"{nameof(NetworkManager)} instance-{i} parameter did not match!");
                Object.DestroyImmediate(instance);
                Assert.IsTrue(m_Destroyed, $"{nameof(NetworkManager)} instance-{i} did not get destroying event notification!");
                m_Instantiated = false;
                m_NetworkManagerInstantiated = null;
                m_Destroyed = false;
            }
            m_NetworkManagerInstantiated = networkManagerPrefab;
            Object.Destroy(prefab);
            yield return null;
            Assert.IsTrue(m_Destroyed, $"{nameof(NetworkManager)} prefab did not get destroying event notification!");
            NetworkManager.OnInstantiated -= NetworkManager_OnInstantiated;
            NetworkManager.OnDestroying -= NetworkManager_OnDestroying;
        }

        private void NetworkManager_OnInstantiated(NetworkManager networkManager)
        {
            m_Instantiated = true;
            m_NetworkManagerInstantiated = networkManager;
        }

        private void NetworkManager_OnDestroying(NetworkManager networkManager)
        {
            m_Destroyed = true;
            Assert.True(m_NetworkManagerInstantiated == networkManager, $"Destroying {nameof(NetworkManager)} and current instance is not a match for the one passed into the event!");
        }

        [UnityTest]
        public IEnumerator OnServerStoppedCalledWhenServerStops()
        {
            bool callbackInvoked = false;
            var gameObject = new GameObject(nameof(OnServerStoppedCalledWhenServerStops));
            m_ServerManager = gameObject.AddComponent<NetworkManager>();

            // Set dummy transport that does nothing
            var transport = gameObject.AddComponent<DummyTransport>();
            m_ServerManager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            Action<bool> onServerStopped = (bool wasAlsoClient) =>
            {
                callbackInvoked = true;
                Assert.IsFalse(wasAlsoClient);
            };

            // Start server to cause initialization process
            Assert.True(m_ServerManager.StartServer());
            Assert.True(m_ServerManager.IsListening);

            m_ServerManager.OnServerStopped += onServerStopped;
            m_ServerManager.Shutdown();
            Object.DestroyImmediate(gameObject);

            yield return WaitUntilManagerShutsdown();

            Assert.False(m_ServerManager.IsListening);
            Assert.True(callbackInvoked, "OnServerStopped wasn't invoked");
        }

        [UnityTest]
        public IEnumerator OnClientStoppedCalledWhenClientStops()
        {
            yield return InitializeServerAndAClient();

            bool callbackInvoked = false;
            Action<bool> onClientStopped = (bool wasAlsoServer) =>
            {
                callbackInvoked = true;
                Assert.IsFalse(wasAlsoServer);
            };

            m_ClientManager.OnClientStopped += onClientStopped;
            m_ClientManager.Shutdown();
            yield return WaitUntilManagerShutsdown();

            Assert.True(callbackInvoked, "OnClientStopped wasn't invoked");
        }

        [UnityTest]
        public IEnumerator OnClientAndServerStoppedCalledWhenHostStops()
        {
            var gameObject = new GameObject(nameof(OnClientAndServerStoppedCalledWhenHostStops));
            m_ServerManager = gameObject.AddComponent<NetworkManager>();

            // Set dummy transport that does nothing
            var transport = gameObject.AddComponent<DummyTransport>();
            m_ServerManager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            int callbacksInvoked = 0;
            Action<bool> onClientStopped = (bool wasAlsoServer) =>
            {
                callbacksInvoked++;
                Assert.IsTrue(wasAlsoServer);
            };

            Action<bool> onServerStopped = (bool wasAlsoClient) =>
            {
                callbacksInvoked++;
                Assert.IsTrue(wasAlsoClient);
            };

            // Start server to cause initialization process
            Assert.True(m_ServerManager.StartHost());
            Assert.True(m_ServerManager.IsListening);

            m_ServerManager.OnServerStopped += onServerStopped;
            m_ServerManager.OnClientStopped += onClientStopped;
            m_ServerManager.Shutdown();
            Object.DestroyImmediate(gameObject);

            yield return WaitUntilManagerShutsdown();

            Assert.False(m_ServerManager.IsListening);
            Assert.AreEqual(2, callbacksInvoked, "either OnServerStopped or OnClientStopped wasn't invoked");
        }

        [UnityTest]
        public IEnumerator OnServerStartedCalledWhenServerStarts()
        {
            var gameObject = new GameObject(nameof(OnServerStartedCalledWhenServerStarts));
            m_ServerManager = gameObject.AddComponent<NetworkManager>();

            // Set dummy transport that does nothing
            var transport = gameObject.AddComponent<DummyTransport>();
            m_ServerManager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            bool callbackInvoked = false;
            Action onServerStarted = () =>
            {
                callbackInvoked = true;
                if (!m_ServerManager.IsServer)
                {
                    Assert.Fail("OnServerStarted called when the server is not active yet");
                }
            };

            // Start server to cause initialization process
            m_ServerManager.OnServerStarted += onServerStarted;

            Assert.True(m_ServerManager.StartServer());
            Assert.True(m_ServerManager.IsListening);

            yield return WaitUntilServerBufferingIsReady();

            Assert.True(callbackInvoked, "OnServerStarted wasn't invoked");
        }

        [UnityTest]
        public IEnumerator OnClientStartedCalledWhenClientStarts()
        {
            bool callbackInvoked = false;
            Action onClientStarted = () =>
            {
                callbackInvoked = true;
                if (!m_ClientManager.IsClient)
                {
                    Assert.Fail("onClientStarted called when the client is not active yet");
                }
            };

            yield return InitializeServerAndAClient(onClientStarted);

            Assert.True(callbackInvoked, "OnClientStarted wasn't invoked");
        }

        [UnityTest]
        public IEnumerator OnClientAndServerStartedCalledWhenHostStarts()
        {
            var gameObject = new GameObject(nameof(OnClientAndServerStartedCalledWhenHostStarts));
            m_ServerManager = gameObject.AddComponent<NetworkManager>();

            // Set dummy transport that does nothing
            var transport = gameObject.AddComponent<DummyTransport>();
            m_ServerManager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            int callbacksInvoked = 0;
            Action onClientStarted = () =>
            {
                callbacksInvoked++;
            };

            Action onServerStarted = () =>
            {
                callbacksInvoked++;
            };

            m_ServerManager.OnServerStarted += onServerStarted;
            m_ServerManager.OnClientStarted += onClientStarted;

            // Start server to cause initialization process
            Assert.True(m_ServerManager.StartHost());
            Assert.True(m_ServerManager.IsListening);

            yield return WaitUntilServerBufferingIsReady();
            Assert.AreEqual(2, callbacksInvoked, "either OnServerStarted or OnClientStarted wasn't invoked");
        }

        private IEnumerator WaitUntilManagerShutsdown()
        {
            /* Need two updates to actually shut down. First one to see the transport failing, which
            marks the NetworkManager as shutting down. Second one where actual shutdown occurs. */
            yield return null;
            yield return null;
        }

        private IEnumerator InitializeServerAndAClient(Action onClientStarted = null)
        {
            // Create multiple NetworkManager instances
            if (!NetcodeIntegrationTestHelpers.Create(1, out m_ServerManager, out NetworkManager[] clients, 30))
            {
                Debug.LogError("Failed to create instances");
                Assert.Fail("Failed to create instances");
            }

            // passing no clients on purpose to start them manually later
            NetcodeIntegrationTestHelpers.Start(false, m_ServerManager, new NetworkManager[] { });

            yield return WaitUntilServerBufferingIsReady();
            m_ClientManager = clients[0];

            if (onClientStarted != null)
            {
                m_ClientManager.OnClientStarted += onClientStarted;
            }

            Assert.True(m_ClientManager.StartClient());
            NetcodeIntegrationTestHelpers.RegisterHandlers(clients[0]);
            // Wait for connection on client side
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients);
        }

        private IEnumerator WaitUntilServerBufferingIsReady()
        {
            /* wait until at least more than 2 server ticks have passed
            Note: Waiting for more than 2 ticks on the server is due
            to the time system applying buffering to the received time
            in NetworkTimeSystem.Sync */
            yield return new WaitUntil(() => m_ServerManager.NetworkTickSystem.ServerTime.Tick > 2);
        }

        [UnityTearDown]
        public virtual IEnumerator Teardown()
        {
            NetcodeIntegrationTestHelpers.Destroy();
            if (m_ServerManager != null)
            {
                m_ServerManager.ShutdownInternal();
                Object.DestroyImmediate(m_ServerManager);
                m_ServerManager = null;
            }
            if (m_ClientManager != null)
            {
                m_ClientManager.ShutdownInternal();
                Object.DestroyImmediate(m_ClientManager);
                m_ClientManager = null;
            }
            yield return null;
        }
    }
}
