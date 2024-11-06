using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class ClientOnlyConnectionTests
    {
        private NetworkManager m_ClientNetworkManager;
        private GameObject m_NetworkManagerGameObject;
        private WaitForSeconds m_DefaultWaitForTick = new WaitForSeconds(1.0f / 30);
        private bool m_WasDisconnected;
        private TimeoutHelper m_TimeoutHelper;

        [SetUp]
        public void Setup()
        {
            m_WasDisconnected = false;
            m_NetworkManagerGameObject = new GameObject();
            m_ClientNetworkManager = m_NetworkManagerGameObject.AddComponent<NetworkManager>();
            m_ClientNetworkManager.NetworkConfig = new NetworkConfig();
            // Default is 1000ms per connection attempt and 60 connection attempts (60s)
            // Currently there is no easy way to set these values other than in-editor
            var unityTransport = m_NetworkManagerGameObject.AddComponent<UnityTransport>();
            unityTransport.ConnectTimeoutMS = 1000;
            unityTransport.MaxConnectAttempts = 1;
            m_TimeoutHelper = new TimeoutHelper(2);
            m_ClientNetworkManager.NetworkConfig.NetworkTransport = unityTransport;
        }

        [UnityTest]
        public IEnumerator ClientFailsToConnect()
        {
            // Wait for the disconnected event
            m_ClientNetworkManager.OnClientDisconnectCallback += ClientNetworkManager_OnClientDisconnectCallback;

            // Only start the client (so it will timeout)
            m_ClientNetworkManager.StartClient();

            // Unity Transport throws an error when it times out
            LogAssert.Expect(LogType.Error, "Failed to connect to server.");

            yield return NetcodeIntegrationTest.WaitForConditionOrTimeOut(() => m_WasDisconnected, m_TimeoutHelper);
            Assert.False(m_TimeoutHelper.TimedOut, "Timed out waiting for client to timeout waiting to connect!");

            // Shutdown the client
            m_ClientNetworkManager.Shutdown();

            // Wait for a tick
            yield return m_DefaultWaitForTick;
        }

        private void ClientNetworkManager_OnClientDisconnectCallback(ulong clientId)
        {
            m_WasDisconnected = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_NetworkManagerGameObject != null)
            {
                Object.DestroyImmediate(m_NetworkManagerGameObject);
            }
        }
    }
}

