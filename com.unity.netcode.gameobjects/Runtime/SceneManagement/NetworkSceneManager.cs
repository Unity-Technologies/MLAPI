using System;
using System.Collections.Generic;
using System.Linq;
using TrollKing.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


namespace Unity.Netcode
{
    /// <summary>
    /// Used for local notifications of various scene events.  The <see cref="NetworkSceneManager.OnSceneEvent"/> of
    /// delegate type <see cref="NetworkSceneManager.SceneEventDelegate"/> uses this class to provide
    /// scene event status.<br />
    /// <em>Note: This is only when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled.</em><br />
    /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
    /// See also: <br />
    /// <seealso cref="SceneEventType"/>
    /// </summary>
    public class SceneEvent
    {
        /// <summary>
        /// The <see cref="UnityEngine.AsyncOperation"/> returned by <see cref="SceneManager"/><br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.Load"/></term></item>
        /// <item><term><see cref="SceneEventType.Unload"/></term></item>
        /// </list>
        /// </summary>
        public AsyncOperationHandle AsyncOperation;

        /// <summary>
        /// Will always be set to the current <see cref="Netcode.SceneEventType"/>
        /// </summary>
        public SceneEventType SceneEventType;

        /// <summary>
        /// If applicable, this reflects the type of scene loading or unloading that is occurring.<br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.Load"/></term></item>
        /// <item><term><see cref="SceneEventType.Unload"/></term></item>
        /// <item><term><see cref="SceneEventType.LoadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.LoadEventCompleted"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadEventCompleted"/></term></item>
        /// </list>
        /// </summary>
        public LoadSceneMode LoadSceneMode;

        /// <summary>
        /// This will be set to the scene name that the event pertains to.<br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.Load"/></term></item>
        /// <item><term><see cref="SceneEventType.Unload"/></term></item>
        /// <item><term><see cref="SceneEventType.LoadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.LoadEventCompleted"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadEventCompleted"/></term></item>
        /// </list>
        /// </summary>
        public string SceneName;

        /// <summary>
        /// When a scene is loaded, the Scene structure is returned.<br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.LoadComplete"/></term></item>
        /// </list>
        /// </summary>
        public Scene Scene;

        /// <summary>
        /// The client identifier can vary depending upon the following conditions: <br />
        /// <list type="number">
        /// <item><term><see cref="Netcode.SceneEventType"/>s that always set the <see cref="ClientId"/>
        /// to the local client identifier, are initiated (and processed locally) by the
        /// server-host, and sent to all clients to be processed.<br />
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.Load"/></term></item>
        /// <item><term><see cref="SceneEventType.Unload"/></term></item>
        /// <item><term><see cref="SceneEventType.Synchronize"/></term></item>
        /// <item><term><see cref="SceneEventType.ReSynchronize"/></term></item>
        /// </list>
        /// </term></item>
        /// <item><term>Events that always set the <see cref="ClientId"/> to the local client identifier,
        /// are initiated (and processed locally) by a client or server-host, and if initiated
        /// by a client will always be sent to and processed on the server-host:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.LoadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadComplete"/></term></item>
        /// <item><term><see cref="SceneEventType.SynchronizeComplete"/></term></item>
        /// </list>
        /// </term></item>
        /// <item><term>
        /// Events that always set the <see cref="ClientId"/> to the ServerId:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.LoadEventCompleted"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadEventCompleted"/></term></item>
        /// </list>
        /// </term></item>
        /// </list>
        /// </summary>
        public ulong ClientId;

        /// <summary>
        /// List of clients that completed a loading or unloading event.<br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.LoadEventCompleted"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadEventCompleted"/></term></item>
        /// </list>
        /// </summary>
        public List<ulong> ClientsThatCompleted;

        /// <summary>
        /// List of clients that timed out during a loading or unloading event.<br />
        /// This is set for the following <see cref="Netcode.SceneEventType"/>s:
        /// <list type="bullet">
        /// <item><term><see cref="SceneEventType.LoadEventCompleted"/></term></item>
        /// <item><term><see cref="SceneEventType.UnloadEventCompleted"/></term></item>
        /// </list>
        /// </summary>
        public List<ulong> ClientsThatTimedOut;
    }

    /// <summary>
    /// Main class for managing network scenes when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled.
    /// Uses the <see cref="SceneEventMessage"/> message to communicate <see cref="SceneEventData"/> between the server and client(s)
    /// </summary>
    public class NetworkSceneManager : IDisposable
    {
        private static readonly NetworkLogScope Log = new NetworkLogScope(nameof(NetworkSceneManager));

        private const NetworkDelivery k_DeliveryType = NetworkDelivery.ReliableFragmentedSequenced;
        internal const int InvalidSceneNameOrPath = -1;

        // Used to be able to turn re-synchronization off
        internal static bool DisableReSynchronization;

        /// <summary>
        /// Used to detect if a scene event is underway
        /// Only 1 scene event can occur on the server at a time for now.
        /// </summary>
        private bool m_IsSceneEventActive = false;

        /// <summary>
        /// The delegate callback definition for scene event notifications.<br />
        /// See also: <br />
        /// <seealso cref="SceneEvent"/><br />
        /// <seealso cref="SceneEventData"/>
        /// </summary>
        /// <param name="sceneEvent"></param>
        public delegate void SceneEventDelegate(SceneEvent sceneEvent);

        /// <summary>
        /// Subscribe to this event to receive all <see cref="SceneEventType"/> notifications.<br />
        /// For more details review over <see cref="SceneEvent"/> and <see cref="SceneEventType"/>.<br />
        /// <b>Alternate Single Event Type Notification Registration Options</b><br />
        /// To receive only a specific event type notification or a limited set of notifications you can alternately subscribe to
        /// each notification type individually via the following events:<br />
        /// <list type="bullet">
        /// <item><term><see cref="OnLoad"/> Invoked only when a <see cref="SceneEventType.Load"/> event is being processed</term></item>
        /// <item><term><see cref="OnUnload"/> Invoked only when an <see cref="SceneEventType.Unload"/> event is being processed</term></item>
        /// <item><term><see cref="OnSynchronize"/> Invoked only when a <see cref="SceneEventType.Synchronize"/> event is being processed</term></item>
        /// <item><term><see cref="OnLoadEventCompleted"/> Invoked only when a <see cref="SceneEventType.LoadEventCompleted"/> event is being processed</term></item>
        /// <item><term><see cref="OnUnloadEventCompleted"/> Invoked only when an <see cref="SceneEventType.UnloadEventCompleted"/> event is being processed</term></item>
        /// <item><term><see cref="OnLoadComplete"/> Invoked only when a <see cref="SceneEventType.LoadComplete"/> event is being processed</term></item>
        /// <item><term><see cref="OnUnloadComplete"/> Invoked only when an <see cref="SceneEventType.UnloadComplete"/> event is being processed</term></item>
        /// <item><term><see cref="OnSynchronizeComplete"/> Invoked only when a <see cref="SceneEventType.SynchronizeComplete"/> event is being processed</term></item>
        /// </list>
        /// <em>Note: Do not start new scene events within NetworkSceneManager scene event notification callbacks.</em><br />
        /// </summary>
        public event SceneEventDelegate OnSceneEvent;

        /// <summary>
        /// Delegate declaration for the OnLoad event.<br />
        /// See also: <br />
        /// <seealso cref="SceneEventType.Load"/>for more information
        /// </summary>
        /// <param name="clientId">the client that is processing this event (the server will receive all of these events for every client and itself)</param>
        /// <param name="sceneName">name of the scene being processed</param>
        /// <param name="loadSceneMode">the LoadSceneMode mode for the scene being loaded</param>
        /// <param name="asyncOperation">the associated <see cref="AsyncOperation"/> that can be used for scene loading progress</param>
        public delegate void OnLoadDelegateHandler(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperationHandle<SceneInstance> asyncOperation);

        /// <summary>
        /// Delegate declaration for the OnUnload event.<br />
        /// See also: <br />
        /// <seealso cref="SceneEventType.Unload"/> for more information
        /// </summary>
        /// <param name="clientId">the client that is processing this event (the server will receive all of these events for every client and itself)</param>
        /// <param name="sceneName">name of the scene being processed</param>
        /// <param name="asyncOperation">the associated <see cref="AsyncOperation"/> that can be used for scene unloading progress</param>
        public delegate void OnUnloadDelegateHandler(ulong clientId, string sceneName, AsyncOperationHandle<SceneInstance> asyncOperation);

        /// <summary>
        /// Delegate declaration for the OnSynchronize event.<br />
        /// See also: <br />
        /// <seealso cref="SceneEventType.Synchronize"/> for more information
        /// </summary>
        /// <param name="clientId">the client that is processing this event (the server will receive all of these events for every client and itself)</param>
        public delegate void OnSynchronizeDelegateHandler(ulong clientId);

        /// <summary>
        /// Delegate declaration for the OnLoadEventCompleted and OnUnloadEventCompleted events.<br />
        /// See also:<br />
        /// <seealso cref="SceneEventType.LoadEventCompleted"/><br />
        /// <seealso cref="SceneEventType.UnloadEventCompleted"/>
        /// </summary>
        /// <param name="sceneName">scene pertaining to this event</param>
        /// <param name="loadSceneMode"><see cref="LoadSceneMode"/> of the associated event completed</param>
        /// <param name="clientsCompleted">the clients that completed the loading event</param>
        /// <param name="clientsTimedOut">the clients (if any) that timed out during the loading event</param>
        public delegate void OnEventCompletedDelegateHandler(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut);

        /// <summary>
        /// Delegate declaration for the OnLoadComplete event.<br />
        /// See also:<br />
        /// <seealso cref="SceneEventType.LoadComplete"/> for more information
        /// </summary>
        /// <param name="clientId">the client that is processing this event (the server will receive all of these events for every client and itself)</param>
        /// <param name="sceneName">the scene name pertaining to this event</param>
        /// <param name="loadSceneMode">the mode the scene was loaded in</param>
        public delegate void OnLoadCompleteDelegateHandler(ulong clientId, string sceneName, LoadSceneMode loadSceneMode);

        /// <summary>
        /// Delegate declaration for the OnUnloadComplete event.<br />
        /// See also:<br />
        /// <seealso cref="SceneEventType.UnloadComplete"/> for more information
        /// </summary>
        /// <param name="clientId">the client that is processing this event (the server will receive all of these events for every client and itself)</param>
        /// <param name="sceneName">the scene name pertaining to this event</param>
        public delegate void OnUnloadCompleteDelegateHandler(ulong clientId, string sceneName);

        /// <summary>
        /// Delegate declaration for the OnSynchronizeComplete event.<br />
        /// See also:<br />
        /// <seealso cref="SceneEventType.SynchronizeComplete"/> for more information
        /// </summary>
        /// <param name="clientId">the client that completed this event</param>
        public delegate void OnSynchronizeCompleteDelegateHandler(ulong clientId);

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.Load"/> event is started by the server.<br />
        /// <em>Note: The server and connected client(s) will always receive this notification.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnLoadDelegateHandler OnLoad;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.Unload"/> event is started by the server.<br />
        /// <em>Note: The server and connected client(s) will always receive this notification.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnUnloadDelegateHandler OnUnload;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.Synchronize"/> event is started by the server
        /// after a client is approved for connection in order to synchronize the client with the currently loaded
        /// scenes and NetworkObjects.  This event signifies the beginning of the synchronization event.<br />
        /// <em>Note: The server and connected client(s) will always receive this notification.
        /// This event is generated on a per newly connected and approved client basis.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnSynchronizeDelegateHandler OnSynchronize;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.LoadEventCompleted"/> event is generated by the server.
        /// This event signifies the end of an existing <see cref="SceneEventType.Load"/> event as it pertains
        /// to all clients connected when the event was started.  This event signifies that all clients (and server) have
        /// finished the <see cref="SceneEventType.Load"/> event.<br />
        /// <em>Note: this is useful to know when all clients have loaded the same scene (single or additive mode)</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnEventCompletedDelegateHandler OnLoadEventCompleted;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.UnloadEventCompleted"/> event is generated by the server.
        /// This event signifies the end of an existing <see cref="SceneEventType.Unload"/> event as it pertains
        /// to all clients connected when the event was started.  This event signifies that all clients (and server) have
        /// finished the <see cref="SceneEventType.Unload"/> event.<br />
        /// <em>Note: this is useful to know when all clients have unloaded a specific scene.  The <see cref="LoadSceneMode"/> will
        /// always be <see cref="LoadSceneMode.Additive"/> for this event.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnEventCompletedDelegateHandler OnUnloadEventCompleted;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.LoadComplete"/> event is generated by a client or server.<br />
        /// <em>Note: The server receives this message from all clients (including itself).
        /// Each client receives their own notification sent to the server.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnLoadCompleteDelegateHandler OnLoadComplete;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.UnloadComplete"/> event is generated by a client or server.<br />
        /// <em>Note: The server receives this message from all clients (including itself).
        /// Each client receives their own notification sent to the server.</em><br />
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnUnloadCompleteDelegateHandler OnUnloadComplete;

        /// <summary>
        /// Invoked when a <see cref="SceneEventType.SynchronizeComplete"/> event is generated by a client. <br />
        /// <em> Note: The server receives this message from the client, but will never generate this event for itself.
        /// Each client receives their own notification sent to the server.  This is useful to know that a client has
        /// completed the entire connection sequence, loaded all scenes, and synchronized all NetworkObjects.</em>
        /// <em>*** Do not start new scene events within scene event notification callbacks.</em><br />
        /// </summary>
        public event OnSynchronizeCompleteDelegateHandler OnSynchronizeComplete;

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
        /// </summary>
        /// <remarks>
        /// <b>Client Side:</b> In order for clients to be notified of this condition you must assign the <see cref="VerifySceneBeforeLoading"/> delegate handler.<br />
        /// <b>Server Side:</b> <see cref="LoadScene(string, LoadSceneMode)"/> will return <see cref="SceneEventProgressStatus"/>.
        /// </remarks>
        public VerifySceneBeforeLoadingDelegateHandler VerifySceneBeforeLoading;

        /// <summary>
        /// Delegate declaration for the <see cref="VerifySceneBeforeUnloading"/> handler that provides
        /// an additional level of scene unloading validation to assure the scene being unloaded should
        /// be unloaded.
        /// </summary>
        /// <param name="scene">The scene to be unloaded</param>
        /// <returns>true (valid) or false (not valid)</returns>
        public delegate bool VerifySceneBeforeUnloadingDelegateHandler(Scene scene);

        /// <summary>
        /// Client Side Only: <br />
        /// Delegate handler defined by <see cref="VerifySceneBeforeUnloadingDelegateHandler"/> that is only invoked when the client
        /// is finished synchronizing and when <see cref="ClientSynchronizationMode"/> is set to <see cref="LoadSceneMode.Additive"/>.
        /// </summary>
        public VerifySceneBeforeUnloadingDelegateHandler VerifySceneBeforeUnloading;

