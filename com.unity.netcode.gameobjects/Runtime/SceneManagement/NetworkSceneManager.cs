using System.Collections.Generic;
using System;
using System.IO;
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
    /// Uses the <see cref="MessageQueueContainer.MessageType.SceneEvent"/> message to communicate <see cref="SceneEventData"/> between the server and client(s)
    /// </summary>
    public class NetworkSceneManager : IDisposable
    {
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
        /// Client Side: In order for clients to be notified of this condition you must subscribe to the <see cref="OnSceneVerificationFailed"/> event.
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
        internal static bool IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

        /// <summary>
        /// Client and Server:
        /// Used for all scene event processing except for ClientSynchEventData specific events
        /// </summary>
        internal SceneEventData SceneEventData;

        /// <summary>
        /// Server Side:
        /// Used specifically for scene synchronization and scene event progress related events.
        /// </summary>
        internal SceneEventData ClientSynchEventData;

        private NetworkManager m_NetworkManager { get; }

        private const MessageQueueContainer.MessageType k_MessageType = MessageQueueContainer.MessageType.SceneEvent;
        private const NetworkChannel k_ChannelType = NetworkChannel.Internal;
        private const NetworkUpdateStage k_NetworkUpdateStage = NetworkUpdateStage.EarlyUpdate;


        internal Scene DontDestroyOnLoadScene;

        /// <summary>
        /// Handle NetworkSeneManager clean up
        /// </summary>
        public void Dispose()
        {
            SceneEventData.Dispose();
            SceneEventData = null;
            ClientSynchEventData.Dispose();
            ClientSynchEventData = null;
        }

        /// <summary>
        /// Gets the scene name from full path to the scene
        /// </summary>
        /// <returns></returns>
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
        /// Constructor
        /// </summary>
        /// <param name="networkManager"></param>
        internal NetworkSceneManager(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
            SceneEventData = new SceneEventData(networkManager);
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
        /// <param name="sceneName">Name of the scene</param>
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
            if (!validated)
            {
                var serverHostorClient = "Client";
                if (m_NetworkManager.IsServer)
                {
                    serverHostorClient = m_NetworkManager.IsHost ? "Host" : "Server";
                }
                Debug.LogWarning($"Scene {sceneName} of Scenes in Build Index {SceneEventData.SceneIndex} being loaded in {loadSceneMode.ToString()} mode failed validation on the {serverHostorClient}!");
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
        private void SendSceneEventData(ulong[] targetClientIds)
        {
            if (targetClientIds.Length == 0)
            {
                // This would be the Host/Server with no clients connected
                // Silently return as there is nothing to be done
                return;
            }

            var context = m_NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                k_MessageType, k_ChannelType, targetClientIds, k_NetworkUpdateStage);

            if (context != null)
            {
                using var nonNullContext = (InternalCommandContext)context;
                var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                bufferSizeCapture.StartMeasureSegment();

                SceneEventData.OnWrite(nonNullContext.NetworkWriter);

                var size = bufferSizeCapture.StopMeasureSegment();
                m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                    targetClientIds, (uint)SceneEventData.SceneEventType, ScenesInBuild[(int)SceneEventData.SceneIndex], size);
                return;
            }

            // This should never happen, but if it does something very bad has happened and we should throw an exception
            throw new Exception($"{nameof(InternalCommandContext)} is null! {nameof(NetworkSceneManager)} failed to send event notification {SceneEventData.SceneEventType} to target clientIds {targetClientIds}!");
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
            // Return scene event already in progress if one is already in progress... :)
            if (s_IsSceneEventActive)
            {
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneEventInProgress);
            }

            // Return invalid scene name status if the scene name is invalid... :)
            if (!IsSceneNameValid(sceneName))
            {
                return new SceneEventProgress(null, SceneEventProgressStatus.InvalidSceneName);
            }

            var sceneEventProgress = new SceneEventProgress(m_NetworkManager);
            sceneEventProgress.SceneBuildIndex = GetBuildIndexFromSceneName(sceneName);
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
            // Send a message to all clients that all clients are done loading or unloading
            var context = m_NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                k_MessageType, k_ChannelType, m_NetworkManager.ConnectedClientsIds, k_NetworkUpdateStage);
            if (context != null)
            {
                using var nonNullContext = (InternalCommandContext)context;
                ClientSynchEventData.SceneEventGuid = sceneEventProgress.Guid;
                ClientSynchEventData.SceneIndex = sceneEventProgress.SceneBuildIndex;
                ClientSynchEventData.SceneEventType = sceneEventProgress.SceneEventType;
                ClientSynchEventData.ClientsCompleted = sceneEventProgress.DoneClients;
                ClientSynchEventData.ClientsTimedOut = m_NetworkManager.ConnectedClients.Keys.Except(sceneEventProgress.DoneClients).ToList();

                var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                bufferSizeCapture.StartMeasureSegment();

                ClientSynchEventData.OnWrite(nonNullContext.NetworkWriter);

                var size = bufferSizeCapture.StopMeasureSegment();
                m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                    m_NetworkManager.ConnectedClientsIds,
                    (uint)sceneEventProgress.SceneEventType,
                    ScenesInBuild[(int)sceneEventProgress.SceneBuildIndex],
                    size);
            }

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

            SceneEventData.SceneEventGuid = sceneEventProgress.Guid;
            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Unload;
            SceneEventData.SceneIndex = GetBuildIndexFromSceneName(sceneName);
            SceneEventData.SceneHandle = sceneHandle;

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventData.SceneEventTypes.S2C_UnLoadComplete;

            ScenesLoaded.Remove(scene.handle);

            AsyncOperation sceneUnload = SceneManager.UnloadSceneAsync(scene);
            sceneUnload.completed += (AsyncOperation asyncOp2) => { OnSceneUnloaded(); };
            sceneEventProgress.SetSceneLoadOperation(sceneUnload);

            // Notify local server that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = SceneEventData.SceneEventType,
                LoadSceneMode = SceneEventData.LoadSceneMode,
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
        private void OnClientUnloadScene()
        {
            if (!IsSceneIndexValid(SceneEventData.SceneIndex))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }

                return;
            }

            var sceneName = ScenesInBuild[(int)SceneEventData.SceneIndex];

            if (!ServerSceneHandleToClientSceneHandle.ContainsKey(SceneEventData.SceneHandle))
            {
                throw new Exception($"Client failed to unload scene {sceneName} " +
                    $"because we are missing the client scene handle due to the server scene handle {SceneEventData.SceneHandle} not being found!");
            }

            var sceneHandle = ServerSceneHandleToClientSceneHandle[SceneEventData.SceneHandle];

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
                sceneUnload.completed += asyncOp2 => OnSceneUnloaded();
            }
