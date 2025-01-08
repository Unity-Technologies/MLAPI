using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    public class NetworkObjectSpawnManyObjectsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;
        // "many" in this case means enough to exceed a ushort_max message size written in the header
        // 1500 is not a magic number except that it's big enough to trigger a failure
        private const int k_SpawnedObjects = 1500;

        private NetworkPrefab m_PrefabToSpawn;

        // Using this component assures we will know precisely how many prefabs were spawned on the client
        public class SpawnObjecTrackingComponent : NetworkBehaviour
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
            gameObject.AddComponent<SpawnObjecTrackingComponent>();

            m_PrefabToSpawn = new NetworkPrefab() { Prefab = gameObject };

            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(m_PrefabToSpawn);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.Prefabs.Add(m_PrefabToSpawn);
            }
        }

        [UnityTest]
        // When this test fails it does so without an exception and will wait the default ~6 minutes
        [Timeout(360000)]
        public IEnumerator WhenManyObjectsAreSpawnedAtOnce_AllAreReceived()
        {
            for (int x = 0; x < k_SpawnedObjects; x++)
            {
                NetworkObject serverObject = Object.Instantiate(m_PrefabToSpawn.Prefab).GetComponent<NetworkObject>();
                serverObject.NetworkManagerOwner = m_ServerNetworkManager;
                serverObject.Spawn();
            }
            // ensure all objects are replicated
            yield return WaitForConditionOrTimeOut(() => SpawnObjecTrackingComponent.SpawnedObjects == k_SpawnedObjects);
            AssertOnTimeout($"Timed out waiting for the client to spawn {k_SpawnedObjects} objects!");
        }
    }
}
