using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    internal class NetworkSpawnManagerTests : NetcodeIntegrationTest
    {
        private ulong serverSideClientId => NetworkManager.ServerClientId;
        private ulong clientSideClientId => m_ClientNetworkManagers[0].LocalClientId;
        private ulong otherClientSideClientId => m_ClientNetworkManagers[1].LocalClientId;

        protected override int NumberOfClients => 2;

        public NetworkSpawnManagerTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        [Test]
        public void TestServerCanAccessItsOwnPlayer()
        {
            // server can access its own player
            var serverSideServerPlayerObject = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(serverSideClientId);
            Assert.NotNull(serverSideServerPlayerObject);
            Assert.AreEqual(serverSideClientId, serverSideServerPlayerObject.OwnerClientId);
        }


        /// <summary>
        /// Test was converted from a Test to UnityTest so distributed authority mode will pass this test.
        /// In distributed authority mode, client-side player spawning is enabled by default which requires
        /// all client (including DAHost) instances to wait for all players to be spawned.
        /// </summary>
        [UnityTest]
        public IEnumerator TestServerCanAccessOtherPlayers()
        {
            yield return null;
            // server can access other players
            var serverSideClientPlayerObject = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(clientSideClientId);
            Assert.NotNull(serverSideClientPlayerObject);
            Assert.AreEqual(clientSideClientId, serverSideClientPlayerObject.OwnerClientId);

            var serverSideOtherClientPlayerObject = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(otherClientSideClientId);
            Assert.NotNull(serverSideOtherClientPlayerObject);
            Assert.AreEqual(otherClientSideClientId, serverSideOtherClientPlayerObject.OwnerClientId);

        }

        [Test]
        public void TestClientCantAccessServerPlayer()
        {
            if (m_DistributedAuthority)
            {
                VerboseDebug($"Ignoring test: Clients have access to other player objects in {m_NetworkTopologyType} mode.");
                return;
            }
            // client can't access server player
            Assert.Throws<NotServerException>(() =>
            {
                m_ClientNetworkManagers[0].SpawnManager.GetPlayerNetworkObject(serverSideClientId);
            });
        }

        [Test]
        public void TestClientCanAccessOwnPlayer()
        {
            // client can access own player
            var clientSideClientPlayerObject = m_ClientNetworkManagers[0].SpawnManager.GetPlayerNetworkObject(clientSideClientId);
            Assert.NotNull(clientSideClientPlayerObject);
            Assert.AreEqual(clientSideClientId, clientSideClientPlayerObject.OwnerClientId);
        }

        [Test]
        public void TestClientCanAccessOtherPlayer()
        {

            if (!m_DistributedAuthority)
            {
                VerboseDebug($"Ignoring test: Clients do not have access to other player objects in {m_NetworkTopologyType} mode.");
                return;
            }

            var otherClientPlayer = m_ClientNetworkManagers[0].SpawnManager.GetPlayerNetworkObject(otherClientSideClientId);
            Assert.NotNull(otherClientPlayer, $"Failed to obtain Client{otherClientSideClientId}'s player object!");
        }

        [Test]
        public void TestClientCantAccessOtherPlayer()
        {
            if (m_DistributedAuthority)
            {
                VerboseDebug($"Ignoring test: Clients have access to other player objects in {m_NetworkTopologyType} mode.");
                return;
            }

            // client can't access other player
            Assert.Throws<NotServerException>(() =>
            {
                m_ClientNetworkManagers[0].SpawnManager.GetPlayerNetworkObject(otherClientSideClientId);
            });
        }

        [Test]
        public void TestServerGetsNullValueIfInvalidId()
        {
            // server gets null value if invalid id
            var nullPlayer = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(9999);
            Assert.Null(nullPlayer);
        }

        [Test]
        public void TestServerCanUseGetLocalPlayerObject()
        {
            // test server can use GetLocalPlayerObject
            var serverSideServerPlayerObject = m_ServerNetworkManager.SpawnManager.GetLocalPlayerObject();
            Assert.NotNull(serverSideServerPlayerObject);
            Assert.AreEqual(serverSideClientId, serverSideServerPlayerObject.OwnerClientId);
        }

        [Test]
        public void TestClientCanUseGetLocalPlayerObject()
        {
            // test client can use GetLocalPlayerObject
            var clientSideClientPlayerObject = m_ClientNetworkManagers[0].SpawnManager.GetLocalPlayerObject();
            Assert.NotNull(clientSideClientPlayerObject);
            Assert.AreEqual(clientSideClientId, clientSideClientPlayerObject.OwnerClientId);
        }

        private bool m_ClientDisconnected;

        [UnityTest]
        public IEnumerator TestConnectAndDisconnect()
        {
            // test when client connects, player object is now available
            yield return CreateAndStartNewClient();
            var newClientNetworkManager = m_ClientNetworkManagers[NumberOfClients];
            var newClientLocalClientId = newClientNetworkManager.LocalClientId;

            // test new client can get that itself locally
            var newPlayerObject = newClientNetworkManager.SpawnManager.GetLocalPlayerObject();
            Assert.NotNull(newPlayerObject);
            Assert.AreEqual(newClientLocalClientId, newPlayerObject.OwnerClientId);
            // test server can get that new client locally
            var serverSideNewClientPlayer = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(newClientLocalClientId);
            Assert.NotNull(serverSideNewClientPlayer);
            Assert.AreEqual(newClientLocalClientId, serverSideNewClientPlayer.OwnerClientId);

            // test when client disconnects, player object no longer available.
            var nbConnectedClients = m_ServerNetworkManager.ConnectedClients.Count;
            m_ClientDisconnected = false;
            newClientNetworkManager.OnClientDisconnectCallback += ClientNetworkManager_OnClientDisconnectCallback;
            m_ServerNetworkManager.DisconnectClient(newClientLocalClientId);
            yield return WaitForConditionOrTimeOut(() => m_ClientDisconnected);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for client to disconnect");
            // Call this to clean up NetcodeIntegrationTestHelpers
            NetcodeIntegrationTestHelpers.StopOneClient(newClientNetworkManager);

            Assert.AreEqual(m_ServerNetworkManager.ConnectedClients.Count, nbConnectedClients - 1);
            serverSideNewClientPlayer = m_ServerNetworkManager.SpawnManager.GetPlayerNetworkObject(newClientLocalClientId);
            Assert.Null(serverSideNewClientPlayer);
        }

        private void ClientNetworkManager_OnClientDisconnectCallback(ulong obj)
        {
            m_ClientDisconnected = true;
        }
    }
}
