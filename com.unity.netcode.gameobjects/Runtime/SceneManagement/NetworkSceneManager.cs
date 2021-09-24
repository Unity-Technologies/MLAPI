using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Unity.Netcode
{
    /// <summary>
    /// Used for local notifications of various scene events.
    /// The <see cref="NetworkSceneManager.OnSceneEvent"/> of delegate type <see cref="NetworkSceneManager.SceneEventDelegate"/> uses this class to provide
    /// scene event status/state.
    /// </summary>
    public class SceneEvent
    {
        /// <summary>
        /// If applicable, this will be set to the <see cref="UnityEngine.AsyncOperation"/> returned by <see cref="SceneManager"/>
        /// load scene and unload scene asynchronous methods.
        /// </summary>
        public AsyncOperation AsyncOperation;

        /// <summary>
        /// Will always be set to the current scene event type (<see cref="SceneEventData.SceneEventTypes"/>) this scene event notification pertains to
        /// </summary>
        public SceneEventData.SceneEventTypes SceneEventType;

        /// <summary>
        /// If applicable, this reflects the type of scene loading or unloading that is occurring.
        /// Unlike <see cref="SceneManager"/>, scene unload events will have the original <see cref="LoadSceneMode"/> applied when the scene was loaded.
        /// </summary>
        public LoadSceneMode LoadSceneMode;

        /// <summary>
        /// Excluding <see cref="SceneEventData.SceneEventTypes.S2C_Event_Sync"/> and <see cref="SceneEventData.SceneEventTypes.C2S_Event_Sync_Complete"/>
        /// This will be set to the scene name that the event pertains to.
        /// </summary>
        public string SceneName;

        /// <summary>
        /// When a scene is loaded, the Scene structure is returned.
        /// </summary>
        public Scene Scene;

        /// <summary>
        /// Events that always set <see cref="ClientId"/> to the local client identifier
        /// and only triggered locally:
        /// <see cref="SceneEventData.SceneEventTypes.S2C_Load"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_Unload"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_Sync"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_ReSync"/>
        ///
        /// Events that always set <see cref="ClientId"/> to the local client identifier,
        /// are triggered locally, and a host or server will trigger externally generated
        /// scene event message types (i.e. sent by a client):
        /// <see cref="SceneEventData.SceneEventTypes.C2S_UnloadComplete"/>
        /// <see cref="SceneEventData.SceneEventTypes.C2S_LoadComplete"/>
        /// <see cref="SceneEventData.SceneEventTypes.C2S_SyncComplete"/>
        ///
        /// Events that always set <see cref="ClientId"/> to the ServerId:
        /// <see cref="SceneEventData.SceneEventTypes.S2C_LoadComplete"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_UnLoadComplete"/>
        /// </summary>
        public ulong ClientId;

        /// <summary>
        /// List of clients that completed a loading or unloading event
        /// Applies only to:
        /// <see cref="SceneEventData.SceneEventTypes.S2C_LoadComplete"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_UnLoadComplete"/>
        /// </summary>
        public List<ulong> ClientsThatCompleted;

        /// <summary>
        /// List of clients that timed out during a loading or unloading event
        /// Applies only to:
        /// <see cref="SceneEventData.SceneEventTypes.S2C_LoadComplete"/>
        /// <see cref="SceneEventData.SceneEventTypes.S2C_UnLoadComplete"/>
        /// </summary>
        public List<ulong> ClientsThatTimedOut;
    }

    /// <summary>
    /// Main class for managing network scenes when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled.
    /// Uses the <see cref="SceneEventMessage"/> message to communicate <see cref="SceneEventData"/> between the server and client(s)
    /// </summary>
    public class NetworkSceneManager : IDisposable
    {
        private const NetworkDelivery k_DeliveryType = NetworkDelivery.ReliableFragmentedSequenced;

        // Used to be able to turn re-synchronization off for future snapshot development purposes.
        internal static bool DisableReSynchronization;

        /// <summary>
        /// Used to detect if a scene event is underway
        /// Only 1 scene event can occur on the server at a time for now.
        /// </summary>
        private static bool s_IsSceneEventActive = false;

        // TODO: Remove `m_IsRunningUnitTest` entirely after we switch to multi-process testing
        // In MultiInstance tests, we cannot allow clients to load additional scenes as they're sharing the same scene space / Unity instance.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly bool m_IsRunningUnitTest = SceneManager.GetActiveScene().name.StartsWith("InitTestScene");
#endif

        /// <summary>
        /// The delegate callback definition for scene event notifications
        /// For more details review over <see cref="SceneEvent"/> and <see cref="SceneEventData"/>
        /// </summary>
        /// <param name="sceneEvent"></param>
        public delegate void SceneEventDelegate(SceneEvent sceneEvent);

        /// <summary>
        /// Event that will notify the local client or server of all scene events that take place
        /// For more details review over <see cref="SceneEvent"/>, <see cref="SceneEventData"/>, and <see cref="SceneEventData.SceneEventTypes"/>
        /// </summary>
        public event SceneEventDelegate OnSceneEvent;

        /// <summary>
        /// Delegate declaration for the <see cref="VerifySceneBeforeLoading"/> handler that provides
        /// an additional level of scene loading security and/or validation to assure the scene being loaded
        /// is valid scene to be loaded in the LoadSceneMode specified.
        /// </summary>
        /// <param name="sceneIndex">Build Settings Scenes in Build List index of the scene</param>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="loadSceneMode">LoadSceneMode the scene is going to be loaded</param>
        /// <returns>true (valid) or false (not valid)</returns>
        public delegate bool VerifySceneBeforeLoadingDelegateHandler(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode);

        /// <summary>
        /// Delegate handler defined by <see cref="VerifySceneBeforeLoadingDelegateHandler"/> that is invoked before the
        /// server or client loads a scene during an active netcode game session.
        /// Client Side: In order for clients to be notified of this condition you must assign the <see cref="VerifySceneBeforeLoading"/> delegate handler.
        /// Server Side: <see cref="LoadScene(string, LoadSceneMode)"/> will return <see cref="SceneEventProgressStatus.SceneFailedVerification"/>.
        /// </summary>
        public VerifySceneBeforeLoadingDelegateHandler VerifySceneBeforeLoading;

        internal readonly Dictionary<Guid, SceneEventProgress> SceneEventProgressTracking = new Dictionary<Guid, SceneEventProgress>();

        /// <summary>
        /// Used to track in-scene placed NetworkObjects
        /// We store them by:
        /// [GlobalObjectIdHash][Scene.Handle][NetworkObject]
        /// The Scene.Handle aspect allows us to distinguish duplicated in-scene placed NetworkObjects created by the loading
        /// of the same additive scene multiple times.
        /// </summary>
        internal readonly Dictionary<uint, Dictionary<int, NetworkObject>> ScenePlacedObjects = new Dictionary<uint, Dictionary<int, NetworkObject>>();

        /// <summary>
        /// This is used for the deserialization of in-scene placed NetworkObjects in order to distinguish duplicated in-scene
        /// placed NetworkObjects created by the loading of the same additive scene multiple times.
        /// </summary>
        internal Scene SceneBeingSynchronized;

        /// <summary>
        /// Used to track which scenes are currently loaded
        /// We store the scenes as [SceneHandle][Scene] in order to handle the loading and unloading of the same scene additively
        /// Scene handle is only unique locally.  So, clients depend upon the <see cref="ServerSceneHandleToClientSceneHandle"/> in order
        /// to be able to know which specific scene instance the server is instructing the client to unload.
        /// The client links the server scene handle to the client local scene handle upon a scene being loaded
        /// <see cref="GetAndAddNewlyLoadedSceneByName"/>
        /// </summary>
        internal Dictionary<int, Scene> ScenesLoaded = new Dictionary<int, Scene>();

        /// <summary>
        /// Since Scene.handle is unique per client, we create a look-up table between the client and server to associate server unique scene
        /// instances with client unique scene instances
        /// </summary>
        internal Dictionary<int, int> ServerSceneHandleToClientSceneHandle = new Dictionary<int, int>();

        /// <summary>
        /// The scenes in the build without their path
        /// </summary>
        internal List<string> ScenesInBuild = new List<string>();

        /// <summary>
        /// The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
        /// they need to be moved into the do not destroy temporary scene
        /// When it is set: Just before starting the asynchronous loading call
        /// When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
        /// not destroy temporary scene are moved into the active scene
        /// </summary>
        internal static bool IsSpawnedObjectsPendingInDontDestroyOnLoad;

        /// <summary>
        /// Client and Server:
        /// Used for all scene event processing except for ClientSynchEventData specific events
        /// </summary>
        internal List<SceneEventData> SceneEventDataPool;

        /// <summary>
        /// The maximum size you can grow your <see cref="SceneEventData"/> pool (100)
        /// </summary>
        public const int MaximumSceneEventDataPoolSizeThreshold = 100;

        /// <summary>
        /// The default size of the <see cref="SceneEventData"/> pool (10)
        /// </summary>
        public const int DefaultSceneEventDataPoolSize = 10;

        /// <summary>
        /// Current maximum <see cref="SceneEventData"/> pool size
        /// </summary>
        private int m_MaxSceneEventDataPoolSize;

        /// <summary>
        /// Next <see cref="SceneEventData"/> pool index
        /// </summary>
        private int m_NextSceneEventDataIndex;


        /// <summary>
        /// Server Side:
        /// Used specifically for scene synchronization and scene event progress related events.
        /// </summary>
        internal SceneEventData ClientSynchEventData;

        private NetworkManager m_NetworkManager { get; }

        internal Scene DontDestroyOnLoadScene;

        /// <summary>
        /// LoadSceneMode.Single: All currently loaded scenes on the client will be unloaded and
        /// the server's currently active scene will be loaded in single mode on the client
        /// unless it was already loaded.
        ///
        /// LoadSceneMode.Additive: All currently loaded scenes are left as they are and any newly loaded
        /// scenes will be loaded additively.  Users need to determine which scenes are valid to load via the
        /// <see cref="VerifySceneBeforeLoading"/> method.
        /// </summary>
        public LoadSceneMode ClientSynchronizationMode { get; internal set; }

        /// <summary>
        /// When true, the <see cref="Debug.LogWarning(object)"/> messages will be turned off
        /// </summary>
        private bool m_DisableValidationWarningMessages;

        /// <summary>
        /// Handle NetworkSeneManager clean up
        /// </summary>
        public void Dispose()
        {
            foreach (var sceneEventData in SceneEventDataPool)
            {
                sceneEventData.Dispose();
            }
            SceneEventDataPool.Clear();
            SceneEventDataPool = null;
            ClientSynchEventData.Dispose();
            ClientSynchEventData = null;
        }

        /// <summary>
        /// Depending upon how many users are connected, you might need to adjust
        /// the <see cref="SceneEventData"/> pool size to a larger value on a host
        /// or a server under the situation where you are loading and/or unloading
        /// many additive scenes over a short period of time.
        /// The default <see cref="SceneEventData"/> pool size is 10 (<see cref="DefaultSceneEventDataPoolSize"/>)
        /// The maximum <see cref="SceneEventData"/> pool size can grow to is 100 (<see cref="MaximumSceneEventDataPoolSizeThreshold"/>)
        /// </summary>
        /// <param name="sceneEventDataPoolSize"></param>
        /// <returns></returns>
        public bool SetSceneEventDataPoolSize(int sceneEventDataPoolSize)
        {
            if (sceneEventDataPoolSize > DefaultSceneEventDataPoolSize)
            {
                if (sceneEventDataPoolSize <= MaximumSceneEventDataPoolSizeThreshold)
                {
                    if (sceneEventDataPoolSize > m_MaxSceneEventDataPoolSize)
                    {
                        for (int i = m_MaxSceneEventDataPoolSize; i < sceneEventDataPoolSize; i++)
                        {
                            SceneEventDataPool.Add(new SceneEventData(m_NetworkManager));
                        }
                        m_MaxSceneEventDataPoolSize = sceneEventDataPoolSize;
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid size [{sceneEventDataPoolSize}], during runtime you can only increase the {nameof(SceneEventData)} " +
                            $"pool size relative to its last set value of [{m_MaxSceneEventDataPoolSize}].");
                    }
                }
                else
                {
                    Debug.LogWarning($"Invalid size [{sceneEventDataPoolSize}], you cannot set the {nameof(SceneEventData)} pool size to a value larger" +
                        $" than the maximum pool size of [{MaximumSceneEventDataPoolSizeThreshold}].");
                }
            }
            else
            {
                Debug.LogWarning($"Invalid size [{sceneEventDataPoolSize}], you cannot set the {nameof(SceneEventData)} pool size to a value smaller " +
                    $"than or equal to the default size of [{DefaultSceneEventDataPoolSize}].");
            }
            return false;
        }

        /// <summary>
        /// Gets the next <see cref="SceneEventData"/> pool item index number
        /// </summary>
        internal int GetNextSceneEventDataIndexToUse()
        {
            var nextIndex = m_NextSceneEventDataIndex;
            m_NextSceneEventDataIndex++;
            m_NextSceneEventDataIndex %= m_MaxSceneEventDataPoolSize;
            return nextIndex;
        }

        /// <summary>
        /// Gets the scene name from full path to the scene
        /// </summary>
        internal string GetSceneNameFromPath(string scenePath)
        {
            var begin = scenePath.LastIndexOf("/", StringComparison.Ordinal) + 1;
            var end = scenePath.LastIndexOf(".", StringComparison.Ordinal);
            return scenePath.Substring(begin, end - begin);
        }

        /// <summary>
        /// Generates the scenes in build list
        /// </summary>
        internal void GenerateScenesInBuild()
        {
            ScenesInBuild.Clear();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                ScenesInBuild.Add(GetSceneNameFromPath(SceneUtility.GetScenePathByBuildIndex(i)));
            }
        }

        /// <summary>
        /// When set to true, this will disable the console warnings about
        /// a scene being invalidated.
        /// </summary>
        /// <param name="disabled">true/false</param>
        public void DisableValidationWarnings(bool disabled)
        {
            m_DisableValidationWarningMessages = disabled;
        }

        /// <summary>
        /// This will change how clients are initially synchronized.
        /// LoadSceneMode.Single: All currently loaded scenes on the client will be unloaded and
        /// the server's currently active scene will be loaded in single mode on the client
        /// unless it was already loaded.
        ///
        /// LoadSceneMode.Additive: All currently loaded scenes are left as they are and any newly loaded
        /// scenes will be loaded additively.  Users need to determine which scenes are valid to load via the
        /// <see cref="VerifySceneBeforeLoading"/> method.
        /// </summary>
        /// <param name="mode"><see cref="LoadSceneMode"/> for initial client synchronization</param>
        public void SetClientSynchronizationMode(LoadSceneMode mode)
        {
            ClientSynchronizationMode = mode;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="networkManager">one <see cref="NetworkManager"/> instance per <see cref="NetworkSceneManager"/> instance</param>
        /// <param name="sceneEventDataPoolSize">maximum <see cref="SceneEventData"/> pool size</param>
        internal NetworkSceneManager(NetworkManager networkManager, int sceneEventDataPoolSize = DefaultSceneEventDataPoolSize)
        {
            m_MaxSceneEventDataPoolSize = sceneEventDataPoolSize;
            m_NetworkManager = networkManager;
            SceneEventDataPool = new List<SceneEventData>();
            for (int i = 0; i < m_MaxSceneEventDataPoolSize; i++)
            {
                SceneEventDataPool.Add(new SceneEventData(networkManager));
            }

            ClientSynchEventData = new SceneEventData(networkManager);

            GenerateScenesInBuild();

            // If NetworkManager has this set to true, then we can get the DDOL (DontDestroyOnLoad) from its GaemObject
            if (networkManager.DontDestroy)
            {
                DontDestroyOnLoadScene = networkManager.gameObject.scene;
            }
            else // Otherwise, we have to create a GameObject and move it into the DDOL to get the scene
            {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // During unit and integration tests, we could initialize and then enable scene management
                // which would make this generate an extra GameObject per instance. The DontDestroyOnLoadScene
                // is internal so tests that are using multiInstance and that are moving NetworkObjects into
                // the DDOL scene will have to manually set this. Otherwise, we can exclude DDOL stuff completely
                // during unit testing.
                if (m_IsRunningUnitTest)
                {
                    return;
                }
#endif
                // Create our DDOL GameObject and move it into the DDOL scene so we can register the DDOL with
                // the NetworkSceneManager and then destroy the DDOL GameObject
                var myDDOLObject = new GameObject("DDOL-NWSM");
                UnityEngine.Object.DontDestroyOnLoad(myDDOLObject);
                DontDestroyOnLoadScene = myDDOLObject.scene;
                UnityEngine.Object.Destroy(myDDOLObject);
            }

            ServerSceneHandleToClientSceneHandle.Add(DontDestroyOnLoadScene.handle, DontDestroyOnLoadScene.handle);
            ScenesLoaded.Add(DontDestroyOnLoadScene.handle, DontDestroyOnLoadScene);
        }

        /// <summary>
        /// If the VerifySceneBeforeLoading delegate handler has been set by the user, this will provide
        /// an additional level of security and/or validation that the scene being loaded in the specified
        /// loading mode is "a valid scene to be loaded in the LoadSceneMode specified".
        /// </summary>
        /// <param name="sceneIndex">index into ScenesInBuild</param>
        /// <param name="loadSceneMode">LoadSceneMode the scene is going to be loaded</param>
        /// <returns>true (Valid) or false (Invalid)</returns>
        internal bool ValidateSceneBeforeLoading(uint sceneIndex, LoadSceneMode loadSceneMode)
        {
            var validated = true;
            var sceneName = ScenesInBuild[(int)sceneIndex];
            if (VerifySceneBeforeLoading != null)
            {
                validated = VerifySceneBeforeLoading.Invoke((int)sceneIndex, sceneName, loadSceneMode);
            }
            if (!validated && !m_DisableValidationWarningMessages)
            {
                var serverHostorClient = "Client";
                if (m_NetworkManager.IsServer)
                {
                    serverHostorClient = m_NetworkManager.IsHost ? "Host" : "Server";
                }

                Debug.LogWarning($"Scene {sceneName} of Scenes in Build Index {sceneIndex} being loaded in {loadSceneMode} mode failed validation on the {serverHostorClient}!");
            }
            return validated;
        }

        /// <summary>
        /// Since SceneManager.GetSceneByName only returns the first scene that matches the name
        /// we must "find" a newly added scene by looking through all loaded scenes and determining
        /// which scene with the same name has not yet been loaded.
        /// In order to support loading the same additive scene within in-scene placed NetworkObjects,
        /// we must do this to be able to soft synchronize the "right version" of the NetworkObject.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        internal Scene GetAndAddNewlyLoadedSceneByName(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sceneLoaded = SceneManager.GetSceneAt(i);
                if (sceneLoaded.name == sceneName)
                {
                    if (!ScenesLoaded.ContainsKey(sceneLoaded.handle))
                    {
                        ScenesLoaded.Add(sceneLoaded.handle, sceneLoaded);
                        return sceneLoaded;
                    }
                }
            }

            throw new Exception($"Failed to find any loaded scene named {sceneName}!");
        }

        /// <summary>
        /// Client Side Only:
        /// This takes a server scene handle that is written by the server before the scene relative
        /// NetworkObject is serialized and converts the server scene handle to a local client handle
        /// so it can set the appropriate SceneBeingSynchronized.
        /// Note: This is now part of the soft synchronization process and is needed for the scenario
        /// where a user loads the same scene additively that has an in-scene placed NetworkObject
        /// which means each scene relative in-scene placed NetworkObject will have the identical GlobalObjectIdHash
        /// value.  Scene handles are used to distinguish between in-scene placed NetworkObjects under this situation.
        /// </summary>
        /// <param name="serverSceneHandle"></param>
        internal void SetTheSceneBeingSynchronized(int serverSceneHandle)
        {
            var clientSceneHandle = serverSceneHandle;
            if (ServerSceneHandleToClientSceneHandle.ContainsKey(serverSceneHandle))
            {
                clientSceneHandle = ServerSceneHandleToClientSceneHandle[serverSceneHandle];
                // If we were already set, then ignore
                if (SceneBeingSynchronized.IsValid() && SceneBeingSynchronized.isLoaded && SceneBeingSynchronized.handle == clientSceneHandle)
                {
                    return;
                }

                // Get the scene currently being synchronized
                SceneBeingSynchronized = ScenesLoaded.ContainsKey(clientSceneHandle) ? ScenesLoaded[clientSceneHandle] : new Scene();

                if (!SceneBeingSynchronized.IsValid() || !SceneBeingSynchronized.isLoaded)
                {
                    // Let's go ahead and use the currently active scene under the scenario where a NetworkObject is determined to exist in a scene that the NetworkSceneManager is not aware of
                    SceneBeingSynchronized = SceneManager.GetActiveScene();

                    // Keeping the warning here in the event we cannot find the scene being synchronized
                    Debug.LogWarning($"[{nameof(NetworkSceneManager)}- {nameof(ScenesLoaded)}] Could not find the appropriate scene to set as being synchronized! Using the currently active scene.");
                }
            }
            else
            {
                // Most common scenario for DontDestroyOnLoad is when NetworkManager is set to not be destroyed
                if (serverSceneHandle == DontDestroyOnLoadScene.handle)
                {
                    SceneBeingSynchronized = m_NetworkManager.gameObject.scene;
                    return;
                }
                else
                {
                    // Let's go ahead and use the currently active scene under the scenario where a NetworkObject is determined to exist in a scene that the NetworkSceneManager is not aware of
                    // or the NetworkObject has yet to be moved to that specific scene (i.e. no DontDestroyOnLoad scene exists yet).
                    SceneBeingSynchronized = SceneManager.GetActiveScene();

                    // This could be the scenario where NetworkManager.DontDestroy is false and we are creating the first NetworkObject (client side) to be in the DontDestroyOnLoad scene
                    // Otherwise, this is some other specific scenario that we might not be handling currently.
                    Debug.LogWarning($"[{nameof(SceneEventData)}- Scene Handle Mismatch] {nameof(serverSceneHandle)} could not be found in {nameof(ServerSceneHandleToClientSceneHandle)}. Using the currently active scene.");
                }
            }
        }

        /// <summary>
        /// During soft synchronization of in-scene placed NetworkObjects, this is now used by NetworkSpawnManager.CreateLocalNetworkObject
        /// </summary>
        /// <param name="globalObjectIdHash"></param>
        /// <returns></returns>
        internal NetworkObject GetSceneRelativeInSceneNetworkObject(uint globalObjectIdHash)
        {
            if (ScenePlacedObjects.ContainsKey(globalObjectIdHash))
            {
                if (ScenePlacedObjects[globalObjectIdHash].ContainsKey(SceneBeingSynchronized.handle))
                {
                    var inScenePlacedNetworkObject = ScenePlacedObjects[globalObjectIdHash][SceneBeingSynchronized.handle];

                    // We can only have 1 duplicated globalObjectIdHash per scene instance, so remove it once it has been returned
                    ScenePlacedObjects[globalObjectIdHash].Remove(SceneBeingSynchronized.handle);

                    return inScenePlacedNetworkObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Generic sending of scene event data
        /// </summary>
        /// <param name="targetClientIds">array of client identifiers to receive the scene event message</param>
        private void SendSceneEventData(int sceneEventDataIndex, ulong[] targetClientIds)
        {
            if (targetClientIds.Length == 0)
            {
                // This would be the Host/Server with no clients connected
                // Silently return as there is nothing to be done
                return;
            }
            var message = new SceneEventMessage
            {
                EventData = SceneEventDataPool[sceneEventDataIndex]
            };
            var size = m_NetworkManager.SendMessage(message, k_DeliveryType, targetClientIds);

            m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                targetClientIds, (uint)SceneEventDataPool[sceneEventDataIndex].SceneEventType, ScenesInBuild[(int)SceneEventDataPool[sceneEventDataIndex].SceneIndex], size);
        }

        /// <summary>
        /// Verifies the scene name is valid relative to the scenes in build list
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns>true (Valid) or false (Invalid)</returns>
        internal bool IsSceneNameValid(string sceneName)
        {
            if (ScenesInBuild.Contains(sceneName))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used to determine if the index value is within the range of valid
        /// build indices.
        /// </summary>
        /// <param name="index">index value to check</param>
        /// <returns>true (Valid) or false (Invalid)</returns>
        internal bool IsSceneIndexValid(uint index)
        {
            return (index >= 0 && index < ScenesInBuild.Count);
        }

        /// <summary>
        /// Gets the build Index value for the scene name
        /// </summary>
        /// <param name="sceneName">scene name</param>
        /// <returns>build index</returns>
        internal uint GetBuildIndexFromSceneName(string sceneName)
        {
            if (IsSceneNameValid(sceneName))
            {
                return (uint)ScenesInBuild.IndexOf(sceneName);
            }
            return uint.MaxValue;
        }

        /// <summary>
        /// Entry method for scene unloading validation
        /// </summary>
        /// <param name="scene">the scene to be unloaded</param>
        /// <returns></returns>
        private SceneEventProgress ValidateSceneEventUnLoading(Scene scene)
        {
            if (!m_NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can start a scene event!");
            }

            if (!m_NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                //Log message about enabling SceneManagement
                throw new Exception($"{nameof(NetworkConfig.EnableSceneManagement)} flag is not enabled in the {nameof(NetworkManager)}'s {nameof(NetworkConfig)}. " +
                    $"Please set {nameof(NetworkConfig.EnableSceneManagement)} flag to true before calling " +
                    $"{nameof(NetworkSceneManager.LoadScene)} or {nameof(NetworkSceneManager.UnloadScene)}.");
            }

            if (!scene.isLoaded)
            {
                Debug.LogWarning($"{nameof(UnloadScene)} was called, but the scene {scene.name} is not currently loaded!");
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneNotLoaded);
            }

            return ValidateSceneEvent(scene.name, true);
        }

        /// <summary>
        /// Entry method for scene loading validation
        /// </summary>
        /// <param name="sceneName">scene name to load</param>
        /// <returns></returns>
        private SceneEventProgress ValidateSceneEventLoading(string sceneName)
        {
            if (!m_NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can start a scene event!");
            }
            if (!m_NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                //Log message about enabling SceneManagement
                throw new Exception($"{nameof(NetworkConfig.EnableSceneManagement)} flag is not enabled in the {nameof(NetworkManager)}'s {nameof(NetworkConfig)}. " +
                    $"Please set {nameof(NetworkConfig.EnableSceneManagement)} flag to true before calling " +
                    $"{nameof(NetworkSceneManager.LoadScene)} or {nameof(NetworkSceneManager.UnloadScene)}.");
            }

            return ValidateSceneEvent(sceneName);
        }

        /// <summary>
        /// Validates the new scene event request by the server-side code.
        /// This also initializes some commonly shared values as well as SceneEventProgress
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns><see cref="SceneEventProgress"/> that should have a <see cref="SceneEventProgress.Status"/> of <see cref="SceneEventProgressStatus.Started"/> otherwise it failed.</returns>
        private SceneEventProgress ValidateSceneEvent(string sceneName, bool isUnloading = false)
        {
            // Return scene event already in progress if one is already in progress
            if (s_IsSceneEventActive)
            {
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneEventInProgress);
            }

            // Return invalid scene name status if the scene name is invalid
            if (!IsSceneNameValid(sceneName))
            {
                return new SceneEventProgress(null, SceneEventProgressStatus.InvalidSceneName);
            }

            var sceneEventProgress = new SceneEventProgress(m_NetworkManager)
            {
                SceneBuildIndex = GetBuildIndexFromSceneName(sceneName)
            };

            SceneEventProgressTracking.Add(sceneEventProgress.Guid, sceneEventProgress);

            if (!isUnloading)
            {
                // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
                // they need to be moved into the do not destroy temporary scene
                // When it is set: Just before starting the asynchronous loading call
                // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
                // not destroy temporary scene are moved into the active scene
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            }

            s_IsSceneEventActive = true;

            // Set our callback delegate handler for completion
            sceneEventProgress.OnComplete = OnSceneEventProgressCompleted;

            return sceneEventProgress;
        }

        /// <summary>
        /// Callback for the <see cref="SceneEventProgress.OnComplete"/> <see cref="SceneEventProgress.OnCompletedDelegate"/> handler
        /// </summary>
        /// <param name="sceneEventProgress"></param>
        /// <returns></returns>
        private bool OnSceneEventProgressCompleted(SceneEventProgress sceneEventProgress)
        {

            ClientSynchEventData.SceneEventGuid = sceneEventProgress.Guid;
            ClientSynchEventData.SceneIndex = sceneEventProgress.SceneBuildIndex;
            ClientSynchEventData.SceneEventType = sceneEventProgress.SceneEventType;
            ClientSynchEventData.ClientsCompleted = sceneEventProgress.DoneClients;
            ClientSynchEventData.ClientsTimedOut = m_NetworkManager.ConnectedClients.Keys.Except(sceneEventProgress.DoneClients).ToList();

            var message = new SceneEventMessage
            {
                EventData = ClientSynchEventData
            };
            var size = m_NetworkManager.SendMessage(message, k_DeliveryType, m_NetworkManager.ConnectedClientsIds);

            m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                m_NetworkManager.ConnectedClientsIds,
                (uint)sceneEventProgress.SceneEventType,
                ScenesInBuild[(int)sceneEventProgress.SceneBuildIndex],
                size);

            // Send a local notification to the server that all clients are done loading or unloading
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = sceneEventProgress.SceneEventType,
                SceneName = ScenesInBuild[(int)sceneEventProgress.SceneBuildIndex],
                ClientId = m_NetworkManager.ServerClientId,
                LoadSceneMode = sceneEventProgress.LoadSceneMode,
                ClientsThatCompleted = sceneEventProgress.DoneClients,
                ClientsThatTimedOut = m_NetworkManager.ConnectedClients.Keys.Except(sceneEventProgress.DoneClients).ToList(),
            });

            SceneEventProgressTracking.Remove(sceneEventProgress.Guid);

            return false;
        }

        /// <summary>
        /// Server Side:
        /// Unloads an additively loaded scene.  If you want to unload a <see cref="LoadSceneMode.Single"/> mode loaded scene load another <see cref="LoadSceneMode.Single"/> scene.
        /// When applicable, the <see cref="AsyncOperation"/> is delivered within the <see cref="SceneEvent"/> via the <see cref="OnSceneEvent"/>
        /// </summary>
        /// <param name="sceneName">scene name to unload</param>
        /// <returns><see cref="SceneEventProgressStatus"/> (<see cref="SceneEventProgressStatus.Started"/> means it was successful)</returns>
        public SceneEventProgressStatus UnloadScene(Scene scene)
        {
            var sceneName = scene.name;
            var sceneHandle = scene.handle;
            if (!scene.isLoaded)
            {
                Debug.LogWarning($"{nameof(UnloadScene)} was called, but the scene {scene.name} is not currently loaded!");
                return SceneEventProgressStatus.SceneNotLoaded;
            }

            var sceneEventProgress = ValidateSceneEventUnLoading(scene);
            if (sceneEventProgress.Status != SceneEventProgressStatus.Started)
            {
                return sceneEventProgress.Status;
            }

            if (!ScenesLoaded.ContainsKey(sceneHandle))
            {
                Debug.LogError($"{nameof(UnloadScene)} internal error! {sceneName} with handle {scene.handle} is not within the internal scenes loaded dictionary!");
                return SceneEventProgressStatus.InternalNetcodeError;
            }
            var sceneEventDataIndex = GetNextSceneEventDataIndexToUse();
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            sceneEventData.SceneEventGuid = sceneEventProgress.Guid;
            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Unload;
            sceneEventData.SceneIndex = GetBuildIndexFromSceneName(sceneName);
            sceneEventData.SceneHandle = sceneHandle;

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventData.SceneEventTypes.S2C_UnLoadComplete;

            ScenesLoaded.Remove(scene.handle);

            AsyncOperation sceneUnload = SceneManager.UnloadSceneAsync(scene);
            sceneUnload.completed += (AsyncOperation asyncOp2) => { OnSceneUnloaded(sceneEventDataIndex); };
            sceneEventProgress.SetSceneLoadOperation(sceneUnload);

            // Notify local server that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.ServerClientId  // Server can only invoke this
            });

            //Return the status
            return sceneEventProgress.Status;
        }

        /// <summary>
        /// Client Side:
        /// Handles <see cref="SceneEventData.SceneEventTypes.S2C_Unload"/> scene events.
        /// </summary>
        private void OnClientUnloadScene(int sceneEventDataIndex)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            if (!IsSceneIndexValid(sceneEventData.SceneIndex))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }

                return;
            }

            var sceneName = ScenesInBuild[(int)sceneEventData.SceneIndex];

            if (!ServerSceneHandleToClientSceneHandle.ContainsKey(sceneEventData.SceneHandle))
            {
                throw new Exception($"Client failed to unload scene {sceneName} " +
                    $"because we are missing the client scene handle due to the server scene handle {sceneEventData.SceneHandle} not being found!");
            }

            var sceneHandle = ServerSceneHandleToClientSceneHandle[sceneEventData.SceneHandle];

            if (!ScenesLoaded.ContainsKey(sceneHandle))
            {
                // Error scene handle not found!
                throw new Exception($"Client failed to unload scene {sceneName} " +
                    $"because the client scene handle {sceneHandle} was not found in ScenesLoaded!");
            }
            s_IsSceneEventActive = true;
            var sceneUnload = (AsyncOperation)null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_IsRunningUnitTest)
            {
                sceneUnload = new AsyncOperation();
            }
            else
            {
                sceneUnload = SceneManager.UnloadSceneAsync(ScenesLoaded[sceneHandle]);
                sceneUnload.completed += asyncOp2 => OnSceneUnloaded(sceneEventDataIndex);
            }
