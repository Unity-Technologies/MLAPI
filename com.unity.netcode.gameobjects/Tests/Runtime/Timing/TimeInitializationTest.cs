using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Tests that the time and tick system are initialized properly
    /// </summary>
    internal class TimeInitializationTest
    {
        private int m_ClientTickCounter;
        private int m_ConnectedTick;
        private NetworkManager m_Client;

        [UnityTest]
        public IEnumerator TestClientTimeInitializationOnConnect([Values(0, 1f)] float serverStartDelay, [Values(0, 1f)] float clientStartDelay, [Values(true, false)] bool isHost)
        {
            // Create multiple NetworkManager instances
            if (!NetcodeIntegrationTestHelpers.Create(1, out NetworkManager server, out NetworkManager[] clients, 30))
            {
                Debug.LogError("Failed to create instances");
                Assert.Fail("Failed to create instances");
            }

            yield return new WaitForSeconds(serverStartDelay);
            NetcodeIntegrationTestHelpers.Start(false, server, new NetworkManager[] { }); // passing no clients on purpose to start them manually later

            // 0 ticks should have passed
            var serverTick = server.NetworkTickSystem.ServerTime.Tick;
            Assert.AreEqual(0, serverTick);

            // server time should be 0
            Assert.AreEqual(0, server.NetworkTickSystem.ServerTime.Time);

            // wait until at least more than 2 server ticks have passed
            // Note: Waiting for more than 2 ticks on the server is due
            // to the time system applying buffering to the received time
            // in NetworkTimeSystem.Sync
            yield return new WaitUntil(() => server.NetworkTickSystem.ServerTime.Tick > 2);

            var serverTimePassed = server.NetworkTickSystem.ServerTime.Time;
            var expectedServerTickCount = Mathf.FloorToInt((float)(serverTimePassed * 30));

            var ticksPassed = server.NetworkTickSystem.ServerTime.Tick - serverTick;
            Assert.AreEqual(expectedServerTickCount, ticksPassed);

            yield return new WaitForSeconds(clientStartDelay);

            Assert.AreEqual(1, clients.Length);
            m_Client = clients[0];

            Assert.Null(m_Client.NetworkTickSystem);

            m_Client.OnClientConnectedCallback += ClientOnOnClientConnectedCallback;

            var clientStartRealTime = Time.time;

            m_Client.StartClient();
            NetcodeIntegrationTestHelpers.RegisterHandlers(clients[0]);

            m_Client.NetworkTickSystem.Tick += NetworkTickSystemOnTick;
            m_ClientTickCounter = 0;

            // Wait for connection on client side
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients);

            var clientStartRealTimeDuration = Time.time - clientStartRealTime;
            var clientStartRealTickDuration = Mathf.FloorToInt(clientStartRealTimeDuration * 30);

            // check tick is initialized with server value
            Assert.AreNotEqual(0, m_ConnectedTick);

            Assert.True(m_ClientTickCounter <= clientStartRealTickDuration);

            yield return null;
        }

        private void NetworkTickSystemOnTick()
        {
            //Debug.Log(m_Client.NetworkTickSystem.ServerTime.Tick);
            m_ClientTickCounter++;
        }

        private void ClientOnOnClientConnectedCallback(ulong id)
        {
            // client connected to server
            m_ConnectedTick = m_Client.NetworkTickSystem.ServerTime.Tick;
            //Debug.Log($"Connected tick: {m_ConnectedTick}");
        }

        [UnityTearDown]
        public virtual IEnumerator Teardown()
        {
            NetcodeIntegrationTestHelpers.Destroy();
            yield return null;
        }
    }
}
