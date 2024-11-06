using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;


namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class NetworkObjectDontDestroyWithOwnerTests : NetcodeIntegrationTest
    {
        private const int k_NumberObjectsToSpawn = 32;
        protected override int NumberOfClients => 1;

        protected GameObject m_PrefabToSpawn;

        public NetworkObjectDontDestroyWithOwnerTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("ClientOwnedObject");
            m_PrefabToSpawn.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
        }

        [UnityTest]
        public IEnumerator DontDestroyWithOwnerTest()
        {
            var client = m_ClientNetworkManagers[0];
            var clientId = client.LocalClientId;
            var networkObjects = SpawnObjects(m_PrefabToSpawn, m_ClientNetworkManagers[0], k_NumberObjectsToSpawn);

            // wait for object spawn on client to reach k_NumberObjectsToSpawn + 1 (k_NumberObjectsToSpawn and 1 for the player)
            yield return WaitForConditionOrTimeOut(() => client.SpawnManager.GetClientOwnedObjects(clientId).Count() == k_NumberObjectsToSpawn + 1);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for client to have 33 NetworkObjects spawned! Only {client.SpawnManager.GetClientOwnedObjects(clientId).Count()} were assigned!");

            // Since clients spawn their objects locally in distributed authority mode, we have to rebuild the list of the client
            // owned objects on the (DAHost) server-side because when the client disconnects it will destroy its local instances.
            if (m_DistributedAuthority)
            {
                networkObjects.Clear();
                var serversideClientOwnedObjects = m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(clientId);

                foreach (var networkObject in serversideClientOwnedObjects)
                {
                    if (!networkObject.IsPlayerObject)
                    {
                        networkObjects.Add(networkObject.gameObject);
                    }
                }
            }

            // disconnect the client that owns all the clients
            NetcodeIntegrationTestHelpers.StopOneClient(client);

            var remainingClients = Mathf.Max(0, TotalClients - 1);
            // wait for disconnect
            yield return WaitForConditionOrTimeOut(() => m_ServerNetworkManager.ConnectedClients.Count == remainingClients);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for client to disconnect!");

            for (int i = 0; i < networkObjects.Count; i++)
            {
                var networkObject = networkObjects[i].GetComponent<NetworkObject>();
                // ensure ownership was transferred back
                Assert.That(networkObject.OwnerClientId == m_ServerNetworkManager.LocalClientId);
            }
        }
    }
}
