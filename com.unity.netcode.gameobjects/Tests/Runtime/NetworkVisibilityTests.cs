using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(SceneManagementState.SceneManagementEnabled, NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(SceneManagementState.SceneManagementDisabled, NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(SceneManagementState.SceneManagementEnabled, NetworkTopologyTypes.ClientServer)]
    [TestFixture(SceneManagementState.SceneManagementDisabled, NetworkTopologyTypes.ClientServer)]
    internal class NetworkVisibilityTests : NetcodeIntegrationTest
    {

        protected override int NumberOfClients => 2;
        private GameObject m_TestNetworkPrefab;
        private bool m_SceneManagementEnabled;
        private GameObject m_SpawnedObject;
        private NetworkManager m_SessionOwner;

        public NetworkVisibilityTests(SceneManagementState sceneManagementState, NetworkTopologyTypes networkTopologyType) : base(networkTopologyType)
        {
            m_SceneManagementEnabled = sceneManagementState == SceneManagementState.SceneManagementEnabled;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestNetworkPrefab = CreateNetworkObjectPrefab("Object");
            m_TestNetworkPrefab.AddComponent<NetworkVisibilityComponent>();
            if (!UseCMBService())
            {
                m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = m_SceneManagementEnabled;
            }

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.EnableSceneManagement = m_SceneManagementEnabled;
            }
            base.OnServerAndClientsCreated();
        }


        protected override IEnumerator OnServerAndClientsConnected()
        {
            m_SessionOwner = UseCMBService() ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;
            m_SpawnedObject = SpawnObject(m_TestNetworkPrefab, m_SessionOwner);

            yield return base.OnServerAndClientsConnected();
        }

        [UnityTest]
        public IEnumerator HiddenObjectsTest()
        {
            var expectedCount = UseCMBService() ? 2 : 3;
#if UNITY_2023_1_OR_NEWER
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsByType<NetworkVisibilityComponent>(FindObjectsSortMode.None).Where((c) => c.IsSpawned).Count() == expectedCount);
#else
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsOfType<NetworkVisibilityComponent>().Where((c) => c.IsSpawned).Count() == expectedCount);
#endif

            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for the visible object count to equal 2!");
        }

        [UnityTest]
        public IEnumerator HideShowAndDeleteTest()
        {
            var expectedCount = UseCMBService() ? 2 : 3;
#if UNITY_2023_1_OR_NEWER
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsByType<NetworkVisibilityComponent>(FindObjectsSortMode.None).Where((c) => c.IsSpawned).Count() == expectedCount);
#else
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsOfType<NetworkVisibilityComponent>().Where((c) => c.IsSpawned).Count() == expectedCount);
#endif
            AssertOnTimeout("Timed out waiting for the visible object count to equal 2!");

            var sessionOwnerNetworkObject = m_SpawnedObject.GetComponent<NetworkObject>();
            var clientIndex = UseCMBService() ? 1 : 0;
            sessionOwnerNetworkObject.NetworkHide(m_ClientNetworkManagers[clientIndex].LocalClientId);
#if UNITY_2023_1_OR_NEWER
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsByType<NetworkVisibilityComponent>(FindObjectsSortMode.None).Where((c) => c.IsSpawned).Count() == expectedCount - 1);
#else
            yield return WaitForConditionOrTimeOut(() => Object.FindObjectsOfType<NetworkVisibilityComponent>().Where((c) => c.IsSpawned).Count() == expectedCount - 1);
#endif
            AssertOnTimeout($"Timed out waiting for {m_SpawnedObject.name} to be hidden from client!");
            var networkObjectId = sessionOwnerNetworkObject.NetworkObjectId;
            sessionOwnerNetworkObject.NetworkShow(m_ClientNetworkManagers[clientIndex].LocalClientId);
            sessionOwnerNetworkObject.Despawn(true);

            // Expect no exceptions
            yield return s_DefaultWaitForTick;

            // Now force a scenario where it normally would have caused an exception
            m_SessionOwner.SpawnManager.ObjectsToShowToClient.Add(m_ClientNetworkManagers[clientIndex].LocalClientId, new System.Collections.Generic.List<NetworkObject>());
            m_SessionOwner.SpawnManager.ObjectsToShowToClient[m_ClientNetworkManagers[clientIndex].LocalClientId].Add(null);

            // Expect no exceptions
            yield return s_DefaultWaitForTick;
        }
    }
}
