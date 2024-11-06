using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class IntegrationTestUpdated : NetcodeIntegrationTest
    {
        private GameObject m_MyNetworkPrefab;
        protected override int NumberOfClients => 1;

        protected override void OnServerAndClientsCreated()
        {
            m_MyNetworkPrefab = CreateNetworkObjectPrefab("Object");
            m_MyNetworkPrefab.AddComponent<NetworkVisibilityComponent>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            SpawnObject(m_MyNetworkPrefab, m_ServerNetworkManager);
            yield return base.OnServerAndClientsConnected();
        }

        [UnityTest]
        public IEnumerator MyFirstIntegationTest()
        {
            // Check the condition for this test and automatically handle varying processing
            // environments and conditions
#if UNITY_2023_1_OR_NEWER
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsByType<NetworkVisibilityComponent>(FindObjectsSortMode.None).Where(
                (c) => c.IsSpawned).Count() == 2);
#else
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsOfType<NetworkVisibilityComponent>().Where(
                (c) => c.IsSpawned).Count() == 2);
#endif
            Assert.False(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for instances " +
                "to be detected!");
        }
    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class IntegrationTestExtended : NetcodeIntegrationTest
    {
        private GameObject m_MyNetworkPrefab;
        protected override int NumberOfClients => 1;

        protected override void OnServerAndClientsCreated()
        {
            m_MyNetworkPrefab = CreateNetworkObjectPrefab("Object");
            m_MyNetworkPrefab.AddComponent<NetworkVisibilityComponent>();
        }

        public IntegrationTestExtended(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            SpawnObject(m_MyNetworkPrefab, m_ServerNetworkManager);
            yield return base.OnServerAndClientsConnected();
        }

        [UnityTest]
        public IEnumerator MyFirstIntegationTest()
        {
            // Check the condition for this test and automatically handle varying processing
            // environments and conditions
#if UNITY_2023_1_OR_NEWER
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsByType<NetworkVisibilityComponent>(FindObjectsSortMode.None).Where(
                (c) => c.IsSpawned).Count() == 2);
#else
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsOfType<NetworkVisibilityComponent>().Where(
                (c) => c.IsSpawned).Count() == 2);
#endif

            Assert.False(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for instances " +
                "to be detected!");
        }
    }

    internal class ExampleTestComponent : NetworkBehaviour
    {
    }

    internal class IntegrationTestPlayers : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 5;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<ExampleTestComponent>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            return base.OnServerAndClientsConnected();
        }

        [Test]
        public void TestClientRelativePlayers()
        {
            // Check that all instances have the ExampleTestComponent
            foreach (var clientRelativePlayers in m_PlayerNetworkObjects)
            {
                foreach (var playerInstance in clientRelativePlayers.Value)
                {
                    var player = playerInstance.Value;
                    Assert.NotNull(player.GetComponent<ExampleTestComponent>());
                }
            }

            // Confirm Player ID 1 on Client ID 4 is not the local player
            Assert.IsFalse(m_PlayerNetworkObjects[4][1].IsLocalPlayer);
            // Confirm Player ID 4 on Client ID 4 is the local player
            Assert.IsTrue(m_PlayerNetworkObjects[4][4].IsLocalPlayer);
            // Confirm Player ID 0 on Client ID 0 (host) NetworkManager is the server
            Assert.IsTrue(m_PlayerNetworkObjects[0][0].NetworkManager.IsServer);
            // Confirm Player ID 0 on Client ID 4 (client) NetworkManager is not the server
            Assert.IsFalse(m_PlayerNetworkObjects[4][0].NetworkManager.IsServer);
        }
    }

    internal class SpawnTest : NetworkBehaviour
    {
        public static int TotalSpawned;
        public override void OnNetworkSpawn() { TotalSpawned++; }
        public override void OnNetworkDespawn() { TotalSpawned--; }
    }
    internal class IntegrationTestSpawning : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;
        private GameObject m_NetworkPrefabToSpawn;
        private int m_NumberToSpawn = 5;

        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            return NetworkManagerInstatiationMode.AllTests;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_NetworkPrefabToSpawn = CreateNetworkObjectPrefab("TrackingTest");
            m_NetworkPrefabToSpawn.gameObject.AddComponent<SpawnTest>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            SpawnObjects(m_NetworkPrefabToSpawn, m_ServerNetworkManager, m_NumberToSpawn);
            return base.OnServerAndClientsConnected();
        }

        [UnityTest]
        [Order(1)]
        public IEnumerator TestRelativeNetworkObjects()
        {
            var expected = m_NumberToSpawn * TotalClients;
            // Wait for all clients to have spawned all instances
            yield return WaitForConditionOrTimeOut(() => SpawnTest.TotalSpawned == expected);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all to " +
                $"spawn! Total Spawned: {SpawnTest.TotalSpawned}");

            var client1Relative = s_GlobalNetworkObjects[1].Values.Where((c) =>
            c.gameObject.GetComponent<SpawnTest>() != null);
            foreach (var networkObject in client1Relative)
            {
                var testComp = networkObject.GetComponent<SpawnTest>();
                // Confirm each one is owned by the server
                Assert.IsTrue(testComp.IsOwnedByServer, $"{testComp.name} is not owned" +
                    $" by the server!");
            }
        }

        [UnityTest]
        [Order(2)]
        public IEnumerator TestDespawnNetworkObjects()
        {
            var serverRelative = s_GlobalNetworkObjects[0].Values.Where((c) =>
            c.gameObject.GetComponent<SpawnTest>() != null).ToList();
            foreach (var networkObject in serverRelative)
            {
                networkObject.Despawn();
            }
            // Wait for all clients to have spawned all instances
            yield return WaitForConditionOrTimeOut(() => SpawnTest.TotalSpawned == 0);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all to " +
                $"despawn! Total Spawned: {SpawnTest.TotalSpawned}");
        }
    }
}
