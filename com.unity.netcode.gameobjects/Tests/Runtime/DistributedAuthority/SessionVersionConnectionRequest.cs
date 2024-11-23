using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class SessionVersionConnectionRequest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 0;

        public SessionVersionConnectionRequest() : base(NetworkTopologyTypes.DistributedAuthority, HostOrServer.DAHost) { }

        private bool m_UseValidSessionVersion;
        private bool m_ClientWasDisconnected;
        private NetworkManager m_ClientNetworkManager;

        /// <summary>
        /// Callback used to mock the scenario where a client has an invalid session version
        /// </summary>
        /// <returns><see cref="SessionConfig"/></returns>
        private SessionConfig GetInavlidSessionConfig()
        {
            return new SessionConfig(m_ServerNetworkManager.SessionConfig.SessionVersion - 1);
        }

        /// <summary>
        /// Overriding this method allows us to configure the newly instantiated client's
        /// NetworkManager prior to it being started.
        /// </summary>
        /// <param name="networkManager">the newly instantiated NetworkManager</param>
        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            m_ClientWasDisconnected = false;
            m_ClientNetworkManager = networkManager;
            m_ClientNetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            if (!m_UseValidSessionVersion)
            {
                networkManager.OnGetSessionConfig = GetInavlidSessionConfig;
            }
            base.OnNewClientCreated(networkManager);
        }

        /// <summary>
        /// Tracks if the client was disconnected or not
        /// </summary>
        private void OnClientDisconnectCallback(ulong clientId)
        {
            m_ClientWasDisconnected = true;
            m_ClientNetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        }

        /// <summary>
        /// This handles disabling the internal integration test logic that waits for
        /// clients to be connected. When we know the client will be disconnected,
        /// we want to have the NetcodeIntegrationTest not wait for the client to
        /// connect (otherwise it will timeout there and fail the test).
        /// </summary>
        /// <param name="networkManager"></param>
        /// <returns>true = wait | false = don't wait</returns>
        protected override bool ShouldWaitForNewClientToConnect(NetworkManager networkManager)
        {
            return m_UseValidSessionVersion;
        }

        /// <summary>
        /// Validates that when the client's session config version is valid a client will be
        /// allowed to connect and when it is not valid the client will be disconnected.
        /// </summary>
        /// <remarks>
        /// This is just a mock of the service logic to validate everything on the NGO side is
        /// working correctly.
        /// </remarks>
        /// <param name="useValidSessionVersion">true = use valid session version | false = use invalid session version</param>
        [UnityTest]
        public IEnumerator ValidateSessionVersion([Values] bool useValidSessionVersion)
        {
            // Test client being disconnected due to invalid session version
            m_UseValidSessionVersion = useValidSessionVersion;
            yield return CreateAndStartNewClient();
            yield return s_DefaultWaitForTick;
            if (!m_UseValidSessionVersion)
            {
                yield return WaitForConditionOrTimeOut(() => m_ClientWasDisconnected);
                AssertOnTimeout("Client was not disconnected when it should have been!");
                Assert.True(m_ClientNetworkManager.DisconnectReason == ConnectionRequestMessage.InvalidSessionVersionMessage, "Client did not receive the correct invalid session version message!");
            }
            else
            {
                Assert.False(m_ClientWasDisconnected, "Client was disconnected when it was expected to connect!");
                Assert.True(m_ClientNetworkManager.IsConnectedClient, "Client did not connect properly using the correct session version!");
            }
        }

        /// <summary>
        /// Invoked at the end of each integration test pass.
        /// Primarily used to clean up for the next pass.
        /// </summary>
        protected override IEnumerator OnTearDown()
        {
            m_ClientNetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            m_ClientNetworkManager = null;
            yield return base.OnTearDown();
        }
    }
}
