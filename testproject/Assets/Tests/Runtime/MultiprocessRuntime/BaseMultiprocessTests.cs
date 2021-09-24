using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.MultiprocessRuntimeTests
{
    public class MultiprocessTestsAttribute : CategoryAttribute
    {
        public const string MultiprocessCategoryName = "Multiprocess";
        public MultiprocessTestsAttribute() : base(MultiprocessCategoryName) { }
    }

    [MultiprocessTests]
    public abstract class BaseMultiprocessTests
    {
        private bool m_HasSceneLoaded = false;
        // TODO: Remove UTR check once we have Multiprocess tests fully working
        protected bool IgnoreMultiprocessTests => MultiprocessOrchestration.ShouldIgnoreUTRTests();

        protected virtual bool IsPerformanceTest => true;

        /// <summary>
        /// Implement this to specify the amount of workers to spawn from your main test runner
        /// TODO there's a good chance this will be re-factored with something fancier once we start integrating with bokken
        /// </summary>
        protected abstract int WorkerCount { get; }

        private const string k_FirstPartOfTestRunnerSceneName = "InitTestScene";

        // Since we want to additively load our BuildMultiprocessTestPlayer.MainSceneName
        // We want to keep a reference to the
        private Scene m_OriginalActiveScene;

        [OneTimeSetUp]
        public virtual void SetupTestSuite()
        {
            MultiProcessLog("Running SetupTestSuite - OneTimeSetup");
            if (IgnoreMultiprocessTests)
            {
                Assert.Ignore("Ignoring tests under UTR. For testing, include the \"-bypassIgnoreUTR\" command line parameter.");
            }

            if (IsPerformanceTest)
            {
                Assert.Ignore("Performance tests should be run from remote test execution on device (this can be ran using the \"run selected tests (your platform)\" button");
            }
            MultiProcessLog($"Currently active scene {SceneManager.GetActiveScene().name}");
            var currentlyActiveScene = SceneManager.GetActiveScene();

            // Just adding a sanity check here to help with debugging in the event that SetupTestSuite is
            // being invoked and the TestRunner scene has not been set to the active scene yet.
            // This could mean that TeardownSuite wasn't called or SceneManager is not finished unloading
            // or could not unload the BuildMultiprocessTestPlayer.MainSceneName.
            if (!currentlyActiveScene.name.StartsWith(k_FirstPartOfTestRunnerSceneName))
            {
                Debug.LogError($"Expected the currently active scene to begin with ({k_FirstPartOfTestRunnerSceneName}) but currently active scene is {currentlyActiveScene.name}");
            }
            m_OriginalActiveScene = currentlyActiveScene;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(BuildMultiprocessTestPlayer.MainSceneName, LoadSceneMode.Additive);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MultiProcessLog($"OnSceneLoaded {scene.name}");
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (scene.name == BuildMultiprocessTestPlayer.MainSceneName)
            {
                SceneManager.SetActiveScene(scene);
            }

            NetworkManager.Singleton.StartHost();

            // Use scene verification to make sure we don't try to get clients to synchronize the TestRunner scene
            NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = VerifySceneIsValidForClientsToLoad;

            m_HasSceneLoaded = true;
        }

        /// <summary>
        /// We want to exclude the TestRunner scene on the host-server side so it won't try to tell clients to
        /// synchronize to this scene when they connect (host-server side only for multiprocess)
        /// </summary>
        /// <returns>true - scene is fine to synchronize/inform clients to load and false - scene should not be loaded by clients</returns>
        private bool VerifySceneIsValidForClientsToLoad(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (sceneName.StartsWith(k_FirstPartOfTestRunnerSceneName))
            {
                return false;
            }
            return true;
        }

        [UnitySetUp]
        public virtual IEnumerator Setup()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null);
            MultiProcessLog("NetworkManager.Singleton != null");
            yield return new WaitUntil(() => NetworkManager.Singleton.IsServer);
            MultiProcessLog("NetworkManager.Singleton.IsServer");
            yield return new WaitUntil(() => NetworkManager.Singleton.IsListening);
            MultiProcessLog("NetworkManager.Singleton.IsListening");
            yield return new WaitUntil(() => m_HasSceneLoaded == true);
            MultiProcessLog("m_HasSceneLoaded");
            var startTime = Time.time;

            MultiProcessLog($"Active Worker Count is {MultiprocessOrchestration.ActiveWorkerCount()} and connected client count is {NetworkManager.Singleton.ConnectedClients.Count}");
            if (MultiprocessOrchestration.ActiveWorkerCount() + 1 < NetworkManager.Singleton.ConnectedClients.Count)
            {
                MultiProcessLog("Is this a bad state?");
            }

            // Moved this out of OnSceneLoaded as OnSceneLoaded is a callback from the SceneManager and just wanted to avoid creating
            // processes from within the same callstack/context as the SceneManager.  This will instantiate up to the WorkerCount and
            // then any subsequent calls to Setup if there are already workers it will skip this step
            if (NetworkManager.Singleton.ConnectedClients.Count - 1 < WorkerCount)
            {
                var numProcessesToCreate = WorkerCount - (NetworkManager.Singleton.ConnectedClients.Count - 1);
                for (int i = 1; i <= numProcessesToCreate; i++)
                {
                    MultiProcessLog($"Spawning testplayer {i} since connected client count is {NetworkManager.Singleton.ConnectedClients.Count} is less than {WorkerCount} and Number of spawned external players is {MultiprocessOrchestration.ActiveWorkerCount()} ");
                    string logPath = MultiprocessOrchestration.StartWorkerNode(); // will automatically start built player as clients
                    MultiProcessLog($"logPath to new process is {logPath}");
                    yield return new WaitForSeconds(5.0f); // Wait 5 seconds before starting the next process because there are odd contentions that happen when starting multiple processes quickly
                    MultiProcessLog($"Active Worker Count {MultiprocessOrchestration.ActiveWorkerCount()} and connected client count is {NetworkManager.Singleton.ConnectedClients.Count}");
                }
            }
            else
            {
                MultiProcessLog($"No need to spawn a new test player as there are already existing processes {MultiprocessOrchestration.ActiveWorkerCount()} and connected clients {NetworkManager.Singleton.ConnectedClients.Count}");
            }

            var timeOutTime = Time.realtimeSinceStartup + TestCoordinator.MaxWaitTimeoutSec;
            while (NetworkManager.Singleton.ConnectedClients.Count <= WorkerCount)
            {
                yield return new WaitForSeconds(0.2f);

                if (Time.realtimeSinceStartup > timeOutTime)
                {
                    throw new Exception($" {DateTime.Now.ToString("G")} Waiting too long to see clients to connect, got {NetworkManager.Singleton.ConnectedClients.Count - 1} clients, and ActiveWorkerCount: {MultiprocessOrchestration.ActiveWorkerCount()} but was expecting {WorkerCount}, failing");
                }
            }
            TestCoordinator.Instance.KeepAliveClientRpc();
            MultiProcessLog($"Active Worker Count {MultiprocessOrchestration.ActiveWorkerCount()} and connected client count is {NetworkManager.Singleton.ConnectedClients.Count}");
        }


        [TearDown]
        public virtual void Teardown()
        {
            MultiProcessLog("Running teardown");
            if (!IgnoreMultiprocessTests)
            {
                TestCoordinator.Instance.TestRunTeardown();
            }
        }

        [OneTimeTearDown]
        public virtual void TeardownSuite()
        {
            MultiProcessLog($"TeardownSuite");
            if (!IgnoreMultiprocessTests)
            {
                MultiProcessLog($"TeardownSuite - ShutdownAllProcesses");
                MultiprocessOrchestration.ShutdownAllProcesses();
                MultiProcessLog($"TeardownSuite - NetworkManager.Singleton.Shutdown");
                NetworkManager.Singleton.Shutdown();
                Object.Destroy(NetworkManager.Singleton.gameObject); // making sure we clear everything before reloading our scene
                MultiProcessLog($"Currently active scene {SceneManager.GetActiveScene().name}");
                MultiProcessLog($"m_OriginalActiveScene.IsValid {m_OriginalActiveScene.IsValid()}");
                if (m_OriginalActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(m_OriginalActiveScene);
                }
                MultiProcessLog($"TeardownSuite - Unload {BuildMultiprocessTestPlayer.MainSceneName}");
                SceneManager.UnloadSceneAsync(BuildMultiprocessTestPlayer.MainSceneName);
                MultiProcessLog($"TeardownSuite - Unload {BuildMultiprocessTestPlayer.MainSceneName}");
            }
        }

        public static void MultiProcessLog(string msg)
        {
            string testName = null;
            try
            {
                testName = TestContext.CurrentContext.Test.Name;
            }
            catch (NullReferenceException nre)
            {
                testName = "N/A";
            }

            if (string.IsNullOrEmpty(testName))
            {
                testName = "unknwon";
            }
            string dString = DateTime.Now.ToString("G");
            Debug.Log($" - MPLOG - {dString} : {testName} : {msg}");
        }
    }
}

