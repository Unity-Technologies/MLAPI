using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class StopStartRuntimeTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        protected override void OnOneTimeSetup()
        {
            m_UseHost = false;
            base.OnOneTimeSetup();
        }


        private bool m_ServerStopped;
        [UnityTest]
        public IEnumerator WhenShuttingDownAndRestarting_SDKRestartsSuccessfullyAndStaysRunning()
        {
            // shutdown the server
            m_ServerNetworkManager.OnServerStopped += OnServerStopped;
            m_ServerNetworkManager.Shutdown();

            // wait until the OnServerStopped is invoked
            m_ServerStopped = false;
            yield return WaitForConditionOrTimeOut(() => m_ServerStopped);
            AssertOnTimeout("Timed out waiting for the server to stop!");

            // Verify the shutdown occurred
            Assert.IsFalse(m_ServerNetworkManager.IsServer);
            Assert.IsFalse(m_ServerNetworkManager.IsListening);
            Assert.IsFalse(m_ServerNetworkManager.IsHost);
            Assert.IsFalse(m_ServerNetworkManager.IsClient);

            m_ServerNetworkManager.StartServer();
            // Verify the server started
            Assert.IsTrue(m_ServerNetworkManager.IsServer);
            Assert.IsTrue(m_ServerNetworkManager.IsListening);

            // Wait several frames / one full network tick
            yield return s_DefaultWaitForTick;

            // Verify the server is still running
            Assert.IsTrue(m_ServerNetworkManager.IsServer);
            Assert.IsTrue(m_ServerNetworkManager.IsListening);
        }

        private void OnServerStopped(bool obj)
        {
            m_ServerNetworkManager.OnServerStopped -= OnServerStopped;
            m_ServerStopped = true;
        }

        [UnityTest]
        public IEnumerator WhenShuttingDownTwiceAndRestarting_SDKRestartsSuccessfullyAndStaysRunning()
        {
            // shutdown the server
            m_ServerNetworkManager.OnServerStopped += OnServerStopped;
            m_ServerNetworkManager.Shutdown();

            // wait until the OnServerStopped is invoked
            m_ServerStopped = false;
            yield return WaitForConditionOrTimeOut(() => m_ServerStopped);
            AssertOnTimeout("Timed out waiting for the server to stop!");

            // Verify the shutdown occurred
            Assert.IsFalse(m_ServerNetworkManager.IsServer);
            Assert.IsFalse(m_ServerNetworkManager.IsListening);
            Assert.IsFalse(m_ServerNetworkManager.IsHost);
            Assert.IsFalse(m_ServerNetworkManager.IsClient);

            // Shutdown the server again.
            m_ServerNetworkManager.Shutdown();

            m_ServerNetworkManager.StartServer();
            // Verify the server started
            Assert.IsTrue(m_ServerNetworkManager.IsServer);
            Assert.IsTrue(m_ServerNetworkManager.IsListening);

            // Wait several frames / one full network tick
            yield return s_DefaultWaitForTick;

            // Verify the server is still running
            Assert.IsTrue(m_ServerNetworkManager.IsServer);
            Assert.IsTrue(m_ServerNetworkManager.IsListening);
        }
    }
}