        /// <summary>
        /// When enabled and <see cref="ClientSynchronizationMode"/> is <see cref="LoadSceneMode.Additive"/>, any scenes not synchronized with
        /// the server will be unloaded unless <see cref="VerifySceneBeforeUnloading"/> returns true. This provides more granular control over
        /// which already loaded client-side scenes not synchronized with the server should be unloaded.
        /// </summary>
        /// <remarks>
        /// If the <see cref="VerifySceneBeforeUnloading"/> delegate callback is not set then any scene loaded on the just synchronized client
        /// will be unloaded.
        /// One scenario is a synchronized client is disconnected for unexpected reasons and attempts to reconnect to the same network session
        /// but still has all scenes that were loaded through server synchronization (initially or through scene events). However, during the
        /// client disconnection period the server unloads one (or more) of the scenes loaded and as such the reconnecting client could still
        /// have the now unloaded scenes still loaded. Enabling this flag coupled with assignment of the assignment of the <see cref="VerifySceneBeforeUnloading"/>
        /// delegate callback provides you with the ability to keep scenes loaded by the client (i.e. UI etc) while discarding any artifact
        /// scenes that no longer need to be loaded.
        /// </remarks>
        public bool PostSynchronizationSceneUnloading;

        private bool m_ActiveSceneSynchronizationEnabled;
        /// <summary>
        /// When enabled, the server or host will synchronize clients with changes to the currently active scene
        /// </summary>
        public bool ActiveSceneSynchronizationEnabled
        {
            get
            {
                return m_ActiveSceneSynchronizationEnabled;
            }
            set
            {
                if (m_ActiveSceneSynchronizationEnabled != value)
                {
                    m_ActiveSceneSynchronizationEnabled = value;
                    if (m_ActiveSceneSynchronizationEnabled)
                    {
                        SceneManager.activeSceneChanged += SceneManager_ActiveSceneChanged;
                    }
                    else
                    {
                        SceneManager.activeSceneChanged -= SceneManager_ActiveSceneChanged;
                    }
                }
            }
        }

        /// <summary>
        /// The SceneManagerHandler implementation
        /// </summary>
        public ISceneManagerHandler SceneManagerHandler = new DefaultSceneManagerHandler();

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

        public class SceneData
        {
            public SceneData(SceneInstance? instance, Scene reference)
            {
                SceneReference = reference;
                SceneInstance = instance;
            }
            public Scene SceneReference;
            public SceneInstance? SceneInstance;
        }

        /// <summary>
        /// Used to track which scenes are currently loaded
        /// We store the scenes as [SceneHandle][Scene] in order to handle the loading and unloading of the same scene additively
        /// Scene handle is only unique locally.  So, clients depend upon the <see cref="ServerSceneHandleToClientSceneHandle"/> in order
        /// to be able to know which specific scene instance the server is instructing the client to unload.
        /// The client links the server scene handle to the client local scene handle upon a scene being loaded
        /// <see cref="GetAndAddNewlyLoadedSceneByName"/>
        /// </summary>
        public Dictionary<int, SceneData> ScenesLoaded = new Dictionary<int, SceneData>();

        /// <summary>
        /// Returns the currently loaded scenes that are synchronized with the session owner or server depending upon the selected
        /// network topology.
        /// </summary>
        /// <remarks>
        /// The <see cref="SceneManager"/> scenes loaded returns all scenes loaded where this returns only the scenes that have been
        /// synchronized remotely. This can be useful when using scene validation and excluding certain scenes from being synchronized.
        /// </remarks>
        /// <returns>List of the known synchronized scenes</returns>
        public List<SceneData> GetSynchronizedScenes()
        {
            return ScenesLoaded.Values.ToList();
        }

        /// <summary>
        /// Since Scene.handle is unique per client, we create a look-up table between the client and server to associate server unique scene
        /// instances with client unique scene instances
        /// </summary>
        internal Dictionary<int, int> ServerSceneHandleToClientSceneHandle = new Dictionary<int, int>();
        internal Dictionary<int, int> ClientSceneHandleToServerSceneHandle = new Dictionary<int, int>();

        internal bool IsRestoringSession;
        /// <summary>
        /// Add the client to server (and vice versa) scene handle lookup.
        /// Add the client-side handle to scene entry in the HandleToScene table.
        /// If it fails (i.e. already added) it returns false.
        /// </summary>
        internal bool UpdateServerClientSceneHandle(int serverHandle, int clientHandle, Scene localScene)
        {
            if (!ServerSceneHandleToClientSceneHandle.ContainsKey(serverHandle))
            {
                Log.Info(() => $"Adding Server Scene Handle {clientHandle} {localScene.name}");
                ServerSceneHandleToClientSceneHandle.Add(serverHandle, clientHandle);
            }
            else if (!IsRestoringSession)
            {
                return false;
            }

            if (!ClientSceneHandleToServerSceneHandle.ContainsKey(clientHandle))
            {
                Log.Info(() => $"Adding Client Scene Handle {clientHandle} {localScene.name}");
                ClientSceneHandleToServerSceneHandle.Add(clientHandle, serverHandle);
            }
            else if (!IsRestoringSession)
            {
                return false;
            }

            // It is "Ok" if this already has an entry
            if (!ScenesLoaded.ContainsKey(clientHandle))
            {
                ScenesLoaded.Add(clientHandle, new SceneData(null, localScene));
            }

            return true;
        }

        /// <summary>
        /// Removes the client to server (and vice versa) scene handles.
        /// If it fails (i.e. already removed) it returns false.
        /// </summary>
        internal bool RemoveServerClientSceneHandle(int serverHandle, int clientHandle)
        {
            if (ServerSceneHandleToClientSceneHandle.ContainsKey(serverHandle))
            {
                Log.Info(() => $"Remove ServerSceneHandleToClientSceneHandle {clientHandle} {serverHandle}");
                ServerSceneHandleToClientSceneHandle.Remove(serverHandle);
            }
            else
            {
                return false;
            }

            if (ClientSceneHandleToServerSceneHandle.ContainsKey(clientHandle))
            {
                Log.Info(() => $"Remove ClientSceneHandleToServerSceneHandle {clientHandle} {serverHandle}");
                ClientSceneHandleToServerSceneHandle.Remove(clientHandle);
            }
            else
            {
                return false;
            }

            if (ScenesLoaded.ContainsKey(clientHandle))
            {
                Log.Info(() => $"Remove ScenesLoaded {clientHandle} {serverHandle}");
                ScenesLoaded.Remove(clientHandle);
            }
            else
            {
                return false;
            }

            return true;
        }

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
        /// Used for all scene event processing
        /// </summary>
        internal Dictionary<uint, SceneEventData> SceneEventDataStore;

        internal Dictionary<string, string> ScenePathsBySceneName;

        private NetworkManager NetworkManager { get; }

        // Keep track of this scene until the NetworkSceneManager is destroyed.
        public Scene DontDestroyOnLoadScene;

        /// <summary>
        /// This setting changes how clients handle scene loading when initially synchronizing with the server.<br />
        /// See: <see cref="SetClientSynchronizationMode(LoadSceneMode)"/>
        /// </summary>
        /// <remarks>
        /// <b>LoadSceneMode.Single:</b> All currently loaded scenes on the client will be unloaded and the
        /// server's currently active scene will be loaded in single mode on the client unless it was already
        /// loaded.<br />
        /// <b>LoadSceneMode.Additive:</b> All currently loaded scenes are left as they are and any newly loaded
        /// scenes will be loaded additively.  Users need to determine which scenes are valid to load via the
        /// <see cref="VerifySceneBeforeLoading"/> and, if <see cref="PostSynchronizationSceneUnloading"/> is
        /// set, <see cref="VerifySceneBeforeUnloading"/> callback(s).
        /// </remarks>
        public LoadSceneMode ClientSynchronizationMode { get; set; }

        /// <summary>
        /// When true, the <see cref="Debug.LogWarning(object)"/> messages will be turned off
        /// </summary>
        private bool m_DisableValidationWarningMessages;

        internal bool HasSceneAuthority()
        {
            if (!NetworkManager)
            {
                return false;
            }
            return (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer) || (NetworkManager.DistributedAuthorityMode && NetworkManager.LocalClient.IsSessionOwner);
        }