#else
            sceneUnload = SceneManager.UnloadSceneAsync(ScenesLoaded[sceneHandle]);
            sceneUnload.completed += asyncOp2 => OnSceneUnloaded(sceneEventDataIndex);
#endif
            ScenesLoaded.Remove(sceneHandle);

            // Remove our server to scene handle lookup
            ServerSceneHandleToClientSceneHandle.Remove(sceneEventData.SceneHandle);

            // Notify the local client that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.LocalClientId   // Server sent this message to the client, but client is executing it
            });


#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_IsRunningUnitTest)
            {
                OnSceneUnloaded(sceneEventDataIndex);
            }
#endif
        }

        /// <summary>
        /// Server and Client:
        /// Invoked when an additively loaded scene is unloaded
        /// </summary>
        private void OnSceneUnloaded(int sceneEventDataIndex)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            // First thing we do, if we are a server, is to send the unload scene event.
            if (m_NetworkManager.IsServer)
            {
                // Server sends the unload scene notification after unloading because it will despawn all scene relative in-scene NetworkObjects
                // If we send this event to all clients before the server is finished unloading they will get warning about an object being
                // despawned that no longer exists
                SendSceneEventData(sceneEventDataIndex, m_NetworkManager.ConnectedClientsIds.Where(c => c != m_NetworkManager.ServerClientId).ToArray());

                //Second, server sets itself as having finished unloading
                if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventGuid))
                {
                    SceneEventProgressTracking[sceneEventData.SceneEventGuid].AddClientAsDone(m_NetworkManager.ServerClientId);
                }
            }

            // Next we prepare to send local notifications for unload complete
            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_UnloadComplete;

            //Notify the client or server that a scene was unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                ClientId = m_NetworkManager.IsServer ? m_NetworkManager.ServerClientId : m_NetworkManager.LocalClientId
            });

            // Clients send a notification back to the server they have completed the unload scene event
            if (!m_NetworkManager.IsServer)
            {
                SendSceneEventData(sceneEventDataIndex, new ulong[] { m_NetworkManager.ServerClientId });
            }

            // This scene event is now considered "complete"
            s_IsSceneEventActive = false;
        }

        /// <summary>
        /// Clears all scenes when loading in single mode
        /// Since we assume a single mode loaded scene will be considered the "currently active scene",
        /// we only unload any additively loaded scenes.
        /// </summary>
        internal void UnloadAdditivelyLoadedScenes()
        {
            // Unload all additive scenes while making sure we don't try to unload the base scene ( loaded in single mode ).
            var currentActiveScene = SceneManager.GetActiveScene();
            foreach (var keyHandleEntry in ScenesLoaded)
            {
                if (currentActiveScene.name != keyHandleEntry.Value.name)
                {
                    OnSceneEvent?.Invoke(new SceneEvent()
                    {
                        AsyncOperation = SceneManager.UnloadSceneAsync(keyHandleEntry.Value),
                        SceneEventType = SceneEventData.SceneEventTypes.S2C_Unload,
                        LoadSceneMode = LoadSceneMode.Additive,
                        SceneName = keyHandleEntry.Value.name,
                        ClientId = m_NetworkManager.ServerClientId
                    });
                }
            }
            // clear out our scenes loaded list
            ScenesLoaded.Clear();
        }

        /// <summary>
        /// Server side:
        /// Loads the scene name in either additive or single loading mode.
        /// When applicable, the <see cref="AsyncOperation"/> is delivered within the <see cref="SceneEvent"/> via <see cref="OnSceneEvent"/>
        /// </summary>
        /// <param name="sceneName">the name of the scene to be loaded</param>
        /// <returns><see cref="SceneEventProgressStatus"/> (<see cref="SceneEventProgressStatus.Started"/> means it was successful)</returns>
        public SceneEventProgressStatus LoadScene(string sceneName, LoadSceneMode loadSceneMode)
        {
            var sceneEventProgress = ValidateSceneEventLoading(sceneName);
            if (sceneEventProgress.Status != SceneEventProgressStatus.Started)
            {
                return sceneEventProgress.Status;
            }

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventData.SceneEventTypes.S2C_LoadComplete;
            sceneEventProgress.LoadSceneMode = loadSceneMode;

            var sceneEventDataIndex = GetNextSceneEventDataIndexToUse();
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];

            // Now set up the current scene event
            sceneEventData.SceneEventGuid = sceneEventProgress.Guid;
            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Load;
            sceneEventData.SceneIndex = GetBuildIndexFromSceneName(sceneName);
            sceneEventData.LoadSceneMode = loadSceneMode;

            // This both checks to make sure the scene is valid and if not resets the active scene event
            s_IsSceneEventActive = ValidateSceneBeforeLoading(sceneEventData.SceneIndex, loadSceneMode);
            if (!s_IsSceneEventActive)
            {
                return SceneEventProgressStatus.SceneFailedVerification;
            }

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Destroy current scene objects before switching.
                m_NetworkManager.SpawnManager.ServerDestroySpawnedSceneObjects();

                // Preserve the objects that should not be destroyed during the scene event
                MoveObjectsToDontDestroyOnLoad();

                // Now Unload all currently additively loaded scenes
                UnloadAdditivelyLoadedScenes();
            }

            // Now start loading the scene
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            sceneLoad.completed += (AsyncOperation asyncOp2) => { OnSceneLoaded(sceneEventDataIndex, sceneName); };
            sceneEventProgress.SetSceneLoadOperation(sceneLoad);

            // Notify the local server that a scene loading event has begun
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.ServerClientId
            });

            //Return our scene progress instance
            return sceneEventProgress.Status;
        }

        /// <summary>
        /// Client Side:
        /// Handles both forms of scene loading
        /// </summary>
        /// <param name="objectStream">Stream data associated with the event</param>
        private void OnClientSceneLoadingEvent(int sceneEventDataIndex)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            if (!IsSceneIndexValid(sceneEventData.SceneIndex))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }
                return;
            }

            var sceneName = ScenesInBuild[(int)sceneEventData.SceneIndex];

            // Run scene validation before loading a scene
            if (!ValidateSceneBeforeLoading(sceneEventData.SceneIndex, sceneEventData.LoadSceneMode))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_IsRunningUnitTest)
            {
                // Send the loading message
                OnSceneEvent?.Invoke(new SceneEvent()
                {
                    AsyncOperation = new AsyncOperation(),
                    SceneEventType = sceneEventData.SceneEventType,
                    LoadSceneMode = sceneEventData.LoadSceneMode,
                    SceneName = sceneName,
                    ClientId = m_NetworkManager.LocalClientId
                });

                // Unit tests must mirror the server's scenes loaded dictionary, otherwise this portion will fail
                if (ScenesLoaded.ContainsKey(sceneEventData.SceneHandle))
                {
                    OnClientLoadedScene(sceneEventDataIndex, ScenesLoaded[sceneEventData.SceneHandle]);
                }
                else
                {
                    throw new Exception($"Could not find the scene handle {sceneEventData.SceneHandle} for scene {sceneName} " +
                        $"during unit test.  Did you forget to register this in the unit test?");
                }
                return;
            }
