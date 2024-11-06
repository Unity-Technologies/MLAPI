using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{

    [TestFixture(NetworkTopologyTypes.ClientServer)]
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    internal class NetworkObjectSpawnManyObjectsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;
        private const int k_SpawnedObjects = 1500;

        private NetworkPrefab m_PrefabToSpawn;

        public NetworkObjectSpawnManyObjectsTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }
        // Using this component assures we will know precisely how many prefabs were spawned on the client
        internal class SpawnObjecTrackingComponent : NetworkBehaviour
        {
            public static int SpawnedObjects;
            public override void OnNetworkSpawn()
            {
                if (!IsServer)
                {
                    SpawnedObjects++;
                }
            }
        }

        protected override void OnServerAndClientsCreated()
        {
            SpawnObjecTrackingComponent.SpawnedObjects = 0;
            // create prefab
            var gameObject = new GameObject("TestObject");
            var networkObject = gameObject.AddComponent<NetworkObject>();
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);
            networkObject.IsSceneObject = false;
            gameObject.AddComponent<SpawnObjecTrackingComponent>();

            m_PrefabToSpawn = new NetworkPrefab() { Prefab = gameObject };

            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(m_PrefabToSpawn);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.Prefabs.Add(m_PrefabToSpawn);
            }
        }

        [UnityTest]
        public IEnumerator WhenManyObjectsAreSpawnedAtOnce_AllAreReceived()
        {
            var timeStarted = Time.realtimeSinceStartup;
            for (int x = 0; x < k_SpawnedObjects; x++)
            {
                NetworkObject serverObject = Object.Instantiate(m_PrefabToSpawn.Prefab).GetComponent<NetworkObject>();
                serverObject.NetworkManagerOwner = m_ServerNetworkManager;
                serverObject.Spawn();
            }

            var timeSpawned = Time.realtimeSinceStartup - timeStarted;
            // Provide plenty of time to spawn all 1500 objects in case the CI VM is running slow
            var timeoutHelper = new TimeoutHelper(30);
            // ensure all objects are replicated
            yield return WaitForConditionOrTimeOut(() => SpawnObjecTrackingComponent.SpawnedObjects == k_SpawnedObjects, timeoutHelper);

            AssertOnTimeout($"Timed out waiting for the client to spawn {k_SpawnedObjects} objects! Time to spawn: {timeSpawned} | Time to timeout: {timeStarted - Time.realtimeSinceStartup}", timeoutHelper);
        }
    }
}
