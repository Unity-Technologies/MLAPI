using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    public class IntegrationTestUpdated : NetcodeIntegrationTest
    {
        private GameObject m_MyNetworkPrefab;
        protected override int NbClients => 1;

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
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsOfType<NetworkVisibilityComponent>().Where(
                (c) => c.IsSpawned).Count() == 2);

            Assert.False(s_GloabalTimeOutHelper.TimedOut, "Timed out waiting for instances " +
                "to be detected!");
        }
    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class IntegrationTestExtended : NetcodeIntegrationTest
    {
        public enum HostOrServer
        {
            Host,
            Server
        }
        private GameObject m_MyNetworkPrefab;
        protected override int NbClients => 1;

        public IntegrationTestExtended(HostOrServer hostOrServer)
        {
            m_UseHost = hostOrServer == HostOrServer.Host ? true : false;
        }

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
            yield return WaitForConditionOrTimeOut(() =>
            Object.FindObjectsOfType<NetworkVisibilityComponent>().Where(
                (c) => c.IsSpawned).Count() == 2);

            Assert.False(s_GloabalTimeOutHelper.TimedOut, "Timed out waiting for instances " +
                "to be detected!");
        }
    }

    public class ExampleTestComponent : NetworkBehaviour
    {
    }

    public class IntegrationTestPlayers : NetcodeIntegrationTest
    {
        protected override int NbClients => 5;

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
            foreach (var clientRelativePlayers in m_ClientSidePlayerNetworkObjects)
            {
                foreach (var playerInstance in clientRelativePlayers.Value)
                {
                    var player = playerInstance.Value;
                    Assert.NotNull(player.GetComponent<ExampleTestComponent>());
                }
            }

            foreach (var serverRelativePlayer in m_ServerSidePlayerNetworkObjects)
            {
                var player = serverRelativePlayer.Value;
                Assert.NotNull(player.GetComponent<ExampleTestComponent>());
            }

            // Confirm Player ID 1 on Client ID 4 is not the local player
            Assert.IsFalse(m_ClientSidePlayerNetworkObjects[4][1].IsLocalPlayer);
            // Confirm Player ID 4 on Client ID 4 is the local player
            Assert.IsTrue(m_ClientSidePlayerNetworkObjects[4][4].IsLocalPlayer);
        }

    }

    public class SpawnTest : NetworkBehaviour
    {
        public static int TotalSpawned;
        public override void OnNetworkSpawn() { TotalSpawned++; }
        public override void OnNetworkDespawn() { TotalSpawned--; }
    }
    public class IntegrationTestSpawning : NetcodeIntegrationTest
    {
        protected override int NbClients => 2;
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
            Assert.False(s_GloabalTimeOutHelper.TimedOut, $"Timed out waiting for all to " +
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
            Assert.False(s_GloabalTimeOutHelper.TimedOut, $"Timed out waiting for all to " +
                $"despawn! Total Spawned: {SpawnTest.TotalSpawned}");
        }
    }
}
