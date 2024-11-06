using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost, LoadSceneMode.Single)]
    [TestFixture(HostOrServer.DAHost, LoadSceneMode.Additive)]
    [TestFixture(HostOrServer.Host, LoadSceneMode.Single)]
    [TestFixture(HostOrServer.Host, LoadSceneMode.Additive)]
    [TestFixture(HostOrServer.Server, LoadSceneMode.Single)]
    [TestFixture(HostOrServer.Server, LoadSceneMode.Additive)]
    public class NetworkSceneManagerSceneVerification : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 4;
        public NetworkSceneManagerSceneVerification(HostOrServer hostOrServer, LoadSceneMode loadSceneMode) : base(hostOrServer)
        {
            m_LoadSceneMode = loadSceneMode;
        }

        private const string k_AdditiveScene1 = "InSceneNetworkObject";
        private const string k_AdditiveScene2 = "AdditiveSceneMultiInstance";

        private string m_CurrentSceneName;
        private Scene m_CurrentScene;
        private LoadSceneMode m_LoadSceneMode;
        private List<Scene> m_ScenesLoaded = new List<Scene>();
        private bool m_CanStartServerOrClients = false;

        private bool m_IsTestingVerifyScene;
        private bool m_ServerVerifyScene;
        private bool m_ClientVerifyScene;
        private int m_ExpectedSceneIndex;
        private int m_ClientsThatFailedVerification;
        private string m_ExpectedSceneName;
        private LoadSceneMode m_ExpectedLoadMode;

        private NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler m_ClientVerificationAction;
        private NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler m_ServerVerificationAction;

        internal class SceneTestInfo
        {
            public bool ShouldWait;
            public bool ProcessedEvent;
            public ulong ClientId;
        }

        private List<SceneTestInfo> m_ShouldWaitList = new List<SceneTestInfo>();
        private List<ulong> m_ClientsReceivedSynchronize = new List<ulong>();

        protected override IEnumerator OnSetup()
        {
            m_CanStartServerOrClients = false;
            m_ClientsReceivedSynchronize.Clear();
            m_ShouldWaitList.Clear();
            m_ScenesLoaded.Clear();
            m_ServerVerifyScene = false;
            m_ClientVerifyScene = false;
            m_CreateServerFirst = false;

            return base.OnSetup();
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            m_ClientVerificationAction = ClientVerifySceneBeforeLoading;
            m_ServerVerificationAction = ServerVerifySceneBeforeLoading;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += ServerSceneManager_OnSceneEvent;
            m_ServerNetworkManager.SceneManager.DisableValidationWarnings(true);
            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(m_LoadSceneMode);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.SceneManager.DisableValidationWarnings(true);
            }

            return base.OnStartedServerAndClients();
        }

        private void ServerSceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            VerboseDebug($"SceneEvent: {sceneEvent.SceneEventType} | {sceneEvent.SceneName} | {sceneEvent.ClientId}");
            switch (sceneEvent.SceneEventType)
            {
                // Validates that we sent the proper number of synchronize events to the clients
                case SceneEventType.Synchronize:
                    {
                        m_ClientsReceivedSynchronize.Add(sceneEvent.ClientId);
                        break;
                    }
                case SceneEventType.Load:
                case SceneEventType.Unload:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        break;
                    }
                case SceneEventType.LoadComplete:
                    {
                        if (sceneEvent.ClientId == NetworkManager.ServerClientId)
                        {
                            var scene = sceneEvent.Scene;
                            m_CurrentScene = scene;
                            m_ScenesLoaded.Add(scene);
                        }
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        SetClientProcessedEvent(sceneEvent.ClientId);
                        break;
                    }
                case SceneEventType.UnloadComplete:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        SetClientProcessedEvent(sceneEvent.ClientId);
                        break;
                    }
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.UnloadEventCompleted:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));

                        // If we are a server and this is being processed by the server, then add the server to the completed list
                        // to validate that the event completed on all clients (and the server).
                        if (!m_ServerNetworkManager.IsHost && sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId &&
                            !sceneEvent.ClientsThatCompleted.Contains(m_ServerNetworkManager.LocalClientId))
                        {
                            sceneEvent.ClientsThatCompleted.Add(m_ServerNetworkManager.LocalClientId);
                        }
                        Assert.IsTrue(ContainsAllClients(sceneEvent.ClientsThatCompleted));
                        SetClientWaitDone(sceneEvent.ClientsThatCompleted);

                        break;
                    }
            }
        }

        protected override bool CanStartServerAndClients()
        {
            return m_CanStartServerOrClients;
        }

        /// <summary>
        /// Resets each SceneTestInfo entry
        /// </summary>
        private void ResetWait()
        {
            foreach (var entry in m_ShouldWaitList)
            {
                entry.ShouldWait = true;
                entry.ProcessedEvent = false;
            }
        }

        /// <summary>
        /// Initializes the m_ShouldWaitList
        /// </summary>
        private void InitializeSceneTestInfo(bool enableSceneVerification = false)
        {
            m_ShouldWaitList.Add(new SceneTestInfo() { ClientId = NetworkManager.ServerClientId, ShouldWait = false });
            m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading = m_ServerVerificationAction;
            m_ScenesLoaded.Clear();
            foreach (var manager in m_ClientNetworkManagers)
            {
                m_ShouldWaitList.Add(new SceneTestInfo() { ClientId = manager.LocalClientId, ShouldWait = false });
                manager.SceneManager.VerifySceneBeforeLoading = m_ClientVerificationAction;
            }
        }

        /// <summary>
        /// Wait until all clients have processed the event and the server has determined the event is completed
        /// Will bail if it takes too long via m_TimeOutMarker
        /// </summary>
        private bool ConditionPassed()
        {
            if (!m_IsTestingVerifyScene)
            {
                return !(m_ShouldWaitList.Select(c => c).Where(c => c.ProcessedEvent != true && c.ShouldWait == true).Count() > 0);
            }
            else
            {
                return !((m_ShouldWaitList.Select(c => c).Where(c => c.ProcessedEvent != true && c.ShouldWait == true &&
                c.ClientId == NetworkManager.ServerClientId).Count() > 0) && m_ClientsThatFailedVerification != NumberOfClients);
            }
        }

        private string PrintFailedCondition()
        {
            if (m_EnableVerboseDebug)
            {
                var message = $"FailedCondition IsTestingVerifyScene {m_IsTestingVerifyScene}:\n";
                var stillWaiting = m_ShouldWaitList.Select(c => c).Where(c => c.ProcessedEvent != true && c.ShouldWait == true && c.ClientId == NetworkManager.ServerClientId);
                foreach (var entry in stillWaiting)
                {
                    message += $"Client-{entry.ClientId}: Processed {entry.ProcessedEvent} | shouldWait {entry.ShouldWait}\n";
                }

                message += $"Clients That Failed Verification: {m_ClientsThatFailedVerification} vs NumberOfClients {NumberOfClients}";
                return message;
            }
            return string.Empty;
        }

        /// <summary>
        /// Determines if the clientId is valid
        /// </summary>
        private bool ContainsClient(ulong clientId)
        {
            return m_ShouldWaitList.Select(c => c.ClientId).Where(c => c == clientId).Count() > 0;
        }

        /// <summary>
        /// Sets the specific clientId entry as having processed the current event
        /// </summary>
        private void SetClientProcessedEvent(ulong clientId)
        {
            m_ShouldWaitList.Select(c => c).Where(c => c.ClientId == clientId).First().ProcessedEvent = true;
        }

        /// <summary>
        /// Sets all known clients' ShouldWait value to false
        /// </summary>
        private void SetClientWaitDone(List<ulong> clients)
        {
            foreach (var clientId in clients)
            {
                m_ShouldWaitList.Select(c => c).Where(c => c.ClientId == clientId).First().ShouldWait = false;
            }
        }

        /// <summary>
        /// Makes sure the ClientsThatCompleted in the scene event complete notification match the known client identifiers
        /// </summary>
        /// <param name="clients">list of client identifiers (ClientsThatCompleted) </param>
        /// <returns>true or false</returns>
        private bool ContainsAllClients(List<ulong> clients)
        {
            // First, make sure we have the expected client count
            if (clients.Count != m_ShouldWaitList.Count)
            {
                return false;
            }

            // Next, make sure we have all client identifiers
            foreach (var sceneTestInfo in m_ShouldWaitList)
            {
                if (!clients.Contains(sceneTestInfo.ClientId))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ServerVerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (m_ExpectedSceneIndex != 0 && m_ExpectedSceneName != null)
            {
                // Ignore the test runner test scene.
                if (sceneIndex != m_ExpectedSceneIndex && sceneName.Contains("InitTestScene"))
                {
                    return false;
                }

                Assert.IsTrue(m_ExpectedSceneIndex == sceneIndex);
                Assert.IsTrue(m_ExpectedSceneName == sceneName);
                Assert.IsTrue(m_ExpectedLoadMode == loadSceneMode);
                return m_ServerVerifyScene;
            }
            return false;
        }

        private bool ClientVerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            Assert.IsTrue(m_ExpectedSceneIndex == sceneIndex);
            Assert.IsTrue(m_ExpectedSceneName == sceneName);
            Assert.IsTrue(m_ExpectedLoadMode == loadSceneMode);
            if (!m_ClientVerifyScene)
            {
                m_ClientsThatFailedVerification++;
            }
            return m_ClientVerifyScene;
        }

        /// <summary>
        /// Unit test to verify that user defined scene verification process works on both the client and
        /// the server side.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator SceneVerifyBeforeLoadTest()
        {
            m_CurrentSceneName = k_AdditiveScene1;
            m_CanStartServerOrClients = true;
            m_IsTestingVerifyScene = false;
            yield return StartServerAndClients();

            // Now prepare for the loading and unloading additive scene testing
            InitializeSceneTestInfo();

            // Test VerifySceneBeforeLoading with both server and client set to true
            ResetWait();
            m_ServerVerifyScene = m_ClientVerifyScene = true;
            m_ExpectedSceneIndex = SceneUtility.GetBuildIndexByScenePath(m_CurrentSceneName);
            m_ExpectedSceneName = m_CurrentSceneName;
            m_ExpectedLoadMode = LoadSceneMode.Additive;
            Assert.AreEqual(m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive), SceneEventProgressStatus.Started);

            // Wait for all clients to load the scene
            yield return WaitForConditionOrTimeOut(ConditionPassed);
            AssertOnTimeout($"Timed out waiting for all clients to load {m_CurrentSceneName}\n{PrintFailedCondition()}");

            // Unload the scene
            ResetWait();

            Assert.AreEqual(m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene), SceneEventProgressStatus.Started);

            yield return WaitForConditionOrTimeOut(ConditionPassed);

            AssertOnTimeout($"Timed out waiting for all clients to unload {m_CurrentSceneName}\n{PrintFailedCondition()}");

            // Test VerifySceneBeforeLoading with m_ServerVerifyScene set to false
            // Server will notify it failed scene verification and no client should load
            ResetWait();
            m_ServerVerifyScene = false;
            Assert.AreEqual(m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive), SceneEventProgressStatus.SceneFailedVerification);

            // Test VerifySceneBeforeLoading with m_ServerVerifyScene set to true and m_ClientVerifyScene set to false
            // Server should load and clients will notify they failed scene verification
            ResetWait();
            m_CurrentSceneName = k_AdditiveScene2;
            m_ExpectedSceneName = m_CurrentSceneName;
            m_ExpectedSceneIndex = SceneUtility.GetBuildIndexByScenePath(m_CurrentSceneName);
            m_ServerVerifyScene = true;
            m_ClientVerifyScene = false;
            m_IsTestingVerifyScene = true;
            m_ClientsThatFailedVerification = 0;

            Assert.AreEqual(m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive), SceneEventProgressStatus.Started);

            // Now wait for server to complete and all clients to fail
            yield return WaitForConditionOrTimeOut(ConditionPassed);
            AssertOnTimeout($"Timed out waiting for all clients to load {m_CurrentSceneName}!\n{PrintFailedCondition()}");

            // Now unload the scene the server loaded from last test
            ResetWait();

            // All clients did not load this scene, so we can ignore them for the wait
            foreach (var listItem in m_ShouldWaitList)
            {
                if (listItem.ClientId == m_ServerNetworkManager.LocalClientId)
                {
                    continue;
                }
                listItem.ProcessedEvent = true;
            }

            m_IsTestingVerifyScene = false;
            var currentSceneName = m_CurrentScene;
            Assert.AreEqual(m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene), SceneEventProgressStatus.Started);

            // Now wait for scenes to unload
            yield return WaitForConditionOrTimeOut(ConditionPassed);
            AssertOnTimeout($"Timed out waiting for all clients to unload {m_CurrentSceneName}!\n{PrintFailedCondition()}");

            // Verify that all NetworkSceneManager instances reflect the change in scenes synchronized
            var scenesSynchronized = m_ServerNetworkManager.SceneManager.GetSynchronizedScenes();
            foreach (var scene in scenesSynchronized)
            {
                Assert.False(scene.name.Equals(currentSceneName), $"Host still thinks scene {currentSceneName} is loaded and synchronized!");
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                scenesSynchronized = client.SceneManager.GetSynchronizedScenes();
                foreach (var scene in scenesSynchronized)
                {
                    Assert.False(scene.name.Equals(currentSceneName), $"Client-{client.LocalClientId} still thinks scene {currentSceneName} is loaded and synchronized!");
                }
            }
        }
    }
}
