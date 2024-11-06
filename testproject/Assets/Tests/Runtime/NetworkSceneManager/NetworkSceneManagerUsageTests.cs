using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class NetworkSceneManagerUsageTests : NetcodeIntegrationTest
    {
        private const string k_AdditiveScene1 = "EmptyScene";

        private bool m_ClientLoadedScene;
        private string m_CurrentSceneName;
        private Scene m_CurrentScene;

        protected override int NumberOfClients => 1;
        public NetworkSceneManagerUsageTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        /// <summary>
        /// Checks that LoadScene cannot be called when EnableSceneManagement is false
        /// </summary>
        [Test]
        public void SceneManagementDisabled([Values(LoadSceneMode.Single, LoadSceneMode.Additive)] LoadSceneMode loadSceneMode)
        {
            m_CurrentSceneName = k_AdditiveScene1;
            m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = false;
            var results = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, loadSceneMode);
            Assert.True(results == SceneEventProgressStatus.SceneManagementNotEnabled, $"[Server][Load][{loadSceneMode}] Failed to receive a {nameof(SceneEventProgressStatus)}.{nameof(SceneEventProgressStatus.SceneManagementNotEnabled)}!");
        }

        /// <summary>
        /// Validate warning message generation for setting client synchronization mode on the client side.
        /// </summary>
        [Test]
        public void ClientSetClientSynchronizationMode()
        {
            if (!m_DistributedAuthority)
            {
                LogAssert.Expect(UnityEngine.LogType.Warning, "[Netcode] Clients should not set this value as it is automatically synchronized with the server's setting!");
            }
            else
            {
                LogAssert.NoUnexpectedReceived();
            }
            m_ClientNetworkManagers[0].SceneManager.SetClientSynchronizationMode(LoadSceneMode.Single);
        }

        /// <summary>
        /// Validate warning message generation for setting client synchronization mode on the server side.
        /// </summary>
        [UnityTest]
        public IEnumerator ServerSetClientSynchronizationModeAfterClientsConnected()
        {
            if (!m_DistributedAuthority)
            {
                // Verify that changing this setting when additional clients are connect will generate the warning
                LogAssert.Expect(UnityEngine.LogType.Warning, "[Netcode] Server is changing client synchronization mode after clients have been synchronized! It is recommended to do this before clients are connected!");
            }
            else
            {
                LogAssert.NoUnexpectedReceived();
            }

            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            // Verify that changing this setting when no additional clients are connected will not generate a warning
            yield return StopOneClient(m_ClientNetworkManagers[0]);

            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Checks that a client cannot call LoadScene
        /// </summary>
        [UnityTest]
        public IEnumerator ClientCannotUseException([Values(LoadSceneMode.Single, LoadSceneMode.Additive)] LoadSceneMode loadSceneMode)
        {
            m_CurrentSceneName = k_AdditiveScene1;
            var statusResult = m_ClientNetworkManagers[0].SceneManager.LoadScene(m_CurrentSceneName, loadSceneMode);
            var expectedResult = SceneEventProgressStatus.ServerOnlyAction;
            if (m_DistributedAuthority)
            {
                expectedResult = SceneEventProgressStatus.SessionOwnerOnlyAction;
            }

            Assert.True(statusResult == expectedResult, $"[Client][Load][{loadSceneMode}] Failed to receive a {nameof(expectedResult)} response!");

            // Check that a client cannot call UnloadScene
            m_ServerNetworkManager.SceneManager.OnSceneEvent += ServerSceneManager_OnSceneEvent;
            m_ClientNetworkManagers[0].SceneManager.OnLoadComplete += ClientSceneManager_OnLoadComplete;
            // Loading additive only because we don't want to unload the
            // Test Runner's scene using LoadSceneMode.Single
            var retStatus = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.AreEqual(retStatus, SceneEventProgressStatus.Started, $"{nameof(NetworkSceneManager.LoadScene)} returned " +
                $"unexpected {nameof(SceneEventProgressStatus)}: {retStatus}!");

            // Wait until we receive the LoadComplete event from the client
            yield return WaitForConditionOrTimeOut(() => m_ClientLoadedScene && m_CurrentScene.isLoaded);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {m_CurrentSceneName} {nameof(SceneEventType.LoadComplete)} event from client!");

            // Now try to unload the scene as a client
            statusResult = m_ClientNetworkManagers[0].SceneManager.UnloadScene(m_CurrentScene);


            Assert.True(statusResult == expectedResult, $"[Client][Unload] Failed to receive a {nameof(expectedResult)} response!");

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.SceneManager.VerifySceneBeforeUnloading = OnClientVerifySceneBeforeUnloading;
            }
            // Loading additive only because we don't want to unload the
            // Test Runner's scene using LoadSceneMode.Single
            retStatus = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.AreEqual(retStatus, SceneEventProgressStatus.Started);
            yield return WaitForConditionOrTimeOut(() => !m_ClientLoadedScene);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {m_CurrentSceneName} {nameof(SceneEventType.UnloadComplete)} event from client!");
        }

        private bool OnClientVerifySceneBeforeUnloading(Scene scene)
        {
            return m_CurrentSceneName == scene.name;
        }

        private void ServerSceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != m_ServerNetworkManager.LocalClientId)
            {
                return;
            }
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        m_CurrentScene = sceneEvent.Scene;
                        break;
                    }
                case SceneEventType.UnloadEventCompleted:
                    {
                        // Only set to false if the scene actually unloaded
                        m_ClientLoadedScene = m_CurrentScene.isLoaded;
                        break;
                    }
            }
        }

        private void ClientSceneManager_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (clientId == m_ClientNetworkManagers[0].LocalClientId && sceneName == m_CurrentSceneName)
            {
                m_ClientLoadedScene = true;
            }
        }

        private int m_LoadEventCompletedInvocationCount;

        /// <summary>
        /// This test validates that a host only receives one OnLoadEventCompleted callback per scene loading event when
        /// no clients are connected.
        /// Note: the fix was within SceneEventProgress but is associated with NetworkSceneManager
        /// </summary>
        [UnityTest]
        public IEnumerator HostReceivesOneLoadEventCompletedNotification()
        {
            // Host only test
            if (!m_UseHost)
            {
                yield break;
            }

            yield return StopOneClient(m_ClientNetworkManagers[0]);
            m_LoadEventCompletedInvocationCount = 0;
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;

            var retStatus = m_ServerNetworkManager.SceneManager.LoadScene(k_AdditiveScene1, LoadSceneMode.Additive);
            Assert.AreEqual(retStatus, SceneEventProgressStatus.Started);
            yield return WaitForConditionOrTimeOut(() => m_LoadEventCompletedInvocationCount > 0);
            AssertOnTimeout($"Host timed out loading scene {k_AdditiveScene1} additively!");

            // Wait one tick to make sure any other notifications are not triggered.
            yield return s_DefaultWaitForTick;

            Assert.IsTrue(m_LoadEventCompletedInvocationCount == 1, $"Expected OnLoadEventCompleted to be triggered once but was triggered {m_LoadEventCompletedInvocationCount} times!");
        }

        private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            m_LoadEventCompletedInvocationCount++;
        }
    }
}
