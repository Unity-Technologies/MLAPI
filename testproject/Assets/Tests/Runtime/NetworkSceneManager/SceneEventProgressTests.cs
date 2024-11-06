using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class SceneEventProgressTests : NetcodeIntegrationTest
    {
        private const string k_SceneUsedToGetAsyncOperation = "EmptyScene";
        private const string k_SceneUsedToGetClientAsyncOperation = "UnitTestBaseScene";

        protected override int NumberOfClients => 4;

        private bool m_SceneEventProgressCompleted;
        private SceneEventProgress m_CurrentSceneEventProgress;

        private List<ulong> m_ClientThatShouldNotHaveCompleted = new List<ulong>();
        private List<ulong> m_ClientThatShouldHaveCompleted = new List<ulong>();

        public SceneEventProgressTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        private bool SceneEventProgressComplete(SceneEventProgress sceneEventProgress)
        {
            m_SceneEventProgressCompleted = true;
            return true;
        }

        private void SetClientFinished(ulong clientId, bool finished)
        {
            if (finished)
            {
                m_ClientThatShouldHaveCompleted.Add(clientId);
                m_CurrentSceneEventProgress.ClientFinishedSceneEvent(clientId);
            }
            else
            {
                m_ClientThatShouldNotHaveCompleted.Add(clientId);
            }
        }

        private void StartNewSceneEventProgress()
        {
            m_SceneEventProgressCompleted = false;
            m_ClientThatShouldNotHaveCompleted.Clear();
            m_ClientThatShouldHaveCompleted.Clear();
            m_CurrentSceneEventProgress = new SceneEventProgress(m_ServerNetworkManager, SceneEventProgressStatus.Started)
            {
                OnComplete = SceneEventProgressComplete
            };
            SceneManager.sceneLoaded += MockServerLoadedSene;
            m_CurrentSceneEventProgress.SetAsyncOperation(SceneManager.LoadSceneAsync(k_SceneUsedToGetAsyncOperation, LoadSceneMode.Additive));
        }


        private void MockServerLoadedSene(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (scene.name == k_SceneUsedToGetAsyncOperation)
            {
                SetClientFinished(NetworkManager.ServerClientId, true);
                SceneManager.sceneLoaded -= MockServerLoadedSene;
            }
        }

        private void VerifyClientsThatCompleted()
        {
            var clientsDidComplete = m_CurrentSceneEventProgress.GetClientsWithStatus(true);
            var clientsDidNotComplete = m_CurrentSceneEventProgress.GetClientsWithStatus(false);

            foreach (var clientId in clientsDidComplete)
            {
                Assert.IsTrue(m_ClientThatShouldHaveCompleted.Contains(clientId), $"Client-{clientId} was not in the SceneEventProgress completed list!");
            }

            foreach (var clientId in clientsDidNotComplete)
            {
                Assert.IsTrue(m_ClientThatShouldNotHaveCompleted.Contains(clientId), $"Client-{clientId} was not in the SceneEventProgress did not complete list!");
            }
        }

        /// <summary>
        /// This verifies when all clients finish, the SceneEventProgress
        /// completes as well.
        /// </summary>
        [UnityTest]
        public IEnumerator AllClientsFinish()
        {
            StartNewSceneEventProgress();

            for (int i = 0; i < NumberOfClients; i++)
            {
                var currentClientNetworkManager = m_ClientNetworkManagers[i];
                SetClientFinished(currentClientNetworkManager.LocalClientId, true);

                // stagger when the clients mock finish loading by 1 network tick
                yield return s_DefaultWaitForTick;
            }

            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to time out!");

            VerifyClientsThatCompleted();
        }

        /// <summary>
        /// This verifies that SceneEventProgress will still invoke the
        /// OnComplete delegate handler when it times out
        /// </summary>
        [UnityTest]
        public IEnumerator CompletesWhenTimedOut()
        {
            // Adjust the timeout for 2 seconds for this test
            m_ServerNetworkManager.NetworkConfig.LoadSceneTimeOut = 2;
            StartNewSceneEventProgress();

            for (int i = 0; i < NumberOfClients; i++)
            {
                // Every other client fails to finish
                var clientFinished = i % 2 == 0;
                var currentClientNetworkManager = m_ClientNetworkManagers[i];
                SetClientFinished(currentClientNetworkManager.LocalClientId, clientFinished);
            }

            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to time out!");

            VerifyClientsThatCompleted();
        }

        /// <summary>
        /// This verifies that SceneEventProgress will still complete
        /// even when some of the originally connected clients disconnect
        /// during a SceneEventProgress.
        /// </summary>
        [UnityTest]
        public IEnumerator ClientsDisconnectDuring()
        {
            StartNewSceneEventProgress();

            for (int i = 0; i < NumberOfClients; i++)
            {
                var currentClientNetworkManager = m_ClientNetworkManagers[i];
                // Two clients will disconnect
                var clientFinished = i % 2 == 0;
                SetClientFinished(currentClientNetworkManager.LocalClientId, clientFinished);

                if (!clientFinished)
                {
                    currentClientNetworkManager.Shutdown();
                }
                // wait anywhere from 100-500ms until processing next client
                var randomWaitPeriod = Random.Range(0.1f, 0.5f);
                yield return new WaitForSeconds(randomWaitPeriod);
            }
            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to time out!");
            VerifyClientsThatCompleted();
        }

        [UnityTest]
        public IEnumerator ClientsShutdownDuring()
        {
            StartNewSceneEventProgress();

            for (int i = 0; i < NumberOfClients; i++)
            {
                var currentClientNetworkManager = m_ClientNetworkManagers[i];
                // Two clients will shutdown
                var clientFinished = i % 2 == 0;
                SetClientFinished(currentClientNetworkManager.LocalClientId, clientFinished);

                if (!clientFinished)
                {
                    currentClientNetworkManager.Shutdown();
                }
                // wait anywhere from 100-500ms until processing next client
                var randomWaitPeriod = Random.Range(0.1f, 0.5f);
                yield return new WaitForSeconds(randomWaitPeriod);
            }
            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to time out!");
            VerifyClientsThatCompleted();
        }

        /// <summary>
        /// This verifies that SceneEventProgress will still complete
        /// even when clients late join.
        /// </summary>
        [UnityTest]
        public IEnumerator ClientsLateJoinDuring()
        {
            StartNewSceneEventProgress();
            for (int i = 0; i < NumberOfClients; i++)
            {
                // Two clients will connect during a SceneEventProgress
                var shouldNewClientJoin = i % 2 == 0;
                var currentClientNetworkManager = m_ClientNetworkManagers[i];
                // All connected clients will finish their SceneEventProgress
                SetClientFinished(currentClientNetworkManager.LocalClientId, true);
                if (shouldNewClientJoin)
                {
                    yield return CreateAndStartNewClient();
                }
                // wait anywhere from 100-500ms until processing next client
                var randomWaitPeriod = Random.Range(0.1f, 0.5f);
                yield return new WaitForSeconds(randomWaitPeriod);
            }
            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to finish!");
            VerifyClientsThatCompleted();
        }

        private List<ulong> m_ClientsThatTimedOutAndFinished = new List<ulong>();

        private SceneEventProgress StartClientSceneEventProgress(NetworkManager networkManager)
        {
            var sceneEventProgress = new SceneEventProgress(networkManager, SceneEventProgressStatus.Started);

            // Mock Scene Loading Event for mocking timed out client
            m_CurrentSceneEventProgress.SceneEventId = (uint)networkManager.LocalClientId;
            var asyncOperation = SceneManager.LoadSceneAsync(k_SceneUsedToGetClientAsyncOperation, LoadSceneMode.Additive);
            asyncOperation.completed += new System.Action<AsyncOperation>(asyncOp2 =>
            {
                m_ClientsThatTimedOutAndFinished.Add(networkManager.LocalClientId);
            });

            m_CurrentSceneEventProgress.SetAsyncOperation(asyncOperation);
            return sceneEventProgress;
        }

        private Dictionary<NetworkManager, SceneEventProgress> m_ClientsToFinishAfterDisconnecting = new Dictionary<NetworkManager, SceneEventProgress>();
        private bool TimedOutClientsFinishedSceneEventProgress()
        {
            // Now, verify all "mock timed out" clients still finished their SceneEventProgress
            foreach (var entry in m_ClientsToFinishAfterDisconnecting)
            {
                if (!m_ClientsThatTimedOutAndFinished.Contains(entry.Key.LocalClientId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// This mocks a client timing out during a SceneEventProgress
        /// </summary>
        [UnityTest]
        public IEnumerator ClientsMockTimeOutDuring()
        {
            m_ClientsThatTimedOutAndFinished.Clear();
            m_ClientsToFinishAfterDisconnecting.Clear();
            StartNewSceneEventProgress();

            for (int i = 0; i < NumberOfClients; i++)
            {
                // Two clients will mock timing out during a SceneEventProgress
                var shouldClientFinish = i % 2 == 0;
                var currentClientNetworkManager = m_ClientNetworkManagers[i];

                // Set whether the client should or should not have finished
                SetClientFinished(currentClientNetworkManager.LocalClientId, shouldClientFinish);
                if (!shouldClientFinish)
                {
                    var sceneEventProgress = StartClientSceneEventProgress(currentClientNetworkManager);
                    m_ClientsToFinishAfterDisconnecting.Add(currentClientNetworkManager, sceneEventProgress);
                    currentClientNetworkManager.Shutdown();
                }
            }

            yield return WaitForConditionOrTimeOut(() => m_SceneEventProgressCompleted);
            AssertOnTimeout($"Timed out waiting for SceneEventProgress to finish!");
            VerifyClientsThatCompleted();

            yield return WaitForConditionOrTimeOut(TimedOutClientsFinishedSceneEventProgress);
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                foreach (var entry in m_ClientsToFinishAfterDisconnecting)
                {
                    Assert.IsTrue(m_ClientsThatTimedOutAndFinished.Contains(entry.Key.LocalClientId), $"Client-{entry.Key.LocalClientId} did not complete its {nameof(SceneEventProgress)}!");
                    // Now, as a final check we try to finish the "mock" timed out client's scene event progress
                    entry.Value.TryFinishingSceneEventProgress();
                }
            }
            m_ClientsToFinishAfterDisconnecting.Clear();
        }
    }
}
