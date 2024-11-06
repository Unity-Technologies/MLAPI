using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class ClientSynchronizationValidationTest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 0;
        private const string k_FirstSceneToLoad = "UnitTestBaseScene";
        private const string k_SecondSceneToLoad = "InSceneNetworkObject";
        private const string k_ThirdSceneToSkip = "EmptyScene";
        private Scene m_RuntimeGeneratedScene;
        private bool m_IncludeSceneVerificationHandler;
        private bool m_RuntimeSceneWasExcludedFromSynch;

        private List<ClientSceneVerificationHandler> m_ClientSceneVerifiers = new List<ClientSceneVerificationHandler>();
        public ClientSynchronizationValidationTest(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override void OnNewClientStarted(NetworkManager networkManager)
        {
            if (m_IncludeSceneVerificationHandler)
            {
                m_ClientSceneVerifiers.Add(new ClientSceneVerificationHandler(networkManager));
            }
            base.OnNewClientStarted(networkManager);
        }

        /// <summary>
        /// Handle excluding runtime scene from synchronization
        /// </summary>
        private bool OnServerVerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            // exclude test runner scene
            if (sceneName.StartsWith(NetcodeIntegrationTestHelpers.FirstPartOfTestRunnerSceneName))
            {
                return false;
            }

            // Exclude the runtime generated scene
            if (sceneIndex == m_RuntimeGeneratedScene.buildIndex && m_RuntimeGeneratedScene.name == sceneName)
            {
                m_RuntimeSceneWasExcludedFromSynch = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Test that validates users can exclude runtime generated scenes from the initial client synchronization
        /// process using <see cref="NetworkSceneManager.VerifySceneBeforeLoading"/>
        /// </summary>
        [UnityTest]
        public IEnumerator ClientSynchWithServerSideRuntimeGeneratedScene()
        {
            m_IncludeSceneVerificationHandler = false;
            m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading = OnServerVerifySceneBeforeLoading;
            m_ServerNetworkManager.SceneManager.DisableValidationWarnings(true);
            // For this test we want to disable the check for scenes in build list
            m_ServerNetworkManager.SceneManager.ExcludeSceneFromSychronization = null;
            // Create a runtime scene in the server side
            m_RuntimeGeneratedScene = SceneManager.CreateScene("RuntimeGeneratedScene");
            yield return s_DefaultWaitForTick;
            yield return CreateAndStartNewClient();

            Assert.True(m_RuntimeSceneWasExcludedFromSynch, $"Server did not exclude the runtime generated scene when creating synchronization message data!");
        }

        /// <summary>
        /// Validates that connecting clients will exclude scenes using <see cref="NetworkSceneManager.VerifySceneBeforeLoading"/>
        /// </summary>
        [UnityTest]
        public IEnumerator ClientVerifySceneBeforeLoading()
        {
            m_IncludeSceneVerificationHandler = true;
            var scenesToLoad = new List<string>() { k_FirstSceneToLoad, k_SecondSceneToLoad, k_ThirdSceneToSkip };
            m_ServerNetworkManager.SceneManager.OnLoadComplete += OnLoadComplete;
            foreach (var sceneToLoad in scenesToLoad)
            {
                m_SceneBeingLoadedIsLoaded = false;
                m_SceneBeingLoaded = sceneToLoad;
                m_ServerNetworkManager.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);

                yield return WaitForConditionOrTimeOut(() => m_SceneBeingLoadedIsLoaded);
                AssertOnTimeout($"Timed out waiting for scene {m_SceneBeingLoaded} to finish loading!");
            }

            yield return CreateAndStartNewClient();

            yield return WaitForConditionOrTimeOut(m_ClientSceneVerifiers[0].HasLoadedExpectedScenes);
            AssertOnTimeout($"Timed out waiting for the client to have loaded the expected scenes");

            // Check to make sure only the two scenes were loaded and one
            // completely skipped.
            foreach (var clientSceneVerifier in m_ClientSceneVerifiers)
            {
                clientSceneVerifier.ValidateScenesLoaded();
            }
        }

        private string m_SceneBeingLoaded;
        private bool m_SceneBeingLoadedIsLoaded;

        private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (m_SceneBeingLoaded == sceneName)
            {
                m_SceneBeingLoadedIsLoaded = true;
            }
        }


        /// <summary>
        /// Determines if all clients loaded only two of the 3 scenes
        /// </summary>
        private class ClientSceneVerificationHandler
        {
            private NetworkManager m_NetworkManager;
            private Dictionary<string, int> m_ValidSceneEventCount = new Dictionary<string, int>();
            public ClientSceneVerificationHandler(NetworkManager networkManager)
            {
                m_NetworkManager = networkManager;
                m_NetworkManager.SceneManager.DisableValidationWarnings(true);
                m_NetworkManager.SceneManager.VerifySceneBeforeLoading = VerifySceneBeforeLoading;
                m_NetworkManager.SceneManager.OnLoad += ClientSceneManager_OnLoad;
                m_NetworkManager.SceneManager.OnLoadComplete += ClientSceneManager_OnLoadComplete;
                m_ValidSceneEventCount.Add(k_FirstSceneToLoad, 0);
                m_ValidSceneEventCount.Add(k_SecondSceneToLoad, 0);
                m_ValidSceneEventCount.Add(k_ThirdSceneToSkip, 0);
            }

            public bool HasLoadedExpectedScenes()
            {
                return m_ValidSceneEventCount[k_FirstSceneToLoad] == 2 && m_ValidSceneEventCount[k_SecondSceneToLoad] == 2;
            }

            public void ValidateScenesLoaded()
            {
                Assert.IsTrue(m_ValidSceneEventCount[k_ThirdSceneToSkip] == 0, $"Client still loaded the invalidated scene {k_ThirdSceneToSkip}!");
                Assert.IsTrue(m_ValidSceneEventCount[k_FirstSceneToLoad] == 2, $"Client did not load and process the validated scene {k_FirstSceneToLoad}! Expected (1) but was ({m_ValidSceneEventCount[k_FirstSceneToLoad]})");
                Assert.IsTrue(m_ValidSceneEventCount[k_SecondSceneToLoad] == 2, $"Client did not load and process the validated scene {k_SecondSceneToLoad}! Expected (1) but was ({m_ValidSceneEventCount[k_SecondSceneToLoad]})");
            }

            private void ClientSceneManager_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
            {
                if (m_ValidSceneEventCount.ContainsKey(sceneName))
                {
                    m_ValidSceneEventCount[sceneName]++;
                }
            }

            private void ClientSceneManager_OnLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
            {
                if (m_ValidSceneEventCount.ContainsKey(sceneName))
                {
                    m_ValidSceneEventCount[sceneName]++;
                }
            }

            private bool VerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
            {
                if (sceneName == k_ThirdSceneToSkip)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
