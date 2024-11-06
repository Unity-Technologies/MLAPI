using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost, true)]
    [TestFixture(HostOrServer.DAHost, false)]
    public class ExtendedNetworkShowAndHideTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 3;
        private bool m_EnableSceneManagement;
        private GameObject m_ObjectToSpawn;
        private NetworkObject m_SpawnedObject;
        private NetworkManager m_ClientToHideFrom;
        private NetworkManager m_LateJoinClient;
        private NetworkManager m_SpawnOwner;

        public ExtendedNetworkShowAndHideTests(HostOrServer hostOrServer, bool enableSceneManagement) : base(hostOrServer)
        {
            m_EnableSceneManagement = enableSceneManagement;
        }

        protected override void OnServerAndClientsCreated()
        {
            if (!UseCMBService())
            {
                m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = m_EnableSceneManagement;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.EnableSceneManagement = m_EnableSceneManagement;
            }

            m_ObjectToSpawn = CreateNetworkObjectPrefab("TestObject");
            m_ObjectToSpawn.SetActive(false);

            base.OnServerAndClientsCreated();
        }

        private bool AllClientsSpawnedObject()
        {
            if (!UseCMBService())
            {
                if (!s_GlobalNetworkObjects.ContainsKey(m_ServerNetworkManager.LocalClientId))
                {
                    return false;
                }
                if (!s_GlobalNetworkObjects[m_ServerNetworkManager.LocalClientId].ContainsKey(m_SpawnedObject.NetworkObjectId))
                {
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!s_GlobalNetworkObjects.ContainsKey(client.LocalClientId))
                {
                    return false;
                }
                if (!s_GlobalNetworkObjects[client.LocalClientId].ContainsKey(m_SpawnedObject.NetworkObjectId))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsClientPromotedToSessionOwner()
        {
            if (!UseCMBService())
            {
                if (m_ServerNetworkManager.CurrentSessionOwner != m_ClientToHideFrom.LocalClientId)
                {
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.IsConnectedClient)
                {
                    continue;
                }
                if (client.CurrentSessionOwner != m_ClientToHideFrom.LocalClientId)
                {
                    return false;
                }
            }
            return true;
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            m_LateJoinClient = networkManager;
            networkManager.NetworkConfig.EnableSceneManagement = m_EnableSceneManagement;
            networkManager.NetworkConfig.Prefabs = m_SpawnOwner.NetworkConfig.Prefabs;
            base.OnNewClientCreated(networkManager);
        }

        /// <summary>
        /// This test validates the following NetworkShow - NetworkHide issue:
        /// - During a session, a spawned object is hidden from a client.
        /// - The current session owner disconnects and the client the object is hidden from is prommoted to the session owner.
        /// - A new client joins and the newly promoted session owner synchronizes the newly joined client with only objects visible to it.
        /// - Any already connected non-session owner client should "NetworkShow" the object to the newly connected client
        /// (but only if the hidden object has SpawnWithObservers enabled)
        /// </summary>
        [UnityTest]
        public IEnumerator HiddenObjectPromotedSessionOwnerNewClientSynchronizes()
        {
            // Get the test relative session owner
            var sessionOwner = UseCMBService() ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;
            m_SpawnOwner = UseCMBService() ? m_ClientNetworkManagers[1] : m_ClientNetworkManagers[0];
            m_ClientToHideFrom = UseCMBService() ? m_ClientNetworkManagers[NumberOfClients - 1] : m_ClientNetworkManagers[1];
            m_ObjectToSpawn.SetActive(true);

            // Spawn the object with a non-session owner client
            m_SpawnedObject = SpawnObject(m_ObjectToSpawn, m_SpawnOwner).GetComponent<NetworkObject>();
            yield return WaitForConditionOrTimeOut(AllClientsSpawnedObject);
            AssertOnTimeout($"Not all clients spawned and instance of {m_SpawnedObject.name}");

            // Hide the spawned object from the to be promoted session owner
            m_SpawnedObject.NetworkHide(m_ClientToHideFrom.LocalClientId);

            yield return WaitForConditionOrTimeOut(() => !m_ClientToHideFrom.SpawnManager.SpawnedObjects.ContainsKey(m_SpawnedObject.NetworkObjectId));
            AssertOnTimeout($"{m_SpawnedObject.name} was not hidden from Client-{m_ClientToHideFrom.LocalClientId}!");

            // Promoted a new session owner (DAHost promotes while CMB Session we disconnect the current session owner)
            if (!UseCMBService())
            {
                m_ServerNetworkManager.PromoteSessionOwner(m_ClientToHideFrom.LocalClientId);
            }
            else
            {
                sessionOwner.Shutdown();
            }

            // Wait for the new session owner to be promoted and for all clients to acknowledge the promotion
            yield return WaitForConditionOrTimeOut(IsClientPromotedToSessionOwner);
            AssertOnTimeout($"Client-{m_ClientToHideFrom.LocalClientId} was not promoted as session owner on all client instances!");

            // Connect a new client instance
            yield return CreateAndStartNewClient();

            // Assure the newly connected client is synchronized with the NetworkObject hidden from the newly promoted session owner
            yield return WaitForConditionOrTimeOut(() => m_LateJoinClient.SpawnManager.SpawnedObjects.ContainsKey(m_SpawnedObject.NetworkObjectId));
            AssertOnTimeout($"Client-{m_LateJoinClient.LocalClientId} never spawned {nameof(NetworkObject)} {m_SpawnedObject.name}!");
        }
    }
}