#endif

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Move ALL NetworkObjects to the temp scene
                MoveObjectsToDontDestroyOnLoad();

                // Now Unload all currently additively loaded scenes
                UnloadAdditivelyLoadedScenes();
            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
            // they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
            // not destroy temporary scene are moved into the active scene
            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            }

            var sceneLoad = SceneManager.LoadSceneAsync(sceneName, sceneEventData.LoadSceneMode);
            sceneLoad.completed += asyncOp2 => OnSceneLoaded(sceneEventDataIndex, sceneName);

            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.LocalClientId
            });
        }


        /// <summary>
        /// Client and Server:
        /// Generic on scene loaded callback method to be called upon a scene loading
        /// </summary>
        private void OnSceneLoaded(int sceneEventDataIndex, string sceneName)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            var nextScene = GetAndAddNewlyLoadedSceneByName(sceneName);
            if (!nextScene.isLoaded || !nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            //Get all NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene);

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Move all objects to the new scene
                MoveObjectsFromDontDestroyOnLoadToScene(nextScene);
            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
            // they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
            // not destroy temporary scene are moved into the active scene
            IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

            if (m_NetworkManager.IsServer)
            {
                OnServerLoadedScene(sceneEventDataIndex, nextScene);
            }
            else
            {
                // For the client, we make a server scene handle to client scene handle look up table
                if (!ServerSceneHandleToClientSceneHandle.ContainsKey(sceneEventData.SceneHandle))
                {
                    ServerSceneHandleToClientSceneHandle.Add(sceneEventData.SceneHandle, nextScene.handle);
                }
                else
                {
                    // If the exact same handle exists then there are problems with using handles
                    throw new Exception($"Server Scene Handle ({sceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
                }

                OnClientLoadedScene(sceneEventDataIndex, nextScene);
            }
        }

        /// <summary>
        /// Server side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnServerLoadedScene(int sceneEventDataIndex, Scene scene)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            // Register in-scene placed NetworkObjects with spawn manager
            foreach (var keyValuePairByGlobalObjectIdHash in ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    if (!keyValuePairBySceneHandle.Value.IsPlayerObject)
                    {
                        m_NetworkManager.SpawnManager.SpawnNetworkObjectLocally(keyValuePairBySceneHandle.Value, m_NetworkManager.SpawnManager.GetNetworkObjectId(), true, false, null, true);
                    }
                }
            }

            // Set the server's scene's handle so the client can build a look up table
            sceneEventData.SceneHandle = scene.handle;

            // Send all clients the scene load event
            for (int j = 0; j < m_NetworkManager.ConnectedClientsList.Count; j++)
            {
                var clientId = m_NetworkManager.ConnectedClientsList[j].ClientId;
                if (clientId != m_NetworkManager.ServerClientId)
                {
                    sceneEventData.TargetClientId = clientId;
                    var message = new SceneEventMessage
                    {
                        EventData = sceneEventData
                    };
                    var size = m_NetworkManager.SendMessage(message, k_DeliveryType, clientId);
                    var bytesReported = m_NetworkManager.LocalClientId == clientId
                            ? 0
                            : size;
                    m_NetworkManager.NetworkMetrics.TrackSceneEventSent(clientId, (uint)sceneEventData.SceneEventType, scene.name, bytesReported);
                }
            }

            s_IsSceneEventActive = false;
            //First, notify local server that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                ClientId = m_NetworkManager.ServerClientId,
                Scene = scene,
            });

            //Second, set the server as having loaded for the associated SceneEventProgress
            if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventGuid))
            {
                SceneEventProgressTracking[sceneEventData.SceneEventGuid].AddClientAsDone(m_NetworkManager.ServerClientId);
            }
        }

        /// <summary>
        /// Client side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnClientLoadedScene(int sceneEventDataIndex, Scene scene)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            sceneEventData.DeserializeScenePlacedObjects();

            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete;
            SendSceneEventData(sceneEventDataIndex, new ulong[] { m_NetworkManager.ServerClientId });
            s_IsSceneEventActive = false;

            // Notify local client that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                ClientId = m_NetworkManager.LocalClientId,
                Scene = scene,
            });
        }

        /// <summary>
        /// Server Side:
        /// This is used for players that have just had their connection approved and will assure they are synchronized
        /// properly if they are late joining
        /// Note: We write out all of the scenes to be loaded first and then all of the NetworkObjects that need to be
        /// synchronized.
        /// </summary>
        /// <param name="clientId">newly joined client identifier</param>
        internal void SynchronizeNetworkObjects(ulong clientId)
        {
            // Update the clients
            m_NetworkManager.SpawnManager.UpdateObservedNetworkObjects(clientId);

            ClientSynchEventData.InitializeForSynch();
            ClientSynchEventData.TargetClientId = clientId;
            ClientSynchEventData.LoadSceneMode = ClientSynchronizationMode;
            var activeScene = SceneManager.GetActiveScene();
            ClientSynchEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Sync;

            // Organize how (and when) we serialize our NetworkObjects
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                var sceneIndex = GetBuildIndexFromSceneName(scene.name);

                if (sceneIndex == uint.MaxValue)
                {
                    continue;
                }
                // This would depend upon whether we are additive or not
                // If we are the base scene, then we set the root scene index;
                if (activeScene == scene)
                {
                    if (!ValidateSceneBeforeLoading(sceneIndex, ClientSynchEventData.LoadSceneMode))
                    {
                        continue;
                    }
                    ClientSynchEventData.SceneIndex = sceneIndex;
                    ClientSynchEventData.SceneHandle = scene.handle;
                }
                else if (!ValidateSceneBeforeLoading(sceneIndex, LoadSceneMode.Additive))
                {
                    continue;
                }

                ClientSynchEventData.AddSceneToSynchronize(sceneIndex, scene.handle);
            }

            ClientSynchEventData.AddSpawnedNetworkObjects();

            var message = new SceneEventMessage
            {
                EventData = ClientSynchEventData
            };
            var size = m_NetworkManager.SendMessage(message, k_DeliveryType, clientId);
            var bytesReported = m_NetworkManager.LocalClientId == clientId
                    ? 0
                    : size;
            m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                clientId, (uint)ClientSynchEventData.SceneEventType, "", bytesReported);

            // Notify the local server that the client has been sent the SceneEventData.SceneEventTypes.S2C_Event_Sync event
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = ClientSynchEventData.SceneEventType,
                ClientId = clientId
            });
        }

        /// <summary>
        /// This is called when the client receives the SCENE_EVENT of type SceneEventData.SceneEventTypes.SYNC
        /// Note: This can recurse one additional time by the client if the current scene loaded by the client
        /// is already loaded.
        /// </summary>
        private void OnClientBeginSync(int sceneEventDataIndex)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            var sceneIndex = sceneEventData.GetNextSceneSynchronizationIndex();
            var sceneHandle = sceneEventData.GetNextSceneSynchronizationHandle();
            if (!IsSceneIndexValid(sceneIndex))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }
                return;
            }
            var sceneName = ScenesInBuild[(int)sceneIndex];
            var activeScene = SceneManager.GetActiveScene();
            var loadSceneMode = sceneIndex == sceneEventData.SceneIndex ? sceneEventData.LoadSceneMode : LoadSceneMode.Additive;

            // Always check to see if the scene needs to be validated
            if (!ValidateSceneBeforeLoading(sceneEventData.SceneIndex, loadSceneMode))
            {
                return;
            }

            // If this is the beginning of the synchronization event, then send client a notification that synchronization has begun
            if (sceneIndex == sceneEventData.SceneIndex)
            {
                OnSceneEvent?.Invoke(new SceneEvent()
                {
                    SceneEventType = SceneEventData.SceneEventTypes.S2C_Sync,
                    ClientId = m_NetworkManager.LocalClientId,
                });

                // Clear the in-scene placed NetworkObjects when we load the first scene in our synchronization process
                ScenePlacedObjects.Clear();
            }

            var shouldPassThrough = false;
            var sceneLoad = (AsyncOperation)null;

            // Check to see if the client already has loaded the scene to be loaded
            if (sceneName == activeScene.name)
            {
                // If the client is already in the same scene, then pass through and
                // don't try to reload it.
                shouldPassThrough = true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_IsRunningUnitTest)
            {
                // In unit tests, we don't allow clients to load additional scenes since
                // MultiInstance unit tests share the same scene space.
                shouldPassThrough = true;
                sceneLoad = new AsyncOperation();
            }
