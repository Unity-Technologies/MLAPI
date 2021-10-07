using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Netcode.RuntimeTests;
using Unity.Netcode;

namespace TestProject.RuntimeTests
{
    public class NetworkSceneManagerTests : BaseMultiInstanceTest
    {
        protected override int NbClients => 9;

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            m_ShouldWaitList = new List<SceneTestInfo>();
            return base.Setup();
        }

        [UnityTearDown]
        public override IEnumerator Teardown()
        {
            return base.Teardown();
        }

        private class SceneTestInfo
        {
            public bool ShouldWait;
            public bool ProcessedEvent;
            public ulong ClientId;
        }

        private float m_TimeOutMarker;
        private bool m_TimedOut;
        private bool m_MultiSceneTest;
        private string m_CurrentSceneName;
        private List<SceneTestInfo> m_ShouldWaitList;
        private Scene m_CurrentScene;
        private const string k_InvalidSceneName = "SomeInvalidSceneName";

        private List<Scene> m_ScenesLoaded = new List<Scene>();


        private NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler m_ClientVerificationAction;
        private NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler m_ServerVerificationAction;

        /// <summary>
        /// Tests the different types of NetworkSceneManager notifications (including exceptions) generated
        /// Also tests invalid loading scenarios (i.e. client trying to load a scene)
        /// </summary>
        [UnityTest]
        public IEnumerator SceneLoadingAndNotifications([Values(LoadSceneMode.Single, LoadSceneMode.Additive)] LoadSceneMode clientSynchronizationMode)
        {
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_CurrentSceneName = "AdditiveScene1";

            // Check that we cannot call LoadScene when EnableSceneManagement is false (from previous legacy test)
            var threwException = false;
            try
            {
                m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = false;
                m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains($"{nameof(NetworkConfig.EnableSceneManagement)} flag is not enabled in the {nameof(NetworkManager)}'s {nameof(NetworkConfig)}. " +
                    $"Please set {nameof(NetworkConfig.EnableSceneManagement)} flag to true before calling " +
                    $"{nameof(NetworkSceneManager.LoadScene)} or {nameof(NetworkSceneManager.UnloadScene)}."))
                {
                    threwException = true;
                }
            }
            Assert.IsTrue(threwException);

            m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = true;

            // Check that a client cannot call LoadScene
            threwException = false;
            try
            {
                m_ClientNetworkManagers[0].SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Only server can start a scene event!"))
                {
                    threwException = true;
                }
            }
            Assert.IsTrue(threwException);


            // Now prepare for the loading and unloading additive scene testing
            InitializeSceneTestInfo(clientSynchronizationMode);

