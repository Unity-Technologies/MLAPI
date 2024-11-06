using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;


namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Tests that check OnNetworkDespawn being invoked
    /// </summary>
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class NetworkObjectOnNetworkDespawnTests : NetcodeIntegrationTest
    {
        private const string k_ObjectName = "TestDespawn";
        public enum InstanceTypes
        {
            Server,
            Client
        }

        protected override int NumberOfClients => 1;
        private GameObject m_ObjectToSpawn;
        private NetworkObject m_NetworkObject;

        private HostOrServer m_HostOrServer;

        public NetworkObjectOnNetworkDespawnTests(HostOrServer hostOrServer) : base(hostOrServer)
        {
            m_HostOrServer = hostOrServer;
        }

        internal class OnNetworkDespawnTestComponent : NetworkBehaviour
        {
            public bool OnNetworkDespawnCalled { get; internal set; }

            public override void OnNetworkSpawn()
            {
                OnNetworkDespawnCalled = false;
                base.OnNetworkSpawn();
            }

            public override void OnNetworkDespawn()
            {
                OnNetworkDespawnCalled = true;
                base.OnNetworkDespawn();
            }
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ObjectToSpawn = CreateNetworkObjectPrefab(k_ObjectName);
            m_ObjectToSpawn.AddComponent<OnNetworkDespawnTestComponent>();
            base.OnServerAndClientsCreated();
        }

        private bool ObjectSpawnedOnAllNetworkManagerInstances()
        {
            if (!s_GlobalNetworkObjects.ContainsKey(m_ServerNetworkManager.LocalClientId))
            {
                return false;
            }
            if (!s_GlobalNetworkObjects[m_ServerNetworkManager.LocalClientId].ContainsKey(m_NetworkObject.NetworkObjectId))
            {
                return false;
            }

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                if (!s_GlobalNetworkObjects.ContainsKey(clientNetworkManager.LocalClientId))
                {
                    return false;
                }
                if (!s_GlobalNetworkObjects[clientNetworkManager.LocalClientId].ContainsKey(m_NetworkObject.NetworkObjectId))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This test validates that <see cref="NetworkBehaviour.OnNetworkDespawn"/> is invoked when the
        /// <see cref="NetworkManager"/> is shutdown.
        /// </summary>
        [UnityTest]
        public IEnumerator TestNetworkObjectDespawnOnShutdown([Values(InstanceTypes.Server, InstanceTypes.Client)] InstanceTypes despawnCheck)
        {
            var networkManager = despawnCheck == InstanceTypes.Server ? m_ServerNetworkManager : m_ClientNetworkManagers[0];
            var networkManagerOwner = m_ServerNetworkManager;
            if (m_DistributedAuthority)
            {
                networkManagerOwner = networkManager;
            }

            // Spawn the test object
            var spawnedObject = SpawnObject(m_ObjectToSpawn, networkManagerOwner);
            m_NetworkObject = spawnedObject.GetComponent<NetworkObject>();


            yield return WaitForConditionOrTimeOut(ObjectSpawnedOnAllNetworkManagerInstances);
            AssertOnTimeout($"Timed out waiting for all {nameof(NetworkManager)} instances to spawn {m_NetworkObject.name}!");

            // Get the spawned object relative to which NetworkManager instance we are testing.
            var relativeSpawnedObject = s_GlobalNetworkObjects[networkManager.LocalClientId][m_NetworkObject.NetworkObjectId];
            var onNetworkDespawnTestComponent = relativeSpawnedObject.GetComponent<OnNetworkDespawnTestComponent>();

            // Confirm it is not set before shutting down the NetworkManager
            Assert.IsFalse(onNetworkDespawnTestComponent.OnNetworkDespawnCalled, $"{nameof(OnNetworkDespawnTestComponent.OnNetworkDespawnCalled)} was set prior to shutting down!");

            // Shutdown the NetworkManager instance we are testing.
            networkManager.Shutdown();

            // Confirm that OnNetworkDespawn is invoked after shutdown
            yield return WaitForConditionOrTimeOut(() => onNetworkDespawnTestComponent.OnNetworkDespawnCalled);
            AssertOnTimeout($"Timed out waiting for {nameof(NetworkObject)} instance to despawn on the {despawnCheck} side!");
        }
    }
}
