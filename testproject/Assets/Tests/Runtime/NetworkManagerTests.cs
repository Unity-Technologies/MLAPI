using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(UseSceneManagement.SceneManagementDisabled)]
    [TestFixture(UseSceneManagement.SceneManagementEnabled)]
    public class NetworkManagerTests : NetcodeIntegrationTest
    {
        private const string k_SceneToLoad = "InSceneNetworkObject";
        protected override int NumberOfClients => 0;

        public enum UseSceneManagement
        {
            SceneManagementEnabled,
            SceneManagementDisabled
        }

        private NetworkObject m_NetworkObject;
        private bool m_NetworkObjectWasSpawned;
        private bool m_NetworkBehaviourIsHostWasSet;
        private bool m_NetworkBehaviourIsClientWasSet;
        private bool m_NetworkBehaviourIsServerWasSet;
        private int m_NumberOfTimesInvoked;
        private AsyncOperation m_AsyncOperation;
        private NetworkObjectTestComponent m_NetworkObjectTestComponent;

        private bool m_UseSceneManagement;

        public NetworkManagerTests(UseSceneManagement useSceneManagement)
        {
            m_UseSceneManagement = useSceneManagement == UseSceneManagement.SceneManagementEnabled;
        }

        private void OnClientConnectedCallback(NetworkObject networkObject, int numberOfTimesInvoked, bool isHost, bool isClient, bool isServer)
        {
            if (networkObject != null)
            {
                m_NetworkObject = networkObject;
                m_NetworkObjectWasSpawned = networkObject.IsSpawned;
                m_NetworkBehaviourIsHostWasSet = isHost;
                m_NetworkBehaviourIsClientWasSet = isClient;
                m_NetworkBehaviourIsServerWasSet = isServer;
                m_NumberOfTimesInvoked = numberOfTimesInvoked;
            }
        }

        private bool TestComponentFound()
        {
            if (!m_AsyncOperation.isDone)
            {
                return false;
            }
#if UNITY_2023_1_OR_NEWER
            m_NetworkObjectTestComponent = Object.FindFirstObjectByType<NetworkObjectTestComponent>();
#else
            m_NetworkObjectTestComponent = Object.FindObjectOfType<NetworkObjectTestComponent>();
#endif
            if (m_NetworkObjectTestComponent == null)
            {
                return false;
            }
            return true;
        }

        protected override IEnumerator OnSetup()
        {
            m_AsyncOperation = SceneManager.LoadSceneAsync(k_SceneToLoad, LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(TestComponentFound);
            AssertOnTimeout($"Failed to find {nameof(NetworkObjectTestComponent)} after loading test scene {k_SceneToLoad}");
        }

        protected override IEnumerator OnTearDown()
        {
            SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(k_SceneToLoad));
            yield return s_DefaultWaitForTick;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = m_UseSceneManagement;
            m_NetworkObjectTestComponent.ConfigureClientConnected(m_ServerNetworkManager, OnClientConnectedCallback);
        }

        [Test]
        public void ValidateHostSettings()
        {
            Assert.IsTrue(m_ServerNetworkManager.LocalClient != null);
            Assert.IsTrue(m_NetworkObjectWasSpawned, $"{m_NetworkObject.name} was not spawned when OnClientConnectedCallback was invoked!");
            Assert.IsTrue(m_NetworkBehaviourIsHostWasSet, $"IsHost was not true when OnClientConnectedCallback was invoked!");
            Assert.IsTrue(m_NetworkBehaviourIsClientWasSet, $"IsClient was not true when OnClientConnectedCallback was invoked!");
            Assert.IsTrue(m_NumberOfTimesInvoked == 1, $"OnClientConnectedCallback was invoked {m_NumberOfTimesInvoked} as opposed to just once!");
            Assert.IsTrue(m_NetworkBehaviourIsServerWasSet, $"IsServer was not true when OnClientConnectedCallback was invoked!");
        }

        public enum ShutdownChecks
        {
            Server,
            Client
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.EnableSceneManagement = m_UseSceneManagement;
            foreach (var prefab in m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                networkManager.NetworkConfig.Prefabs.Add(prefab);
            }
            base.OnNewClientCreated(networkManager);
        }

        /// <summary>
        /// Validate shutting down a second time does not cause an exception.
        /// </summary>        
        [UnityTest]
        public IEnumerator ValidateShutdown([Values] ShutdownChecks shutdownCheck)
        {


            if (shutdownCheck == ShutdownChecks.Server)
            {
                // Register for the server stopped notification so we know we have shutdown completely
                m_ServerNetworkManager.OnServerStopped += OnServerStopped;
                // Shutdown
                m_ServerNetworkManager.Shutdown();
            }
            else
            {
                // For this test (simplify the complexity) with a late joining client, just remove the 
                // in-scene placed NetworkObject prior to the client connecting
                // (We are testing the shutdown sequence)
                var spawnedObjects = m_ServerNetworkManager.SpawnManager.SpawnedObjectsList.ToList();

                for (int i = spawnedObjects.Count - 1; i >= 0; i--)
                {
                    var spawnedObject = spawnedObjects[i];
                    if (spawnedObject.IsSceneObject != null && spawnedObject.IsSceneObject.Value)
                    {
                        spawnedObject.Despawn();
                    }
                }

                yield return s_DefaultWaitForTick;

                yield return CreateAndStartNewClient();

                // Register for the server stopped notification so we know we have shutdown completely
                m_ClientNetworkManagers[0].OnClientStopped += OnClientStopped;
                m_ClientNetworkManagers[0].Shutdown();
            }

            // Let the network manager instance shutdown
            yield return s_DefaultWaitForTick;

            // Validate the shutdown is no longer in progress
            if (shutdownCheck == ShutdownChecks.Server)
            {
                Assert.False(m_ServerNetworkManager.ShutdownInProgress, $"[{shutdownCheck}] Shutdown in progress was still detected!");
            }
            else
            {
                Assert.False(m_ClientNetworkManagers[0].ShutdownInProgress, $"[{shutdownCheck}] Shutdown in progress was still detected!");
            }
        }

        private void OnClientStopped(bool obj)
        {
            m_ServerNetworkManager.OnServerStopped -= OnClientStopped;
            // Verify that we can invoke shutdown again without an exception
            m_ServerNetworkManager.Shutdown();
        }

        private void OnServerStopped(bool obj)
        {
            m_ServerNetworkManager.OnServerStopped -= OnServerStopped;
            // Verify that we can invoke shutdown again without an exception
            m_ServerNetworkManager.Shutdown();
        }
    }
}