#else
            sceneUnload = SceneManager.UnloadSceneAsync(ScenesLoaded[sceneHandle]);
            sceneUnload.completed += asyncOp2 => OnSceneUnloaded();
#endif
            ScenesLoaded.Remove(sceneHandle);

            // Remove our server to scene handle lookup
            ServerSceneHandleToClientSceneHandle.Remove(SceneEventData.SceneHandle);

            // Notify the local client that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = SceneEventData.SceneEventType,
                LoadSceneMode = SceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.LocalClientId   // Server sent this message to the client, but client is executing it
            });


#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_IsRunningUnitTest)
            {
                OnSceneUnloaded();
            }
#endif
        }

        /// <summary>
        /// Server and Client:
        /// Invoked when an additively loaded scene is unloaded
        /// </summary>
        private void OnSceneUnloaded()
        {
            // First thing we do, if we are a server, is to send the unload scene event.
            if (m_NetworkManager.IsServer)
            {
                // Server sends the unload scene notification after unloading because it will despawn all scene relative in-scene NetworkObjects
                // If we send this event to all clients before the server is finished unloading they will get warning about an object being
                // despawned that no longer exists
                SendSceneEventData(m_NetworkManager.ConnectedClientsIds.Where(c => c != m_NetworkManager.ServerClientId).ToArray());

                //Second, server sets itself as having finished unloading
                if (SceneEventProgressTracking.ContainsKey(SceneEventData.SceneEventGuid))
                {
                    SceneEventProgressTracking[SceneEventData.SceneEventGuid].AddClientAsDone(m_NetworkManager.ServerClientId);
                }
            }

            // Next we prepare to send local notifications for unload complete
            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_UnloadComplete;

            //Notify the client or server that a scene was unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventType,
                LoadSceneMode = SceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
                ClientId = m_NetworkManager.IsServer ? m_NetworkManager.ServerClientId : m_NetworkManager.LocalClientId
            });

            // Clients send a notification back to the server they have completed the unload scene event
            if (!m_NetworkManager.IsServer)
            {
                SendSceneEventData(new ulong[] { m_NetworkManager.ServerClientId });
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

            // Now set up the current scene event
            SceneEventData.SceneEventGuid = sceneEventProgress.Guid;
            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_Load;
            SceneEventData.SceneIndex = GetBuildIndexFromSceneName(sceneName);
            SceneEventData.LoadSceneMode = loadSceneMode;

            // This both checks to make sure the scene is valid and if not resets the active scene event
            s_IsSceneEventActive = ValidateSceneBeforeLoading(SceneEventData.SceneIndex, loadSceneMode);
            if (!s_IsSceneEventActive)
            {
                return SceneEventProgressStatus.SceneFailedVerification;
            }

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
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
            sceneLoad.completed += (AsyncOperation asyncOp2) => { OnSceneLoaded(sceneName); };
            sceneEventProgress.SetSceneLoadOperation(sceneLoad);

            // Notify the local server that a scene loading event has begun
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = SceneEventData.SceneEventType,
                LoadSceneMode = SceneEventData.LoadSceneMode,
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
        private void OnClientSceneLoadingEvent(Stream objectStream)
        {
            if (!IsSceneIndexValid(SceneEventData.SceneIndex))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }
                return;
            }

            var sceneName = ScenesInBuild[(int)SceneEventData.SceneIndex];

            // Run scene validation before loading a scene
            if (!ValidateSceneBeforeLoading(SceneEventData.SceneIndex, SceneEventData.LoadSceneMode))
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
                    SceneEventType = SceneEventData.SceneEventType,
                    LoadSceneMode = SceneEventData.LoadSceneMode,
                    SceneName = sceneName,
                    ClientId = m_NetworkManager.LocalClientId
                });

                // Unit tests must mirror the server's scenes loaded dictionary, otherwise this portion will fail
                if (ScenesLoaded.ContainsKey(SceneEventData.SceneHandle))
                {
                    OnClientLoadedScene(ScenesLoaded[SceneEventData.SceneHandle]);
                }
                else
                {
                    throw new Exception($"Could not find the scene handle {SceneEventData.SceneHandle} for scene {sceneName} " +
                        $"during unit test.  Did you forget to register this in the unit test?");
                }
                return;
            }
