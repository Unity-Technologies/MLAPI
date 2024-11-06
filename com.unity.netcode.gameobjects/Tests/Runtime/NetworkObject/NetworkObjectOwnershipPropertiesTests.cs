using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{

    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    internal class NetworkObjectOwnershipPropertiesTests : NetcodeIntegrationTest
    {
        private class DummyNetworkBehaviour : NetworkBehaviour
        {

        }

        protected override int NumberOfClients => 2;
        private GameObject m_PrefabToSpawn;
        private NetworkObject m_OwnerSpawnedInstance;
        private NetworkObject m_TargetOwnerInstance;
        private NetworkManager m_InitialOwner;
        private NetworkManager m_NextTargetOwner;

        private ulong m_InitialOwnerId;
        private ulong m_TargetOwnerId;
        private bool m_SpawnedInstanceIsOwner;
        private bool m_InitialOwnerOwnedBySever;
        private bool m_TargetOwnerOwnedBySever;

        public NetworkObjectOwnershipPropertiesTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override IEnumerator OnTearDown()
        {
            m_OwnerSpawnedInstance = null;
            m_InitialOwner = null;
            m_NextTargetOwner = null;
            m_PrefabToSpawn = null;
            return base.OnTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("ClientOwnedObject");
            m_PrefabToSpawn.gameObject.AddComponent<DummyNetworkBehaviour>();
            m_PrefabToSpawn.GetComponent<NetworkObject>().SetOwnershipStatus(NetworkObject.OwnershipStatus.Distributable);
        }

        public enum InstanceTypes
        {
            Server,
            Client
        }

        private StringBuilder m_OwnershipPropagatedFailures = new StringBuilder();
        private bool OwnershipPropagated()
        {
            var conditionMet = true;
            m_OwnershipPropagatedFailures.Clear();
            // In distributed authority mode, we will check client owner to DAHost owner with InstanceTypes.Server and client owner to client
            // when InstanceTypes.Client
            if (m_DistributedAuthority)
            {
                if (!m_ClientNetworkManagers[1].SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId))
                {
                    conditionMet = false;
                    m_OwnershipPropagatedFailures.AppendLine($"Client-{m_ClientNetworkManagers[1].LocalClientId} has no ownership entry for {m_OwnerSpawnedInstance.name} ({m_OwnerSpawnedInstance.NetworkObjectId})");
                }
                if (!m_ClientNetworkManagers[0].SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId))
                {
                    conditionMet = false;
                    m_OwnershipPropagatedFailures.AppendLine($"Client-{m_ClientNetworkManagers[0].LocalClientId} has no ownership entry for {m_OwnerSpawnedInstance.name} ({m_OwnerSpawnedInstance.NetworkObjectId})");
                }
                if (!m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId))
                {
                    conditionMet = false;
                    m_OwnershipPropagatedFailures.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} has no ownership entry for {m_OwnerSpawnedInstance.name} ({m_OwnerSpawnedInstance.NetworkObjectId})");
                }
            }
            else
            {
                if (m_NextTargetOwner != m_ServerNetworkManager)
                {
                    if (!m_NextTargetOwner.SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId))
                    {
                        conditionMet = false;
                        m_OwnershipPropagatedFailures.AppendLine($"Client-{m_NextTargetOwner.LocalClientId} has no ownership entry for {m_OwnerSpawnedInstance.name} ({m_OwnerSpawnedInstance.NetworkObjectId})");
                    }
                }
                if (!m_ServerNetworkManager.SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId))
                {
                    conditionMet = false;
                    m_OwnershipPropagatedFailures.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} has no ownership entry for {m_OwnerSpawnedInstance.name} ({m_OwnerSpawnedInstance.NetworkObjectId})");
                }
            }
            return conditionMet;
        }

        private void ValidateOwnerShipProperties(bool targetIsOwner = false)
        {
            Assert.AreEqual(m_OwnerSpawnedInstance.IsOwner, m_SpawnedInstanceIsOwner);
            Assert.AreEqual(m_OwnerSpawnedInstance.IsOwnedByServer, m_InitialOwnerOwnedBySever);
            Assert.AreEqual(targetIsOwner ? m_TargetOwnerId : m_InitialOwnerId, m_OwnerSpawnedInstance.OwnerClientId);

            var initialOwnerBehaviour = m_OwnerSpawnedInstance.GetComponent<DummyNetworkBehaviour>();
            Assert.AreEqual(initialOwnerBehaviour.IsOwner, m_SpawnedInstanceIsOwner);
            Assert.AreEqual(initialOwnerBehaviour.IsOwnedByServer, m_InitialOwnerOwnedBySever);
            Assert.AreEqual(targetIsOwner ? m_TargetOwnerId : m_InitialOwnerId, initialOwnerBehaviour.OwnerClientId);

            Assert.AreEqual(m_TargetOwnerInstance.IsOwner, targetIsOwner);
            Assert.AreEqual(m_TargetOwnerInstance.IsOwnedByServer, m_TargetOwnerOwnedBySever);

            Assert.AreEqual(targetIsOwner ? m_TargetOwnerId : m_InitialOwnerId, m_TargetOwnerInstance.OwnerClientId);
            var targetOwnerBehaviour = m_TargetOwnerInstance.GetComponent<DummyNetworkBehaviour>();
            Assert.AreEqual(targetOwnerBehaviour.IsOwner, targetIsOwner);
            Assert.AreEqual(targetOwnerBehaviour.IsOwnedByServer, m_TargetOwnerOwnedBySever);
            Assert.AreEqual(targetIsOwner ? m_TargetOwnerId : m_InitialOwnerId, m_TargetOwnerInstance.OwnerClientId);
        }


        [UnityTest]
        public IEnumerator ValidatePropertiesWithOwnershipChanges([Values(InstanceTypes.Server, InstanceTypes.Client)] InstanceTypes instanceType)
        {
            m_NextTargetOwner = instanceType == InstanceTypes.Server ? m_ServerNetworkManager : m_ClientNetworkManagers[0];
            m_InitialOwner = instanceType == InstanceTypes.Client ? m_ServerNetworkManager : m_ClientNetworkManagers[0];

            // In distributed authority mode, we will check client owner to DAHost owner with InstanceTypes.Server and client owner to client
            // when InstanceTypes.Client
            if (m_DistributedAuthority)
            {
                m_InitialOwner = m_ClientNetworkManagers[0];
                if (instanceType == InstanceTypes.Client)
                {
                    m_NextTargetOwner = m_ClientNetworkManagers[1];
                }
                m_PrefabToSpawn.GetComponent<NetworkObject>().SetOwnershipStatus(NetworkObject.OwnershipStatus.Transferable);
            }

            m_InitialOwnerId = m_InitialOwner.LocalClientId;
            m_TargetOwnerId = m_NextTargetOwner.LocalClientId;
            m_InitialOwnerOwnedBySever = m_InitialOwner.IsServer;
            m_TargetOwnerOwnedBySever = m_InitialOwner.IsServer;
            var objectInstance = SpawnObject(m_PrefabToSpawn, m_InitialOwner);

            m_OwnerSpawnedInstance = objectInstance.GetComponent<NetworkObject>();
            m_SpawnedInstanceIsOwner = m_OwnerSpawnedInstance.NetworkManager == m_InitialOwner;
            // Sanity check to verify that the next owner to target is not the owner of the spawned object
            var hasEntry = m_InitialOwner.SpawnManager.GetClientOwnedObjects(m_NextTargetOwner.LocalClientId).Any(x => x.NetworkObjectId == m_OwnerSpawnedInstance.NetworkObjectId);
            Assert.False(hasEntry);

            // Since CreateObjectMessage gets proxied by DAHost, just wait until the next target owner has the spawned instance in the s_GlobalNetworkObjects table.
            yield return WaitForConditionOrTimeOut(() => s_GlobalNetworkObjects.ContainsKey(m_NextTargetOwner.LocalClientId) && s_GlobalNetworkObjects[m_NextTargetOwner.LocalClientId].ContainsKey(m_OwnerSpawnedInstance.NetworkObjectId));
            AssertOnTimeout($"Timed out waiting for Client-{m_NextTargetOwner.LocalClientId} to have an instance entry of {m_OwnerSpawnedInstance.name}-{m_OwnerSpawnedInstance.NetworkObjectId}!");

            // Get the target client's instance of the spawned object
            m_TargetOwnerInstance = s_GlobalNetworkObjects[m_NextTargetOwner.LocalClientId][m_OwnerSpawnedInstance.NetworkObjectId];

            // Validate that NetworkObject and NetworkBehaviour ownership properties are correct
            ValidateOwnerShipProperties();

            // The authority always changes the ownership
            // Client-Server: It will always be the host instance
            // Distributed Authority: It can be either the DAHost or the client
            if (m_DistributedAuthority)
            {
                // Use the target client's instance to change ownership
                m_TargetOwnerInstance.ChangeOwnership(m_NextTargetOwner.LocalClientId);
                if (instanceType == InstanceTypes.Client)
                {
                    var networkManagersList = new System.Collections.Generic.List<NetworkManager>() { m_ServerNetworkManager, m_ClientNetworkManagers[0] };
                    // Provide enough time for the client to receive and process the spawned message.
                    yield return WaitForMessageReceived<ChangeOwnershipMessage>(networkManagersList);
                }
                else
                {
                    // Provide enough time for the client to receive and process the change in ownership message.
                    yield return WaitForMessageReceived<ChangeOwnershipMessage>(m_ClientNetworkManagers.ToList());
                }
            }
            else
            {
                m_OwnerSpawnedInstance.ChangeOwnership(m_NextTargetOwner.LocalClientId);
                // Provide enough time for the client to receive and process the change in ownership message.
                yield return WaitForMessageReceived<ChangeOwnershipMessage>(m_ClientNetworkManagers.ToList());
            }

            // Ensure it's the ownership tables are updated
            yield return WaitForConditionOrTimeOut(OwnershipPropagated);
            AssertOnTimeout($"Timed out waiting for ownership to propagate!\n{m_OwnershipPropagatedFailures}");

            m_SpawnedInstanceIsOwner = m_OwnerSpawnedInstance.NetworkManager == m_NextTargetOwner;
            if (m_SpawnedInstanceIsOwner)
            {
                m_InitialOwnerOwnedBySever = m_OwnerSpawnedInstance.NetworkManager.IsServer;
            }
            m_InitialOwnerOwnedBySever = m_NextTargetOwner.IsServer;
            m_TargetOwnerOwnedBySever = m_NextTargetOwner.IsServer;

            // Validate that NetworkObject and NetworkBehaviour ownership properties are correct
            ValidateOwnerShipProperties(true);
        }
    }
}
