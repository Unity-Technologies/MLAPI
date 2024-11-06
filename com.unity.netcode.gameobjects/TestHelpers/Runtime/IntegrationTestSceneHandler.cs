using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.Netcode.TestHelpers.Runtime
{
    /// <summary>
    /// The default SceneManagerHandler used for all NetcodeIntegrationTest derived children.
    /// This enables clients to load scenes within the same scene hierarchy during integration
    /// testing.
    /// </summary>
    internal class IntegrationTestSceneHandler : ISceneManagerHandler, IDisposable
    {
        private Scene m_InvalidScene = new Scene();

        internal struct SceneEntry
        {
            public bool IsAssigned;
            public Scene Scene;
        }

        public bool IsIntegrationTest() { return true; }

        internal static Dictionary<NetworkManager, Dictionary<string, Dictionary<int, SceneEntry>>> SceneNameToSceneHandles = new Dictionary<NetworkManager, Dictionary<string, Dictionary<int, SceneEntry>>>();

        // All IntegrationTestSceneHandler instances register their associated NetworkManager
        internal static List<NetworkManager> NetworkManagers = new List<NetworkManager>();

        internal static CoroutineRunner CoroutineRunner;

        internal static Queue<QueuedSceneJob> QueuedSceneJobs = new Queue<QueuedSceneJob>();
        internal List<Coroutine> CoroutinesRunning = new List<Coroutine>();
        internal static Coroutine SceneJobProcessor;
        internal static QueuedSceneJob CurrentQueuedSceneJob;
        protected static WaitForSeconds s_WaitForSeconds;


        public delegate bool CanClientsLoadUnloadDelegateHandler();
        public static event CanClientsLoadUnloadDelegateHandler CanClientsLoad;
        public static event CanClientsLoadUnloadDelegateHandler CanClientsUnload;


        public static bool VerboseDebugMode;
        /// <summary>
        /// Used for loading scenes on the client-side during
        /// an integration test
        /// </summary>
        internal class QueuedSceneJob
        {
            public enum JobTypes
            {
                Loading,
                Unloading,
                Completed
            }
            public JobTypes JobType;
            public string SceneName;
            public SceneInstance Scene;
            public SceneEventProgress SceneEventProgress;
            public IntegrationTestSceneHandler IntegrationTestSceneHandler;
        }

        internal NetworkManager NetworkManager;

        internal string NetworkManagerName;

        /// <summary>
        /// Used to control when clients should attempt to fake-load a scene
        /// Note: Unit/Integration tests that only use <see cref="NetcodeIntegrationTestHelpers"/>
        /// need to subscribe to the CanClientsLoad and CanClientsUnload events
        /// in order to control when clients can fake-load.
        /// Tests that derive from <see cref="NetcodeIntegrationTest"/> already have integrated
        /// support and you can override <see cref="NetcodeIntegrationTest.CanClientsLoad"/> and
        /// <see cref="NetcodeIntegrationTest.CanClientsUnload"/>.
        /// </summary>
        protected bool OnCanClientsLoad()
        {
            if (CanClientsLoad != null)
            {
                return CanClientsLoad.Invoke();
            }
            return true;
        }

        protected bool OnCanClientsUnload()
        {
            if (CanClientsUnload != null)
            {
                return CanClientsUnload.Invoke();
            }
            return true;
        }


        internal static void VerboseDebug(string message)
        {
            if (VerboseDebugMode)
            {
                Debug.Log(message);
            }
        }

        /// <summary>
        /// Processes scene loading jobs
        /// </summary>
        /// <param name="queuedSceneJob">job to process</param>
        internal static IEnumerator ProcessLoadingSceneJob(QueuedSceneJob queuedSceneJob)
        {
            var itegrationTestSceneHandler = queuedSceneJob.IntegrationTestSceneHandler;
            while (!itegrationTestSceneHandler.OnCanClientsLoad())
            {
                yield return s_WaitForSeconds;
            }

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            // We always load additively for all scenes during integration tests
            var asyncOperation = Addressables.LoadSceneAsync(queuedSceneJob.SceneName, LoadSceneMode.Additive);

            queuedSceneJob.SceneEventProgress.SetAsyncOperation(asyncOperation);

            // Wait for it to finish
            while (queuedSceneJob.JobType != QueuedSceneJob.JobTypes.Completed)
            {
                yield return s_WaitForSeconds;
            }
            yield return s_WaitForSeconds;
        }

        /// <summary>
        /// Handles scene loading and assists with making sure the right NetworkManagerOwner
        /// is assigned to newly instantiated NetworkObjects.
        ///
        /// Note: Static property usage is OK since jobs are processed one at a time
        /// </summary>
        private static void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (CurrentQueuedSceneJob.JobType != QueuedSceneJob.JobTypes.Completed && CurrentQueuedSceneJob.SceneName == scene.name)
            {
                SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

                ProcessInSceneObjects(scene, CurrentQueuedSceneJob.IntegrationTestSceneHandler.NetworkManager);

                CurrentQueuedSceneJob.JobType = QueuedSceneJob.JobTypes.Completed;
            }
        }

        /// <summary>
        /// Handles some pre-spawn processing of in-scene placed NetworkObjects
        /// to make sure the appropriate NetworkManagerOwner is assigned.  It
        /// also makes sure that each in-scene placed NetworkObject has an
        /// ObjectIdentifier component if one is not assigned to it or its
        /// children.
        /// </summary>
        /// <param name="scene">the scenes that was just loaded</param>
        /// <param name="networkManager">the relative NetworkManager</param>
        private static void ProcessInSceneObjects(Scene scene, NetworkManager networkManager)
        {
            // Get all in-scene placed NeworkObjects that were instantiated when this scene loaded
#if UNITY_2023_1_OR_NEWER
            var inSceneNetworkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.IsSceneObject != false && c.GetSceneOriginHandle() == scene.handle);
#else
            var inSceneNetworkObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsSceneObject != false && c.GetSceneOriginHandle() == scene.handle);