#endif
            if (!shouldPassThrough)
            {
                // If not, then load the scene
                sceneLoad = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                sceneLoad.completed += asyncOp2 => ClientLoadedSynchronization(sceneEventDataIndex, sceneIndex, sceneHandle);
            }

            // Notify local client that a scene load has begun
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = SceneEventData.SceneEventTypes.S2C_Load,
                LoadSceneMode = loadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.LocalClientId,
            });

            if (shouldPassThrough)
            {
                // If so, then pass through
                ClientLoadedSynchronization(sceneEventDataIndex, sceneIndex, sceneHandle);
            }
        }

        /// <summary>
        /// Once a scene is loaded ( or if it was already loaded) this gets called.
        /// This handles all of the in-scene and dynamically spawned NetworkObject synchronization
        /// </summary>
        /// <param name="sceneIndex">Netcode scene index that was loaded</param>
        private void ClientLoadedSynchronization(int sceneEventDataIndex, uint sceneIndex, int sceneHandle)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            var sceneName = ScenesInBuild[(int)sceneIndex];
            var nextScene = GetAndAddNewlyLoadedSceneByName(sceneName);

            if (!nextScene.isLoaded || !nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }

            var loadSceneMode = (sceneIndex == sceneEventData.SceneIndex ? sceneEventData.LoadSceneMode : LoadSceneMode.Additive);

            // For now, during a synchronization event, we will make the first scene the "base/master" scene that denotes a "complete scene switch"
            if (loadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            if (!ServerSceneHandleToClientSceneHandle.ContainsKey(sceneHandle))
            {
                ServerSceneHandleToClientSceneHandle.Add(sceneHandle, nextScene.handle);
            }
            else
            {
                // If the exact same handle exists then there are problems with using handles
                throw new Exception($"Server Scene Handle ({sceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
            }

            // Apply all in-scene placed NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene, false);

            // Send notification back to server that we finished loading this scene
            ClientSynchEventData.LoadSceneMode = loadSceneMode;
            ClientSynchEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete;
            ClientSynchEventData.SceneIndex = sceneIndex;


            var message = new SceneEventMessage
            {
                EventData = ClientSynchEventData
            };
            var size = m_NetworkManager.SendMessage(message, k_DeliveryType, m_NetworkManager.ServerClientId);

            m_NetworkManager.NetworkMetrics.TrackSceneEventSent(m_NetworkManager.ServerClientId, (uint)ClientSynchEventData.SceneEventType, sceneName, size);

            // Send notification to local client that the scene has finished loading
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete,
                LoadSceneMode = loadSceneMode,
                SceneName = sceneName,
                Scene = nextScene,
                ClientId = m_NetworkManager.LocalClientId,
            });

            // Check to see if we still have scenes to load and synchronize with
            HandleClientSceneEvent(sceneEventDataIndex);
        }

        /// <summary>
        /// Client Side:
        /// Handles incoming Scene_Event messages for clients
        /// </summary>
        /// <param name="stream">data associated with the event</param>
        private void HandleClientSceneEvent(int sceneEventDataIndex)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            switch (sceneEventData.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.S2C_Load:
                    {
                        OnClientSceneLoadingEvent(sceneEventDataIndex);
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_Unload:
                    {
                        OnClientUnloadScene(sceneEventDataIndex);
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_Sync:
                    {
                        if (!sceneEventData.IsDoneWithSynchronization())
                        {
                            OnClientBeginSync(sceneEventDataIndex);
                        }
                        else
                        {
                            // Include anything in the DDOL scene
                            PopulateScenePlacedObjects(DontDestroyOnLoadScene, false);
                            // Synchronize the NetworkObjects for this scene
                            sceneEventData.SynchronizeSceneNetworkObjects(m_NetworkManager);

                            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_SyncComplete;
                            SendSceneEventData(sceneEventDataIndex, new ulong[] { m_NetworkManager.ServerClientId });

                            // All scenes are synchronized, let the server know we are done synchronizing
                            m_NetworkManager.IsConnectedClient = true;

                            // Notify the client that they have finished synchronizing
                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                ClientId = m_NetworkManager.LocalClientId, // Client sent this to the server
                            });

                            // Client is now synchronized and fully "connected".  This also means the client can send "RPCs" at this time
                            m_NetworkManager.InvokeOnClientConnectedCallback(m_NetworkManager.LocalClientId);
                        }
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_ReSync:
                    {
                        // Notify the client that they have been re-synchronized after being synchronized with an in progress game session
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            ClientId = m_NetworkManager.ServerClientId,  // Server sent this to client
                        });

                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_LoadComplete:
                case SceneEventData.SceneEventTypes.S2C_UnLoadComplete:
                    {
                        // Notify client that all clients have finished loading or unloading
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                            ClientId = m_NetworkManager.ServerClientId,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            ClientsThatCompleted = sceneEventData.ClientsCompleted,
                            ClientsThatTimedOut = sceneEventData.ClientsTimedOut,
                        });
                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{sceneEventData.SceneEventType} is not currently supported!");
                        break;
                    }
            }
        }

        /// <summary>
        /// Server Side:
        /// Handles incoming Scene_Event messages for host or server
        /// </summary>
        /// <param name="clientId">client who sent the event</param>
        /// <param name="stream">data associated with the event</param>
        private void HandleServerSceneEvent(int sceneEventDataIndex, ulong clientId)
        {
            var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
            switch (sceneEventData.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.C2S_LoadComplete:
                    {
                        // Notify the local server that the client has finished loading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                            ClientId = clientId
                        });

                        if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventGuid))
                        {
                            SceneEventProgressTracking[sceneEventData.SceneEventGuid].AddClientAsDone(clientId);
                        }

                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_UnloadComplete:
                    {
                        if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventGuid))
                        {
                            SceneEventProgressTracking[sceneEventData.SceneEventGuid].AddClientAsDone(clientId);
                        }
                        // Notify the local server that the client has finished unloading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            SceneName = ScenesInBuild[(int)sceneEventData.SceneIndex],
                            ClientId = clientId
                        });

                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_SyncComplete:
                    {
                        // Notify the local server that a client has finished synchronizing
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            SceneName = string.Empty,
                            ClientId = clientId
                        });

                        // While we did invoke the C2S_SyncComplete event notification, we will also call the traditional client connected callback on the server
                        // which assures the client is "ready to receive RPCs" as well.
                        m_NetworkManager.InvokeOnClientConnectedCallback(clientId);

                        if (sceneEventData.ClientNeedsReSynchronization() && !DisableReSynchronization)
                        {
                            sceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_ReSync;
                            SendSceneEventData(sceneEventDataIndex, new ulong[] { clientId });

                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                SceneName = string.Empty,
                                ClientId = clientId
                            });
                        }

                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{sceneEventData.SceneEventType} is not currently supported!");
                        break;
                    }
            }
        }

        /// <summary>
        /// Both Client and Server: Incoming scene event entry point
        /// </summary>
        /// <param name="clientId">client who sent the scene event</param>
        /// <param name="reader">data associated with the scene event</param>
        internal void HandleSceneEvent(ulong clientId, FastBufferReader reader)
        {
            if (m_NetworkManager != null)
            {
                var sceneEventDataIndex = GetNextSceneEventDataIndexToUse();
                var sceneEventData = SceneEventDataPool[sceneEventDataIndex];
                sceneEventData.Deserialize(reader);

                var bytesReported = m_NetworkManager.LocalClientId == clientId
                    ? 0
                    : reader.Length;

                m_NetworkManager.NetworkMetrics.TrackSceneEventReceived(
                   clientId, (uint)sceneEventData.SceneEventType, ScenesInBuild[(int)sceneEventData.SceneIndex], bytesReported);

                if (sceneEventData.IsSceneEventClientSide())
                {
                    HandleClientSceneEvent(sceneEventDataIndex);
                }
                else
                {
                    HandleServerSceneEvent(sceneEventDataIndex, clientId);
                }
            }
            else
            {
                Debug.LogError($"{nameof(NetworkSceneManager.HandleSceneEvent)} was invoked but {nameof(NetworkManager)} reference was null!");
            }
        }

        /// <summary>
        /// Moves all NetworkObjects that don't have the <see cref="NetworkObject.DestroyWithScene"/> set to
        /// the "Do not destroy on load" scene.
        /// </summary>
        private void MoveObjectsToDontDestroyOnLoad()
        {
            // Move ALL NetworkObjects to the temp scene
            var objectsToKeep = new HashSet<NetworkObject>(m_NetworkManager.SpawnManager.SpawnedObjectsList);

            foreach (var sobj in objectsToKeep)
            {
                if (!sobj.DestroyWithScene || (sobj.IsSceneObject != null && sobj.IsSceneObject.Value && sobj.gameObject.scene == DontDestroyOnLoadScene))
                {
                    // Only move objects with no parent as child objects will follow
                    if (sobj.gameObject.transform.parent == null)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(sobj.gameObject);
                        // Since we are doing a scene transition, disable the GameObject until the next scene is loaded
                        sobj.gameObject.SetActive(false);
                    }
                }
                else if (m_NetworkManager.IsServer)
                {
                    sobj.Despawn(true);
                }
            }
        }

        /// <summary>
        /// Should be invoked on both the client and server side after:
        /// -- A new scene has been loaded
        /// -- Before any "DontDestroyOnLoad" NetworkObjects have been added back into the scene.
        /// Added the ability to choose not to clear the scene placed objects for additive scene loading.
        /// We organize our ScenePlacedObjects by:
        /// [GlobalObjectIdHash][SceneHandle][NetworkObject]
        /// Using the local scene relative Scene.handle as a sub-key to the root dictionary allows us to
        /// distinguish between duplicate in-scene placed NetworkObjects
        /// </summary>
        private void PopulateScenePlacedObjects(Scene sceneToFilterBy, bool clearScenePlacedObjects = true)
        {
            if (clearScenePlacedObjects)
            {
                ScenePlacedObjects.Clear();
            }

            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();

            // Just add every NetworkObject found that isn't already in the list
            // With additive scenes, we can have multiple in-scene placed NetworkObjects with the same GlobalObjectIdHash value
            // During Client Side Synchronization: We add them on a FIFO basis, for each scene loaded without clearing, and then
            // at the end of scene loading we use this list to soft synchronize all in-scene placed NetworkObjects
            foreach (var networkObjectInstance in networkObjects)
            {
                // We check to make sure the NetworkManager instance is the same one to be "MultiInstanceHelpers" compatible and filter the list on a per scene basis (additive scenes)
                if (networkObjectInstance.IsSceneObject == null && networkObjectInstance.NetworkManager == m_NetworkManager && networkObjectInstance.gameObject.scene == sceneToFilterBy &&
                    networkObjectInstance.gameObject.scene.handle == sceneToFilterBy.handle)
                {
                    if (!ScenePlacedObjects.ContainsKey(networkObjectInstance.GlobalObjectIdHash))
                    {
                        ScenePlacedObjects.Add(networkObjectInstance.GlobalObjectIdHash, new Dictionary<int, NetworkObject>());
                    }

                    if (!ScenePlacedObjects[networkObjectInstance.GlobalObjectIdHash].ContainsKey(networkObjectInstance.gameObject.scene.handle))
                    {
                        ScenePlacedObjects[networkObjectInstance.GlobalObjectIdHash].Add(networkObjectInstance.gameObject.scene.handle, networkObjectInstance);
                    }
                    else
                    {
                        var exitingEntryName = ScenePlacedObjects[networkObjectInstance.GlobalObjectIdHash][networkObjectInstance.gameObject.scene.handle] != null ?
                            ScenePlacedObjects[networkObjectInstance.GlobalObjectIdHash][networkObjectInstance.gameObject.scene.handle].name : "Null Entry";
                        throw new Exception($"{networkObjectInstance.name} tried to registered with {nameof(ScenePlacedObjects)} which already contains " +
                            $"the same {nameof(NetworkObject.GlobalObjectIdHash)} value {networkObjectInstance.GlobalObjectIdHash} for {exitingEntryName}!");
                    }
                }
            }
        }

        /// <summary>
        /// Moves all spawned NetworkObjects (from do not destroy on load) to the scene specified
        /// </summary>
        /// <param name="scene">scene to move the NetworkObjects to</param>
        private void MoveObjectsFromDontDestroyOnLoadToScene(Scene scene)
        {
            // Move ALL NetworkObjects to the temp scene
            var objectsToKeep = m_NetworkManager.SpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                if (sobj.gameObject.scene == DontDestroyOnLoadScene && (sobj.IsSceneObject == null || sobj.IsSceneObject.Value))
                {
                    continue;
                }

                // Only move objects with no parent as child objects will follow
                if (sobj.gameObject.transform.parent == null)
                {
                    // set it back to active at this point
                    sobj.gameObject.SetActive(true);
                    SceneManager.MoveGameObjectToScene(sobj.gameObject, scene);
                }
            }
        }
    }
}
