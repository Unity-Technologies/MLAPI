using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode.Interest;
using UnityEngine;
using Unity.Profiling;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode
{
    /// <summary>
    /// The main component of the library
    /// </summary>
    [AddComponentMenu("Netcode/" + nameof(NetworkManager), -100)]
    public class NetworkManager : MonoBehaviour, INetworkUpdateSystem
    {
#pragma warning disable IDE1006 // disable naming rule violation check

        // RuntimeAccessModifiersILPP will make this `public`
        internal static readonly Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>> __rpc_func_table = new Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // RuntimeAccessModifiersILPP will make this `public`
        internal static readonly Dictionary<uint, string> __rpc_name_table = new Dictionary<uint, string>();
#endif

#pragma warning restore IDE1006 // restore naming rule violation check

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_SyncTime = new ProfilerMarker($"{nameof(NetworkManager)}.SyncTime");
        private static ProfilerMarker s_TransportPoll = new ProfilerMarker($"{nameof(NetworkManager)}.TransportPoll");
        private static ProfilerMarker s_TransportConnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportConnect");
        private static ProfilerMarker s_HandleIncomingData = new ProfilerMarker($"{nameof(NetworkManager)}.{nameof(HandleIncomingData)}");
        private static ProfilerMarker s_TransportDisconnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportDisconnect");
        private static ProfilerMarker s_InvokeRpc = new ProfilerMarker($"{nameof(NetworkManager)}.{nameof(InvokeRpc)}");
#endif

        private const double k_TimeSyncFrequency = 1.0d; // sync every second, TODO will be removed once timesync is done via snapshots

        internal MessageQueueContainer MessageQueueContainer { get; private set; }

        internal InterestManager<NetworkClient, NetworkObject> InterestManager { get; private set; }
        internal SnapshotSystem SnapshotSystem { get; private set; }
        internal NetworkBehaviourUpdater BehaviourUpdater { get; private set; }

        private NetworkPrefabHandler m_PrefabHandler;

        public NetworkPrefabHandler PrefabHandler
        {
            get
            {
                if (m_PrefabHandler == null)
                {
                    m_PrefabHandler = new NetworkPrefabHandler();
                }

                return m_PrefabHandler;
            }
        }

        /// <summary>
        /// Returns the <see cref="GameObject"/> to use as the override as could be defined within the NetworkPrefab list
        /// Note: This should be used to create <see cref="GameObject"/> pools (with <see cref="NetworkObject"/> components)
        /// under the scenario where you are using the Host model as it spawns everything locally. As such, the override
        /// will not be applied when spawning locally on a Host.
        /// Related Classes and Interfaces:
        /// <see cref="NetworkPrefabHandler"/>
        /// <see cref="INetworkPrefabInstanceHandler"/>
        /// </summary>
        /// <param name="gameObject">the <see cref="GameObject"/> to be checked for a <see cref="NetworkManager"/> defined NetworkPrefab override</param>
        /// <returns>a <see cref="GameObject"/> that is either the override or if no overrides exist it returns the same as the one passed in as a parameter</returns>
        public GameObject GetNetworkPrefabOverride(GameObject gameObject)
        {
            var networkObject = gameObject.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                if (NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(networkObject.GlobalObjectIdHash))
                {
                    switch (NetworkConfig.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].Override)
                    {
                        case NetworkPrefabOverride.Hash:
                        case NetworkPrefabOverride.Prefab:
                            {
                                return NetworkConfig.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].OverridingTargetPrefab;
                            }
                    }
                }
            }
            return gameObject;
        }

        public NetworkTimeSystem NetworkTimeSystem { get; private set; }

        public NetworkTickSystem NetworkTickSystem { get; private set; }

        public NetworkTime LocalTime => NetworkTickSystem?.LocalTime ?? default;

        public NetworkTime ServerTime => NetworkTickSystem?.ServerTime ?? default;

        /// <summary>
        /// Gets or sets if the NetworkManager should be marked as DontDestroyOnLoad
        /// </summary>
        [HideInInspector] public bool DontDestroy = true;

        /// <summary>
        /// Gets or sets if the application should be set to run in background
        /// </summary>
        [HideInInspector] public bool RunInBackground = true;

        /// <summary>
        /// The log level to use
        /// </summary>
        [HideInInspector] public LogLevel LogLevel = LogLevel.Normal;

        /// <summary>
        /// The singleton instance of the NetworkManager
        /// </summary>
        public static NetworkManager Singleton { get; private set; }

        /// <summary>
        /// Gets the SpawnManager for this NetworkManager
        /// </summary>
        public NetworkSpawnManager SpawnManager { get; private set; }

        public CustomMessagingManager CustomMessagingManager { get; private set; }

        public NetworkSceneManager SceneManager { get; private set; }

        // Has to have setter for tests
        internal IInternalMessageHandler MessageHandler { get; set; }

        /// <summary>
        /// Gets the networkId of the server
        /// </summary>
        public ulong ServerClientId => NetworkConfig.NetworkTransport?.ServerClientId ??
                                       throw new NullReferenceException(
                                           $"The transport in the active {nameof(NetworkConfig)} is null");

        /// <summary>
        /// Returns ServerClientId if IsServer or LocalClientId if not
        /// </summary>
        public ulong LocalClientId
        {
            get => IsServer ? NetworkConfig.NetworkTransport.ServerClientId : m_LocalClientId;
            internal set => m_LocalClientId = value;
        }

        private ulong m_LocalClientId;

        /// <summary>
        /// Gets a dictionary of connected clients and their clientId keys. This is only populated on the server.
        /// </summary>
        public readonly Dictionary<ulong, NetworkClient> ConnectedClients = new Dictionary<ulong, NetworkClient>();

        /// <summary>
        /// Gets a list of connected clients. This is only populated on the server.
        /// </summary>
        public readonly List<NetworkClient> ConnectedClientsList = new List<NetworkClient>();

        /// <summary>
        /// Gets a list of just the IDs of all connected clients.
        /// </summary>
        public ulong[] ConnectedClientsIds => ConnectedClientsList.Select(c => c.ClientId).ToArray();

        /// <summary>
        /// Gets a dictionary of the clients that have been accepted by the transport but are still pending by the Netcode. This is only populated on the server.
        /// </summary>
        public readonly Dictionary<ulong, PendingClient> PendingClients = new Dictionary<ulong, PendingClient>();

        /// <summary>
        /// Gets Whether or not a server is running
        /// </summary>
        public bool IsServer { get; internal set; }

        /// <summary>
        /// Gets Whether or not a client is running
        /// </summary>
        public bool IsClient { get; internal set; }

        /// <summary>
        /// Gets if we are running as host
        /// </summary>
        public bool IsHost => IsServer && IsClient;

        /// <summary>
        /// Gets Whether or not we are listening for connections
        /// </summary>
        public bool IsListening { get; internal set; }

        /// <summary>
        /// Gets if we are connected as a client
        /// </summary>
        public bool IsConnectedClient { get; internal set; }

        /// <summary>
        /// The callback to invoke once a client connects. This callback is only ran on the server and on the local client that connects.
        /// </summary>
        public event Action<ulong> OnClientConnectedCallback = null;

        internal void InvokeOnClientConnectedCallback(ulong clientId) => OnClientConnectedCallback?.Invoke(clientId);

        /// <summary>
        /// The callback to invoke when a client disconnects. This callback is only ran on the server and on the local client that disconnects.
        /// </summary>
        public event Action<ulong> OnClientDisconnectCallback = null;

        internal void InvokeOnClientDisconnectCallback(ulong clientId) => OnClientDisconnectCallback?.Invoke(clientId);

        /// <summary>
        /// The callback to invoke once the server is ready
        /// </summary>
        public event Action OnServerStarted = null;

        /// <summary>
        /// Delegate type called when connection has been approved. This only has to be set on the server.
        /// </summary>
        /// <param name="createPlayerObject">If true, a player object will be created. Otherwise the client will have no object.</param>
        /// <param name="playerPrefabHash">The prefabHash to use for the client. If createPlayerObject is false, this is ignored. If playerPrefabHash is null, the default player prefab is used.</param>
        /// <param name="approved">Whether or not the client was approved</param>
        /// <param name="position">The position to spawn the client at. If null, the prefab position is used.</param>
        /// <param name="rotation">The rotation to spawn the client with. If null, the prefab position is used.</param>
        public delegate void ConnectionApprovedDelegate(bool createPlayerObject, uint? playerPrefabHash, bool approved,
            Vector3? position, Quaternion? rotation);

        /// <summary>
        /// The callback to invoke during connection approval
        /// </summary>
        public event Action<byte[], ulong, ConnectionApprovedDelegate> ConnectionApprovalCallback = null;

        internal void InvokeConnectionApproval(byte[] payload, ulong clientId, ConnectionApprovedDelegate action) =>
            ConnectionApprovalCallback?.Invoke(payload, clientId, action);

        /// <summary>
        /// The current NetworkConfig
        /// </summary>
        [HideInInspector] public NetworkConfig NetworkConfig;

        /// <summary>
        /// The current hostname we are connected to, used to validate certificate
        /// </summary>
        public string ConnectedHostname { get; private set; }

        internal INetworkMetrics NetworkMetrics { get; private set; }

        internal static event Action OnSingletonReady;

        // the interest settings objects receive unless they have a pre-prefab override
        public InterestSettings InterestSettings;


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (NetworkConfig == null)
            {
                return; // May occur when the component is added
            }

            if (GetComponentInChildren<NetworkObject>() != null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkManager)} cannot be a {nameof(NetworkObject)}.");
                }
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var activeSceneName = activeScene.name;
            if (!NetworkConfig.RegisteredScenes.Contains(activeSceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Active scene is not registered as a network scene. Netcode has added it");
                }

                NetworkConfig.RegisteredScenes.Add(activeSceneName);
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (!UnityEditor.EditorApplication.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(this);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                };
            }

            // If the scene is not dirty or the asset database is currently updating then we can skip updating the NetworkPrefab information
            if (!activeScene.isDirty || UnityEditor.EditorApplication.isUpdating)
            {
                return;
            }

            // During OnValidate we will always clear out NetworkPrefabOverrideLinks and rebuild it
            NetworkConfig.NetworkPrefabOverrideLinks.Clear();

            // Check network prefabs and assign to dictionary for quick look up
            for (int i = 0; i < NetworkConfig.NetworkPrefabs.Count; i++)
            {
                var networkPrefab = NetworkConfig.NetworkPrefabs[i];
                if (networkPrefab != null && networkPrefab.Prefab != null)
                {
                    var networkObject = networkPrefab.Prefab.GetComponent<NetworkObject>();
                    if (networkObject == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogError($"Cannot register {nameof(NetworkPrefab)}[{i}], it does not have a {nameof(NetworkObject)} component at its root");
                        }
                    }
                    else
                    {
                        {
                            var childNetworkObjects = new List<NetworkObject>();
                            networkPrefab.Prefab.GetComponentsInChildren( /* includeInactive = */ true, childNetworkObjects);
                            if (childNetworkObjects.Count > 1) // total count = 1 root NetworkObject + n child NetworkObjects
                            {
                                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                                {
                                    NetworkLog.LogWarning($"{nameof(NetworkPrefab)}[{i}] has child {nameof(NetworkObject)}(s) but they will not be spawned across the network (unsupported {nameof(NetworkPrefab)} setup)");
                                }
                            }
                        }

                        // Default to the standard NetworkPrefab.Prefab's NetworkObject first
                        var globalObjectIdHash = networkObject.GlobalObjectIdHash;

                        // Now check to see if it has an override
                        switch (networkPrefab.Override)
                        {
                            case NetworkPrefabOverride.Prefab:
                                {
                                    if (NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride == null &&
                                        NetworkConfig.NetworkPrefabs[i].Prefab != null)
                                    {
                                        if (networkPrefab.SourcePrefabToOverride == null && networkPrefab.Prefab != null)
                                        {
                                            networkPrefab.SourcePrefabToOverride = networkPrefab.Prefab;
                                        }

                                        globalObjectIdHash = networkPrefab.SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash;
                                    }

                                    break;
                                }
                            case NetworkPrefabOverride.Hash:
                                globalObjectIdHash = networkPrefab.SourceHashToOverride;
                                break;
                        }

                        // Add to the NetworkPrefabOverrideLinks or handle a new (blank) entries
                        if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(globalObjectIdHash))
                        {
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(globalObjectIdHash, networkPrefab);
                        }
                        else
                        {
                            // Duplicate entries can happen when adding a new entry into a list of existing entries
                            // Either this is user error or a new entry, either case we replace it with a new, blank, NetworkPrefab under this condition
                            NetworkConfig.NetworkPrefabs[i] = new NetworkPrefab();
                        }
                    }
                }
            }
        }
