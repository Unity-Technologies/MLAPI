#if !MULTIPLAYER_TOOLS
using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Tests the times of two clients connecting to a server using the SIPTransport (returns 50ms RTT but has no latency simulation)
    /// </summary>
    internal class TimeIntegrationTest : NetcodeIntegrationTest
    {
        private const double k_AdditionalTimeTolerance = 0.3333d; // magic number and in theory not needed but without this mac os test fail in Yamato because it looks like we get random framerate drops during unit test.

        private NetworkTimeState m_ServerState;
        private NetworkTimeState m_Client1State;
        private NetworkTimeState m_Client2State;

        protected override int NumberOfClients => 2;

        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            return NetworkManagerInstatiationMode.DoNotCreate;
        }

        private void UpdateTimeStates()
        {
            m_ServerState = new NetworkTimeState(m_ServerNetworkManager);
            m_Client1State = new NetworkTimeState(m_ClientNetworkManagers[0]);
            m_Client2State = new NetworkTimeState(m_ClientNetworkManagers[1]);
        }

        [UnityTest]
        [TestCase(60, 30u, ExpectedResult = null)]
        [TestCase(30, 30u, ExpectedResult = null)]
        [TestCase(40, 30u, ExpectedResult = null)]
        [TestCase(10, 30u, ExpectedResult = null)]
        [TestCase(60, 60u, ExpectedResult = null)]
        [TestCase(60, 10u, ExpectedResult = null)]
        public IEnumerator TestTimeIntegrationTest(int targetFrameRate, uint tickRate)
        {
            yield return StartSomeClientsAndServerWithPlayersCustom(true, NumberOfClients, targetFrameRate, tickRate);

            var additionalTimeTolerance = k_AdditionalTimeTolerance;
            // Mac can dip down below 10fps when set at a 10fps range (i.e. known to hit as low as 8.85 fps)
            // With the really low frame rate, add some additional time tolerance
            if (targetFrameRate == 10)
            {
                additionalTimeTolerance += 0.0333333333333f;
            }

            double frameInterval = 1d / targetFrameRate;
            double tickInterval = 1d / tickRate;

            var server = m_ServerNetworkManager;
            var firstClient = m_ClientNetworkManagers[0];
            var secondClient = m_ClientNetworkManagers[1];

            // increase the buffer time of client 2 // the values for client 1 are 0.0333/0.0333 here
            secondClient.NetworkTimeSystem.LocalBufferSec = 0.2;
            secondClient.NetworkTimeSystem.ServerBufferSec = 0.1;

            UpdateTimeStates();

            // wait for a few ticks to pass
            for (int i = 0; i < 4; i++)
            {
                yield return s_DefaultWaitForTick;
            }

            var framesToRun = 3d / frameInterval;

            var waitForNextFrame = new WaitForFixedUpdate();

            for (int i = 0; i < framesToRun; i++)
            {
                // Assure we wait for 1 frame to get the current frame time to check for low frame rates relative to expected frame rates
                yield return waitForNextFrame;

                // Adjust the time tolerance based on slower than expected FPS
                var currentFPS = 1.0f / Time.deltaTime;
                var fpsAdjustment = 1.0f;
                var currentAdjustment = additionalTimeTolerance;
                if (currentFPS < targetFrameRate)
                {
                    // Get the % slower and increase the time tolerance based on that %
                    var fpsDelta = targetFrameRate - currentFPS;
                    fpsAdjustment = 1.0f / fpsDelta;
                    currentAdjustment += additionalTimeTolerance * fpsAdjustment;
                }

                UpdateTimeStates();

                // compares whether client times have the correct offset to server
                m_ServerState.AssertCheckDifference(m_Client1State, tickInterval, tickInterval, tickInterval * 2 + frameInterval * 2 + currentAdjustment);
                m_ServerState.AssertCheckDifference(m_Client2State, 0.2, 0.1, tickInterval * 2 + frameInterval * 2 + currentAdjustment);

                // compares the two client times, only difference should be based on buffering.
                m_Client1State.AssertCheckDifference(m_Client2State, 0.2 - tickInterval, (0.1 - tickInterval), tickInterval * 2 + frameInterval * 2 + currentAdjustment);
            }
        }

        protected override IEnumerator OnTearDown()
        {
            // Always "shutdown in a tear-down" otherwise you can cause all proceeding tests to fail
            ShutdownAndCleanUp();
            yield return base.OnTearDown();
        }

        // This is from NetcodeIntegrationTest but we need a custom version of this to modifiy the config
        private IEnumerator StartSomeClientsAndServerWithPlayersCustom(bool useHost, int nbClients, int targetFrameRate, uint tickRate)
        {
            // Create multiple NetworkManager instances
            if (!NetcodeIntegrationTestHelpers.Create(nbClients, out NetworkManager server, out NetworkManager[] clients, targetFrameRate))
            {
                Debug.LogError("Failed to create instances");
                Assert.Fail("Failed to create instances");
            }

            m_ClientNetworkManagers = clients;
            m_ServerNetworkManager = server;

            // Create playerPrefab
            m_PlayerPrefab = new GameObject("Player");
            NetworkObject networkObject = m_PlayerPrefab.AddComponent<NetworkObject>();

            /*
             * Normally we would only allow player prefabs to be set to a prefab. Not runtime created objects.
             * In order to prevent having a Resource folder full of a TON of prefabs that we have to maintain,
             * NetcodeIntegrationTestHelpers has a helper function that lets you mark a runtime created object to be
             * treated as a prefab by the Netcode. That's how we can get away with creating the player prefab
             * at runtime without it being treated as a SceneObject or causing other conflicts with the Netcode.
             */
            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            // Set the player prefab
            server.NetworkConfig.PlayerPrefab = m_PlayerPrefab;

            for (int i = 0; i < clients.Length; i++)
            {
                clients[i].NetworkConfig.PlayerPrefab = m_PlayerPrefab;
                clients[i].NetworkConfig.TickRate = tickRate;
            }

            server.NetworkConfig.TickRate = tickRate;

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(useHost, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // Wait for connection on client and server side
            yield return WaitForClientsConnectedOrTimeOut();
            AssertOnTimeout($"Timed-out waiting for all clients to connect!");
        }

        private readonly struct NetworkTimeState : IEquatable<NetworkTimeState>
        {
            public NetworkTime LocalTime { get; }
            public NetworkTime ServerTime { get; }

            public NetworkTimeState(NetworkManager manager)
            {
                LocalTime = manager.NetworkTickSystem.LocalTime;
                ServerTime = manager.NetworkTickSystem.ServerTime;
            }

            public void AssertCheckDifference(NetworkTimeState clientState, double localBufferDifference, double serverBufferDifference, double tolerance)
            {
                var difLocalAbs = Math.Abs(clientState.LocalTime.Time - LocalTime.Time - localBufferDifference);
                var difServerAbs = Math.Abs(ServerTime.Time - clientState.ServerTime.Time - serverBufferDifference);

                Assert.True(difLocalAbs < tolerance, $"localtime difference: {difLocalAbs} bigger than tolerance: {tolerance}");
                Assert.True(difServerAbs < tolerance, $"servertime difference: {difServerAbs} bigger than tolerance: {tolerance}");
            }

            public bool Equals(NetworkTimeState other)
            {
                return LocalTime.Time.Equals(other.LocalTime.Time) && ServerTime.Time.Equals(other.ServerTime.Time);
            }

            public override bool Equals(object obj)
            {
                return obj is NetworkTimeState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (LocalTime.Time.GetHashCode() * 397) ^ ServerTime.Time.GetHashCode();
                }
            }
        }
    }
}
#endif