#endif

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
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
            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            }

            var sceneLoad = SceneManager.LoadSceneAsync(sceneName, SceneEventData.LoadSceneMode);
            sceneLoad.completed += asyncOp2 => OnSceneLoaded(sceneName);

            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = SceneEventData.SceneEventType,
                LoadSceneMode = SceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = m_NetworkManager.LocalClientId
            });
        }


        /// <summary>
        /// Client and Server:
        /// Generic on scene loaded callback method to be called upon a scene loading
        /// </summary>
        private void OnSceneLoaded(string sceneName)
        {
            var nextScene = GetAndAddNewlyLoadedSceneByName(sceneName);
            if (!nextScene.isLoaded || !nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            //Get all NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene);

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Move all objects to the new scene
                MoveObjectsToScene(nextScene);
            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
            // they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
            // not destroy temporary scene are moved into the active scene
            IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

            if (m_NetworkManager.IsServer)
            {
                OnServerLoadedScene(nextScene);
            }
            else
            {
                // For the client, we make a server scene handle to client scene handle look up table
                if (!ServerSceneHandleToClientSceneHandle.ContainsKey(SceneEventData.SceneHandle))
                {
                    ServerSceneHandleToClientSceneHandle.Add(SceneEventData.SceneHandle, nextScene.handle);
                }
                else
                {
                    // If the exact same handle exists then there are problems with using handles
                    throw new Exception($"Server Scene Handle ({SceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
                }

                OnClientLoadedScene(nextScene);
            }
        }

        /// <summary>
        /// Server side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnServerLoadedScene(Scene scene)
        {
            // Register in-scene placed NetworkObjects with spawn manager
            foreach (var keyValuePairByGlobalObjectIdHash in ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    if (!keyValuePairBySceneHandle.Value.IsPlayerObject)
                    {
                        m_NetworkManager.SpawnManager.SpawnNetworkObjectLocally(keyValuePairBySceneHandle.Value, m_NetworkManager.SpawnManager.GetNetworkObjectId(), true, false, null, null, false, true);
                    }
                }
            }

            // Set the server's scene's handle so the client can build a look up table
            SceneEventData.SceneHandle = scene.handle;

            // Send all clients the scene load event
            for (int j = 0; j < m_NetworkManager.ConnectedClientsList.Count; j++)
            {
                var clientId = m_NetworkManager.ConnectedClientsList[j].ClientId;
                if (clientId != m_NetworkManager.ServerClientId)
                {
                    var context = m_NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                        k_MessageType, k_ChannelType, new ulong[] { clientId }, k_NetworkUpdateStage);
                    if (context != null)
                    {
                        // Set the target client id that will be used during in scene NetworkObject serialization
                        SceneEventData.TargetClientId = clientId;
                        using var nonNullContext = (InternalCommandContext)context;

                        var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                        bufferSizeCapture.StartMeasureSegment();

                        SceneEventData.OnWrite(nonNullContext.NetworkWriter);

                        var size = bufferSizeCapture.StopMeasureSegment();
                        m_NetworkManager.NetworkMetrics.TrackSceneEventSent(clientId, (uint)SceneEventData.SceneEventType, scene.name, size);
                    }
                    else
                    {
                        throw new Exception($"{nameof(NetworkSceneManager)} failed to send event notification {SceneEventData.SceneEventType} to target clientId {clientId}!");
                    }
                }
            }

            s_IsSceneEventActive = false;
            //First, notify local server that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete,
                LoadSceneMode = SceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
                ClientId = m_NetworkManager.ServerClientId,
                Scene = scene,
            });

            //Second, set the server as having loaded for the associated SceneEventProgress
            if (SceneEventProgressTracking.ContainsKey(SceneEventData.SceneEventGuid))
            {
                SceneEventProgressTracking[SceneEventData.SceneEventGuid].AddClientAsDone(m_NetworkManager.ServerClientId);
            }
        }

        /// <summary>
        /// Client side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnClientLoadedScene(Scene scene)
        {
            SceneEventData.DeserializeScenePlacedObjects();

            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete;
            SendSceneEventData(new ulong[] { m_NetworkManager.ServerClientId });
            s_IsSceneEventActive = false;

            // Notify local client that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete,
                LoadSceneMode = SceneEventData.LoadSceneMode,
                SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
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
            ClientSynchEventData.InitializeForSynch();
            ClientSynchEventData.TargetClientId = clientId;
            ClientSynchEventData.LoadSceneMode = LoadSceneMode.Single;
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
                    if (!ValidateSceneBeforeLoading(sceneIndex, LoadSceneMode.Single))
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

            var context = m_NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                k_MessageType, k_ChannelType, new ulong[] { clientId }, k_NetworkUpdateStage);
            if (context != null)
            {

                using var nonNullContext = (InternalCommandContext)context;

                var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                bufferSizeCapture.StartMeasureSegment();

                ClientSynchEventData.OnWrite(nonNullContext.NetworkWriter);

                var size = bufferSizeCapture.StopMeasureSegment();
                foreach (var sceneIndex in ClientSynchEventData.ScenesToSynchronize)
                {
                    m_NetworkManager.NetworkMetrics.TrackSceneEventSent(
                       clientId, (uint)ClientSynchEventData.SceneEventType, ScenesInBuild[(int)sceneIndex], size);
                }
            }

            // Notify the local server that the client has been sent the SceneEventData.SceneEventTypes.S2C_Event_Sync event
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventData.SceneEventType,
                ClientId = clientId
            });
        }

        /// <summary>
        /// This is called when the client receives the SCENE_EVENT of type SceneEventData.SceneEventTypes.SYNC
        /// Note: This can recurse one additional time by the client if the current scene loaded by the client
        /// is already loaded.
        /// </summary>
        private void OnClientBeginSync()
        {
            var sceneIndex = SceneEventData.GetNextSceneSynchronizationIndex();
            var sceneHandle = SceneEventData.GetNextSceneSynchronizationHandle();
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
            var loadSceneMode = sceneIndex == SceneEventData.SceneIndex ? SceneEventData.LoadSceneMode : LoadSceneMode.Additive;

            // Always check to see if the scene needs to be validated
            if (!ValidateSceneBeforeLoading(SceneEventData.SceneIndex, loadSceneMode))
            {
                return;
            }

            // If this is the beginning of the synchronization event, then send client a notification that synchronization has begun
            if (sceneIndex == SceneEventData.SceneIndex)
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
                sceneLoad.completed += asyncOp2 => ClientLoadedSynchronization(sceneIndex, sceneHandle);
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
                ClientLoadedSynchronization(sceneIndex, sceneHandle);
            }
        }

        /// <summary>
        /// Once a scene is loaded ( or if it was already loaded) this gets called.
        /// This handles all of the in-scene and dynamically spawned NetworkObject synchronization
        /// </summary>
        /// <param name="sceneIndex">Netcode scene index that was loaded</param>
        private void ClientLoadedSynchronization(uint sceneIndex, int sceneHandle)
        {
            var sceneName = ScenesInBuild[(int)sceneIndex];
            var nextScene = GetAndAddNewlyLoadedSceneByName(sceneName);

            if (!nextScene.isLoaded || !nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }

            var loadSceneMode = (sceneIndex == SceneEventData.SceneIndex ? SceneEventData.LoadSceneMode : LoadSceneMode.Additive);

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
                throw new Exception($"Server Scene Handle ({SceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
            }

            // Apply all in-scene placed NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene, false);

            // Send notification back to server that we finished loading this scene
            ClientSynchEventData.LoadSceneMode = loadSceneMode;
            ClientSynchEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_LoadComplete;
            ClientSynchEventData.SceneIndex = sceneIndex;

            var context = m_NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                k_MessageType, k_ChannelType, new ulong[] { m_NetworkManager.ServerClientId }, k_NetworkUpdateStage);
            if (context != null)
            {
                using var nonNullContext = (InternalCommandContext)context;

                var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                bufferSizeCapture.StartMeasureSegment();

                ClientSynchEventData.OnWrite(nonNullContext.NetworkWriter);

                var size = bufferSizeCapture.StopMeasureSegment();
                m_NetworkManager.NetworkMetrics.TrackSceneEventSent(m_NetworkManager.ServerClientId, (uint)ClientSynchEventData.SceneEventType, sceneName, size);
            }

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
            HandleClientSceneEvent(null);
        }

        /// <summary>
        /// Client Side:
        /// Handles incoming Scene_Event messages for clients
        /// </summary>
        /// <param name="stream">data associated with the event</param>
        private void HandleClientSceneEvent(Stream stream)
        {
            switch (SceneEventData.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.S2C_Load:
                    {
                        OnClientSceneLoadingEvent(stream);
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_Unload:
                    {
                        OnClientUnloadScene();
                        break;
                    }
                case SceneEventData.SceneEventTypes.S2C_Sync:
                    {
                        if (!SceneEventData.IsDoneWithSynchronization())
                        {
                            OnClientBeginSync();
                        }
                        else
                        {
                            // Include anything in the DDOL scene
                            PopulateScenePlacedObjects(DontDestroyOnLoadScene, false);
                            // Synchronize the NetworkObjects for this scene
                            SceneEventData.SynchronizeSceneNetworkObjects(m_NetworkManager);

                            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.C2S_SyncComplete;
                            SendSceneEventData(new ulong[] { m_NetworkManager.ServerClientId });

                            // All scenes are synchronized, let the server know we are done synchronizing
                            m_NetworkManager.IsConnectedClient = true;

                            // Notify the client that they have finished synchronizing
                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = SceneEventData.SceneEventType,
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
                            SceneEventType = SceneEventData.SceneEventType,
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
                            SceneEventType = SceneEventData.SceneEventType,
                            SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
                            ClientId = m_NetworkManager.ServerClientId,
                            LoadSceneMode = SceneEventData.LoadSceneMode,
                            ClientsThatCompleted = SceneEventData.ClientsCompleted,
                            ClientsThatTimedOut = SceneEventData.ClientsTimedOut,
                        });
                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{SceneEventData.SceneEventType} is not currently supported!");
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
        private void HandleServerSceneEvent(ulong clientId, Stream stream)
        {
            switch (SceneEventData.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.C2S_LoadComplete:
                    {
                        // Notify the local server that the client has finished loading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = SceneEventData.SceneEventType,
                            LoadSceneMode = SceneEventData.LoadSceneMode,
                            SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
                            ClientId = clientId
                        });

                        if (SceneEventProgressTracking.ContainsKey(SceneEventData.SceneEventGuid))
                        {
                            SceneEventProgressTracking[SceneEventData.SceneEventGuid].AddClientAsDone(clientId);
                        }

                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_UnloadComplete:
                    {
                        if (SceneEventProgressTracking.ContainsKey(SceneEventData.SceneEventGuid))
                        {
                            SceneEventProgressTracking[SceneEventData.SceneEventGuid].AddClientAsDone(clientId);
                        }
                        // Notify the local server that the client has finished unloading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = SceneEventData.SceneEventType,
                            LoadSceneMode = SceneEventData.LoadSceneMode,
                            SceneName = ScenesInBuild[(int)SceneEventData.SceneIndex],
                            ClientId = clientId
                        });

                        break;
                    }
                case SceneEventData.SceneEventTypes.C2S_SyncComplete:
                    {
                        // Notify the local server that a client has finished synchronizing
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = SceneEventData.SceneEventType,
                            SceneName = string.Empty,
                            ClientId = clientId
                        });

                        // While we did invoke the C2S_SyncComplete event notification, we will also call the traditional client connected callback on the server
                        // which assures the client is "ready to receive RPCs" as well.
                        m_NetworkManager.InvokeOnClientConnectedCallback(clientId);

                        if (SceneEventData.ClientNeedsReSynchronization() && !DisableReSynchronization)
                        {
                            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.S2C_ReSync;
                            SendSceneEventData(new ulong[] { clientId });

                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = SceneEventData.SceneEventType,
                                SceneName = string.Empty,
                                ClientId = clientId
                            });
                        }

                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{SceneEventData.SceneEventType} is not currently supported!");
                        break;
                    }
            }
        }

        /// <summary>
        /// Both Client and Server: Incoming scene event entry point
        /// </summary>
        /// <param name="clientId">client who sent the scene event</param>
        /// <param name="stream">data associated with the scene event</param>
        internal void HandleSceneEvent(ulong clientId, Stream stream)
        {
            if (m_NetworkManager != null)
            {
                if (stream != null)
                {
                    var reader = NetworkReaderPool.GetReader(stream);
                    SceneEventData.OnRead(reader);
                    NetworkReaderPool.PutBackInPool(reader);

                    if (SceneEventData.SceneEventType == SceneEventData.SceneEventTypes.S2C_Sync)
                    {
                        // For this event the server may be sending scene event data about multiple scenes, so we need
                        // to track a metric for each one.
                        foreach (var sceneIndex in SceneEventData.ScenesToSynchronize)
                        {
                            m_NetworkManager.NetworkMetrics.TrackSceneEventReceived(
                               clientId, (uint)SceneEventData.SceneEventType, ScenesInBuild[(int)sceneIndex], stream.Length);
                        }
                    }
                    else
                    {
                        // For all other scene event types, we are only dealing with one scene at a time, so we can read it
                        // from the SceneEventData directly.
                        m_NetworkManager.NetworkMetrics.TrackSceneEventReceived(
                           clientId, (uint)SceneEventData.SceneEventType, ScenesInBuild[(int)SceneEventData.SceneIndex], stream.Length);
                    }

                    if (SceneEventData.IsSceneEventClientSide())
                    {
                        HandleClientSceneEvent(stream);
                    }
                    else
                    {
                        HandleServerSceneEvent(clientId, stream);
                    }
                }
                else
                {
                    Debug.LogError($"Scene Event {nameof(OnClientSceneLoadingEvent)} was invoked with a null stream!");
                    return;
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
                //In case an object has been set as a child of another object it has to be removed from the parent in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                if (!sobj.DestroyWithScene || (sobj.IsSceneObject != null && sobj.IsSceneObject.Value && sobj.gameObject.scene == DontDestroyOnLoadScene))
                {
                    UnityEngine.Object.DontDestroyOnLoad(sobj.gameObject);
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
        private void MoveObjectsToScene(Scene scene)
        {
            // Move ALL NetworkObjects to the temp scene
            var objectsToKeep = m_NetworkManager.SpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                //In case an object has been set as a child of another object it has to be removed from the parent in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                if (sobj.gameObject.scene == DontDestroyOnLoadScene && (sobj.IsSceneObject == null || sobj.IsSceneObject.Value))
                {
                    continue;
                }

                SceneManager.MoveGameObjectToScene(sobj.gameObject, scene);
            }
        }
    }
}