        /// <summary>
        /// Handle NetworkSeneManager clean up
        /// </summary>
        public void Dispose()
        {
            // Always assure we no longer listen to scene changes when disposed.
            SceneManager.activeSceneChanged -= SceneManager_ActiveSceneChanged;
            SceneUnloadEventHandler.Shutdown();
            foreach (var keypair in SceneEventDataStore)
            {
                if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"{nameof(SceneEventDataStore)} is disposing {nameof(SceneEventData.SceneEventId)} '{keypair.Key}'.");
                }
                keypair.Value.Dispose();
            }
            SceneEventDataStore.Clear();
            SceneEventDataStore = null;
        }

        /// <summary>
        /// Creates a new SceneEventData object for a new scene event
        /// </summary>
        /// <returns>SceneEventData instance</returns>
        internal SceneEventData BeginSceneEvent()
        {
            var sceneEventData = new SceneEventData(NetworkManager);
            SceneEventDataStore.Add(sceneEventData.SceneEventId, sceneEventData);
            return sceneEventData;
        }

        /// <summary>
        /// Disposes and removes SceneEventData object for the scene event
        /// </summary>
        /// <param name="sceneEventId">SceneEventId to end</param>
        internal void EndSceneEvent(uint sceneEventId)
        {
            if (SceneEventDataStore.ContainsKey(sceneEventId))
            {
                SceneEventDataStore[sceneEventId].Dispose();
                SceneEventDataStore.Remove(sceneEventId);
            }
            else
            {
                Debug.LogWarning($"Trying to dispose and remove SceneEventData Id '{sceneEventId}' that no longer exists!");
            }
        }

        /// <summary>
        /// Used for integration tests, normal runtime mode this will always be LoadSceneMode.Single
        /// </summary>
        internal LoadSceneMode DeferLoadingFilter = LoadSceneMode.Single;
        /// <summary>
        /// Determines if a remote client should defer object creation initiated by CreateObjectMessage
        /// until a scene event is completed.
        /// </summary>
        /// <remarks>
        /// Deferring object creation should only occur when there is a possibility the objects could be
        /// instantiated in a currently active scene that will be unloaded during single mode scene loading
        /// to prevent the newly created objects from being destroyed when the scene is unloaded.
        /// </remarks>
        internal bool ShouldDeferCreateObject()
        {
            // This applies only to remote clients and when scene management is enabled
            if (!NetworkManager.NetworkConfig.EnableSceneManagement || HasSceneAuthority())
            {
                return false;
            }
            var synchronizeEventDetected = false;
            var loadingEventDetected = false;
            foreach (var entry in SceneEventDataStore)
            {
                if (entry.Value.SceneEventType == SceneEventType.Synchronize)
                {
                    synchronizeEventDetected = true;
                }

                // When loading a scene and the load scene mode is single we should defer object creation
                if (entry.Value.SceneEventType == SceneEventType.Load && entry.Value.LoadSceneMode == DeferLoadingFilter)
                {
                    loadingEventDetected = true;
                }
            }

            // Synchronizing while in client synchronization mode single --> Defer
            // When not synchronizing but loading a scene in single mode --> Defer
            return (synchronizeEventDetected && ClientSynchronizationMode == LoadSceneMode.Single) || (!synchronizeEventDetected && loadingEventDetected);
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
        /// This setting changes how clients handle scene loading when initially synchronizing with the server.<br />
        /// The server or host should set this value as clients will automatically be synchronized with the server (or host) side.
        /// </summary>
        /// <remarks>
        /// <b>LoadSceneMode.Single:</b> All currently loaded scenes on the client will be unloaded and the
        /// server's currently active scene will be loaded in single mode on the client unless it was already
        /// loaded.<br />
        /// <b>LoadSceneMode.Additive:</b> All currently loaded scenes are left as they are and any newly loaded
        /// scenes will be loaded additively.  Users need to determine which scenes are valid to load via the
        /// <see cref="VerifySceneBeforeLoading"/> and, if <see cref="PostSynchronizationSceneUnloading"/> is
        /// set, <see cref="VerifySceneBeforeUnloading"/> callback(s).
        /// </remarks>
        /// <param name="mode"><see cref="LoadSceneMode"/> for initial client synchronization</param>
        public void SetClientSynchronizationMode(LoadSceneMode mode)
        {
            var networkManager = NetworkManager;
            SceneManagerHandler.SetClientSynchronizationMode(ref networkManager, mode);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="networkManager">one <see cref="Netcode.NetworkManager"/> instance per <see cref="NetworkSceneManager"/> instance</param>
        /// <param name="sceneEventDataPoolSize">maximum <see cref="SceneEventData"/> pool size</param>
        internal NetworkSceneManager(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
            SceneEventDataStore = new Dictionary<uint, SceneEventData>();

            // Since NetworkManager is now always migrated to the DDOL we will use this to get the DDOL scene
            DontDestroyOnLoadScene = networkManager.gameObject.scene;

            // Since the server tracks loaded scenes, we need to add any currently loaded scenes on the
            // server side when the NetworkManager is started and NetworkSceneManager instantiated when
            // scene management is enabled.

            if (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer && networkManager.NetworkConfig.EnableSceneManagement)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var loadedScene = SceneManager.GetSceneAt(i);
                    ScenesLoaded.Add(loadedScene.handle, new SceneData(null, loadedScene));
                }
                SceneManagerHandler.PopulateLoadedScenes(ref ScenesLoaded, NetworkManager);
            }

            // Add to the server to client scene handle table
            UpdateServerClientSceneHandle(DontDestroyOnLoadScene.handle, DontDestroyOnLoadScene.handle, DontDestroyOnLoadScene);
        }

        internal void InitializeScenesLoaded()
        {
            if (!NetworkManager.DistributedAuthorityMode)
            {
                return;
            }

            if (HasSceneAuthority() && NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var loadedScene = SceneManager.GetSceneAt(i);
                    UpdateServerClientSceneHandle(loadedScene.handle, loadedScene.handle, loadedScene);
                }
                SceneManagerHandler.PopulateLoadedScenes(ref ScenesLoaded, NetworkManager);
            }
        }

        /// <summary>
        /// Synchronizes clients when the currently active scene is changed
        /// </summary>
        private void SceneManager_ActiveSceneChanged(Scene current, Scene next)
        {
            if ((!NetworkManager.DistributedAuthorityMode && !NetworkManager.IsServer) || (NetworkManager.DistributedAuthorityMode && !NetworkManager.LocalClient.IsSessionOwner))
            {
                return;
            }

            // If no clients are connected, then don't worry about notifications
            if (!(NetworkManager.ConnectedClientsIds.Count > (NetworkManager.IsHost ? 1 : 0)))
            {
                return;
            }

            // Don't notify if a scene event is in progress
            foreach (var sceneEventEntry in SceneEventProgressTracking)
            {
                if (!sceneEventEntry.Value.HasTimedOut() && sceneEventEntry.Value.Status == SceneEventProgressStatus.Started)
                {
                    return;
                }
            }

            // Notify clients of the change in active scene
            var sceneEvent = BeginSceneEvent();
            sceneEvent.SceneEventType = SceneEventType.ActiveSceneChanged;
            sceneEvent.ActiveSceneAsset = next.name;
            SendSceneEventData(sceneEvent.SceneEventId, NetworkManager.ConnectedClientsIds.Where(c => c != NetworkManager.ServerClientId).ToArray());
            EndSceneEvent(sceneEvent.SceneEventId);
        }

        /// <summary>
        /// If the VerifySceneBeforeLoading delegate handler has been set by the user, this will provide
        /// an additional level of security and/or validation that the scene being loaded in the specified
        /// loading mode is "a valid scene to be loaded in the LoadSceneMode specified".
        /// </summary>
        /// <param name="sceneIndex">index into ScenesInBuild</param>
        /// <param name="loadSceneMode">LoadSceneMode the scene is going to be loaded</param>
        /// <returns>true (Valid) or false (Invalid)</returns>
        internal bool ValidateSceneBeforeLoading(string sceneName, LoadSceneMode loadSceneMode)
        {
            Log.Debug(() => $"ValidateSceneBeforeLoading {sceneName} {loadSceneMode}");
            return true;

            // var validated = true;
            // var sceneIndex = SceneUtility.GetBuildIndexByScenePath(sceneName);
            // if (VerifySceneBeforeLoading != null)
            // {
            //     validated = VerifySceneBeforeLoading.Invoke(sceneIndex, sceneName, loadSceneMode);
            // }
            // if (!validated && !m_DisableValidationWarningMessages)
            // {
            //     var serverHostorClient = "Client";
            //     if (NetworkManager.IsServer)
            //     {
            //         serverHostorClient = NetworkManager.IsHost ? "Host" : "Server";
            //     }
            //
            //     Debug.LogWarning($"Scene {sceneName} of Scenes in Build Index {sceneIndex} being loaded in {loadSceneMode} mode failed validation on the {serverHostorClient}!");
            // }
            // return validated;
        }

        /// <summary>
        /// Used for NetcodeIntegrationTest testing in order to properly
        /// assign the right loaded scene to the right client's ScenesLoaded list
        /// </summary>
        internal Func<string, Scene> OverrideGetAndAddNewlyLoadedSceneByName;

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
            if (OverrideGetAndAddNewlyLoadedSceneByName != null)
            {
                return OverrideGetAndAddNewlyLoadedSceneByName.Invoke(sceneName);
            }
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sceneLoaded = SceneManager.GetSceneAt(i);
                    if (sceneLoaded.name == sceneName)
                    {
                        if (!ScenesLoaded.ContainsKey(sceneLoaded.handle))
                        {
                            ScenesLoaded.Add(sceneLoaded.handle, new SceneData(null, sceneLoaded));
                            SceneManagerHandler.StartTrackingScene(sceneLoaded, true, NetworkManager);
                            return sceneLoaded;
                        }
                    }
                }
                throw new Exception($"Failed to find any loaded scene named {sceneName}!");
            }
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
                SceneBeingSynchronized = ScenesLoaded.ContainsKey(clientSceneHandle) ? ScenesLoaded[clientSceneHandle].SceneReference : new Scene();

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
                    SceneBeingSynchronized = NetworkManager.gameObject.scene;
                    return;
                }
                else
                {
                    // Let's go ahead and use the currently active scene under the scenario where a NetworkObject is determined to exist in a scene that the NetworkSceneManager is not aware of
                    // or the NetworkObject has yet to be moved to that specific scene (i.e. no DontDestroyOnLoad scene exists yet).
                    SceneBeingSynchronized = SceneManager.GetActiveScene();

                    // This could be the scenario where NetworkManager.DontDestroy is false and we are creating the first NetworkObject (client side) to be in the DontDestroyOnLoad scene
                    // Otherwise, this is some other specific scenario that we might not be handling currently.
                    Debug.LogWarning($"[{nameof(SceneEventData)}- Scene Handle Mismatch] {nameof(serverSceneHandle)} ({serverSceneHandle}) could not be found in {nameof(ServerSceneHandleToClientSceneHandle)}. Using the currently active scene.");
                }
            }
        }

        /// <summary>
        /// During soft synchronization of in-scene placed NetworkObjects, this is now used by NetworkSpawnManager.CreateLocalNetworkObject
        /// </summary>
        /// <param name="globalObjectIdHash"></param>
        /// <returns></returns>
        internal NetworkObject GetSceneRelativeInSceneNetworkObject(uint globalObjectIdHash, int? networkSceneHandle)
        {
            if (ScenePlacedObjects.ContainsKey(globalObjectIdHash))
            {
                var sceneHandle = SceneBeingSynchronized.handle;
                if (networkSceneHandle.HasValue && networkSceneHandle.Value != 0 && ServerSceneHandleToClientSceneHandle.ContainsKey(networkSceneHandle.Value))
                {
                    sceneHandle = ServerSceneHandleToClientSceneHandle[networkSceneHandle.Value];
                }
                if (ScenePlacedObjects[globalObjectIdHash].ContainsKey(sceneHandle))
                {
                    return ScenePlacedObjects[globalObjectIdHash][sceneHandle];
                }
            }
            return null;
        }

        /// <summary>
        /// Generic sending of scene event data
        /// </summary>
        /// <param name="targetClientIds">array of client identifiers to receive the scene event message</param>
        private void SendSceneEventData(uint sceneEventId, ulong[] targetClientIds)
        {
            if (targetClientIds.Length == 0 && !NetworkManager.DistributedAuthorityMode)
            {
                // This would be the Host/Server with no clients connected
                // Silently return as there is nothing to be done
                return;
            }
            var sceneEvent = SceneEventDataStore[sceneEventId];
            sceneEvent.SenderClientId = NetworkManager.LocalClientId;

            if (NetworkManager.DistributedAuthorityMode && !NetworkManager.DAHost)
            {
                if (NetworkManager.DistributedAuthorityMode && HasSceneAuthority())
                {
                    sceneEvent.TargetClientId = NetworkManager.ServerClientId;
                    var message = new SceneEventMessage
                    {
                        EventData = sceneEvent,
                    };
                    var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, NetworkManager.ServerClientId);
                    NetworkManager.NetworkMetrics.TrackSceneEventSent(NetworkManager.ServerClientId, (uint)sceneEvent.SceneEventType, sceneEvent.SceneAsset, size);
                }
                foreach (var clientId in targetClientIds)
                {
                    sceneEvent.TargetClientId = clientId;
                    var message = new SceneEventMessage
                    {
                        EventData = sceneEvent,
                    };
                    var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, NetworkManager.ServerClientId);
                    NetworkManager.NetworkMetrics.TrackSceneEventSent(clientId, (uint)sceneEvent.SceneEventType, sceneEvent.SceneAsset, size);
                }
            }
            else
            {
                var message = new SceneEventMessage
                {
                    EventData = sceneEvent,
                };
                var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, targetClientIds);
                NetworkManager.NetworkMetrics.TrackSceneEventSent(targetClientIds, (uint)SceneEventDataStore[sceneEventId].SceneEventType, sceneEvent.SceneAsset, size);
            }

        }

        /// <summary>
        /// Entry method for scene unloading validation
        /// </summary>
        /// <param name="scene">the scene to be unloaded</param>
        /// <returns></returns>
        private SceneEventProgress ValidateSceneEventUnloading(Scene scene)
        {
            if (!NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                Debug.LogWarning($"{nameof(LoadScene)} was called, but {nameof(NetworkConfig.EnableSceneManagement)} was not enabled! Enable {nameof(NetworkConfig.EnableSceneManagement)} prior to starting a client, host, or server prior to using {nameof(NetworkSceneManager)}!");
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneManagementNotEnabled);
            }

            if (!HasSceneAuthority())
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    Debug.LogWarning($"[{nameof(SceneEventProgressStatus.SessionOwnerOnlyAction)}][Unload] Clients cannot invoke the {nameof(UnloadScene)} method!");
                    return new SceneEventProgress(null, SceneEventProgressStatus.SessionOwnerOnlyAction);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(SceneEventProgressStatus.ServerOnlyAction)}][Unload] Clients cannot invoke the {nameof(UnloadScene)} method!");
                    return new SceneEventProgress(null, SceneEventProgressStatus.ServerOnlyAction);
                }
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
            if (!NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                Debug.LogWarning($"{nameof(LoadScene)} was called, but {nameof(NetworkConfig.EnableSceneManagement)} was not enabled! Enable {nameof(NetworkConfig.EnableSceneManagement)} prior to starting a client, host, or server prior to using {nameof(NetworkSceneManager)}!");
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneManagementNotEnabled);
            }

            if (!HasSceneAuthority())
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    Debug.LogWarning($"[{nameof(SceneEventProgressStatus.SessionOwnerOnlyAction)}][Load] Only the session owner can invoke the {nameof(LoadScene)} method!");
                    return new SceneEventProgress(null, SceneEventProgressStatus.SessionOwnerOnlyAction);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(SceneEventProgressStatus.ServerOnlyAction)}][Load] Clients cannot invoke the {nameof(LoadScene)} method!");
                    return new SceneEventProgress(null, SceneEventProgressStatus.ServerOnlyAction);
                }
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
            if (m_IsSceneEventActive)
            {
                return new SceneEventProgress(null, SceneEventProgressStatus.SceneEventInProgress);
            }

            // // Return invalid scene name status if the scene name is invalid
            // if (SceneUtility.GetBuildIndexByScenePath(sceneName) == InvalidSceneNameOrPath)
            // {
            //     Debug.LogError($"Scene '{sceneName}' couldn't be loaded because it has not been added to the build settings scenes in build list.");
            //     return new SceneEventProgress(null, SceneEventProgressStatus.InvalidSceneName);
            // }
            var locatorInfo = Addressables.GetLocatorInfo(sceneName);

            var resourceLocationAsync = Addressables.LoadResourceLocationsAsync(sceneName);
            resourceLocationAsync.WaitForCompletion();
            if (resourceLocationAsync.Status == AsyncOperationStatus.Succeeded)
            {
                if (resourceLocationAsync.Result.Count >= 1)
                {
                    var location = resourceLocationAsync.Result[0];
                    var provider = location.ProviderId;
                    if (!provider.Contains("Scene"))
                    {
                        Debug.LogWarning($"Provider is not a scene provider! {provider}");
                    }

                    var resourceType = location.ResourceType;
                    if (resourceType != typeof(SceneInstance))
                    {
                        throw new Exception($"Scene is not of the SceneInstance type! {resourceType}");
                    }
                    if (location.HasDependencies)
                    {
                        // Has dependencies!
                        // download something?
                        // no! this is just a verification method
                    }

                    if (location.PrimaryKey != sceneName)
                    {
                        throw new Exception($"Scene is not primary key of the scene name! {location.PrimaryKey} {sceneName}");
                    }
                }
            }
            else
            {
                throw new Exception($"Scene '{sceneName}' couldn't be loaded for its resource location.");
            }

            var sceneEventProgress = new SceneEventProgress(NetworkManager)
            {
                SceneName = sceneName
            };

            SceneEventProgressTracking.Add(sceneEventProgress.Guid, sceneEventProgress);

            m_IsSceneEventActive = true;

            // Set our callback delegate handler for completion
            sceneEventProgress.OnComplete = OnSceneEventProgressCompleted;

            return sceneEventProgress;
        }

        /// <summary>
        /// Callback for the <see cref="SceneEventProgress.OnComplete"/> <see cref="SceneEventProgress.OnCompletedDelegate"/> handler
        /// </summary>
        /// <param name="sceneEventProgress"></param>
        private bool OnSceneEventProgressCompleted(SceneEventProgress sceneEventProgress)
        {
            var sceneEventData = BeginSceneEvent();
            var clientsThatCompleted = sceneEventProgress.GetClientsWithStatus(true);
            var clientsThatTimedOut = sceneEventProgress.GetClientsWithStatus(false);
            sceneEventData.SceneEventProgressId = sceneEventProgress.Guid;
            sceneEventData.SceneAsset = sceneEventProgress.SceneName;
            sceneEventData.SceneEventType = sceneEventProgress.SceneEventType;
            sceneEventData.ClientsCompleted = clientsThatCompleted;
            sceneEventData.LoadSceneMode = sceneEventProgress.LoadSceneMode;
            sceneEventData.ClientsTimedOut = clientsThatTimedOut;

            if (NetworkManager.DistributedAuthorityMode)
            {
                SendSceneEventData(sceneEventData.SceneEventId, NetworkManager.ConnectedClientsIds.Where(c => c != NetworkManager.LocalClientId).ToArray());
            }
            else
            {
                var message = new SceneEventMessage
                {
                    EventData = sceneEventData
                };

                var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, NetworkManager.ConnectedClientsIds);
                NetworkManager.NetworkMetrics.TrackSceneEventSent(
                    NetworkManager.ConnectedClientsIds,
                    (uint)sceneEventProgress.SceneEventType,
                    sceneEventProgress.SceneName,
                    size);
            }

            // Send a local notification to the session owner that all clients are done loading or unloading
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = sceneEventProgress.SceneEventType,
                SceneName = sceneEventProgress.SceneName,
                ClientId = NetworkManager.CurrentSessionOwner,
                LoadSceneMode = sceneEventProgress.LoadSceneMode,
                ClientsThatCompleted = clientsThatCompleted,
                ClientsThatTimedOut = clientsThatTimedOut,
            });

            if (sceneEventData.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                OnLoadEventCompleted?.Invoke(sceneEventProgress.SceneName, sceneEventProgress.LoadSceneMode, sceneEventData.ClientsCompleted, sceneEventData.ClientsTimedOut);
            }
            else
            {
                OnUnloadEventCompleted?.Invoke(sceneEventProgress.SceneName, sceneEventProgress.LoadSceneMode, sceneEventData.ClientsCompleted, sceneEventData.ClientsTimedOut);
            }

            EndSceneEvent(sceneEventData.SceneEventId);
            return true;
        }

        /// <summary>
        /// <b>Server Side:</b>
        /// Unloads an additively loaded scene.  If you want to unload a <see cref="LoadSceneMode.Single"/> mode loaded scene load another <see cref="LoadSceneMode.Single"/> scene.
        /// When applicable, the <see cref="AsyncOperation"/> is delivered within the <see cref="SceneEvent"/> via the <see cref="OnSceneEvent"/>
        /// </summary>
        /// <param name="scene"></param>
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

            var sceneEventProgress = ValidateSceneEventUnloading(scene);
            if (sceneEventProgress.Status != SceneEventProgressStatus.Started)
            {
                return sceneEventProgress.Status;
            }

            if (!ScenesLoaded.ContainsKey(sceneHandle))
            {
                Debug.LogError($"{nameof(UnloadScene)} internal error! {sceneName} with handle {scene.handle} is not within the internal scenes loaded dictionary!");
                return SceneEventProgressStatus.InternalNetcodeError;
            }

            if (NetworkManager.DistributedAuthorityMode)
            {
                if (ClientSceneHandleToServerSceneHandle.ContainsKey(sceneHandle))
                {
                    sceneHandle = ClientSceneHandleToServerSceneHandle[sceneHandle];
                }
            }

            // Any NetworkObjects marked to not be destroyed with a scene and reside within the scene about to be unloaded
            // should be migrated temporarily into the DDOL, once the scene is unloaded they will be migrated into the
            // currently active scene.
            var networkManager = NetworkManager;
            SceneManagerHandler.MoveObjectsFromSceneToDontDestroyOnLoad(ref networkManager, scene);

            var sceneEventData = BeginSceneEvent();
            sceneEventData.SceneEventProgressId = sceneEventProgress.Guid;
            sceneEventData.SceneEventType = SceneEventType.Unload;
            sceneEventData.SceneAsset = sceneName;
            sceneEventData.LoadSceneMode = LoadSceneMode.Additive; // The only scenes unloaded are scenes that were additively loaded
            sceneEventData.SceneHandle = sceneHandle;

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventType.UnloadEventCompleted;

            sceneEventProgress.SceneEventId = sceneEventData.SceneEventId;
            sceneEventProgress.OnSceneEventCompleted = OnSceneUnloaded;

            if (!RemoveServerClientSceneHandle(sceneEventData.SceneHandle, scene.handle))
            {
                Debug.LogError($"Failed to remove {sceneEventData.SceneAsset} scene handles [Server ({sceneEventData.SceneHandle})][Local({scene.handle})]");
            }

            AsyncOperationHandle<SceneInstance> sceneUnload;
            if (ScenesLoaded[sceneHandle].SceneInstance.HasValue)
            {
                sceneUnload = SceneManagerHandler.UnloadSceneAsync(ScenesLoaded[sceneHandle], sceneEventProgress);
            }
            else
            {
                throw new Exception("Fuuuuck");
                sceneUnload = default;
            }
            // Notify local server that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = NetworkManager.LocalClientId  // Session owner can only invoke this
            });

            OnUnload?.Invoke(NetworkManager.LocalClientId, sceneName, sceneUnload);

            //Return the status
            return sceneEventProgress.Status;
        }

        /// <summary>
        /// <b>Client Side:</b>
        /// Handles <see cref="SceneEventType.Unload"/> scene events.
        /// </summary>
        private void OnClientUnloadScene(uint sceneEventId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            var sceneName = sceneEventData.SceneAsset;

            if (!ServerSceneHandleToClientSceneHandle.ContainsKey(sceneEventData.SceneHandle))
            {
                Debug.Log($"Client failed to unload scene {sceneName} " +
                    $"because we are missing the client scene handle due to the server scene handle {sceneEventData.SceneHandle} not being found.");
                EndSceneEvent(sceneEventId);
                return;
            }

            var sceneHandle = ServerSceneHandleToClientSceneHandle[sceneEventData.SceneHandle];

            if (!ScenesLoaded.ContainsKey(sceneHandle))
            {
                // Error scene handle not found!
                throw new Exception($"Client failed to unload scene {sceneName} " +
                    $"because the client scene handle {sceneHandle} was not found in ScenesLoaded!");
            }

            var scene = ScenesLoaded[sceneHandle];
            // Any NetworkObjects marked to not be destroyed with a scene and reside within the scene about to be unloaded
            // should be migrated temporarily into the DDOL, once the scene is unloaded they will be migrated into the
            // currently active scene.
            var networkManager = NetworkManager;
            SceneManagerHandler.MoveObjectsFromSceneToDontDestroyOnLoad(ref networkManager, scene.SceneReference);
            m_IsSceneEventActive = true;
            var sceneEventProgress = new SceneEventProgress(NetworkManager)
            {
                SceneEventId = sceneEventData.SceneEventId,
                OnSceneEventCompleted = OnSceneUnloaded,
            };

            if (NetworkManager.DistributedAuthorityMode)
            {
                SceneEventProgressTracking.Add(sceneEventData.SceneEventProgressId, sceneEventProgress);
            }

            AsyncOperationHandle<SceneInstance> sceneUnload;
            if (ScenesLoaded[sceneHandle].SceneInstance.HasValue)
            {
                sceneUnload = SceneManagerHandler.UnloadSceneAsync(ScenesLoaded[sceneHandle], sceneEventProgress);
            }
            else
            {
                throw new Exception("Fuuuuck");
                sceneUnload = default;
            }
            SceneManagerHandler.StopTrackingScene(sceneHandle, sceneName, NetworkManager);

            // Remove our server to scene handle lookup
            if (!RemoveServerClientSceneHandle(sceneEventData.SceneHandle, sceneHandle))
            {
                // If the exact same handle exists then there are problems with using handles
                throw new Exception($"Failed to remove server scene handle ({sceneEventData.SceneHandle}) or client scene handle({sceneHandle})! Happened during scene unload for {sceneName}.");
            }

            // Notify the local client that a scene is going to be unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneUnload,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = LoadSceneMode.Additive,     // The only scenes unloaded are scenes that were additively loaded
                SceneName = sceneName,
                ClientId = NetworkManager.LocalClientId   // Server sent this message to the client, but client is executing it
            });

            OnUnload?.Invoke(NetworkManager.LocalClientId, sceneName, sceneUnload);
        }

        /// <summary>
        /// Server and Client:
        /// Invoked when an additively loaded scene is unloaded
        /// </summary>
        private void OnSceneUnloaded(uint sceneEventId, string sceneName)
        {
            // If we are shutdown or about to shutdown, then ignore this event
            if (!NetworkManager.IsListening || NetworkManager.ShutdownInProgress)
            {
                EndSceneEvent(sceneEventId);
                return;
            }

            // Migrate the NetworkObjects marked to not be destroyed with the scene into the currently active scene
            MoveObjectsFromDontDestroyOnLoadToScene(SceneManager.GetActiveScene());
            var sceneEventData = SceneEventDataStore[sceneEventId];

            if (HasSceneAuthority())
            {
                var sessionOwner = NetworkManager.DistributedAuthorityMode ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;

                // Server sends the unload scene notification after unloading because it will despawn all scene relative in-scene NetworkObjects
                // If we send this event to all clients before the server is finished unloading they will get warning about an object being
                // despawned that no longer exists
                SendSceneEventData(sceneEventId, NetworkManager.ConnectedClientsIds.Where(c => c != sessionOwner).ToArray());

                //Only if we are session owner do we want register having loaded for the associated SceneEventProgress
                if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventProgressId) && HasSceneAuthority())
                {
                    SceneEventProgressTracking[sceneEventData.SceneEventProgressId].ClientFinishedSceneEvent(sessionOwner);
                }
            }
            else if (NetworkManager.DistributedAuthorityMode)
            {
                SceneEventProgressTracking.Remove(sceneEventData.SceneEventProgressId);
                m_IsSceneEventActive = false;
            }

            // Next we prepare to send local notifications for unload complete
            sceneEventData.SceneEventType = SceneEventType.UnloadComplete;

            //Notify the client or server that a scene was unloaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneEventData.SceneAsset,
                ClientId = NetworkManager.LocalClientId,
            });

            OnUnloadComplete?.Invoke(NetworkManager.LocalClientId, sceneEventData.SceneAsset);

            if (!HasSceneAuthority())
            {
                sceneEventData.TargetClientId = NetworkManager.CurrentSessionOwner;
                sceneEventData.SenderClientId = NetworkManager.LocalClientId;
                var message = new SceneEventMessage
                {
                    EventData = sceneEventData,
                };
                // This might seem like it needs more logic to determine the target, but the only scenario where we send to the session owner is if the
                // current instance is the DAHost.
                var target = NetworkManager.DAHost ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;
                var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, target);
                NetworkManager.NetworkMetrics.TrackSceneEventSent(target, (uint)sceneEventData.SceneEventType, sceneEventData.SceneAsset, size);
            }
            EndSceneEvent(sceneEventId);
            // This scene event is now considered "complete"
            m_IsSceneEventActive = false;
        }

        private void EmptySceneUnloadedOperation(uint sceneEventId, string sceneName)
        {
            // Do nothing (this is a stub call since it is only used to flush all additively loaded scenes)
        }

        /// <summary>
        /// Clears all scenes when loading in single mode
        /// Since we assume a single mode loaded scene will be considered the "currently active scene",
        /// we only unload any additively loaded scenes.
        /// </summary>
        internal void UnloadAdditivelyLoadedScenes(uint sceneEventId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            // Unload all additive scenes while making sure we don't try to unload the base scene ( loaded in single mode ).
            var currentActiveScene = SceneManager.GetActiveScene();
            foreach (var keyHandleEntry in ScenesLoaded)
            {
                // Validate the scene as well as ignore the DDOL (which will have a negative buildIndex)
                if (currentActiveScene.name != keyHandleEntry.Value.SceneReference.name && keyHandleEntry.Value.SceneReference != DontDestroyOnLoadScene)
                {
                    var sceneEventProgress = new SceneEventProgress(NetworkManager)
                    {
                        SceneEventId = sceneEventId,
                        OnSceneEventCompleted = EmptySceneUnloadedOperation
                    };

                    if (ClientSceneHandleToServerSceneHandle.TryGetValue(keyHandleEntry.Value.SceneReference.handle, out var serverSceneHandle))
                    {
                        ServerSceneHandleToClientSceneHandle.Remove(serverSceneHandle);
                    }
                    Log.Info(() => $"Remove ClientSceneHandleToServerSceneHandle {keyHandleEntry.Value.SceneReference.handle}");
                    ClientSceneHandleToServerSceneHandle.Remove(keyHandleEntry.Value.SceneReference.handle);

                    var sceneUnload = SceneManagerHandler.UnloadSceneAsync(keyHandleEntry.Value, sceneEventProgress);

                    SceneUnloadEventHandler.RegisterScene(this, keyHandleEntry.Value.SceneReference, LoadSceneMode.Additive, sceneUnload);

                }
            }
            // clear out our scenes loaded list
            ScenesLoaded.Clear();
            SceneManagerHandler.ClearSceneTracking(NetworkManager);
        }

        public SceneEventProgress LoadAddressableScene(AssetReference sceneReference, LoadSceneMode loadSceneMode)
        {
            var resourceAsync = Addressables.LoadResourceLocationsAsync(sceneReference);
            resourceAsync.WaitForCompletion();

            var sceneName = "";

            if (resourceAsync.Status == AsyncOperationStatus.Succeeded)
            {
                var sceneKey = resourceAsync.Result[0].PrimaryKey;
                sceneName = sceneKey;
            }
            else
            {
                throw new Exception($"Failed to load scene from resource {resourceAsync.OperationException}");
            }

            var sceneEventProgress = ValidateSceneEventLoading(sceneName);
            if (sceneEventProgress.Status != SceneEventProgressStatus.Started)
            {
                return sceneEventProgress;
            }

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventType.LoadEventCompleted;
            sceneEventProgress.LoadSceneMode = loadSceneMode;

            var sceneEventData = BeginSceneEvent();

            // Now set up the current scene event
            sceneEventData.SceneEventProgressId = sceneEventProgress.Guid;
            sceneEventData.SceneEventType = SceneEventType.Load;
            sceneEventData.SceneAsset = sceneName;
            sceneEventData.LoadSceneMode = loadSceneMode;
            var sceneEventId = sceneEventData.SceneEventId;
            // This both checks to make sure the scene is valid and if not resets the active scene event
            m_IsSceneEventActive = ValidateSceneBeforeLoading(sceneEventData.SceneAsset, loadSceneMode);
            if (!m_IsSceneEventActive)
            {
                EndSceneEvent(sceneEventId);
                sceneEventProgress.Status = SceneEventProgressStatus.SceneFailedVerification;
                return sceneEventProgress;
            }

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Destroy current scene objects before switching.
                NetworkManager.SpawnManager.ServerDestroySpawnedSceneObjects();

                // Preserve the objects that should not be destroyed during the scene event
                MoveObjectsToDontDestroyOnLoad();

                // Now Unload all currently additively loaded scenes
                UnloadAdditivelyLoadedScenes(sceneEventId);

                // Register the active scene for unload scene event notifications
                SceneUnloadEventHandler.RegisterScene(this, SceneManager.GetActiveScene(), LoadSceneMode.Single);
            }

            // Now start loading the scene
            sceneEventProgress.SceneEventId = sceneEventId;
            sceneEventProgress.OnSceneEventCompleted = OnSceneLoaded;
            var sceneLoad = SceneManagerHandler.LoadSceneAsync(sceneName, loadSceneMode, sceneEventProgress);

            // Notify the local server that a scene loading event has begun
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = NetworkManager.ServerClientId
            });

            OnLoad?.Invoke(NetworkManager.ServerClientId, sceneName, sceneEventData.LoadSceneMode, sceneLoad);

            //Return our scene progress instance
            return sceneEventProgress;
        }


        private static Dictionary<string, string> s_ResourceLocationsBySceneName = new();

        public bool PrepareToLoadScene(string sceneName, Action<bool> loaded)
        {
            return false;
        }

        /// <summary>
        /// <b>Server side:</b>
        /// Loads the scene name in either additive or single loading mode.
        /// When applicable, the <see cref="AsyncOperation"/> is delivered within the <see cref="SceneEvent"/> via <see cref="OnSceneEvent"/>
        /// </summary>
        /// <param name="sceneName">the name of the scene to be loaded</param>
        /// <param name="loadSceneMode">how the scene will be loaded (single or additive mode)</param>
        /// <returns><see cref="SceneEventProgressStatus"/> (<see cref="SceneEventProgressStatus.Started"/> means it was successful)</returns>
        public SceneEventProgress LoadScene(string sceneName, LoadSceneMode loadSceneMode)
        {
            // Debug.Log($"[NetworkSceneManager] LoadScene sceneName={sceneName}");
            if (!s_ResourceLocationsBySceneName.TryGetValue(sceneName, out var found))
            {
                var resourceLocationAsync = Addressables.LoadResourceLocationsAsync(sceneName);
                if (!resourceLocationAsync.IsValid())
                {
                    return null;
                }
            }

            // Debug.Log($"[NetworkSceneManager] LoadScene Finished LoadResources sceneName={sceneName}");
            var sceneEventProgress = ValidateSceneEventLoading(sceneName);
            if (sceneEventProgress.Status != SceneEventProgressStatus.Started)
            {
                return sceneEventProgress;
            }

            // This will be the message we send to everyone when this scene event sceneEventProgress is complete
            sceneEventProgress.SceneEventType = SceneEventType.LoadEventCompleted;
            sceneEventProgress.LoadSceneMode = loadSceneMode;

            var sceneEventData = BeginSceneEvent();

            // Now set up the current scene event
            sceneEventData.SceneEventProgressId = sceneEventProgress.Guid;
            sceneEventData.SceneEventType = SceneEventType.Load;
            sceneEventData.SceneAsset = sceneName;
            sceneEventData.LoadSceneMode = loadSceneMode;
            var sceneEventId = sceneEventData.SceneEventId;
            // This both checks to make sure the scene is valid and if not resets the active scene event
            m_IsSceneEventActive = ValidateSceneBeforeLoading(sceneEventData.SceneAsset, loadSceneMode);
            if (!m_IsSceneEventActive)
            {
                EndSceneEvent(sceneEventId);
                return new SceneEventProgress(NetworkManager.Singleton, SceneEventProgressStatus.SceneFailedVerification);
            }

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
                // they need to be moved into the do not destroy temporary scene
                // When it is set: Just before starting the asynchronous loading call
                // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
                // not destroy temporary scene are moved into the active scene
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;

                // Destroy current scene objects before switching.
                NetworkManager.SpawnManager.ServerDestroySpawnedSceneObjects();

                // Preserve the objects that should not be destroyed during the scene event
                MoveObjectsToDontDestroyOnLoad();

                // Now Unload all currently additively loaded scenes
                UnloadAdditivelyLoadedScenes(sceneEventId);

                // Register the active scene for unload scene event notifications
                SceneUnloadEventHandler.RegisterScene(this, SceneManager.GetActiveScene(), LoadSceneMode.Single);
            }

            // Now start loading the scene
            sceneEventProgress.SceneEventId = sceneEventId;
            sceneEventProgress.OnSceneEventCompleted = OnSceneLoaded;
            // Debug.Log($"[NetworkSceneManager] BEGIN SceneManagerHandler.LoadSceneAsync sceneName={sceneName} loadSceneMode={loadSceneMode}");
            var sceneLoad = SceneManagerHandler.LoadSceneAsync(sceneName, loadSceneMode, sceneEventProgress);
            // Debug.Log($"[NetworkSceneManager] END SceneManagerHandler.LoadSceneAsync sceneName={sceneName} loadSceneMode={loadSceneMode}");
            // Notify the local server that a scene loading event has begun
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = NetworkManager.LocalClientId
            });
            OnLoad?.Invoke(NetworkManager.LocalClientId, sceneName, sceneEventData.LoadSceneMode, sceneLoad);

            //Return our scene progress instance
            return sceneEventProgress;
        }

        /// <summary>
        /// Helper class used to handle "odd ball" scene unload event notification scenarios
        /// when scene switching.
        /// </summary>
        internal class SceneUnloadEventHandler
        {
            private static Dictionary<NetworkManager, List<SceneUnloadEventHandler>> s_Instances = new Dictionary<NetworkManager, List<SceneUnloadEventHandler>>();

            internal static void RegisterScene(NetworkSceneManager networkSceneManager, Scene scene, LoadSceneMode loadSceneMode, AsyncOperationHandle asyncOperation = default)
            {
                var networkManager = networkSceneManager.NetworkManager;
                if (!s_Instances.ContainsKey(networkManager))
                {
                    s_Instances.Add(networkManager, new List<SceneUnloadEventHandler>());
                }
                var clientId = networkManager.LocalClientId;
                s_Instances[networkManager].Add(new SceneUnloadEventHandler(networkSceneManager, scene, clientId, loadSceneMode, asyncOperation));
            }

            private static void SceneUnloadComplete(SceneUnloadEventHandler sceneUnloadEventHandler)
            {
                if (sceneUnloadEventHandler == null || sceneUnloadEventHandler.m_NetworkSceneManager == null || sceneUnloadEventHandler.m_NetworkSceneManager.NetworkManager == null)
                {
                    return;
                }
                var networkManager = sceneUnloadEventHandler.m_NetworkSceneManager.NetworkManager;
                if (s_Instances.ContainsKey(networkManager))
                {
                    s_Instances[networkManager].Remove(sceneUnloadEventHandler);
                    if (s_Instances[networkManager].Count == 0)
                    {
                        s_Instances.Remove(networkManager);
                    }
                }
            }

            /// <summary>
            /// Called by NetworkSceneManager when it is disposing
            /// </summary>
            internal static void Shutdown()
            {
                foreach (var instanceEntry in s_Instances)
                {
                    foreach (var instance in instanceEntry.Value)
                    {
                        instance.OnShutdown();
                    }
                    instanceEntry.Value.Clear();
                }
                s_Instances.Clear();
            }

            private NetworkSceneManager m_NetworkSceneManager;
            private AsyncOperationHandle m_AsyncOperation;
            private LoadSceneMode m_LoadSceneMode;
            private ulong m_ClientId;
            private Scene m_Scene;
            private bool m_ShuttingDown;

            private void OnShutdown()
            {
                m_ShuttingDown = true;
                SceneManager.sceneUnloaded -= SceneUnloaded;
            }

            private void SceneUnloaded(Scene scene)
            {
                if (m_Scene.handle == scene.handle && !m_ShuttingDown)
                {
                    if (m_NetworkSceneManager != null && m_NetworkSceneManager.NetworkManager != null)
                    {
                        m_NetworkSceneManager.OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            AsyncOperation = m_AsyncOperation,
                            SceneEventType = SceneEventType.UnloadComplete,
                            SceneName = m_Scene.name,
                            LoadSceneMode = m_LoadSceneMode,
                            ClientId = m_ClientId
                        });
                        m_NetworkSceneManager.OnUnloadComplete?.Invoke(m_ClientId, m_Scene.name);
                    }
                    SceneManager.sceneUnloaded -= SceneUnloaded;
                    SceneUnloadComplete(this);
                }
            }

            private SceneUnloadEventHandler(NetworkSceneManager networkSceneManager, Scene scene, ulong clientId, LoadSceneMode loadSceneMode, AsyncOperationHandle asyncOperation = default)
            {
                m_LoadSceneMode = loadSceneMode;
                m_AsyncOperation = asyncOperation;
                m_NetworkSceneManager = networkSceneManager;
                m_ClientId = clientId;
                m_Scene = scene;
                SceneManager.sceneUnloaded += SceneUnloaded;
                // Send the initial unload event notification
                m_NetworkSceneManager.OnSceneEvent?.Invoke(new SceneEvent()
                {
                    AsyncOperation = m_AsyncOperation,
                    SceneEventType = SceneEventType.Unload,
                    SceneName = m_Scene.name,
                    LoadSceneMode = m_LoadSceneMode,
                    ClientId = clientId
                });

                m_NetworkSceneManager.OnUnload?.Invoke(networkSceneManager.NetworkManager.LocalClientId, m_Scene.name, default);
            }
        }

        /// <summary>
        /// Client Side:
        /// Handles both forms of scene loading
        /// </summary>
        /// <param name="objectStream">Stream data associated with the event</param>
        private void OnClientSceneLoadingEvent(uint sceneEventId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            var sceneName = sceneEventData.SceneAsset;

            // Run scene validation before loading a scene
            if (!ValidateSceneBeforeLoading(sceneEventData.SceneAsset, sceneEventData.LoadSceneMode))
            {
                EndSceneEvent(sceneEventId);
                return;
            }

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Move ALL NetworkObjects to the temp scene
                MoveObjectsToDontDestroyOnLoad();

                // Now Unload all currently additively loaded scenes
                UnloadAdditivelyLoadedScenes(sceneEventData.SceneEventId);
            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned
            // they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do
            // not destroy temporary scene are moved into the active scene
            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;

                // Register the active scene for unload scene event notifications
                SceneUnloadEventHandler.RegisterScene(this, SceneManager.GetActiveScene(), LoadSceneMode.Single);

            }
            var sceneEventProgress = new SceneEventProgress(NetworkManager)
            {
                SceneEventId = sceneEventId,
                OnSceneEventCompleted = OnSceneLoaded,
                Status = SceneEventProgressStatus.Started,
            };

            if (NetworkManager.DistributedAuthorityMode)
            {
                SceneEventProgressTracking.Add(sceneEventData.SceneEventProgressId, sceneEventProgress);
                m_IsSceneEventActive = true;
            }
            var sceneLoad = SceneManagerHandler.LoadSceneAsync(sceneName, sceneEventData.LoadSceneMode, sceneEventProgress);

            OnSceneEvent?.Invoke(new SceneEvent()
            {
                AsyncOperation = sceneLoad,
                SceneEventType = sceneEventData.SceneEventType,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneName,
                ClientId = NetworkManager.LocalClientId
            });

            OnLoad?.Invoke(NetworkManager.LocalClientId, sceneName, sceneEventData.LoadSceneMode, sceneLoad);
        }

        /// <summary>
        /// Client and Server:
        /// Generic on scene loaded callback method to be called upon a scene loading
        /// </summary>
        private void OnSceneLoaded(uint sceneEventId, string loadedSceneName)
        {
            // If we are shutdown or about to shutdown, then ignore this event
            if (!NetworkManager.IsListening || NetworkManager.ShutdownInProgress)
            {
                EndSceneEvent(sceneEventId);
                return;
            }

            var sceneEventData = SceneEventDataStore[sceneEventId];
            var nextScene = GetAndAddNewlyLoadedSceneByName(loadedSceneName);
            if (!nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }
            // If we async loaded a single scene, the active will activate it

            if (sceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            if (NetworkManager.DistributedAuthorityMode)
            {
                var networkSceneHandle = nextScene.handle;
                if (!HasSceneAuthority())
                {
                    networkSceneHandle = sceneEventData.SceneHandle;
                }

                // Update the server scene handle to client scene handle look up table
                if (!UpdateServerClientSceneHandle(networkSceneHandle, nextScene.handle, nextScene))
                {
                    // If the exact same handle exists then there are problems with using handles
                    Debug.LogWarning($"Server Scene Handle ({networkSceneHandle}) already exist!  Happened during scene load of {nextScene.name} with the local handle ({nextScene.handle})");
                }
            }
            else if (NetworkManager.IsServer)
            {
                // Update the server scene handle to client scene handle look up table
                if (!UpdateServerClientSceneHandle(nextScene.handle, nextScene.handle, nextScene))
                {
                    // If the exact same handle exists then there are problems with using handles
                    Debug.LogWarning($"Server Scene Handle ({nextScene.handle}) already exist!  Happened during scene load of {nextScene.name} with the local handle ({nextScene.handle})");
                }
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

            if (HasSceneAuthority())
            {
                OnSessionOwnerLoadedScene(sceneEventId, nextScene);
            }
            else
            {
                if (!NetworkManager.DistributedAuthorityMode)
                {
                    // For the client, we make a server scene handle to client scene handle look up table
                    if (!UpdateServerClientSceneHandle(sceneEventData.SceneHandle, nextScene.handle, nextScene))
                    {
                        // If the exact same handle exists then there are problems with using handles
                        throw new Exception($"Server Scene Handle ({sceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
                    }
                }
                OnClientLoadedScene(sceneEventId, nextScene);
            }
        }

        /// <summary>
        /// Server/SessionOwner side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnSessionOwnerLoadedScene(uint sceneEventId, Scene scene)
        {
            // Debug.Log($"NetworkSceneManager - OnServerLoadedScene eventId:{sceneEventId} scene:{scene.name}");

            var sceneEventData = SceneEventDataStore[sceneEventId];
            // Register in-scene placed NetworkObjects with spawn manager
            foreach (var keyValuePairByGlobalObjectIdHash in ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    if (!keyValuePairBySceneHandle.Value.IsPlayerObject)
                    {
                        // All in-scene placed NetworkObjects default to being owned by the server
                        NetworkManager.SpawnManager.SpawnNetworkObjectLocally(keyValuePairBySceneHandle.Value,
                            NetworkManager.SpawnManager.GetNetworkObjectId(), true, false, NetworkManager.LocalClientId, true);
                    }
                }
            }

            foreach (var keyValuePairByGlobalObjectIdHash in ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    if (!keyValuePairBySceneHandle.Value.IsPlayerObject)
                    {
                        keyValuePairBySceneHandle.Value.InternalInSceneNetworkObjectsSpawned();
                    }
                }
            }

            // Add any despawned when spawned in-scene placed NetworkObjects to the scene event data
            sceneEventData.AddDespawnedInSceneNetworkObjects();

            // Set the server's scene's handle so the client can build a look up table
            sceneEventData.SceneHandle = scene.handle;

            var sessionOwner = NetworkManager.ServerClientId;
            // Send all clients the scene load event
            if (NetworkManager.DistributedAuthorityMode)
            {
                sessionOwner = NetworkManager.CurrentSessionOwner;
            }

            SendSceneEventData(sceneEventData.SceneEventId, NetworkManager.ConnectedClientsIds.Where(c => c != sessionOwner).ToArray());

            m_IsSceneEventActive = false;
            //First, notify local server that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventType.LoadComplete,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneEventData.SceneAsset,
                ClientId = NetworkManager.ServerClientId,
                Scene = scene,
            });

            OnLoadComplete?.Invoke(NetworkManager.ServerClientId, sceneEventData.SceneAsset, sceneEventData.LoadSceneMode);

            //Second, only if we are a host do we want register having loaded for the associated SceneEventProgress
            if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventProgressId) && NetworkManager.IsClient)
            {
                SceneEventProgressTracking[sceneEventData.SceneEventProgressId].ClientFinishedSceneEvent(NetworkManager.LocalClientId);
            }
            EndSceneEvent(sceneEventId);
        }

        /// <summary>
        /// Client side:
        /// On scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnClientLoadedScene(uint sceneEventId, Scene scene)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            sceneEventData.DeserializeScenePlacedObjects();

            sceneEventData.SceneEventType = SceneEventType.LoadComplete;

            if (NetworkManager.DistributedAuthorityMode)
            {
                sceneEventData.TargetClientId = NetworkManager.CurrentSessionOwner;
                sceneEventData.SenderClientId = NetworkManager.LocalClientId;
                var message = new SceneEventMessage
                {
                    EventData = sceneEventData,
                };
                var target = NetworkManager.DAHost ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;
                var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, target);
                NetworkManager.NetworkMetrics.TrackSceneEventSent(target, (uint)sceneEventData.SceneEventType, sceneEventData.SceneAsset, size);
            }
            else
            {
                SendSceneEventData(sceneEventId, new ulong[] { NetworkManager.ServerClientId });
            }

            m_IsSceneEventActive = false;

            // Process any pending create object messages that the client received while loading a scene
            ProcessDeferredCreateObjectMessages();

            if (NetworkManager.DistributedAuthorityMode)
            {
                SceneEventProgressTracking.Remove(sceneEventData.SceneEventProgressId);
                m_IsSceneEventActive = false;
            }

            // Notify local client that the scene was loaded
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventType.LoadComplete,
                LoadSceneMode = sceneEventData.LoadSceneMode,
                SceneName = sceneEventData.SceneAsset,
                ClientId = NetworkManager.LocalClientId,
                Scene = scene,
            });

            OnLoadComplete?.Invoke(NetworkManager.LocalClientId, sceneEventData.SceneAsset, sceneEventData.LoadSceneMode);

            EndSceneEvent(sceneEventId);
        }

        /// <summary>
        /// Used for integration testing, due to the complexities of having all clients loading scenes
        /// this is needed to "filter" out the scenes not loaded by NetworkSceneManager
        /// (i.e. we don't want a late joining player to load all of the other client scenes)
        /// </summary>
        internal Func<Scene, bool> ExcludeSceneFromSychronization;

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
            NetworkManager.SpawnManager.UpdateObservedNetworkObjects(clientId);

            var sceneEventData = BeginSceneEvent();
            sceneEventData.ClientSynchronizationMode = ClientSynchronizationMode;
            sceneEventData.InitializeForSynch();
            sceneEventData.TargetClientId = clientId;
            sceneEventData.LoadSceneMode = ClientSynchronizationMode;
            var activeScene = SceneManager.GetActiveScene();
            sceneEventData.SceneEventType = SceneEventType.Synchronize;
            // if (BuildIndexToHash.ContainsKey(activeScene.buildIndex))
            // {
            //     sceneEventData.ActiveSceneHash = BuildIndexToHash[activeScene.buildIndex];
            // }

            // Organize how (and when) we serialize our NetworkObjects
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                // NetworkSceneManager does not synchronize scenes that are not loaded by NetworkSceneManager
                // unless the scene in question is the currently active scene.
                if (ExcludeSceneFromSychronization != null && !ExcludeSceneFromSychronization(scene))
                {
                    continue;
                }

                var sceneHash = scene.name;
                if (scene == DontDestroyOnLoadScene)
                {
                    continue;
                }

                // This would depend upon whether we are additive or not
                // If we are the base scene, then we set the root scene index;
                if (activeScene == scene)
                {
                    if (!ValidateSceneBeforeLoading(sceneHash, sceneEventData.LoadSceneMode))
                    {
                        continue;
                    }
                    sceneEventData.SceneAsset = scene.name;

                    // If we are just a normal client, then always use the server scene handle
                    if (NetworkManager.DistributedAuthorityMode)
                    {
                        sceneEventData.SenderClientId = NetworkManager.LocalClientId;
                        sceneEventData.SceneHandle = ClientSceneHandleToServerSceneHandle[scene.handle];
                    }
                    else
                    {
                        sceneEventData.SceneHandle = scene.handle;
                    }
                }
                else if (!ValidateSceneBeforeLoading(sceneHash, LoadSceneMode.Additive))
                {
                    continue;
                }

                // If we are just a normal client and in distributed authority mode, then always use the known server scene handle
                if (NetworkManager.DistributedAuthorityMode && !NetworkManager.DAHost)
                {
                    sceneEventData.AddSceneToSynchronize(sceneHash, ClientSceneHandleToServerSceneHandle[scene.handle]);
                }
                else
                {
                    sceneEventData.AddSceneToSynchronize(sceneHash, scene.handle);
                }
            }

            sceneEventData.AddSpawnedNetworkObjects();
            sceneEventData.AddDespawnedInSceneNetworkObjects();
            var message = new SceneEventMessage
            {
                EventData = sceneEventData
            };

            var size = 0;
            if (NetworkManager.DistributedAuthorityMode && !NetworkManager.DAHost)
            {
                size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, NetworkManager.ServerClientId);
            }
            else
            {
                size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, clientId);
            }
            NetworkManager.NetworkMetrics.TrackSceneEventSent(clientId, (uint)sceneEventData.SceneEventType, "", size);


            // Notify the local server that the client has been sent the synchronize event
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = sceneEventData.SceneEventType,
                ClientId = clientId
            });

            OnSynchronize?.Invoke(clientId);

            EndSceneEvent(sceneEventData.SceneEventId);
        }

        /// <summary>
        /// This is called when the client receives the <see cref="SceneEventType.Synchronize"/> event
        /// Note: This can recurse one additional time by the client if the current scene loaded by the client
        /// is already loaded.
        /// </summary>
        private void OnClientBeginSync(uint sceneEventId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            var sceneHash = sceneEventData.GetNextSceneSynchronizationHash();
            var sceneHandle = sceneEventData.GetNextSceneSynchronizationHandle();
            var sceneName = sceneHash;
            var activeScene = SceneManager.GetActiveScene();

            var loadSceneMode = sceneHash == sceneEventData.SceneAsset ? sceneEventData.LoadSceneMode : LoadSceneMode.Additive;

            // Store the sceneHandle and hash
            sceneEventData.NetworkSceneHandle = sceneHandle;
            sceneEventData.SceneAsset = sceneHash;

            // If this is the beginning of the synchronization event, then send client a notification that synchronization has begun
            if (sceneHash == sceneEventData.SceneAsset)
            {
                OnSceneEvent?.Invoke(new SceneEvent()
                {
                    SceneEventType = SceneEventType.Synchronize,
                    ClientId = NetworkManager.LocalClientId,
                });

                OnSynchronize?.Invoke(NetworkManager.LocalClientId);
            }

            // Always check to see if the scene needs to be validated
            if (!ValidateSceneBeforeLoading(sceneHash, loadSceneMode))
            {
                HandleClientSceneEvent(sceneEventId);
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"Client declined to load the scene {sceneName}, continuing with synchronization.");
                }
                return;
            }

            var sceneLoad = (AsyncOperationHandle<SceneInstance>)default;

            // Determines if the client has the scene to be loaded already loaded, if so will return true and the client will skip loading this scene
            // For ClientSynchronizationMode LoadSceneMode.Single, we pass in whether the scene being loaded is the first/primary active scene and if it is already loaded
            // it should pass through to post load processing (ClientLoadedSynchronization).
            // For ClientSynchronizationMode LoadSceneMode.Additive, if the scene is already loaded or the active scene is the scene to be loaded (does not require it to
            // be the initial primary scene) then go ahead and pass through to post load processing (ClientLoadedSynchronization).
            var shouldPassThrough = SceneManagerHandler.ClientShouldPassThrough(sceneName, sceneName == sceneEventData.SceneAsset, ClientSynchronizationMode, NetworkManager);

            if (!shouldPassThrough)
            {
                // If not, then load the scene
                var sceneEventProgress = new SceneEventProgress(NetworkManager)
                {
                    SceneEventId = sceneEventId,
                    OnSceneEventCompleted = ClientLoadedSynchronization
                };
                // Debug.Log($"[NetworkSceneManager] OnClientBeginSync BEGIN SceneManagerHandler.LoadSceneAsync sceneName={sceneName} loadSceneMode={loadSceneMode}");
                sceneLoad = SceneManagerHandler.LoadSceneAsync(sceneName, loadSceneMode, sceneEventProgress);
                // Debug.Log($"[NetworkSceneManager] OnClientBeginSync END SceneManagerHandler.LoadSceneAsync sceneName={sceneName} loadSceneMode={loadSceneMode}");

                // Notify local client that a scene load has begun
                OnSceneEvent?.Invoke(new SceneEvent()
                {
                    AsyncOperation = sceneLoad,
                    SceneEventType = SceneEventType.Load,
                    LoadSceneMode = loadSceneMode,
                    SceneName = sceneName,
                    ClientId = NetworkManager.LocalClientId,
                });

                OnLoad?.Invoke(NetworkManager.LocalClientId, sceneName, loadSceneMode, sceneLoad);
            }
            else
            {
                // If so, then pass through
                ClientLoadedSynchronization(sceneEventId, sceneName);
            }
        }

        /// <summary>
        /// Once a scene is loaded ( or if it was already loaded) this gets called.
        /// This handles all of the in-scene and dynamically spawned NetworkObject synchronization
        /// </summary>
        /// <param name="sceneIndex">Netcode scene index that was loaded</param>
        private void ClientLoadedSynchronization(uint sceneEventId, string sceneName)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            var nextScene = SceneManagerHandler.GetSceneFromLoadedScenes(sceneName, NetworkManager);
            if (!nextScene.IsValid())
            {
                nextScene = GetAndAddNewlyLoadedSceneByName(sceneName);
            }

            if (!nextScene.isLoaded || !nextScene.IsValid())
            {
                throw new Exception($"Failed to find valid scene internal Unity.Netcode for {nameof(GameObject)}s error!");
            }

            var loadSceneMode = (sceneEventData.ClientSceneName == sceneEventData.SceneAsset ? sceneEventData.LoadSceneMode : LoadSceneMode.Additive);

            // For now, during a synchronization event, we will make the first scene the "base/master" scene that denotes a "complete scene switch"
            if (loadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            // For the client, we make a server scene handle to client scene handle look up table
            if (!UpdateServerClientSceneHandle(sceneEventData.NetworkSceneHandle, nextScene.handle, nextScene))
            {
                // If the exact same handle exists then there are problems with using handles
                throw new Exception($"Server Scene Handle ({sceneEventData.SceneHandle}) already exist!  Happened during scene load of {nextScene.name} with Client Handle ({nextScene.handle})");
            }

            // Apply all in-scene placed NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene, false);

            // Send notification back to server that we finished loading this scene
            var responseSceneEventData = BeginSceneEvent();
            responseSceneEventData.LoadSceneMode = loadSceneMode;
            responseSceneEventData.SceneEventType = SceneEventType.LoadComplete;
            responseSceneEventData.SceneAsset = sceneEventData.ClientSceneName;

            var target = NetworkManager.ServerClientId;
            if (NetworkManager.DistributedAuthorityMode)
            {
                responseSceneEventData.SenderClientId = NetworkManager.LocalClientId;
                responseSceneEventData.TargetClientId = NetworkManager.CurrentSessionOwner;
                target = NetworkManager.DAHost ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;
            }
            var message = new SceneEventMessage
            {
                EventData = responseSceneEventData
            };
            var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, target);

            NetworkManager.NetworkMetrics.TrackSceneEventSent(NetworkManager.ServerClientId, (uint)responseSceneEventData.SceneEventType, sceneName, size);

            EndSceneEvent(responseSceneEventData.SceneEventId);

            // Send notification to local client that the scene has finished loading
            OnSceneEvent?.Invoke(new SceneEvent()
            {
                SceneEventType = SceneEventType.LoadComplete,
                LoadSceneMode = loadSceneMode,
                SceneName = sceneName,
                Scene = nextScene,
                ClientId = NetworkManager.LocalClientId,
            });

            OnLoadComplete?.Invoke(NetworkManager.LocalClientId, sceneName, loadSceneMode);

            // Check to see if we still have scenes to load and synchronize with
            HandleClientSceneEvent(sceneEventId);
        }

        /// <summary>
        /// Makes sure that client-side instantiated dynamically spawned NetworkObjects are migrated
        /// into the same scene (if not already) as they are on the server-side during the initial
        /// client connection synchronization process.
        /// </summary>
        private void SynchronizeNetworkObjectScene()
        {
            foreach (var networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                // This is only done for dynamically spawned NetworkObjects
                // Theoretically, a server could have NetworkObjects in a server-side only scene, if the client doesn't have that scene loaded
                // then skip it (it will reside in the currently active scene in this scenario on the client-side)
                if (networkObject.IsSceneObject.Value == false && ServerSceneHandleToClientSceneHandle.ContainsKey(networkObject.NetworkSceneHandle))
                {
                    networkObject.SceneOriginHandle = ServerSceneHandleToClientSceneHandle[networkObject.NetworkSceneHandle];



                    // If the NetworkObject does not have a parent and is not in the same scene as it is on the server side, then find the right scene
                    // and move it to that scene.
                    if (networkObject.gameObject.scene.handle != networkObject.SceneOriginHandle && networkObject.transform.parent == null)
                    {
                        if (ScenesLoaded.ContainsKey(networkObject.SceneOriginHandle))
                        {
                            var scene = ScenesLoaded[networkObject.SceneOriginHandle];
                            if (scene.SceneReference == DontDestroyOnLoadScene)
                            {
                                Debug.Log($"{networkObject.gameObject.name} migrating into DDOL!");
                            }

                            SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene.SceneReference);
                        }
                        else if (NetworkManager.LogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarningServer($"[Client-{NetworkManager.LocalClientId}][{networkObject.gameObject.name}] Server - " +
                                $"client scene mismatch detected! Client-side has no scene loaded with handle ({networkObject.SceneOriginHandle})!");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Client Side:
        /// Handles incoming Scene_Event messages for clients
        /// </summary>
        /// <param name="stream">data associated with the event</param>
        private void HandleClientSceneEvent(uint sceneEventId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            switch (sceneEventData.SceneEventType)
            {
                case SceneEventType.ActiveSceneChanged:
                    {
                        var scene = SceneManager.GetSceneByName(sceneEventData.ClientSceneName);
                        if (scene.isLoaded)
                        {
                            SceneManager.SetActiveScene(scene);
                        }
                        EndSceneEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.ObjectSceneChanged:
                    {
                        EndSceneEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.Load:
                    {
                        OnClientSceneLoadingEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.Unload:
                    {
                        OnClientUnloadScene(sceneEventId);
                        break;
                    }
                case SceneEventType.Synchronize:
                    {
                        if (!sceneEventData.IsDoneWithSynchronization())
                        {
                            OnClientBeginSync(sceneEventId);
                        }
                        else
                        {
                            // Include anything in the DDOL scene
                            PopulateScenePlacedObjects(DontDestroyOnLoadScene, false);

                            // If needed, set the currently active scene
                            var targetActiveScene = SceneManager.GetSceneByName(sceneEventData.ClientSceneName);
                            if (targetActiveScene.isLoaded && targetActiveScene.handle != SceneManager.GetActiveScene().handle)
                            {
                                SceneManager.SetActiveScene(targetActiveScene);
                            }

                            // Spawn and Synchronize all NetworkObjects
                            sceneEventData.SynchronizeSceneNetworkObjects(NetworkManager);

                            // If needed, migrate dynamically spawned NetworkObjects to the same scene as they are on the server
                            SynchronizeNetworkObjectScene();

                            // Process any pending create object messages that the client received during synchronization
                            ProcessDeferredCreateObjectMessages();

                            sceneEventData.SceneEventType = SceneEventType.SynchronizeComplete;
                            if (NetworkManager.DistributedAuthorityMode)
                            {
                                if (NetworkManager.CMBServiceConnection)
                                {
                                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                                    {
                                        if (clientId == NetworkManager.LocalClientId)
                                        {
                                            continue;
                                        }
                                        sceneEventData.TargetClientId = clientId;
                                        sceneEventData.SenderClientId = NetworkManager.LocalClientId;
                                        var message = new SceneEventMessage
                                        {
                                            EventData = sceneEventData,
                                        };
                                        var target = NetworkManager.DAHost ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;
                                        var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, target);
                                        NetworkManager.NetworkMetrics.TrackSceneEventSent(target, (uint)sceneEventData.SceneEventType, sceneEventData.SceneAsset, size);
                                    }
                                }
                                else
                                {
                                    sceneEventData.TargetClientId = NetworkManager.CurrentSessionOwner;
                                    sceneEventData.SenderClientId = NetworkManager.LocalClientId;
                                    var message = new SceneEventMessage
                                    {
                                        EventData = sceneEventData,
                                    };
                                    var target = NetworkManager.DAHost ? NetworkManager.CurrentSessionOwner : NetworkManager.ServerClientId;
                                    var size = NetworkManager.ConnectionManager.SendMessage(ref message, k_DeliveryType, target);
                                    NetworkManager.NetworkMetrics.TrackSceneEventSent(target, (uint)sceneEventData.SceneEventType, sceneEventData.SceneAsset, size);
                                }
                            }
                            else
                            {
                                SendSceneEventData(sceneEventId, new ulong[] { NetworkManager.ServerClientId });
                            }

                            // All scenes are synchronized, let the server know we are done synchronizing
                            NetworkManager.IsConnectedClient = true;

                            // With distributed authority, either the client-side automatically spawns the default assigned player prefab or
                            // if AutoSpawnPlayerPrefabClientSide is disabled the client-side will determine what player prefab to spawn and
                            // when it gets spawned.
                            if (NetworkManager.DistributedAuthorityMode && NetworkManager.AutoSpawnPlayerPrefabClientSide)
                            {
                                NetworkManager.ConnectionManager.CreateAndSpawnPlayer(NetworkManager.LocalClientId);
                            }

                            // Process any SceneEventType.ObjectSceneChanged messages that
                            // were deferred while synchronizing and migrate the associated
                            // NetworkObjects to their newly assigned scenes.
                            sceneEventData.ProcessDeferredObjectSceneChangedEvents();

                            // Only if PostSynchronizationSceneUnloading is set and we are running in client synchronization
                            // mode additive do we unload any remaining scene that was not synchronized (otherwise any loaded
                            // scene not synchronized by the server will remain loaded)
                            if (PostSynchronizationSceneUnloading && ClientSynchronizationMode == LoadSceneMode.Additive)
                            {
                                SceneManagerHandler.UnloadUnassignedScenes(NetworkManager);
                            }

                            // Client is now synchronized and fully "connected".  This also means the client can send "RPCs" at this time
                            NetworkManager.ConnectionManager.InvokeOnClientConnectedCallback(NetworkManager.LocalClientId);

                            // Notify the client that they have finished synchronizing
                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                ClientId = NetworkManager.LocalClientId, // Client sent this to the server
                            });

                            OnSynchronizeComplete?.Invoke(NetworkManager.LocalClientId);

                            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                            {
                                NetworkLog.LogInfo($"[Client-{NetworkManager.LocalClientId}][Scene Management Enabled] Synchronization complete!");
                            }
                            // For convenience, notify all NetworkBehaviours that synchronization is complete.
                            NetworkManager.SpawnManager.NotifyNetworkObjectsSynchronized();

                            if (NetworkManager.DistributedAuthorityMode && HasSceneAuthority() && IsRestoringSession)
                            {
                                IsRestoringSession = false;
                                PostSynchronizationSceneUnloading = m_OriginalPostSynchronizationSceneUnloading;
                            }

                            EndSceneEvent(sceneEventId);
                        }
                        break;
                    }
                case SceneEventType.ReSynchronize:
                    {
                        // Notify the local client that they have been re-synchronized after being synchronized with an in progress game session
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            ClientId = NetworkManager.ServerClientId,  // Server sent this to client
                        });

                        EndSceneEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.UnloadEventCompleted:
                    {
                        // Notify the local client that all clients have finished loading or unloading
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            SceneName = sceneEventData.SceneAsset,
                            ClientId = NetworkManager.ServerClientId,
                            ClientsThatCompleted = sceneEventData.ClientsCompleted,
                            ClientsThatTimedOut = sceneEventData.ClientsTimedOut,
                        });

                        if (sceneEventData.SceneEventType == SceneEventType.LoadEventCompleted)
                        {
                            OnLoadEventCompleted?.Invoke(sceneEventData.SceneAsset, sceneEventData.LoadSceneMode, sceneEventData.ClientsCompleted, sceneEventData.ClientsTimedOut);
                        }
                        else
                        {
                            OnUnloadEventCompleted?.Invoke(sceneEventData.SceneAsset, sceneEventData.LoadSceneMode, sceneEventData.ClientsCompleted, sceneEventData.ClientsTimedOut);
                        }

                        EndSceneEvent(sceneEventId);

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
        /// Session Owner Side:
        /// Handles incoming Scene_Event messages for the current session owner
        /// </summary>
        private void HandleSessionOwnerEvent(uint sceneEventId, ulong clientId)
        {
            var sceneEventData = SceneEventDataStore[sceneEventId];
            switch (sceneEventData.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        // Notify the local server that the client has finished loading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            SceneName = sceneEventData.SceneAsset,
                            ClientId = clientId
                        });

                        OnLoadComplete?.Invoke(clientId, sceneEventData.SceneAsset, sceneEventData.LoadSceneMode);

                        if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventProgressId))
                        {
                            SceneEventProgressTracking[sceneEventData.SceneEventProgressId].ClientFinishedSceneEvent(clientId);
                        }
                        EndSceneEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.UnloadComplete:
                    {
                        if (SceneEventProgressTracking.ContainsKey(sceneEventData.SceneEventProgressId))
                        {
                            SceneEventProgressTracking[sceneEventData.SceneEventProgressId].ClientFinishedSceneEvent(clientId);
                        }
                        // Notify the local server that the client has finished unloading a scene
                        OnSceneEvent?.Invoke(new SceneEvent()
                        {
                            SceneEventType = sceneEventData.SceneEventType,
                            LoadSceneMode = sceneEventData.LoadSceneMode,
                            SceneName = sceneEventData.SceneAsset,
                            ClientId = clientId
                        });

                        OnUnloadComplete?.Invoke(clientId, sceneEventData.SceneAsset);

                        EndSceneEvent(sceneEventId);
                        break;
                    }
                case SceneEventType.SynchronizeComplete:
                    {
                        // At this point the client is considered fully "connected"
                        if ((NetworkManager.DistributedAuthorityMode && NetworkManager.LocalClient.IsSessionOwner) || !NetworkManager.DistributedAuthorityMode)
                        {
                            // Notify the local server that a client has finished synchronizing
                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                SceneName = string.Empty,
                                ClientId = clientId
                            });
                            if (NetworkManager.ConnectedClients.ContainsKey(clientId))
                            {
                                NetworkManager.ConnectedClients[clientId].IsConnected = true;
                            }
                        }
                        else
                        {
                            // Notify the local server that a client has finished synchronizing
                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                SceneName = string.Empty,
                                ClientId = clientId
                            });

                            // Show any NetworkObjects that are:
                            // - Hidden from the session owner
                            // - Owned by this client
                            // - Has NetworkObject.SpawnWithObservers set to true (the default)
                            NetworkManager.SpawnManager.ShowHiddenObjectsToNewlyJoinedClient(clientId);

                            // DANGO-EXP TODO: Remove this once service distributes objects
                            // Non-session owners receive this notification from newly connected clients and upon receiving
                            // the event they will redistribute their NetworkObjects
                            NetworkManager.SpawnManager.DistributeNetworkObjects(clientId);
                            EndSceneEvent(sceneEventId);
                            return;
                        }

                        // All scenes are synchronized, let the server know we are done synchronizing
                        OnSynchronizeComplete?.Invoke(clientId);

                        // At this time the client is fully synchronized with all loaded scenes and
                        // NetworkObjects and should be considered "fully connected". Send the
                        // notification that the client is connected.
                        NetworkManager.ConnectionManager.InvokeOnClientConnectedCallback(clientId);

                        if (NetworkManager.IsHost)
                        {
                            NetworkManager.ConnectionManager.InvokeOnPeerConnectedCallback(clientId);
                        }

                        // Check to see if the client needs to resynchronize and before sending the message make sure the client is still connected to avoid
                        // a potential crash within the MessageSystem (i.e. sending to a client that no longer exists)
                        if (sceneEventData.ClientNeedsReSynchronization() && !DisableReSynchronization && NetworkManager.ConnectedClients.ContainsKey(clientId))
                        {
                            sceneEventData.SceneEventType = SceneEventType.ReSynchronize;
                            SendSceneEventData(sceneEventId, new ulong[] { clientId });

                            OnSceneEvent?.Invoke(new SceneEvent()
                            {
                                SceneEventType = sceneEventData.SceneEventType,
                                SceneName = string.Empty,
                                ClientId = clientId
                            });
                        }
                        // DANGO-EXP TODO: Remove this once service distributes objects
                        NetworkManager.SpawnManager.DistributeNetworkObjects(clientId);
                        EndSceneEvent(sceneEventId);
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
        ///  Skips scene handling to be able to test CMB DA_NGO Codec tests
        /// </summary>
        internal bool SkipSceneHandling;

        private bool m_OriginalPostSynchronizationSceneUnloading;

        /// <summary>
        /// Both Client and Server: Incoming scene event entry point
        /// </summary>
        /// <param name="clientId">client who sent the scene event</param>
        /// <param name="reader">data associated with the scene event</param>
        internal void HandleSceneEvent(ulong clientId, FastBufferReader reader)
        {
            if (NetworkManager != null)
            {
                var sceneEventData = BeginSceneEvent();
                sceneEventData.Deserialize(reader);
                if (SkipSceneHandling)
                {
                    return;
                }

                // DA HOST Will keep track of session owner and if it is not the scene owner it will forward the message
                // to the current session owner
                if (NetworkManager.DistributedAuthorityMode && NetworkManager.DAHost)
                {
                    // If the event is server directed
                    if (!sceneEventData.IsSceneEventClientSide())
                    {
                        // If the DAHost is not the session owner, then forward the message to the current session owner
                        if (NetworkManager.CurrentSessionOwner != NetworkManager.LocalClientId)
                        {
                            var message = new SceneEventMessage()
                            {
                                EventData = sceneEventData,
                            };
                            // Forward synchronization to client then exit early because DAHost is not the current session owner
                            foreach (var client in NetworkManager.ConnectedClientsIds)
                            {
                                if (client == NetworkManager.LocalClientId)
                                {
                                    continue;
                                }
                                NetworkManager.MessageManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, client);
                            }
                        }
                    }
                    else
                    {
                        // DAHost will forward any messages not targeting the DAHost to the targeted client
                        if (sceneEventData.TargetClientId != NetworkManager.LocalClientId)
                        {
                            if (NetworkManager.LogLevel == LogLevel.Developer)
                            {
                                NetworkLog.LogInfoServer($"[Forward To: Client-{sceneEventData.TargetClientId}][{Enum.GetName(typeof(SceneEventType), sceneEventData.SceneEventType)}]");
                            }
                            sceneEventData.ForwardSynchronization = sceneEventData.SceneEventType == SceneEventType.Synchronize;
                            sceneEventData.IsForwarding = true;
                            var message = new SceneEventMessage()
                            {
                                EventData = sceneEventData,
                            };
                            NetworkManager.MessageManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, sceneEventData.TargetClientId);
                            EndSceneEvent(sceneEventData.SceneEventId);
                            return;
                        }
                    }
                }

                NetworkManager.NetworkMetrics.TrackSceneEventReceived(
                   clientId, (uint)sceneEventData.SceneEventType, sceneEventData.SceneAsset, reader.Length);

                if (sceneEventData.IsSceneEventClientSide())
                {
                    // If the client is being synchronized for the first time do some initialization
                    if (sceneEventData.SceneEventType == SceneEventType.Synchronize)
                    {
                        ScenePlacedObjects.Clear();
                        // Set the server's configured client synchronization mode on the client side
                        ClientSynchronizationMode = sceneEventData.ClientSynchronizationMode;

                        // Only if ClientSynchronizationMode is Additive and the client receives a synchronize scene event
                        if (ClientSynchronizationMode == LoadSceneMode.Additive)
                        {
                            if (NetworkManager.DistributedAuthorityMode && HasSceneAuthority() && IsRestoringSession && clientId == NetworkManager.ServerClientId)
                            {
                                m_OriginalPostSynchronizationSceneUnloading = PostSynchronizationSceneUnloading;
                                PostSynchronizationSceneUnloading = true;
                            }

                            // Check for scenes already loaded and create a table of scenes already loaded (SceneEntries) that will be
                            // used if the server is synchronizing the same scenes (i.e. if a matching scene is already loaded on the
                            // client side, then that scene will be used as opposed to loading another scene). This allows for clients
                            // to reconnect to a network session without having to unload all of the scenes and reload all of the scenes.
                            SceneManagerHandler.PopulateLoadedScenes(ref ScenesLoaded, NetworkManager);
                        }
                    }
                    HandleClientSceneEvent(sceneEventData.SceneEventId);
                }
                else
                {
                    var sendingClient = clientId;
                    if (NetworkManager.DistributedAuthorityMode)
                    {
                        sendingClient = sceneEventData.SenderClientId;
                    }
                    HandleSessionOwnerEvent(sceneEventData.SceneEventId, sendingClient);
                }
            }
            else
            {
                Debug.LogError($"{nameof(HandleSceneEvent)} was invoked but {nameof(Netcode.NetworkManager)} reference was null!");
            }
        }

        /// <summary>
        /// Moves all NetworkObjects that don't have the <see cref="NetworkObject.DestroyWithScene"/> set to
        /// the "Do not destroy on load" scene.
        /// </summary>
        internal void MoveObjectsToDontDestroyOnLoad()
        {
            // Create a local copy of the spawned objects list since the spawn manager will adjust the list as objects
            // are despawned.
            var localSpawnedObjectsHashSet = new HashSet<NetworkObject>(NetworkManager.SpawnManager.SpawnedObjectsList);
            foreach (var networkObject in localSpawnedObjectsHashSet)
            {
                if (networkObject == null || (networkObject != null && networkObject.gameObject.scene == DontDestroyOnLoadScene))
                {
                    continue;
                }

                // Only NetworkObjects marked to not be destroyed with the scene
                if (!networkObject.DestroyWithScene)
                {
                    // Only move dynamically spawned NetworkObjects with no parent as the children will follow
                    if (networkObject.gameObject.transform.parent == null && networkObject.IsSceneObject != null && !networkObject.IsSceneObject.Value)
                    {
                        try
                        {
                            Object.DontDestroyOnLoad(networkObject.gameObject);
                            // When temporarily migrating to the DDOL, adjust the network and origin scene handles so no messages are generated
                            // about objects being moved to a new scene.
                            networkObject.NetworkSceneHandle = ClientSceneHandleToServerSceneHandle[networkObject.gameObject.scene.handle];
                            networkObject.SceneOriginHandle = networkObject.gameObject.scene.handle;
                        }
                        catch (Exception e)
                        {
                            string allEntities = "";
                            foreach (var kvp in ClientSceneHandleToServerSceneHandle)
                            {
                                allEntities += $"{kvp.Key}={kvp.Value},";
                            }
                            Log.Error(() => $"Failed to move object={networkObject} to DDOL [{allEntities}] e={e}");
                        }
                    }
                }
                else if (networkObject.HasAuthority)
                {
                    networkObject.Despawn();
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
        internal void PopulateScenePlacedObjects(Scene sceneToFilterBy, bool clearScenePlacedObjects = true)
        {
            if (clearScenePlacedObjects)
            {
                ScenePlacedObjects.Clear();
            }

#if UNITY_2023_1_OR_NEWER
            var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID);
#else
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif

            // Just add every NetworkObject found that isn't already in the list
            // With additive scenes, we can have multiple in-scene placed NetworkObjects with the same GlobalObjectIdHash value
            // During Client Side Synchronization: We add them on a FIFO basis, for each scene loaded without clearing, and then
            // at the end of scene loading we use this list to soft synchronize all in-scene placed NetworkObjects
            foreach (var networkObjectInstance in networkObjects)
            {
                var globalObjectIdHash = networkObjectInstance.GlobalObjectIdHash;
                var sceneHandle = networkObjectInstance.gameObject.scene.handle;
                // We check to make sure the NetworkManager instance is the same one to be "NetcodeIntegrationTestHelpers" compatible and filter the list on a per scene basis (for additive scenes)
                if (networkObjectInstance.IsSceneObject != false && (networkObjectInstance.NetworkManager == NetworkManager ||
                    networkObjectInstance.NetworkManagerOwner == null) && sceneHandle == sceneToFilterBy.handle)
                {
                    if (!ScenePlacedObjects.ContainsKey(globalObjectIdHash))
                    {
                        ScenePlacedObjects.Add(globalObjectIdHash, new Dictionary<int, NetworkObject>());
                    }

                    if (!ScenePlacedObjects[globalObjectIdHash].ContainsKey(sceneHandle))
                    {
                        ScenePlacedObjects[globalObjectIdHash].Add(sceneHandle, networkObjectInstance);
                    }
                    else
                    {
                        var exitingEntryName = ScenePlacedObjects[globalObjectIdHash][sceneHandle] != null ? ScenePlacedObjects[globalObjectIdHash][sceneHandle].name : "Null Entry";
                        throw new Exception($"{networkObjectInstance.name} tried to registered with {nameof(ScenePlacedObjects)} which already contains " +
                            $"the same {nameof(NetworkObject.GlobalObjectIdHash)} value {globalObjectIdHash} for {exitingEntryName}!");
                    }
                }
            }
        }

        /// <summary>
        /// Moves all spawned NetworkObjects (from do not destroy on load) to the scene specified
        /// </summary>
        /// <param name="scene">scene to move the NetworkObjects to</param>
        internal void MoveObjectsFromDontDestroyOnLoadToScene(Scene scene)
        {
            foreach (var networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject == null)
                {
                    continue;
                }
                // If it is in the DDOL then
                if (networkObject.gameObject.scene == DontDestroyOnLoadScene && !networkObject.DestroyWithScene)
                {
                    // only move dynamically spawned network objects, with no parent as child objects will follow,
                    // back into the currently active scene
                    if (networkObject.gameObject.transform.parent == null && networkObject.IsSceneObject != null && !networkObject.IsSceneObject.Value)
                    {
                        if (NetworkManager.DistributedAuthorityMode)
                        {
                            // When migrating out of the DDOL to the currently active scene, adjust the network and origin scene handles so no messages are generated
                            // about objects being moved to a new scene.
                            if (SceneManagerHandler.IsIntegrationTest() && SceneManager.GetActiveScene() == scene)
                            {
                                networkObject.NetworkSceneHandle = scene.handle;
                            }
                            else
                            {
                                networkObject.NetworkSceneHandle = ClientSceneHandleToServerSceneHandle[scene.handle];
                            }
                            networkObject.SceneOriginHandle = scene.handle;
                        }

                        SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene);
                    }
                }
            }
        }

        /// <summary>
        /// Holds a list of scene handles (server-side relative) and NetworkObjects migrated into it
        /// during the current frame.
        /// </summary>
        internal Dictionary<int, Dictionary<ulong, List<NetworkObject>>> ObjectsMigratedIntoNewScene = new Dictionary<int, Dictionary<ulong, List<NetworkObject>>>();

        internal bool IsSceneEventInProgress()
        {
            if (!NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                return false;
            }

            foreach (var sceneEventEntry in SceneEventProgressTracking)
            {
                if (!sceneEventEntry.Value.HasTimedOut() && sceneEventEntry.Value.SceneEventType != SceneEventType.Synchronize && sceneEventEntry.Value.Status == SceneEventProgressStatus.Started)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles notifying clients when a NetworkObject has been migrated into a new scene
        /// </summary>
        internal void NotifyNetworkObjectSceneChanged(NetworkObject networkObject)
        {
            // Really, this should never happen but in case it does
            if (!networkObject.HasAuthority)
            {
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogErrorServer("[Please Report This Error][NotifyNetworkObjectSceneChanged] A client is trying to notify of an object's scene change!");
                }
                return;
            }

            // Ignore in-scene placed NetworkObjects
            if (networkObject.IsSceneObject != false)
            {
                // Really, this should ever happen but in case it does
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogErrorServer("[Please Report This Error][NotifyNetworkObjectSceneChanged] Trying to notify in-scene placed object scene change!");
                }
                return;
            }

            // Ignore if the scene is the currently active scene and the NetworkObject is auto synchronizing/migrating
            // to the currently active scene.
            if (networkObject.gameObject.scene == SceneManager.GetActiveScene() && networkObject.ActiveSceneSynchronization)
            {
                return;
            }

            // Don't notify if a scene event is in progress
            // Note: This does not apply to SceneEventType.Synchronize since synchronization isn't a global connected client event.
            if (IsSceneEventInProgress())
            {
                return;
            }

            // Otherwise, add the NetworkObject into the list of NetworkObjects who's scene has changed
            if (!ObjectsMigratedIntoNewScene.ContainsKey(networkObject.NetworkSceneHandle))
            {
                ObjectsMigratedIntoNewScene.Add(networkObject.NetworkSceneHandle, new Dictionary<ulong, List<NetworkObject>>());
            }

            if (!ObjectsMigratedIntoNewScene[networkObject.NetworkSceneHandle].ContainsKey(NetworkManager.LocalClientId))
            {
                ObjectsMigratedIntoNewScene[networkObject.NetworkSceneHandle].Add(NetworkManager.LocalClientId, new List<NetworkObject>());
            }

            ObjectsMigratedIntoNewScene[networkObject.NetworkSceneHandle][NetworkManager.LocalClientId].Add(networkObject);
        }

        /// <summary>
        /// Invoked by clients when processing a <see cref="SceneEventType.ObjectSceneChanged"/> event
        /// or invoked by <see cref="SceneEventData.ProcessDeferredObjectSceneChangedEvents"/> when a client finishes
        /// synchronization.
        /// </summary>
        public void MigrateNetworkObjectsIntoScenes()
        {
            try
            {
                foreach (var sceneEntry in ObjectsMigratedIntoNewScene)
                {
                    if (ServerSceneHandleToClientSceneHandle.ContainsKey(sceneEntry.Key))
                    {
                        var clientSceneHandle = ServerSceneHandleToClientSceneHandle[sceneEntry.Key];
                        foreach (var ownerEntry in sceneEntry.Value)
                        {
                            if (ownerEntry.Key == NetworkManager.LocalClientId)
                            {
                                continue;
                            }
                            if (ScenesLoaded.ContainsKey(clientSceneHandle))
                            {
                                var scene = ScenesLoaded[clientSceneHandle];
                                foreach (var networkObject in ownerEntry.Value)
                                {
                                    SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene.SceneReference);
                                    networkObject.NetworkSceneHandle = sceneEntry.Key;
                                    networkObject.SceneOriginHandle = scene.SceneReference.handle;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NetworkLog.LogErrorServer($"{ex.Message}\n Stack Trace:\n {ex.StackTrace}");
            }
        }


        private List<int> m_ScenesToRemoveFromObjectMigration = new List<int>();

        /// <summary>
        /// Should be invoked during PostLateUpdate just prior to the NetworkMessageManager processes its outbound message queue.
        /// </summary>
        internal void CheckForAndSendNetworkObjectSceneChanged()
        {
            // Early exit if not the server or there is nothing pending
            if (ObjectsMigratedIntoNewScene.Count == 0)
            {
                return;
            }

            MigrateNetworkObjectsIntoScenes();

            // Double check that the NetworkObjects to migrate still exist
            m_ScenesToRemoveFromObjectMigration.Clear();
            foreach (var sceneEntry in ObjectsMigratedIntoNewScene)
            {
                if (!sceneEntry.Value.ContainsKey(NetworkManager.LocalClientId))
                {
                    continue;
                }
                var ownerSceneEntry = sceneEntry.Value[NetworkManager.LocalClientId];
                for (int i = sceneEntry.Value[NetworkManager.LocalClientId].Count - 1; i >= 0; i--)
                {
                    // Remove NetworkObjects that are no longer spawned
                    if (!sceneEntry.Value[NetworkManager.LocalClientId][i].IsSpawned)
                    {
                        sceneEntry.Value[NetworkManager.LocalClientId].RemoveAt(i);
                    }
                }
                // If the scene entry no longer has any NetworkObjects to migrate
                // then add it to the list of scenes to be removed from the table
                // of scenes containing NetworkObjects to migrate.
                if (sceneEntry.Value.Count == 0)
                {
                    m_ScenesToRemoveFromObjectMigration.Add(sceneEntry.Key);
                }
            }

            // Remove owner sceneHandle entries that no longer have any NetworkObjects remaining
            foreach (var sceneHandle in m_ScenesToRemoveFromObjectMigration)
            {
                ObjectsMigratedIntoNewScene[sceneHandle].Remove(NetworkManager.LocalClientId);
            }

            var localOwnerHasEntries = false;

            foreach (var sceneEntry in ObjectsMigratedIntoNewScene)
            {
                if (sceneEntry.Value.ContainsKey(NetworkManager.LocalClientId))
                {
                    localOwnerHasEntries = true;
                    break;
                }
            }

            // If the local owner has no entries, then exit
            if (!localOwnerHasEntries)
            {
                ObjectsMigratedIntoNewScene.Clear();
                return;
            }

            // Some NetworkObjects still exist, send the message
            var sceneEvent = BeginSceneEvent();
            sceneEvent.SceneEventType = SceneEventType.ObjectSceneChanged;
            SendSceneEventData(sceneEvent.SceneEventId, NetworkManager.ConnectedClientsIds.Where(c => c != NetworkManager.LocalClientId).ToArray());
            ObjectsMigratedIntoNewScene.Clear();
            EndSceneEvent(sceneEvent.SceneEventId);
        }

        // Used to handle client-side scene migration messages received while
        // a client is synchronizing
        internal struct DeferredObjectsMovedEvent
        {
            internal ulong OwnerId;
            internal Dictionary<int, List<ulong>> ObjectsMigratedTable;
        }
        internal List<DeferredObjectsMovedEvent> DeferredObjectsMovedEvents = new List<DeferredObjectsMovedEvent>();

        internal struct DeferredObjectCreation
        {
            internal ulong SenderId;
            internal uint MessageSize;
            // When we transfer session owner and we are using a DAHost, this will be pertinent (otherwise it is not when connected to a DA service)
            internal ulong[] ObserverIds;
            internal ulong[] NewObserverIds;
            internal NetworkObject.SceneObject SceneObject;
            internal FastBufferReader FastBufferReader;
        }

        internal List<DeferredObjectCreation> DeferredObjectCreationList = new List<DeferredObjectCreation>();
        internal int DeferredObjectCreationCount;

        // The added clientIds is specific to DAHost when session ownership changes and a normal client is controlling scene loading
        internal void DeferCreateObject(ulong senderId, uint messageSize, NetworkObject.SceneObject sceneObject, FastBufferReader fastBufferReader, ulong[] observerIds, ulong[] newObserverIds)
        {
            var deferredObjectCreationEntry = new DeferredObjectCreation()
            {
                SenderId = senderId,
                MessageSize = messageSize,
                ObserverIds = observerIds,
                NewObserverIds = newObserverIds,
                SceneObject = sceneObject,
            };

            unsafe
            {
                deferredObjectCreationEntry.FastBufferReader = new FastBufferReader(fastBufferReader.GetUnsafePtrAtCurrentPosition(), Allocator.Persistent, fastBufferReader.Length - fastBufferReader.Position);
            }

            DeferredObjectCreationList.Add(deferredObjectCreationEntry);
        }

        private void ProcessDeferredCreateObjectMessages()
        {
            // If no pending create object messages exit early
            if (DeferredObjectCreationList.Count == 0)
            {
                return;
            }
            var networkManager = NetworkManager;
            // Process all deferred create object messages.
            for (int i = 0; i < DeferredObjectCreationList.Count; i++)
            {
                var deferredObjectCreation = DeferredObjectCreationList[i];
                CreateObjectMessage.CreateObject(ref networkManager, ref deferredObjectCreation);
            }
            DeferredObjectCreationCount = DeferredObjectCreationList.Count;
            DeferredObjectCreationList.Clear();
        }

        public enum MapTypes
        {
            ServerToClient,
            ClientToServer
        }
        public struct SceneMap : INetworkSerializable
        {
            public MapTypes MapType;
            public Scene Scene;
            public bool ScenePresent;
            public string SceneName;
            public int ServerHandle;
            public int MappedLocalHandle;
            public int LocalHandle;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref MapType);
                serializer.SerializeValue(ref ScenePresent);
                if (serializer.IsReader)
                {
                    SceneName = "Not Present";
                }
                if (ScenePresent)
                {
                    serializer.SerializeValue(ref SceneName);
                    serializer.SerializeValue(ref LocalHandle);

                }
                serializer.SerializeValue(ref ServerHandle);
                serializer.SerializeValue(ref MappedLocalHandle);
            }
        }

        // public List<SceneMap> GetSceneMapping(MapTypes mapType)
        // {
        //     var mapping = new List<SceneMap>();
        //     if (mapType == MapTypes.ServerToClient)
        //     {
        //         foreach (var entry in ServerSceneHandleToClientSceneHandle)
        //         {
        //             var scene = ScenesLoaded[entry.Value];
        //             var sceneIsPresent = scene.IsValid() && scene.isLoaded;
        //             var sceneMap = new SceneMap()
        //             {
        //                 MapType = mapType,
        //                 ServerHandle = entry.Key,
        //                 MappedLocalHandle = entry.Value,
        //                 LocalHandle = scene.handle,
        //                 Scene = scene,
        //                 ScenePresent = sceneIsPresent,
        //                 SceneName = sceneIsPresent ? scene.name : "NotPresent",
        //             };
        //             mapping.Add(sceneMap);
        //         }
        //     }
        //     else
        //     {
        //         foreach (var entry in ClientSceneHandleToServerSceneHandle)
        //         {
        //             var scene = ScenesLoaded[entry.Key];
        //             var sceneIsPresent = scene.IsValid() && scene.isLoaded;
        //             var sceneMap = new SceneMap()
        //             {
        //                 MapType = mapType,
        //                 ServerHandle = entry.Value,
        //                 MappedLocalHandle = entry.Key,
        //                 LocalHandle = scene.handle,
        //                 Scene = scene,
        //                 ScenePresent = sceneIsPresent,
        //                 SceneName = sceneIsPresent ? scene.name : "NotPresent",
        //             };
        //             mapping.Add(sceneMap);
        //         }
        //     }
        //
        //     return mapping;
        // }

    }
}