            // Test loading additive scenes and the associated event messaging and notification pipelines
            ResetWait();
            var result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Check error status for trying to load during an already in progress scene event
            result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.SceneEventInProgress);

            // Wait for all clients to load the scene
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            // Test unloading additive scenes and the associated event messaging and notification pipelines
            ResetWait();

            // Check that a client cannot call UnloadScene
            threwException = false;
            try
            {
                m_ClientNetworkManagers[0].SceneManager.UnloadScene(m_CurrentScene);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Only server can start a scene event!"))
                {
                    threwException = true;
                }
            }
            Assert.IsTrue(threwException);

            result = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.True(result == SceneEventProgressStatus.Started);

            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            // Check error status for trying to unloading something not loaded
            ResetWait();
            result = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.True(result == SceneEventProgressStatus.SceneNotLoaded);

            LogAssert.Expect(LogType.Error, $"Scene '{k_InvalidSceneName}' couldn't be loaded because it has not been added to the build settings scenes in build list.");
            // Check error status for trying to load an invalid scene name
            result = m_ServerNetworkManager.SceneManager.LoadScene(k_InvalidSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.InvalidSceneName);

            yield break;
        }

        /// <summary>
        /// Initializes the m_ShouldWaitList
        /// </summary>
        private void InitializeSceneTestInfo(LoadSceneMode clientSynchronizationMode, bool enableSceneVerification = false)
        {
            m_ShouldWaitList.Add(new SceneTestInfo() { ClientId = m_ServerNetworkManager.ServerClientId, ShouldWait = false });
            if (enableSceneVerification)
            {
                m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading = m_ServerVerificationAction;
                m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(clientSynchronizationMode);
                if (m_MultiSceneTest)
                {
                    m_ScenesLoaded.Clear();
                }
            }

            foreach (var manager in m_ClientNetworkManagers)
            {
                m_ShouldWaitList.Add(new SceneTestInfo() { ClientId = manager.LocalClientId, ShouldWait = false });
                if (enableSceneVerification)
                {
                    manager.SceneManager.VerifySceneBeforeLoading = m_ClientVerificationAction;
                    manager.SceneManager.SetClientSynchronizationMode(clientSynchronizationMode);
                }
            }
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

            m_TimeOutMarker = Time.realtimeSinceStartup + 4.0f;
            m_TimedOut = false;
        }

        /// <summary>
        /// Wait until all clients have processed the event and the server has determined the event is completed
        /// Will bail if it takes too long via m_TimeOutMarker
        /// </summary>
        /// <returns></returns>
        private bool ShouldWait()
        {
            m_TimedOut = m_TimeOutMarker < Time.realtimeSinceStartup;
            if (!m_IsTestingVerifyScene)
            {
                return (m_ShouldWaitList.Select(c => c).Where(c => c.ProcessedEvent != true && c.ShouldWait == true).Count() > 0) && !m_TimedOut;
            }
            else
            {
                return (m_ShouldWaitList.Select(c => c).Where(c => c.ProcessedEvent != true && c.ShouldWait == true &&
                c.ClientId == m_ServerNetworkManager.ServerClientId).Count() > 0) && !m_TimedOut && m_ClientsThatFailedVerification != NbClients;
            }
        }

        /// <summary>
        /// Determines if the clientId is valid
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        private bool ContainsClient(ulong clientId)
        {
            return m_ShouldWaitList.Select(c => c.ClientId).Where(c => c == clientId).Count() > 0;
        }

        /// <summary>
        /// Sets the specific clientId entry as having processed the current event
        /// </summary>
        /// <param name="clientId"></param>
        private void SetClientProcessedEvent(ulong clientId)
        {
            m_ShouldWaitList.Select(c => c).Where(c => c.ClientId == clientId).First().ProcessedEvent = true;
        }

        /// <summary>
        /// Sets all known clients' ShouldWait value to false
        /// </summary>
        /// <param name="clientId"></param>
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

        /// <summary>
        /// This test only needs to check the server side for the proper event notifications of loading a scene, each
        /// client response that it loaded the scene, and the final  event notifications S2C_LoadComplete and S2C_UnloadComplete
        /// that signify all clients have processed through the loading and unloading process.
        /// </summary>
        /// <param name="sceneEvent"></param>
        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.S2C_Sync:
                    {
                        // Verify that the Client Synchronization Mode set by the server is being received by the client (which means it is applied when loading the first scene)
                        Assert.AreEqual(m_ClientNetworkManagers.ToArray().Where(c => c.LocalClientId == sceneEvent.ClientId).First().SceneManager.ClientSynchronizationMode, sceneEvent.LoadSceneMode);
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_Load:
                case SceneEventData.SceneEventTypes.S2C_Unload:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        Assert.IsNotNull(sceneEvent.AsyncOperation);
                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_LoadComplete:
                    {
                        if (sceneEvent.ClientId == m_ServerNetworkManager.ServerClientId)
                        {
                            var sceneHandle = sceneEvent.Scene.handle;
                            var scene = sceneEvent.Scene;
                            m_CurrentScene = scene;
                            if (m_MultiSceneTest)
                            {
                                m_ScenesLoaded.Add(scene);
                            }

                            foreach (var manager in m_ClientNetworkManagers)
                            {
                                if (!manager.SceneManager.ScenesLoaded.ContainsKey(sceneHandle))
                                {
                                    manager.SceneManager.ScenesLoaded.Add(sceneHandle, scene);
                                }

                                if (!manager.SceneManager.ServerSceneHandleToClientSceneHandle.ContainsKey(sceneHandle))
                                {
                                    manager.SceneManager.ServerSceneHandleToClientSceneHandle.Add(sceneHandle, sceneHandle);
                                }
                            }
                        }
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        SetClientProcessedEvent(sceneEvent.ClientId);
                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_UnloadComplete:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        SetClientProcessedEvent(sceneEvent.ClientId);
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_LoadComplete:
                case SceneEventData.SceneEventTypes.S2C_UnLoadComplete:
                    {
                        Assert.AreEqual(sceneEvent.SceneName, m_CurrentSceneName);
                        Assert.IsTrue(ContainsClient(sceneEvent.ClientId));
                        Assert.IsTrue(ContainsAllClients(sceneEvent.ClientsThatCompleted));
                        SetClientWaitDone(sceneEvent.ClientsThatCompleted);
                        break;
                    }
            }
        }

        private bool m_IsTestingVerifyScene;
        private bool m_ServerVerifyScene;
        private bool m_ClientVerifyScene;
        private int m_ExpectedSceneIndex;
        private int m_ClientsThatFailedVerification;
        private string m_ExpectedSceneName;
        private LoadSceneMode m_ExpectedLoadMode;

        private bool ServerVerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            Assert.IsTrue(m_ExpectedSceneIndex == sceneIndex);
            Assert.IsTrue(m_ExpectedSceneName == sceneName);
            Assert.IsTrue(m_ExpectedLoadMode == loadSceneMode);

            return m_ServerVerifyScene;
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
        public IEnumerator SceneVerifyBeforeLoadTest([Values(LoadSceneMode.Single, LoadSceneMode.Additive)] LoadSceneMode clientSynchronizationMode)
        {
            m_ClientVerificationAction = ClientVerifySceneBeforeLoading;
            m_ServerVerificationAction = ServerVerifySceneBeforeLoading;

            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_CurrentSceneName = "AdditiveScene1";

            // Now prepare for the loading and unloading additive scene testing
            InitializeSceneTestInfo(clientSynchronizationMode, true);

            // Test VerifySceneBeforeLoading with both server and client set to true
            ResetWait();
            m_ServerVerifyScene = m_ClientVerifyScene = true;
            m_ExpectedSceneIndex = SceneUtility.GetBuildIndexByScenePath(m_CurrentSceneName);
            m_ExpectedSceneName = m_CurrentSceneName;
            m_ExpectedLoadMode = LoadSceneMode.Additive;
            var result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Wait for all clients to load the scene
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            // Unload the scene
            ResetWait();
            result = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.True(result == SceneEventProgressStatus.Started);

            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            // Test VerifySceneBeforeLoading with m_ServerVerifyScene set to false
            // Server will notify it failed scene verification and no client should load
            ResetWait();
            m_ServerVerifyScene = false;
            result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.SceneFailedVerification);

            // Test VerifySceneBeforeLoading with m_ServerVerifyScene set to true and m_ClientVerifyScene set to false
            // Server should load and clients will notify they failed scene verification
            ResetWait();
            m_CurrentSceneName = "AdditiveScene2";
            m_ExpectedSceneName = m_CurrentSceneName;
            m_ExpectedSceneIndex = SceneUtility.GetBuildIndexByScenePath(m_CurrentSceneName);
            m_ServerVerifyScene = true;
            m_ClientVerifyScene = false;
            m_IsTestingVerifyScene = true;
            m_ClientsThatFailedVerification = 0;
            result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Now wait for server to complete and all clients to fail
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            // Now unload the scene the server loaded from last test
            ResetWait();
            m_IsTestingVerifyScene = false;
            result = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Now wait for server to unload and clients will fake unload
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);

            yield break;
        }

        private IEnumerator LoadScene(string sceneName)
        {
            // Test VerifySceneBeforeLoading with both server and client set to true
            ResetWait();
            m_ServerVerifyScene = m_ClientVerifyScene = true;
            m_ExpectedSceneIndex = SceneUtility.GetBuildIndexByScenePath(m_CurrentSceneName);
            m_ExpectedSceneName = m_CurrentSceneName;
            m_ExpectedLoadMode = LoadSceneMode.Additive;
            var result = m_ServerNetworkManager.SceneManager.LoadScene(m_CurrentSceneName, LoadSceneMode.Additive);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Wait for all clients to load the scene
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);
        }

        private IEnumerator UnloadScene(Scene scene)
        {
            // Unload the scene
            ResetWait();

            m_CurrentSceneName = scene.name;

            var result = m_ServerNetworkManager.SceneManager.UnloadScene(scene);
            Assert.True(result == SceneEventProgressStatus.Started);

            // Wait for all clients to unload the scene
            yield return new WaitWhile(ShouldWait);
            Assert.IsFalse(m_TimedOut);
        }

        /// <summary>
        /// Server will only allow the base unit test scene to load once in SceneEventDataPoolTest
        /// since clients share the same scene space.
        /// </summary>
        private bool DataPoolVerifySceneServer(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (sceneName == k_BaseUnitTestSceneName)
            {
                return !SceneManager.GetSceneByBuildIndex(sceneIndex).isLoaded;
            }
            return true;
        }

        /// <summary>
        /// Clients always load whatever the server tells them to load for SceneEventDataPoolTest
        /// </summary>
        private bool DataPoolVerifySceneClient(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            return true;
        }

        private const string k_BaseUnitTestSceneName = "UnitTestBaseScene";
        private const string k_MultiInstanceTestScenename = "AdditiveSceneMultiInstance";

        /// <summary>
        /// Small to heavy scene loading scenario to test the dynamically generated SceneEventData objects under a load.
        /// Will load from 1 to 32 scenes in both single and additive ClientSynchronizationMode
        /// </summary>
        [UnityTest]
        public IEnumerator SceneEventDataPoolSceneLoadingTest([Values(LoadSceneMode.Single, LoadSceneMode.Additive)] LoadSceneMode clientSynchronizationMode, [Values(1, 2, 4, 8, 16, 32)] int numberOfScenesToLoad)
        {
            m_MultiSceneTest = true;
            m_ClientVerificationAction = DataPoolVerifySceneClient;
            m_ServerVerificationAction = DataPoolVerifySceneServer;

            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_ServerNetworkManager.SceneManager.DisableValidationWarnings(true);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.SceneManager.DisableValidationWarnings(true);
            }

            // Now prepare for the loading and unloading additive scene testing
            InitializeSceneTestInfo(clientSynchronizationMode, true);

            Scene currentlyActiveScene = SceneManager.GetActiveScene();

            // Now load the base scene
            m_CurrentSceneName = k_BaseUnitTestSceneName;
            yield return LoadScene(m_CurrentSceneName);

            var firstScene = m_CurrentScene;

            m_CurrentSceneName = k_MultiInstanceTestScenename;
            SceneManager.SetActiveScene(m_CurrentScene);
            // Now load the scene(s)
            for (int i = 0; i < numberOfScenesToLoad; i++)
            {
                yield return LoadScene(m_CurrentSceneName);
            }

            // Reverse how we unload the scenes
            m_ScenesLoaded.Reverse();

            // Now unload the scene(s)
            foreach (var scene in m_ScenesLoaded)
            {
                yield return UnloadScene(scene);
            }
            SceneManager.SetActiveScene(currentlyActiveScene);
            m_MultiSceneTest = false;
            yield break;
        }
    }

    /// <summary>
    /// This is where all of the SceneEventData specific tests should reside.
    /// </summary>
    public class SceneEventDataTests
    {
        /// <summary>
        /// This verifies that change from Allocator.TmpJob to Allocator.Persistent
        /// will not cause memory leak warning notifications if the scene event takes
        /// longer than 4 frames to complete.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator FastReaderAllocationTest()
        {
            var fastBufferWriter = new FastBufferWriter(1024, Unity.Collections.Allocator.Persistent);
            var networkManagerGameObject = new GameObject("NetworkManager - Host");

            var networkManager = networkManagerGameObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig()
            {
                ConnectionApproval = false,
                NetworkPrefabs = new List<NetworkPrefab>(),
                NetworkTransport = networkManagerGameObject.AddComponent<SIPTransport>(),
            };

            networkManager.StartHost();

            var sceneEventData = new SceneEventData(networkManager);
            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Load;
            sceneEventData.SceneHash = XXHash.Hash32("SomeRandomSceneName");
            sceneEventData.SceneEventProgressId = Guid.NewGuid();
            sceneEventData.LoadSceneMode = LoadSceneMode.Single;
            sceneEventData.SceneHandle = 32768;

            sceneEventData.Serialize(fastBufferWriter);
            var nativeArray = new Unity.Collections.NativeArray<byte>(fastBufferWriter.ToArray(), Unity.Collections.Allocator.Persistent);
            var fastBufferReader = new FastBufferReader(nativeArray, Unity.Collections.Allocator.Persistent, fastBufferWriter.ToArray().Length);

            var incomingSceneEventData = new SceneEventData(networkManager);
            incomingSceneEventData.Deserialize(fastBufferReader);

            // Wait for 30 frames
            var framesToWait = Time.frameCount + 30;
            yield return new WaitUntil(() => Time.frameCount > framesToWait);

            // As long as no errors occurred, the test verifies that
            incomingSceneEventData.Dispose();
            fastBufferReader.Dispose();
            nativeArray.Dispose();
            fastBufferWriter.Dispose();
            networkManager.Shutdown();
            UnityEngine.Object.Destroy(networkManagerGameObject);
        }
    }
}