#endif

        private void Initialize(bool server)
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Initialize));
            }

            // Register INetworkUpdateSystem for receiving data from the wire
            // Must always be registered before any other systems or messages can end up being re-ordered by frame timing
            // Cannot allow any new data to arrive from the wire after MessageQueueContainer's Initialization update
            // has run
            this.RegisterNetworkUpdate(NetworkUpdateStage.EarlyUpdate);

            LocalClientId = 0;
            PendingClients.Clear();
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();
            NetworkObject.OrphanChildren.Clear();

            // Create spawn manager instance
            SpawnManager = new NetworkSpawnManager(this);

            CustomMessagingManager = new CustomMessagingManager(this);

            SceneManager = new NetworkSceneManager(this);

            BehaviourUpdater = new NetworkBehaviourUpdater();

            // Only create this if it's not already set (like in test cases)
            MessageHandler ??= CreateMessageHandler();

            if (NetworkMetrics == null)
            {
#if MULTIPLAYER_TOOLS
                NetworkMetrics = new NetworkMetrics();
#else
                NetworkMetrics = new NullNetworkMetrics();
#endif
            }

            if (NetworkConfig.NetworkTransport == null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError("No transport has been selected!");
                }

                return;
            }

            //This 'if' should never enter
            if (SnapshotSystem != null)
            {
                SnapshotSystem.Dispose();
                SnapshotSystem = null;
            }

            SnapshotSystem = new SnapshotSystem();

            if (server)
            {
                NetworkTimeSystem = NetworkTimeSystem.ServerTimeSystem();
            }
            else
            {
                NetworkTimeSystem = new NetworkTimeSystem(1.0 / NetworkConfig.TickRate, 1.0 / NetworkConfig.TickRate, 0.2);
            }

            NetworkTickSystem = new NetworkTickSystem(NetworkConfig.TickRate, 0, 0);
            NetworkTickSystem.Tick += OnNetworkManagerTick;

            // This should never happen, but in the event that it does there should be (at a minimum) a unity error logged.
            if (MessageQueueContainer != null)
            {
                Debug.LogError(
                    "Init was invoked, but messageQueueContainer was already initialized! (destroying previous instance)");
                MessageQueueContainer.Dispose();
                MessageQueueContainer = null;
            }
            InterestManager = new InterestManager<NetworkClient, NetworkObject>();

            // The MessageQueueContainer must be initialized within the Init method ONLY
            // It should ONLY be shutdown and destroyed in the Shutdown method (other than just above)
            MessageQueueContainer = new MessageQueueContainer(this);

            // Register INetworkUpdateSystem (always register this after messageQueueContainer has been instantiated)
            this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);

            if (NetworkConfig.EnableSceneManagement)
            {
                NetworkConfig.RegisteredScenes.Sort(StringComparer.Ordinal);

                for (int i = 0; i < NetworkConfig.RegisteredScenes.Count; i++)
                {
                    SceneManager.RegisteredSceneNames.Add(NetworkConfig.RegisteredScenes[i]);
                    SceneManager.SceneIndexToString.Add((uint)i, NetworkConfig.RegisteredScenes[i]);
                    SceneManager.SceneNameToIndex.Add(NetworkConfig.RegisteredScenes[i], (uint)i);
                }

                SceneManager.SetCurrentSceneIndex();
            }

            // This is used to remove entries not needed or invalid
            var removeEmptyPrefabs = new List<int>();

            // Always clear our prefab override links before building
            NetworkConfig.NetworkPrefabOverrideLinks.Clear();

            // Build the NetworkPrefabOverrideLinks dictionary
            for (int i = 0; i < NetworkConfig.NetworkPrefabs.Count; i++)
            {
                if (NetworkConfig.NetworkPrefabs[i] == null || NetworkConfig.NetworkPrefabs[i].Prefab == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogWarning(
                            $"{nameof(NetworkPrefab)} cannot be null ({nameof(NetworkPrefab)} at index: {i})");
                    }

                    removeEmptyPrefabs.Add(i);

                    continue;
                }
                else if (NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>() == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogWarning(
                            $"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") is missing a {nameof(NetworkObject)} component");
                    }

                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    Debug.LogWarning($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") will be removed and ignored.");
                    removeEmptyPrefabs.Add(i);

                    continue;
                }

                var networkObject = NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>();

                // Assign the appropriate GlobalObjectIdHash to the appropriate NetworkPrefab
                if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(networkObject.GlobalObjectIdHash))
                {
                    switch (NetworkConfig.NetworkPrefabs[i].Override)
                    {
                        default:
                        case NetworkPrefabOverride.None:
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(networkObject.GlobalObjectIdHash,
                                NetworkConfig.NetworkPrefabs[i]);
                            break;
                        case NetworkPrefabOverride.Prefab:
                            {
                                var sourcePrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash;
                                NetworkConfig.NetworkPrefabOverrideLinks.Add(sourcePrefabGlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);

                                var targetPrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;
                                NetworkConfig.OverrideToNetworkPrefab.Add(targetPrefabGlobalObjectIdHash, sourcePrefabGlobalObjectIdHash);
                            }
                            break;
                        case NetworkPrefabOverride.Hash:
                            {
                                var sourcePrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].SourceHashToOverride;
                                NetworkConfig.NetworkPrefabOverrideLinks.Add(sourcePrefabGlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);

                                var targetPrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;
                                NetworkConfig.OverrideToNetworkPrefab.Add(targetPrefabGlobalObjectIdHash, sourcePrefabGlobalObjectIdHash);
                            }
                            break;
                    }
                }
                else
                {
                    // This should never happen, but in the case it somehow does log an error and remove the duplicate entry
                    Debug.LogError($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") has a duplicate {nameof(NetworkObject.GlobalObjectIdHash)} {networkObject.GlobalObjectIdHash} entry! Removing entry from list!");
                    removeEmptyPrefabs.Add(i);
                }
            }

            // If we have a player prefab, then we need to verify it is in the list of NetworkPrefabOverrideLinks for client side spawning.
            if (NetworkConfig.PlayerPrefab != null)
            {
                var playerPrefabNetworkObject = NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>();
                if (playerPrefabNetworkObject != null)
                {
                    //In the event there is no NetworkPrefab entry (i.e. no override for default player prefab)
                    if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(playerPrefabNetworkObject
                        .GlobalObjectIdHash))
                    {
                        //Then add a new entry for the player prefab
                        var playerNetworkPrefab = new NetworkPrefab();
                        playerNetworkPrefab.Prefab = NetworkConfig.PlayerPrefab;
                        NetworkConfig.NetworkPrefabs.Insert(0, playerNetworkPrefab);
                        NetworkConfig.NetworkPrefabOverrideLinks.Add(playerPrefabNetworkObject.GlobalObjectIdHash,
                            playerNetworkPrefab);
                    }
                }
                else
                {
                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    Debug.LogError($"{nameof(NetworkConfig.PlayerPrefab)} (\"{NetworkConfig.PlayerPrefab.name}\") has no NetworkObject assigned to it!.");
                }
            }

            // Clear out anything that is invalid or not used (for invalid entries we already logged warnings to the user earlier)
            // Iterate backwards so indices don't shift as we remove
            for (int i = removeEmptyPrefabs.Count - 1; i >= 0; i--)
            {
                NetworkConfig.NetworkPrefabs.RemoveAt(removeEmptyPrefabs[i]);
            }

            removeEmptyPrefabs.Clear();

            NetworkConfig.NetworkTransport.OnTransportEvent += HandleRawTransportPoll;

            NetworkConfig.NetworkTransport.ResetChannelCache();

            NetworkConfig.NetworkTransport.Init();
        }

        /// <summary>
        /// Starts a server
        /// </summary>
        public SocketTasks StartServer()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("StartServer()");
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start server while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning(
                            "No ConnectionApproval callback defined. Connection approval will timeout");
                    }
                }
            }

            Initialize(true);

            var socketTasks = NetworkConfig.NetworkTransport.StartServer();

            IsServer = true;
            IsClient = false;
            IsListening = true;

            SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

            OnServerStarted?.Invoke();

            return socketTasks;
        }

        /// <summary>
        /// Starts a client
        /// </summary>
        public SocketTasks StartClient()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartClient));
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start client while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            Initialize(false);

            var socketTasks = NetworkConfig.NetworkTransport.StartClient();

            IsServer = false;
            IsClient = true;
            IsListening = true;

            return socketTasks;
        }

        /// <summary>
        /// Stops the running server
        /// </summary>
        public void StopServer()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StopServer));
            }

            var disconnectedIds = new HashSet<ulong>();

            //Don't know if I have to disconnect the clients. I'm assuming the NetworkTransport does all the cleaning on shtudown. But this way the clients get a disconnect message from server (so long it does't get lost)

            // make sure all messages are flushed before transport disconnect clients
            if (MessageQueueContainer != null)
            {
                MessageQueueContainer.ProcessAndFlushMessageQueue(
                    queueType: MessageQueueContainer.MessageQueueProcessingTypes.Send,
                    NetworkUpdateStage.PostLateUpdate); // flushing messages in case transport's disconnect
            }

            foreach (KeyValuePair<ulong, NetworkClient> pair in ConnectedClients)
            {
                if (!disconnectedIds.Contains(pair.Key))
                {
                    disconnectedIds.Add(pair.Key);

                    if (pair.Key == NetworkConfig.NetworkTransport.ServerClientId)
                    {
                        continue;
                    }

                    NetworkConfig.NetworkTransport.DisconnectRemoteClient(pair.Key);
                }
            }

            foreach (KeyValuePair<ulong, PendingClient> pair in PendingClients)
            {
                if (!disconnectedIds.Contains(pair.Key))
                {
                    disconnectedIds.Add(pair.Key);
                    if (pair.Key == NetworkConfig.NetworkTransport.ServerClientId)
                    {
                        continue;
                    }

                    NetworkConfig.NetworkTransport.DisconnectRemoteClient(pair.Key);
                }
            }

            IsServer = false;
            Shutdown();
        }

        /// <summary>
        /// Stops the running host
        /// </summary>
        public void StopHost()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StopHost));
            }

            IsServer = false;
            IsClient = false;
            StopServer();

            //We don't stop client since we dont actually have a transport connection to our own host. We just handle host messages directly in the netcode
        }

        /// <summary>
        /// Stops the running client
        /// </summary>
        public void StopClient()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StopClient));
            }

            IsClient = false;
            NetworkConfig.NetworkTransport.DisconnectLocalClient();
            IsConnectedClient = false;
            Shutdown();
        }

        /// <summary>
        /// Starts a Host
        /// </summary>
        public SocketTasks StartHost()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartHost));
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start host while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning(
                            "No ConnectionApproval callback defined. Connection approval will timeout");
                    }
                }
            }

            Initialize(true);

            var socketTasks = NetworkConfig.NetworkTransport.StartServer();

            IsServer = true;
            IsClient = true;
            IsListening = true;

            if (NetworkConfig.ConnectionApproval)
            {
                InvokeConnectionApproval(NetworkConfig.ConnectionData, ServerClientId,
                    (createPlayerObject, playerPrefabHash, approved, position, rotation) =>
                    {
                        // You cannot decline the local server. Force approved to true
                        if (!approved)
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning(
                                    "You cannot decline the host connection. The connection was automatically approved.");
                            }
                        }

                        HandleApproval(ServerClientId, createPlayerObject, playerPrefabHash, true, position, rotation);
                    });
            }
            else
            {
                HandleApproval(ServerClientId, NetworkConfig.PlayerPrefab != null, null, true, null, null);
            }

            SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

            OnServerStarted?.Invoke();

            return socketTasks;
        }

        public void SetSingleton()
        {
            Singleton = this;

            OnSingletonReady?.Invoke();
        }

        private void OnEnable()
        {
            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (RunInBackground)
            {
                Application.runInBackground = true;
            }

            if (Singleton == null)
            {
                SetSingleton();
            }
        }

        private void OnDestroy()
        {
            Shutdown();

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        public void Shutdown()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Shutdown));
            }

            // Unregister INetworkUpdateSystem before shutting down the MessageQueueContainer
            this.UnregisterAllNetworkUpdates();

            //If an instance of the MessageQueueContainer is still around, then shut it down and remove the reference
            if (MessageQueueContainer != null)
            {
                MessageQueueContainer.Dispose();
                MessageQueueContainer = null;
            }

            if (SnapshotSystem != null)
            {
                SnapshotSystem.Dispose();
                SnapshotSystem = null;
            }

            if (NetworkTickSystem != null)
            {
                NetworkTickSystem.Tick -= OnNetworkManagerTick;
                NetworkTickSystem = null;
            }

            if (InterestManager != null)
            {
                InterestManager.Dispose();
                InterestManager = null;
            }

            IsListening = false;
            IsServer = false;
            IsClient = false;
            NetworkConfig.NetworkTransport.OnTransportEvent -= HandleRawTransportPoll;

            if (SpawnManager != null)
            {
                SpawnManager.DestroyNonSceneObjects();
                SpawnManager.ServerResetShudownStateForSceneObjects();

                SpawnManager = null;
            }

            if (SceneManager != null)
            {
                SceneManager = null;
            }

            if (MessageHandler != null)
            {
                MessageHandler = null;
            }

            if (CustomMessagingManager != null)
            {
                CustomMessagingManager = null;
            }

            m_MessageBatcher.Shutdown();

            if (BehaviourUpdater != null)
            {
                BehaviourUpdater = null;
            }

            //The Transport is set during Init time, thus it is possible for the Transport to be null
            NetworkConfig?.NetworkTransport?.Shutdown();
        }

        // INetworkUpdateSystem
        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            switch (updateStage)
            {
                case NetworkUpdateStage.EarlyUpdate:
                    OnNetworkEarlyUpdate();
                    break;
                case NetworkUpdateStage.PreUpdate:
                    OnNetworkPreUpdate();
                    break;
            }
        }

        private void OnNetworkEarlyUpdate()
        {
            NetworkMetrics.DispatchFrame();

            if (IsListening)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_TransportPoll.Begin();
#endif
                var isLoopBack = false;

                //If we are in loopback mode, we don't need to touch the transport
                if (!isLoopBack)
                {
                    NetworkEvent networkEvent;
                    int processedEvents = 0;
                    do
                    {
                        processedEvents++;
                        networkEvent = NetworkConfig.NetworkTransport.PollEvent(out ulong clientId, out NetworkChannel networkChannel, out ArraySegment<byte> payload, out float receiveTime);
                        HandleRawTransportPoll(networkEvent, clientId, networkChannel, payload, receiveTime);
                        // Only do another iteration if: there are no more messages AND (there is no limit to max events or we have processed less than the maximum)
                    } while (IsListening && networkEvent != NetworkEvent.Nothing);
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_TransportPoll.End();
#endif
            }
        }

        // TODO Once we have a way to subscribe to NetworkUpdateLoop with order we can move this out of NetworkManager but for now this needs to be here because we need strict ordering.
        private void OnNetworkPreUpdate()
        {
            // Only update RTT here, server time is updated by time sync messages
            NetworkTimeSystem.Advance(Time.deltaTime);
            NetworkTickSystem.UpdateTick(NetworkTimeSystem.LocalTime, NetworkTimeSystem.ServerTime);

            if (IsServer == false)
            {
                NetworkTimeSystem.Sync(NetworkTimeSystem.LastSyncedServerTimeSec + Time.deltaTime, NetworkConfig.NetworkTransport.GetCurrentRtt(ServerClientId) / 1000d);
            }
        }

        /// <summary>
        /// This function runs once whenever the local tick is incremented and is responsible for the following (in order):
        /// - collect commands/inputs and send them to the server (TBD)
        /// - call NetworkFixedUpdate on all NetworkBehaviours in prediction/client authority mode
        /// - create a snapshot from resulting state
        /// </summary>
        private void OnNetworkManagerTick()
        {
            if (NetworkConfig.EnableNetworkVariable)
            {
                // Do NetworkVariable updates
                BehaviourUpdater.NetworkBehaviourUpdate(this
                );
            }

            int timeSyncFrequencyTicks = (int)(k_TimeSyncFrequency * NetworkConfig.TickRate);
            if (IsServer && NetworkTickSystem.ServerTime.Tick % timeSyncFrequencyTicks == 0)
            {
                SyncTime();
            }
        }

        private void SendConnectionRequest()
        {
            var clientIds = new[] { ServerClientId };
            var context = MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.ConnectionRequest, NetworkChannel.Internal,
                clientIds, NetworkUpdateStage.EarlyUpdate);
            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    nonNullContext.NetworkWriter.WriteUInt64Packed(NetworkConfig.GetConfig());

                    if (NetworkConfig.ConnectionApproval)
                    {
                        nonNullContext.NetworkWriter.WriteByteArray(NetworkConfig.ConnectionData);
                    }
                }
            }
        }

        private IEnumerator ApprovalTimeout(ulong clientId)
        {
            NetworkTime timeStarted = LocalTime;

            //We yield every frame incase a pending client disconnects and someone else gets its connection id
            while ((LocalTime - timeStarted).Time < NetworkConfig.ClientConnectionBufferTimeout && PendingClients.ContainsKey(clientId))
            {
                yield return null;
            }

            if (PendingClients.ContainsKey(clientId) && !ConnectedClients.ContainsKey(clientId))
            {
                // Timeout
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"Client {clientId} Handshake Timed Out");
                }

                DisconnectClient(clientId);
            }
        }

        internal IEnumerator TimeOutSwitchSceneProgress(SceneSwitchProgress switchSceneProgress)
        {
            yield return new WaitForSecondsRealtime(NetworkConfig.LoadSceneTimeOut);
            switchSceneProgress.SetTimedOut();
        }

        private void HandleRawTransportPoll(NetworkEvent networkEvent, ulong clientId, NetworkChannel networkChannel,
            ArraySegment<byte> payload, float receiveTime)
        {
            switch (networkEvent)
            {
                case NetworkEvent.Connect:
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportConnect.Begin();
#endif
                    if (IsServer)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo("Client Connected");
                        }

                        PendingClients.Add(clientId, new PendingClient()
                        {
                            ClientId = clientId,
                            ConnectionState = PendingClient.State.PendingConnection
                        });

                        StartCoroutine(ApprovalTimeout(clientId));
                    }
                    else
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo("Connected");
                        }

                        SendConnectionRequest();
                        StartCoroutine(ApprovalTimeout(clientId));
                    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportConnect.End();
