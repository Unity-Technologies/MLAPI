using System.Collections;
using NUnit.Framework;
using TestProject.ManualTests;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.DAHost)]
    public class NetworkBehaviourSessionSynchronized : NetcodeIntegrationTest
    {
        private const string k_SceneToLoad = "SessionSynchronize";
        protected override int NumberOfClients => 0;

        private bool m_SceneLoaded;

        public NetworkBehaviourSessionSynchronized(HostOrServer hostOrServer) : base(hostOrServer) { }

        [UnityTest]
        public IEnumerator InScenePlacedSessionSynchronized()
        {
            m_SceneLoaded = false;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(() => m_SceneLoaded);
            AssertOnTimeout($"Timed out waiting for scene {k_SceneToLoad} to load!");
            yield return CreateAndStartNewClient();
            AssertOnTimeout($"Timed out waiting for client to join session!");
            var firstObject = SessionSynchronizedTest.FirstObject;
            var secondObject = SessionSynchronizedTest.SecondObject;

            Assert.True(firstObject == secondObject.ClientSideReferencedBehaviour);
            Assert.True(secondObject == firstObject.ClientSideReferencedBehaviour);
            Assert.True(firstObject.OtherValueObtained == secondObject.ValueToCheck);
            Assert.True(secondObject.OtherValueObtained == firstObject.ValueToCheck);
            Assert.True(firstObject.OnInSceneObjectsSpawnedInvoked);
            Assert.True(secondObject.OnInSceneObjectsSpawnedInvoked);
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId && sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                m_SceneLoaded = true;
                m_ServerNetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }
    }
}
