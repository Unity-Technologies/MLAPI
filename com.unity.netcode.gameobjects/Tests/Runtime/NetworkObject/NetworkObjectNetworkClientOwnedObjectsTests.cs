using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkObjectNetworkClientOwnedObjectsTests : NetcodeIntegrationTest
    {
        private class DummyNetworkBehaviour : NetworkBehaviour
        {

        }

        protected override int NumberOfClients => 1;
        private GameObject m_NetworkObject;

        protected override void OnServerAndClientsCreated()
        {
            m_NetworkObject = CreateNetworkObjectPrefab("ClientOwnedObject");
            m_NetworkObject.gameObject.AddComponent<DummyNetworkBehaviour>();
        }

        [UnityTest]
        public IEnumerator ChangeOwnershipOwnedObjectsAddTest()
        {
            NetworkObject serverObject = m_NetworkObject.GetComponent<NetworkObject>();
            serverObject.NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.Spawn();

            // Provide enough time for the client to receive and process the spawned message.
            yield return WaitForMessageReceived<CreateObjectMessage>(m_ClientNetworkManagers.ToList());

            // The object is owned by server
            Assert.False(m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(m_ClientNetworkManagers[0].LocalClientId).Any(x => x.NetworkObjectId == serverObject.NetworkObjectId));

            // Change the ownership
            serverObject.ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            // Provide enough time for the client to receive and process the change in ownership message.
            yield return WaitForMessageReceived<ChangeOwnershipMessage>(m_ClientNetworkManagers.ToList());

            // Ensure it's now added to the list
            Assert.True(m_ClientNetworkManagers[0].SpawnManager.GetClientOwnedObjects(m_ClientNetworkManagers[0].LocalClientId).Any(x => x.NetworkObjectId == serverObject.NetworkObjectId));
            Assert.True(m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(m_ClientNetworkManagers[0].LocalClientId).Any(x => x.NetworkObjectId == serverObject.NetworkObjectId));
        }

        [UnityTest]
        public IEnumerator WhenOwnershipIsChanged_OwnershipValuesUpdateCorrectly()
        {
            NetworkObject serverObject = m_NetworkObject.GetComponent<NetworkObject>();
            serverObject.NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.Spawn();

            // Provide enough time for the client to receive and process the spawned message.
            yield return WaitForMessageReceived<CreateObjectMessage>(m_ClientNetworkManagers.ToList());

            // The object is owned by server
            Assert.False(m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(m_ClientNetworkManagers[0].LocalClientId).Any(x => x.NetworkObjectId == serverObject.NetworkObjectId));

            // Change the ownership
            serverObject.ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            // Provide enough time for the client to receive and process the change in ownership message.
            yield return WaitForMessageReceived<ChangeOwnershipMessage>(m_ClientNetworkManagers.ToList());

            Assert.IsFalse(serverObject.IsOwner);
            Assert.IsFalse(serverObject.IsOwnedByServer);
            Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, serverObject.OwnerClientId);

            var serverBehaviour = serverObject.GetComponent<DummyNetworkBehaviour>();
            Assert.IsFalse(serverBehaviour.IsOwner);
            Assert.IsFalse(serverBehaviour.IsOwnedByServer);
            Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, serverBehaviour.OwnerClientId);

#if UNITY_2023_1_OR_NEWER
            var clientObject = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((obj) => obj.NetworkManagerOwner == m_ClientNetworkManagers[0]).FirstOrDefault();
#else
            var clientObject = Object.FindObjectsOfType<NetworkObject>().Where((obj) => obj.NetworkManagerOwner == m_ClientNetworkManagers[0]).FirstOrDefault();
#endif


            Assert.IsNotNull(clientObject);
            Assert.IsTrue(clientObject.IsOwner);
            Assert.IsFalse(clientObject.IsOwnedByServer);
            Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, clientObject.OwnerClientId);

            var clientBehaviour = clientObject.GetComponent<DummyNetworkBehaviour>();
            Assert.IsTrue(clientBehaviour.IsOwner);
            Assert.IsFalse(clientBehaviour.IsOwnedByServer);
            Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, clientBehaviour.OwnerClientId);

            serverObject.RemoveOwnership();

            // Provide enough time for the client to receive and process the change in ownership message.
            yield return WaitForMessageReceived<ChangeOwnershipMessage>(m_ClientNetworkManagers.ToList());

            Assert.IsTrue(serverObject.IsOwner);
            Assert.IsTrue(serverObject.IsOwnedByServer);
            Assert.AreEqual(NetworkManager.ServerClientId, serverObject.OwnerClientId);
            Assert.IsTrue(serverBehaviour.IsOwner);
            Assert.IsTrue(serverBehaviour.IsOwnedByServer);
            Assert.AreEqual(NetworkManager.ServerClientId, serverBehaviour.OwnerClientId);

            Assert.IsFalse(clientObject.IsOwner);
            Assert.IsTrue(clientObject.IsOwnedByServer);
            Assert.AreEqual(NetworkManager.ServerClientId, clientObject.OwnerClientId);
            Assert.IsFalse(clientBehaviour.IsOwner);
            Assert.IsTrue(clientBehaviour.IsOwnedByServer);
            Assert.AreEqual(NetworkManager.ServerClientId, clientBehaviour.OwnerClientId);
        }
    }
}