#endif
                    break;
                case NetworkEvent.Data:
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo($"Incoming Data From {clientId}: {payload.Count} bytes");
                        }

                        HandleIncomingData(clientId, networkChannel, payload, receiveTime);
                        break;
                    }
                case NetworkEvent.Disconnect:
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportDisconnect.Begin();
#endif

                    if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                    {
                        NetworkLog.LogInfo($"Disconnect Event From {clientId}");
                    }

                    if (IsServer)
                    {
                        OnClientDisconnectFromServer(clientId);
                    }
                    else
                    {
                        IsConnectedClient = false;
                        StopClient();
                    }

                    OnClientDisconnectCallback?.Invoke(clientId);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportDisconnect.End();
#endif
                    break;
            }
        }

        private readonly NetworkBuffer m_InputBufferWrapper = new NetworkBuffer(new byte[0]);
        private readonly MessageBatcher m_MessageBatcher = new MessageBatcher();

        internal void HandleIncomingData(ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> data,
            float receiveTime)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.Begin();
#endif
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("Unwrapping Data Header");
            }

            m_InputBufferWrapper.SetTarget(data.Array);
            m_InputBufferWrapper.SetLength(data.Count + data.Offset);
            m_InputBufferWrapper.Position = data.Offset;

            using (var messageStream = m_InputBufferWrapper)
            {
                // Client tried to send a network message that was not the connection request before he was accepted.

                if (MessageQueueContainer.IsUsingBatching())
                {
                    m_MessageBatcher.ReceiveItems(messageStream, ReceiveCallback, clientId, receiveTime, networkChannel);
                }
                else
                {
                    var messageType = (MessageQueueContainer.MessageType)messageStream.ReadByte();
                    MessageHandler.MessageReceiveQueueItem(clientId, messageStream, receiveTime, messageType, networkChannel);
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.End();
#endif
        }

        private void ReceiveCallback(NetworkBuffer messageBuffer, MessageQueueContainer.MessageType messageType,
            ulong clientId, float receiveTime, NetworkChannel receiveChannel)
        {
            MessageHandler.MessageReceiveQueueItem(clientId, messageBuffer, receiveTime, messageType, receiveChannel);
        }

        /// <summary>
        /// Called when an inbound queued RPC is invoked
        /// </summary>
        /// <param name="item">frame queue item to invoke</param>
#pragma warning disable 618
        internal void InvokeRpc(MessageFrameItem item, NetworkUpdateStage networkUpdateStage)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_InvokeRpc.Begin();
#endif
            using (var reader = PooledNetworkReader.Get(item.NetworkBuffer))
            {
                var networkObjectId = reader.ReadUInt64Packed();
                var networkBehaviourId = reader.ReadUInt16Packed();
                var networkMethodId = reader.ReadUInt32Packed();

                if (__rpc_func_table.ContainsKey(networkMethodId))
                {
                    if (!SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                    {
                        return;
                    }

                    var networkObject = SpawnManager.SpawnedObjects[networkObjectId];

                    var networkBehaviour = networkObject.GetNetworkBehaviourAtOrderIndex(networkBehaviourId);
                    if (networkBehaviour == null)
                    {
                        return;
                    }

                    var rpcParams = new __RpcParams();
                    switch (item.MessageType)
                    {
                        case MessageQueueContainer.MessageType.ServerRpc:
                            rpcParams.Server = new ServerRpcParams
                            {
                                Receive = new ServerRpcReceiveParams
                                {
                                    UpdateStage = (NetworkUpdateStage)networkUpdateStage,
                                    SenderClientId = item.NetworkId
                                }
                            };
                            break;
                        case MessageQueueContainer.MessageType.ClientRpc:
                            rpcParams.Client = new ClientRpcParams
                            {
                                Receive = new ClientRpcReceiveParams
                                {
                                    UpdateStage = (NetworkUpdateStage)networkUpdateStage
                                }
                            };
                            break;
                    }

                    __rpc_func_table[networkMethodId](networkBehaviour, new NetworkSerializer(item.NetworkReader), rpcParams);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (__rpc_name_table.TryGetValue(networkMethodId, out var rpcMethodName))
                    {
                        NetworkMetrics.TrackRpcReceived(
                            item.NetworkId,
                            networkObjectId,
                            rpcMethodName,
                            networkBehaviour.__getTypeName(),
                            item.StreamSize);
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Disconnects the remote client.
        /// </summary>
        /// <param name="clientId">The ClientId to disconnect</param>
        public void DisconnectClient(ulong clientId)
        {
            if (!IsServer)
            {
                throw new NotServerException("Only server can disconnect remote clients. Use StopClient instead.");
            }

            ConnectedClients.Remove(clientId);
            PendingClients.Remove(clientId);

            for (int i = ConnectedClientsList.Count - 1; i > -1; i--)
            {
                if (ConnectedClientsList[i].ClientId == clientId)
                {
                    ConnectedClientsList.RemoveAt(i);
                }
            }

            NetworkConfig.NetworkTransport.DisconnectRemoteClient(clientId);
        }

        internal void OnClientDisconnectFromServer(ulong clientId)
        {
            PendingClients.Remove(clientId);

            if (ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
            {
                if (IsServer)
                {
                    var playerObject = networkClient.PlayerObject;
                    if (playerObject != null)
                    {
                        if (PrefabHandler.ContainsHandler(ConnectedClients[clientId].PlayerObject.GlobalObjectIdHash))
                        {
                            PrefabHandler.HandleNetworkPrefabDestroy(ConnectedClients[clientId].PlayerObject);
                        }
                        else
                        {
                            Destroy(playerObject.gameObject);
                        }
                    }

                    for (int i = 0; i < networkClient.OwnedObjects.Count; i++)
                    {
                        var ownedObject = networkClient.OwnedObjects[i];
                        if (ownedObject != null)
                        {
                            if (!ownedObject.DontDestroyWithOwner)
                            {
                                if (PrefabHandler.ContainsHandler(ConnectedClients[clientId].OwnedObjects[i]
                                    .GlobalObjectIdHash))
                                {
                                    PrefabHandler.HandleNetworkPrefabDestroy(ConnectedClients[clientId].OwnedObjects[i]);
                                }
                                else
                                {
                                    Destroy(ownedObject.gameObject);
                                }
                            }
                            else
                            {
                                ownedObject.RemoveOwnership();
                            }
                        }
                    }

                    // TODO: Could(should?) be replaced with more memory per client, by storing the visiblity

                    foreach (var sobj in SpawnManager.SpawnedObjectsList)
                    {
                        sobj.Observers.Remove(clientId);
                    }
                }

                for (int i = 0; i < ConnectedClientsList.Count; i++)
                {
                    if (ConnectedClientsList[i].ClientId == clientId)
                    {
                        ConnectedClientsList.RemoveAt(i);
                        break;
                    }
                }

                ConnectedClients.Remove(clientId);
            }
        }

        private void SyncTime()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_SyncTime.Begin();
#endif
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("Syncing Time To Clients");
            }

            ulong[] clientIds = ConnectedClientsIds;
            var context = MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.TimeSync, NetworkChannel.SyncChannel,
                clientIds, NetworkUpdateStage.EarlyUpdate);
            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    nonNullContext.NetworkWriter.WriteInt32Packed(NetworkTickSystem.ServerTime.Tick);
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_SyncTime.End();
#endif
        }

        private readonly List<NetworkObject> m_ObservedObjects = new List<NetworkObject>();

        internal void HandleApproval(ulong ownerClientId, bool createPlayerObject, uint? playerPrefabHash, bool approved, Vector3? position, Quaternion? rotation)
        {
            if (approved)
            {
                // Inform new client it got approved
                PendingClients.Remove(ownerClientId);

                var client = new NetworkClient { ClientId = ownerClientId, };
                ConnectedClients.Add(ownerClientId, client);
                ConnectedClientsList.Add(client);

                if (createPlayerObject)
                {
                    var networkObject = SpawnManager.CreateLocalNetworkObject(false, playerPrefabHash ?? NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash, ownerClientId, null, position, rotation);
                    SpawnManager.SpawnNetworkObjectLocally(networkObject, SpawnManager.GetNetworkObjectId(), false, true, ownerClientId, null, false, false);

                    ConnectedClients[ownerClientId].PlayerObject = networkObject;
                }

                m_ObservedObjects.Clear();

                foreach (var sobj in SpawnManager.SpawnedObjectsList)
                {
                    if (ownerClientId == ServerClientId || sobj.CheckObjectVisibility == null || sobj.CheckObjectVisibility(ownerClientId))
                    {
                        m_ObservedObjects.Add(sobj);
                        sobj.Observers.Add(ownerClientId);
                    }
                }

                if (ownerClientId != ServerClientId)
                {
                    // Don't send any data over the wire if the host "connected"
                    ulong[] clientIds = { ownerClientId };

                    var context = MessageQueueContainer.EnterInternalCommandContext(
                        MessageQueueContainer.MessageType.ConnectionApproved, NetworkChannel.Internal,
                        clientIds, NetworkUpdateStage.EarlyUpdate);

                    if (context != null)
                    {
                        using (var nonNullContext = (InternalCommandContext)context)
                        {
                            nonNullContext.NetworkWriter.WriteUInt64Packed(ownerClientId);

                            if (NetworkConfig.EnableSceneManagement)
                            {
                                nonNullContext.NetworkWriter.WriteUInt32Packed(NetworkSceneManager.CurrentSceneIndex);
                                nonNullContext.NetworkWriter.WriteByteArray(NetworkSceneManager.CurrentSceneSwitchProgressGuid
                                    .ToByteArray());
                            }

                            nonNullContext.NetworkWriter.WriteInt32Packed(LocalTime.Tick);
                            nonNullContext.NetworkWriter.WriteUInt32Packed((uint)m_ObservedObjects.Count);

                            for (int i = 0; i < m_ObservedObjects.Count; i++)
                            {
                                m_ObservedObjects[i].SerializeSceneObject(nonNullContext.NetworkWriter, ownerClientId);
                            }
                        }
                    }
                }

                OnClientConnectedCallback?.Invoke(ownerClientId);

                if (!createPlayerObject || (playerPrefabHash == null && NetworkConfig.PlayerPrefab == null))
                {
                    return;
                }

                // Inform old clients of the new player
                foreach (KeyValuePair<ulong, NetworkClient> clientPair in ConnectedClients)
                {
                    if (clientPair.Key == ownerClientId ||
                        clientPair.Key == ServerClientId || // Server already spawned it
                        ConnectedClients[ownerClientId].PlayerObject == null ||
                        !ConnectedClients[ownerClientId].PlayerObject.Observers.Contains(clientPair.Key))
                    {
                        continue; //The new client.
                    }

                    var context = MessageQueueContainer.EnterInternalCommandContext(
                        MessageQueueContainer.MessageType.CreateObject, NetworkChannel.Internal,
                        new[] { clientPair.Key }, NetworkUpdateLoop.UpdateStage);
                    if (context != null)
                    {
                        using (var nonNullContext = (InternalCommandContext)context)
                        {
                            nonNullContext.NetworkWriter.WriteBool(true);
                            nonNullContext.NetworkWriter.WriteUInt64Packed(ConnectedClients[ownerClientId].PlayerObject.NetworkObjectId);
                            nonNullContext.NetworkWriter.WriteUInt64Packed(ownerClientId);

                            //Does not have a parent
                            nonNullContext.NetworkWriter.WriteBool(false);

                            // This is not a scene object
                            nonNullContext.NetworkWriter.WriteBool(false);

                            nonNullContext.NetworkWriter.WriteUInt32Packed(playerPrefabHash ?? NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash);

                            if (ConnectedClients[ownerClientId].PlayerObject.IncludeTransformWhenSpawning == null || ConnectedClients[ownerClientId].PlayerObject.IncludeTransformWhenSpawning(ownerClientId))
                            {
                                nonNullContext.NetworkWriter.WriteBool(true);
                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.x);
                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.y);
                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.z);

                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.x);
                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.y);
                                nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.z);
                            }
                            else
                            {
                                nonNullContext.NetworkWriter.WriteBool(false);
                            }

                            nonNullContext.NetworkWriter.WriteBool(false); //No payload data

                            if (NetworkConfig.EnableNetworkVariable)
                            {
                                ConnectedClients[ownerClientId].PlayerObject.WriteNetworkVariableData(nonNullContext.NetworkWriter.GetStream(), clientPair.Key);
                            }
                        }
                    }
                }
            }
            else
            {
                PendingClients.Remove(ownerClientId);
                NetworkConfig.NetworkTransport.DisconnectRemoteClient(ownerClientId);
            }
        }

        private IInternalMessageHandler CreateMessageHandler()
        {
            IInternalMessageHandler messageHandler = new InternalMessageHandler(this);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            messageHandler = new InternalMessageHandlerProfilingDecorator(messageHandler);
#endif

            return messageHandler;
        }
    }
}
