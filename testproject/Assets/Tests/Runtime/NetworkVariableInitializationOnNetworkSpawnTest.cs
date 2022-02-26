using System.Collections;
using NUnit.Framework;
using TestProject.RuntimeTests.Support;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    public class NetworkVariableInitializationOnNetworkSpawnTest
    {
        private GameObject m_Prefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Make sure these static values are reset
            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnClient = false;
            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnServer = false;
            NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient = false;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            // Shutdown and clean up both of our NetworkManager instances
            if (m_Prefab)
            {
                NetcodeIntegrationTestHelpers.Destroy();
                Object.Destroy(m_Prefab);
                m_Prefab = null;
            }

            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnClient = false;
            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnServer = false;
            NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient = false;
            yield break;
        }

        private IEnumerator RunTest()
        {
            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnServer = false;
            NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnClient = false;
            NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient = false;
            NetworkVariableInitOnNetworkSpawn.ExpectedSpawnValueOnClient = 5;

            const int numClients = 1;
            Assert.True(NetcodeIntegrationTestHelpers.Create(numClients, out NetworkManager server, out NetworkManager[] clients));
            m_Prefab = new GameObject("Object");
            var networkObject = m_Prefab.AddComponent<NetworkObject>();
            m_Prefab.AddComponent<NetworkVariableInitOnNetworkSpawn>();

            var waitForTickInterval = new WaitForSeconds(1.0f / server.NetworkConfig.TickRate);
            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            var validNetworkPrefab = new NetworkPrefab();
            validNetworkPrefab.Prefab = m_Prefab;
            server.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            foreach (var client in clients)
            {
                client.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            }

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(true, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // [Client-Side] Wait for a connection to the server
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients, null, 512);

            // [Host-Side] Check to make sure all clients are connected
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnectedToServer(server, clients.Length + 1, null, 512);

            var serverObject = Object.Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            NetworkObject serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = server;
            serverNetworkObject.Spawn();
            // Wait 1 tick for client side spawn
            yield return waitForTickInterval;

            // Wait until all objects have spawned.
            const int expectedNetworkObjects = numClients + 2; // +2 = one for prefab, one for server.
            const int maxFrames = 240;
            var doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (Object.FindObjectsOfType<NetworkObject>().Length != expectedNetworkObjects)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully spawn all expected NetworkObjects");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            serverObject.GetComponent<NetworkVariableInitOnNetworkSpawn>().Variable.Value = NetworkVariableInitOnNetworkSpawn.ExpectedSpawnValueOnClient;

            // Wait 1 tick for client side spawn
            yield return waitForTickInterval;

            // Get the NetworkVariableInitOnNetworkSpawn NetworkObject on the client-side
            var clientClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation(x => x.GetComponent<NetworkVariableInitOnNetworkSpawn>() != null && x.IsOwnedByServer, clients[0], clientClientPlayerResult);
            var networkVariableInitOnNetworkSpawnClientSide = clientClientPlayerResult.Result.GetComponent<NetworkVariableInitOnNetworkSpawn>();

            var timedOut = false;
            var timeOutPeriod = Time.realtimeSinceStartup + 2.0f;

            while (!timedOut)
            {
                if (networkVariableInitOnNetworkSpawnClientSide.Variable.Value == NetworkVariableInitOnNetworkSpawn.ExpectedSpawnValueOnClient)
                {
                    break;
                }
                timedOut = timeOutPeriod < Time.realtimeSinceStartup;
            }

            Assert.False(timedOut, $"Timed out while waiting for Variable.Value to equal {NetworkVariableInitOnNetworkSpawn.ExpectedSpawnValueOnClient}");

        }

        [UnityTest]
        [Description("When a network variable is initialized in OnNetworkSpawn on the server, the spawned object's NetworkVariable on the client is initialized with the same value.")]
        public IEnumerator WhenANetworkVariableIsInitializedInOnNetworkSpawnOnTheServer_TheSpawnedObjectsNetworkVariableOnTheClientIsInitializedWithTheSameValue()
        {
            yield return RunTest();
            Assert.IsTrue(NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnServer);
            Assert.IsTrue(NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnClient);
        }

        [UnityTest]
        [Ignore("Snapshot specific")]
        [Description("When a network variable is initialized in OnNetworkSpawn on the server, OnValueChanged is not called")]
        public IEnumerator WhenANetworkVariableIsInitializedInOnNetworkSpawnOnTheServer_OnValueChangedIsNotCalled()
        {
            yield return RunTest();
            Assert.IsFalse(NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient);
        }

        [UnityTest]
        [Ignore("Snapshot specific")]
        [Description("When a network variable is changed just after OnNetworkSpawn on the server, OnValueChanged is called after OnNetworkSpawn")]
        public IEnumerator WhenANetworkVariableIsInitializedJustAfterOnNetworkSpawnOnTheServer_OnValueChangedIsCalledAfterOnNetworkSpawn()
        {
            const int numClients = 1;
            Assert.True(NetcodeIntegrationTestHelpers.Create(numClients, out NetworkManager server, out NetworkManager[] clients));
            m_Prefab = new GameObject("Object");
            var networkObject = m_Prefab.AddComponent<NetworkObject>();
            m_Prefab.AddComponent<NetworkVariableInitOnNetworkSpawn>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            var validNetworkPrefab = new NetworkPrefab();
            validNetworkPrefab.Prefab = m_Prefab;
            server.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            foreach (var client in clients)
            {
                client.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            }

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(true, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // [Client-Side] Wait for a connection to the server
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients, null, 512);

            // [Host-Side] Check to make sure all clients are connected
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnectedToServer(server, clients.Length + 1, null, 512);

            var serverObject = Object.Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            NetworkObject serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = server;
            serverNetworkObject.Spawn();

            Assert.IsFalse(NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient);

            // Wait for the client-side to have actually spawned first
            const int maxFrames = 240;
            var doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (!NetworkVariableInitOnNetworkSpawn.NetworkSpawnCalledOnClient)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully spawn all expected NetworkObjects");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            // Then set the value on the server
            serverNetworkObject.GetComponent<NetworkVariableInitOnNetworkSpawn>().Variable.Value = 10;

            // Expect the value to spawn with the already updated value
            NetworkVariableInitOnNetworkSpawn.ExpectedSpawnValueOnClient = 10;

            // Wait until all objects have spawned.
            //const int expectedNetworkObjects = numClients + 2; // +2 = one for prefab, one for server.
            doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (!NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully spawn all expected NetworkObjects");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }
            // Test of ordering is handled by an assert within OnNetworkSpawn
            Assert.IsTrue(NetworkVariableInitOnNetworkSpawn.OnValueChangedCalledOnClient);
        }
    }
}