#endif

            foreach (var sobj in inSceneNetworkObjects)
            {
                ProcessInSceneObject(sobj, networkManager);
            }
        }

        /// <summary>
        /// Assures to apply an ObjectNameIdentifier to all children
        /// </summary>
        private static void ProcessInSceneObject(NetworkObject networkObject, NetworkManager networkManager)
        {
            if (networkObject.NetworkManagerOwner != networkManager)
            {
                networkObject.NetworkManagerOwner = networkManager;
            }
            if (networkObject.GetComponent<ObjectNameIdentifier>() == null)
            {
                networkObject.gameObject.AddComponent<ObjectNameIdentifier>();
                var networkObjects = networkObject.gameObject.GetComponentsInChildren<NetworkObject>();
                foreach (var child in networkObjects)
                {
                    if (child == networkObject)
                    {
                        continue;
                    }
                    ProcessInSceneObject(child, networkManager);
                }
            }
        }

        /// <summary>
        /// Processes scene unloading jobs
        /// </summary>
        /// <param name="queuedSceneJob">job to process</param>
        internal static IEnumerator ProcessUnloadingSceneJob(QueuedSceneJob queuedSceneJob)
        {
            var itegrationTestSceneHandler = queuedSceneJob.IntegrationTestSceneHandler;
            while (!itegrationTestSceneHandler.OnCanClientsUnload())
            {
                yield return s_WaitForSeconds;
            }

            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
            if (queuedSceneJob.Scene.Scene.IsValid() && queuedSceneJob.Scene.Scene.isLoaded && !queuedSceneJob.Scene.Scene.name.Contains(NetcodeIntegrationTestHelpers.FirstPartOfTestRunnerSceneName))
            {
                var asyncOperation = Addressables.UnloadSceneAsync(queuedSceneJob.Scene);
                queuedSceneJob.SceneEventProgress.SetAsyncOperation(asyncOperation);
            }
            else
            {
                CurrentQueuedSceneJob.JobType = QueuedSceneJob.JobTypes.Completed;
            }

            // Wait for it to finish
            while (queuedSceneJob.JobType != QueuedSceneJob.JobTypes.Completed)
            {
                yield return s_WaitForSeconds;
            }
        }

        /// <summary>
        /// Handles closing out scene unloading jobs
        /// </summary>
        private static void SceneManager_sceneUnloaded(Scene scene)
        {
            if (CurrentQueuedSceneJob.JobType != QueuedSceneJob.JobTypes.Completed && CurrentQueuedSceneJob.Scene.Scene.name == scene.name)
            {
                SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;

                CurrentQueuedSceneJob.JobType = QueuedSceneJob.JobTypes.Completed;
            }
        }

        /// <summary>
        /// Processes all jobs within the queue.
        /// When all jobs are finished, the coroutine stops.
        /// </summary>
        internal static IEnumerator JobQueueProcessor()
        {
            while (QueuedSceneJobs.Count != 0)
            {
                CurrentQueuedSceneJob = QueuedSceneJobs.Dequeue();
                VerboseDebug($"[ITSH-START] {CurrentQueuedSceneJob.IntegrationTestSceneHandler.NetworkManagerName} processing {CurrentQueuedSceneJob.JobType} for scene {CurrentQueuedSceneJob.SceneName}.");
                if (CurrentQueuedSceneJob.JobType == QueuedSceneJob.JobTypes.Loading)
                {
                    yield return ProcessLoadingSceneJob(CurrentQueuedSceneJob);
                }
                else if (CurrentQueuedSceneJob.JobType == QueuedSceneJob.JobTypes.Unloading)
                {
                    yield return ProcessUnloadingSceneJob(CurrentQueuedSceneJob);
                }
                VerboseDebug($"[ITSH-STOP] {CurrentQueuedSceneJob.IntegrationTestSceneHandler.NetworkManagerName} processing {CurrentQueuedSceneJob.JobType} for scene {CurrentQueuedSceneJob.SceneName}.");
            }
            SceneJobProcessor = null;
            yield break;
        }

        /// <summary>
        /// Adds a job to the job queue, and if  the JobQueueProcessor coroutine
        /// is not running then it will be started as well.
        /// </summary>
        /// <param name="queuedSceneJob">job to add to the queue</param>
        private void AddJobToQueue(QueuedSceneJob queuedSceneJob)
        {
            QueuedSceneJobs.Enqueue(queuedSceneJob);
            if (SceneJobProcessor == null)
            {
                SceneJobProcessor = CoroutineRunner.StartCoroutine(JobQueueProcessor());
            }
        }

        private string m_ServerSceneBeingLoaded;
        /// <summary>
        /// Server always loads like it normally would
        /// </summary>
        public AsyncOperationHandle<SceneInstance> GenericLoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode, SceneEventProgress sceneEventProgress)
        {
            m_ServerSceneBeingLoaded = sceneName;
            if (NetcodeIntegrationTest.IsRunning)
            {
                SceneManager.sceneLoaded += Sever_SceneLoaded;
            }
            var operation = Addressables.LoadSceneAsync(sceneName, loadSceneMode);
            // sceneEventProgress.SetAsyncOperation(operation);
            return operation;
        }

        private void Sever_SceneLoaded(Scene scene, LoadSceneMode arg1)
        {
            if (m_ServerSceneBeingLoaded == scene.name)
            {
                SceneManager.sceneLoaded -= Sever_SceneLoaded;
                ProcessInSceneObjects(scene, NetworkManager);
            }
        }

        /// <summary>
        /// Server always unloads like it normally would
        /// </summary>
        public AsyncOperationHandle<SceneInstance> GenericUnloadSceneAsync(SceneInstance scene, SceneEventProgress sceneEventProgress)
        {
            var operation = Addressables.UnloadSceneAsync(scene);
            sceneEventProgress.SetAsyncOperation(operation);
            return operation;
        }


        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(string sceneAssetKey, LoadSceneMode loadSceneMode, SceneEventProgress sceneEventProgress)
        {
            // Server and non NetcodeIntegrationTest tests use the generic load scene method
            if (!NetcodeIntegrationTest.IsRunning)
            {
                return GenericLoadSceneAsync(sceneAssetKey, loadSceneMode, sceneEventProgress);
            }
            else // NetcodeIntegrationTest Clients always get added to the jobs queue
            {
                AddJobToQueue(new QueuedSceneJob() { IntegrationTestSceneHandler = this, SceneName = sceneAssetKey, SceneEventProgress = sceneEventProgress, JobType = QueuedSceneJob.JobTypes.Loading });
            }

            return default;
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(NetworkSceneManager.SceneData scene, SceneEventProgress sceneEventProgress)
        {
            // Server and non NetcodeIntegrationTest tests use the generic unload scene method
            if (!NetcodeIntegrationTest.IsRunning)
            {
                return GenericUnloadSceneAsync(scene.SceneInstance.Value, sceneEventProgress);
            }
            else // NetcodeIntegrationTest Clients always get added to the jobs queue
            {
                AddJobToQueue(new QueuedSceneJob() { IntegrationTestSceneHandler = this, Scene = scene.SceneInstance.Value, SceneEventProgress = sceneEventProgress, JobType = QueuedSceneJob.JobTypes.Unloading });
            }
            // This is OK to return a "nothing" AsyncOperation since we are simulating client loading
            return default;
        }

        /// <summary>
        /// Replacement callback takes other NetworkManagers into consideration
        /// </summary>
        internal Scene GetAndAddNewlyLoadedSceneByName(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sceneLoaded = SceneManager.GetSceneAt(i);
                if (sceneLoaded.name == sceneName)
                {
                    var skip = false;
                    foreach (var networkManager in NetworkManagers)
                    {
                        if (NetworkManager.LocalClientId == networkManager.LocalClientId || !networkManager.IsListening)
                        {
                            continue;
                        }

                        if (networkManager.SceneManager.ScenesLoaded.ContainsKey(sceneLoaded.handle))
                        {
                            if (NetworkManager.LogLevel == LogLevel.Developer)
                            {
                                NetworkLog.LogInfo($"{NetworkManager.name}'s ScenesLoaded contains {sceneLoaded.name} with a handle of {sceneLoaded.handle}.  Skipping over scene.");
                            }
                            skip = true;
                            break;
                        }
                    }

                    if (!skip && !NetworkManager.SceneManager.ScenesLoaded.ContainsKey(sceneLoaded.handle))
                    {
                        if (NetworkManager.LogLevel == LogLevel.Developer)
                        {
                            NetworkLog.LogInfo($"{NetworkManager.name} adding {sceneLoaded.name} with a handle of {sceneLoaded.handle} to its ScenesLoaded.");
                        }
                        if (DoesANetworkManagerHoldThisScene(sceneLoaded))
                        {
                            continue;
                        }
                        NetworkManager.SceneManager.ScenesLoaded.Add(sceneLoaded.handle, new NetworkSceneManager.SceneData(null, sceneLoaded));
                        StartTrackingScene(sceneLoaded, true, NetworkManager);
                        return sceneLoaded;
                    }
                }
            }

            throw new Exception($"Failed to find any loaded scene named {sceneName}!");
        }

        private bool ExcludeSceneFromSynchronizationCheck(Scene scene)
        {
            if (!NetworkManager.SceneManager.ScenesLoaded.ContainsKey(scene.handle) && SceneManager.GetActiveScene().handle != scene.handle)
            {
                return false;
            }
            return true;
        }

        public void ClearSceneTracking(NetworkManager networkManager)
        {
            SceneNameToSceneHandles.Clear();
        }

        public void StopTrackingScene(int handle, string name, NetworkManager networkManager)
        {
            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                return;
            }

            if (SceneNameToSceneHandles[networkManager].ContainsKey(name))
            {
                if (SceneNameToSceneHandles[networkManager][name].ContainsKey(handle))
                {
                    SceneNameToSceneHandles[networkManager][name].Remove(handle);
                    if (SceneNameToSceneHandles[networkManager][name].Count == 0)
                    {
                        SceneNameToSceneHandles[networkManager].Remove(name);
                    }
                }
            }
        }

        public void StartTrackingScene(Scene scene, bool assigned, NetworkManager networkManager)
        {
            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                SceneNameToSceneHandles.Add(networkManager, new Dictionary<string, Dictionary<int, SceneEntry>>());
            }

            if (!SceneNameToSceneHandles[networkManager].ContainsKey(scene.name))
            {
                SceneNameToSceneHandles[networkManager].Add(scene.name, new Dictionary<int, SceneEntry>());
            }

            if (!SceneNameToSceneHandles[networkManager][scene.name].ContainsKey(scene.handle))
            {
                var sceneEntry = new SceneEntry()
                {
                    IsAssigned = true,
                    Scene = scene
                };
                SceneNameToSceneHandles[networkManager][scene.name].Add(scene.handle, sceneEntry);
            }
        }

        private bool DoesANetworkManagerHoldThisScene(Scene scene)
        {
            foreach (var netManEntry in SceneNameToSceneHandles)
            {
                if (!netManEntry.Value.ContainsKey(scene.name))
                {
                    continue;
                }
                // The other NetworkManager only has to have an entry to
                // disqualify this scene instance
                if (netManEntry.Value[scene.name].ContainsKey(scene.handle))
                {
                    return true;
                }
            }

            return false;
        }

        public bool DoesSceneHaveUnassignedEntry(string sceneName, NetworkManager networkManager)
        {
            var scenesWithSceneName = new List<Scene>();
            var scenesAssigned = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName)
                {
                    scenesWithSceneName.Add(scene);
                }
            }

            // Check for other NetworkManager instances already having been assigned this scene
            foreach (var netManEntry in SceneNameToSceneHandles)
            {
                // Ignore this NetworkManager instance at this stage
                if (netManEntry.Key == networkManager)
                {
                    continue;
                }

                foreach (var scene in scenesWithSceneName)
                {
                    if (!netManEntry.Value.ContainsKey(scene.name))
                    {
                        continue;
                    }
                    // The other NetworkManager only has to have an entry to
                    // disqualify this scene instance
                    if (netManEntry.Value[scene.name].ContainsKey(scene.handle))
                    {
                        scenesAssigned.Add(scene);
                    }
                }
            }

            // Remove all of the assigned scenes from the list of scenes with the
            // passed in scene name.
            foreach (var assignedScene in scenesAssigned)
            {
                if (scenesWithSceneName.Contains(assignedScene))
                {
                    scenesWithSceneName.Remove(assignedScene);
                }
            }

            // If all currently loaded scenes with the scene name are taken
            // then we return false
            if (scenesWithSceneName.Count == 0)
            {
                return false;
            }

            // If we made it here, then no other NetworkManager is tracking this scene
            // and if we don't have an entry for this NetworkManager then we can use any
            // of the remaining scenes loaded with that name.
            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                return true;
            }

            // If we don't yet have a scene name in this NetworkManager's lookup table,
            // then we can use any of the remaining availabel scenes with that scene name
            if (!SceneNameToSceneHandles[networkManager].ContainsKey(sceneName))
            {
                return true;
            }

            foreach (var scene in scenesWithSceneName)
            {
                // If we don't have an entry for this scene handle (with the scene name) then we
                // can use that scene
                if (!SceneNameToSceneHandles[networkManager][scene.name].ContainsKey(scene.handle))
                {
                    return true;
                }

                // This entry is not assigned, then we can use the associated scene
                if (!SceneNameToSceneHandles[networkManager][scene.name][scene.handle].IsAssigned)
                {
                    return true;
                }
            }

            // None of the scenes with the same scene name can be used
            return false;
        }

        public Scene GetSceneFromLoadedScenes(string sceneName, NetworkManager networkManager)
        {

            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                return m_InvalidScene;
            }
            if (SceneNameToSceneHandles[networkManager].ContainsKey(sceneName))
            {
                foreach (var sceneHandleEntry in SceneNameToSceneHandles[networkManager][sceneName])
                {
                    if (!sceneHandleEntry.Value.IsAssigned)
                    {
                        var sceneEntry = sceneHandleEntry.Value;
                        sceneEntry.IsAssigned = true;
                        SceneNameToSceneHandles[networkManager][sceneName][sceneHandleEntry.Key] = sceneEntry;
                        return sceneEntry.Scene;
                    }
                }
            }
            // This is tricky since NetworkManager instances share the same scene hierarchy during integration tests.
            // TODO 2023: Determine if there is a better way to associate the active scene for client NetworkManager instances.
            var activeScene = SceneManager.GetActiveScene();

            if (sceneName == activeScene.name && networkManager.SceneManager.ClientSynchronizationMode == LoadSceneMode.Additive)
            {
                // For now, just return the current active scene
                // Note: Clients will not be able to synchronize in-scene placed NetworkObjects in an integration test for
                // scenes loaded that have in-scene placed NetworkObjects prior to the clients joining (i.e. there will only
                // ever be one instance of the active scene). To test in-scene placed NetworkObjects and make an integration
                // test loaded scene be the active scene, don't set scene as an active scene on the server side until all
                // clients have connected and loaded the scene.
                return activeScene;
            }
            // If we found nothing return an invalid scene
            return m_InvalidScene;
        }

        public void PopulateLoadedScenes(ref Dictionary<int, NetworkSceneManager.SceneData> scenesLoaded, NetworkManager networkManager)
        {
            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                SceneNameToSceneHandles.Add(networkManager, new Dictionary<string, Dictionary<int, SceneEntry>>());
            }

            var sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // Ignore scenes that belong to other NetworkManager instances

                if (DoesANetworkManagerHoldThisScene(scene))
                {
                    continue;
                }

                if (!DoesSceneHaveUnassignedEntry(scene.name, networkManager))
                {
                    continue;
                }

                if (!SceneNameToSceneHandles[networkManager].ContainsKey(scene.name))
                {
                    SceneNameToSceneHandles[networkManager].Add(scene.name, new Dictionary<int, SceneEntry>());
                }

                if (!SceneNameToSceneHandles[networkManager][scene.name].ContainsKey(scene.handle))
                {
                    var sceneEntry = new SceneEntry()
                    {
                        IsAssigned = false,
                        Scene = scene
                    };
                    SceneNameToSceneHandles[networkManager][scene.name].Add(scene.handle, sceneEntry);
                    if (!scenesLoaded.ContainsKey(scene.handle))
                    {
                        scenesLoaded.Add(scene.handle, new NetworkSceneManager.SceneData(null, scene));
                    }
                }
                else
                {
                    throw new Exception($"[{networkManager.LocalClient.PlayerObject.name}][Duplicate Handle] Scene {scene.name} already has scene handle {scene.handle} registered!");
                }
            }
        }

        private Dictionary<Scene, NetworkManager> m_ScenesToUnload = new Dictionary<Scene, NetworkManager>();

        /// <summary>
        /// Handles unloading any scenes that might remain on a client that
        /// need to be unloaded.
        /// </summary>
        /// <param name="networkManager"></param>
        public void UnloadUnassignedScenes(NetworkManager networkManager = null)
        {
            if (!SceneNameToSceneHandles.ContainsKey(networkManager))
            {
                return;
            }
            var relativeSceneNameToSceneHandles = SceneNameToSceneHandles[networkManager];
            var sceneManager = networkManager.SceneManager;
            SceneManager.sceneUnloaded += SceneManager_SceneUnloaded;

            foreach (var sceneEntry in relativeSceneNameToSceneHandles)
            {
                var scenHandleEntries = relativeSceneNameToSceneHandles[sceneEntry.Key];
                foreach (var sceneHandleEntry in scenHandleEntries)
                {
                    if (!sceneHandleEntry.Value.IsAssigned)
                    {
                        if (sceneManager.VerifySceneBeforeUnloading == null || sceneManager.VerifySceneBeforeUnloading.Invoke(sceneHandleEntry.Value.Scene))
                        {
                            m_ScenesToUnload.Add(sceneHandleEntry.Value.Scene, networkManager);
                        }
                    }
                }
            }

            foreach (var sceneToUnload in m_ScenesToUnload)
            {
                SceneManager.UnloadSceneAsync(sceneToUnload.Key);
                // Update the ScenesLoaded when we unload scenes
                if (sceneManager.ScenesLoaded.ContainsKey(sceneToUnload.Key.handle))
                {
                    sceneManager.ScenesLoaded.Remove(sceneToUnload.Key.handle);
                }
            }
        }

        /// <summary>
        /// Removes the scene entry from the scene name to scene handle table
        /// </summary>
        private void SceneManager_SceneUnloaded(Scene scene)
        {
            if (m_ScenesToUnload.ContainsKey(scene))
            {
                var networkManager = m_ScenesToUnload[scene];
                var relativeSceneNameToSceneHandles = SceneNameToSceneHandles[networkManager];
                if (relativeSceneNameToSceneHandles.ContainsKey(scene.name))
                {
                    var scenHandleEntries = relativeSceneNameToSceneHandles[scene.name];
                    if (scenHandleEntries.ContainsKey(scene.handle))
                    {
                        scenHandleEntries.Remove(scene.handle);
                        if (scenHandleEntries.Count == 0)
                        {
                            relativeSceneNameToSceneHandles.Remove(scene.name);
                        }
                        m_ScenesToUnload.Remove(scene);
                        if (m_ScenesToUnload.Count == 0)
                        {
                            SceneManager.sceneUnloaded -= SceneManager_SceneUnloaded;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Integration test version that handles migrating dynamically spawned NetworkObjects to
        /// the DDOL when a scene is unloaded
        /// </summary>
        /// <param name="networkManager"><see cref="NetworkManager"/> relative instance</param>
        /// <param name="scene">scene being unloaded</param>
        public void MoveObjectsFromSceneToDontDestroyOnLoad(ref NetworkManager networkManager, Scene scene)
        {
            // Create a local copy of the spawned objects list since the spawn manager will adjust the list as objects
            // are despawned.
#if UNITY_2023_1_OR_NEWER
            var networkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.IsSpawned);
#else
            var networkObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsSpawned);
#endif
            foreach (var networkObject in networkObjects)
            {
                if (networkObject == null || (networkObject != null && networkObject.gameObject.scene.handle != scene.handle))
                {
                    if (networkObject != null)
                    {
                        VerboseDebug($"[MoveObjects from {scene.name} | {scene.handle}] Ignoring {networkObject.gameObject.name} because it isn't in scene {networkObject.gameObject.scene.name} ");
                    }
                    continue;
                }

                bool skipPrefab = false;

                foreach (var networkPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab == null)
                    {
                        continue;
                    }
                    if (networkObject == networkPrefab.Prefab.GetComponent<NetworkObject>())
                    {
                        skipPrefab = true;
                        break;
                    }
                }
                if (skipPrefab)
                {
                    continue;
                }

                // Only NetworkObjects marked to not be destroyed with the scene and are not already in the DDOL are preserved
                if (!networkObject.DestroyWithScene && networkObject.gameObject.scene != networkManager.SceneManager.DontDestroyOnLoadScene)
                {
                    // Only move dynamically spawned NetworkObjects with no parent as the children will follow
                    if (networkObject.gameObject.transform.parent == null && networkObject.IsSceneObject != null && !networkObject.IsSceneObject.Value)
                    {
                        VerboseDebug($"[MoveObjects from {scene.name} | {scene.handle}] Moving {networkObject.gameObject.name} because it is in scene {networkObject.gameObject.scene.name} with DWS = {networkObject.DestroyWithScene}.");
                        Object.DontDestroyOnLoad(networkObject.gameObject);
                    }
                }
                else if (networkManager.IsServer)
                {
                    if (networkObject.NetworkManager == networkManager)
                    {
                        VerboseDebug($"[MoveObjects from {scene.name} | {scene.handle}] Destroying {networkObject.gameObject.name} because it is in scene {networkObject.gameObject.scene.name} with DWS = {networkObject.DestroyWithScene}.");
                        networkObject.Despawn();
                    }
                    else //For integration testing purposes, migrate remaining into DDOL
                    {
                        VerboseDebug($"[MoveObjects from {scene.name} | {scene.handle}] Temporarily migrating {networkObject.gameObject.name} into DDOL to await server destroy message.");
                        Object.DontDestroyOnLoad(networkObject.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the client synchronization mode which impacts whether both the server or client take into consideration scenes loaded before
        /// starting the <see cref="NetworkManager"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="LoadSceneMode.Single"/>: Does not take preloaded scenes into consideration
        /// <see cref="LoadSceneMode.Single"/>: Does take preloaded scenes into consideration
        /// </remarks>
        /// <param name="networkManager">relative <see cref="NetworkManager"/> instance</param>
        /// <param name="mode"><see cref="LoadSceneMode.Single"/> or <see cref="LoadSceneMode.Additive"/></param>
        public void SetClientSynchronizationMode(ref NetworkManager networkManager, LoadSceneMode mode)
        {

            var sceneManager = networkManager.SceneManager;

            // In client-server, we don't let client's set this value.
            // In dsitributed authority, since session owner can be promoted clients can set this value
            if (!networkManager.DistributedAuthorityMode && !networkManager.IsServer)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Clients should not set this value as it is automatically synchronized with the server's setting!");
                }
                return;
            }
            else if (!networkManager.DistributedAuthorityMode && networkManager.ConnectedClientsIds.Count > (networkManager.IsHost ? 1 : 0) && sceneManager.ClientSynchronizationMode != mode)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server is changing client synchronization mode after clients have been synchronized! It is recommended to do this before clients are connected!");
                }
            }



            // For additive client synchronization, we take into consideration scenes
            // already loaded.
            if (mode == LoadSceneMode.Additive)
            {
                if (networkManager.IsServer)
                {
                    sceneManager.OnSceneEvent -= SceneManager_OnSceneEvent;
                    sceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
                }

                if (!SceneNameToSceneHandles.ContainsKey(networkManager))
                {
                    SceneNameToSceneHandles.Add(networkManager, new Dictionary<string, Dictionary<int, SceneEntry>>());
                }

                var networkManagerScenes = SceneNameToSceneHandles[networkManager];

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    // Ignore scenes that belong to other NetworkManager instances
                    if (!DoesSceneHaveUnassignedEntry(scene.name, networkManager))
                    {
                        continue;
                    }

                    // If using scene verification
                    if (sceneManager.VerifySceneBeforeLoading != null)
                    {
                        // Determine if we should take this scene into consideration
                        if (!sceneManager.VerifySceneBeforeLoading.Invoke(scene.buildIndex, scene.name, LoadSceneMode.Additive))
                        {
                            continue;
                        }
                    }

                    // If the scene is not already in the ScenesLoaded list, then add it
                    if (!sceneManager.ScenesLoaded.ContainsKey(scene.handle))
                    {
                        StartTrackingScene(scene, true, networkManager);
                        sceneManager.ScenesLoaded.Add(scene.handle, new NetworkSceneManager.SceneData(null, scene));
                    }
                }
            }
            // Set the client synchronization mode
            sceneManager.ClientSynchronizationMode = mode;
        }

        /// <summary>
        /// During integration testing, if the server loads a scene then
        /// we want to start tracking it.
        /// </summary>
        /// <param name="sceneEvent"></param>
        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            // Filter for server only scene events
            if (!NetworkManager.IsServer || sceneEvent.ClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        StartTrackingScene(sceneEvent.Scene, true, NetworkManager);
                        break;
                    }
            }
        }

        /// <summary>
        /// Handles determining if a client should attempt to load a scene during synchronization.
        /// </summary>
        /// <param name="sceneName">name of the scene to be loaded</param>
        /// <param name="isPrimaryScene">when in client synchronization mode single, this determines if the scene is the primary active scene</param>
        /// <param name="clientSynchronizationMode">the current client synchronization mode</param>
        /// <param name="networkManager"><see cref="NetworkManager"/>relative instance</param>
        /// <returns></returns>
        public bool ClientShouldPassThrough(string sceneName, bool isPrimaryScene, LoadSceneMode clientSynchronizationMode, NetworkManager networkManager)
        {
            var shouldPassThrough = clientSynchronizationMode == LoadSceneMode.Single ? false : DoesSceneHaveUnassignedEntry(sceneName, networkManager);
            var activeScene = SceneManager.GetActiveScene();

            // If shouldPassThrough is not yet true and the scene to be loaded is the currently active scene
            if (!shouldPassThrough && sceneName == activeScene.name)
            {
                // In additive client synchronization mode we always pass through.
                // Unlike the default behavior(i.e. DefaultSceneManagerHandler), for integration testing we always return false
                // if it is the active scene and the client synchronization mode is LoadSceneMode.Single because the client should
                // load the active scene additively for this NetworkManager instance (i.e. can't have multiple active scenes).
                if (clientSynchronizationMode == LoadSceneMode.Additive)
                {
                    // don't try to reload this scene and pass through to post load processing.
                    shouldPassThrough = true;
                }
            }
            return shouldPassThrough;
        }

        /// <summary>
        /// Constructor now must take NetworkManager
        /// </summary>
        public IntegrationTestSceneHandler(NetworkManager networkManager)
        {
            networkManager.SceneManager.OverrideGetAndAddNewlyLoadedSceneByName = GetAndAddNewlyLoadedSceneByName;
            networkManager.SceneManager.ExcludeSceneFromSychronization = ExcludeSceneFromSynchronizationCheck;
            NetworkManagers.Add(networkManager);
            NetworkManagerName = networkManager.name;
            if (s_WaitForSeconds == null)
            {
                s_WaitForSeconds = new WaitForSeconds(1.0f / networkManager.NetworkConfig.TickRate);
            }
            NetworkManager = networkManager;
            if (CoroutineRunner == null)
            {
                CoroutineRunner = new GameObject("UnitTestSceneHandlerCoroutine").AddComponent<CoroutineRunner>();
                // Move the CoroutineRunner into the DDOL in case we unload the scene it was instantiated in.
                // (which if that gets destroyed then it basically stops all integration test queue processing)
                Object.DontDestroyOnLoad(CoroutineRunner);
            }
        }

        public void Dispose()
        {
            NetworkManagers.Clear();
            if (SceneJobProcessor != null)
            {
                CoroutineRunner.StopCoroutine(SceneJobProcessor);
                SceneJobProcessor = null;
            }

            foreach (var job in QueuedSceneJobs)
            {
                if (job.JobType != QueuedSceneJob.JobTypes.Completed)
                {
                    if (job.JobType == QueuedSceneJob.JobTypes.Loading)
                    {
                        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
                    }
                    else
                    {
                        SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;
                    }
                    job.JobType = QueuedSceneJob.JobTypes.Completed;
                }
            }
            QueuedSceneJobs.Clear();
            if (CoroutineRunner != null && CoroutineRunner.gameObject != null)
            {
                Object.Destroy(CoroutineRunner.gameObject);
            }

        }
    }
}
