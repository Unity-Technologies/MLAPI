using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if MULTIPLAYER_TOOLS
using Unity.Multiplayer.Tools;
#endif
using Unity.Profiling;
using UnityEngine.SceneManagement;
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
        /// The current host name we are connected to, used to validate certificate
        /// </summary>
        public string ConnectedHostname { get; private set; }

        internal INetworkMetrics NetworkMetrics { get; private set; }

        internal static event Action OnSingletonReady;

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

            // If the scene is not dirty or the asset database is currently updating then we can skip updating the NetworkPrefab information
            if (!activeScene.isDirty || EditorApplication.isUpdating)
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

#if MULTIPLAYER_TOOLS
            NetworkSolutionInterface.SetInterface(new NetworkSolutionInterfaceParameters
            {
                NetworkObjectProvider = new NetworkObjectProvider(this)
            });
#endif

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

            SnapshotSystem = new SnapshotSystem(this);

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

            // The MessageQueueContainer must be initialized within the Init method ONLY
            // It should ONLY be shutdown and destroyed in the Shutdown method (other than just above)
            MessageQueueContainer = new MessageQueueContainer(this);

            // Register INetworkUpdateSystem (always register this after messageQueueContainer has been instantiated)
            this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);

            // This is used to remove entries not needed or invalid
            var removeEmptyPrefabs = new List<int>();

            // Always clear our prefab override links before building
            NetworkConfig.NetworkPrefabOverrideLinks.Clear();

            // Build the NetworkPrefabOverrideLinks dictionary
            for (int i = 0; i < NetworkConfig.NetworkPrefabs.Count; i++)
            {
                var sourcePrefabGlobalObjectIdHash = (uint)0;
                var targetPrefabGlobalObjectIdHash = (uint)0;
                var networkObject = (NetworkObject)null;
                if (NetworkConfig.NetworkPrefabs[i] == null || (NetworkConfig.NetworkPrefabs[i].Prefab == null && NetworkConfig.NetworkPrefabs[i].Override == NetworkPrefabOverride.None))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogWarning(
                            $"{nameof(NetworkPrefab)} cannot be null ({nameof(NetworkPrefab)} at index: {i})");
                    }

                    removeEmptyPrefabs.Add(i);
                    continue;
                }
                else if (NetworkConfig.NetworkPrefabs[i].Override == NetworkPrefabOverride.None)
                {
                    networkObject = NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>();
                    if (networkObject == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogWarning($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") is missing " +
                                $"a {nameof(NetworkObject)} component (entry will be ignored).");
                        }
                        removeEmptyPrefabs.Add(i);
                        continue;
                    }

                    // Otherwise get the GlobalObjectIdHash value
                    sourcePrefabGlobalObjectIdHash = networkObject.GlobalObjectIdHash;
                }
                else // Validate Overrides
                {
                    // Validate source prefab override values first
                    switch (NetworkConfig.NetworkPrefabs[i].Override)
                    {
                        case NetworkPrefabOverride.Hash:
                            {
                                if (NetworkConfig.NetworkPrefabs[i].SourceHashToOverride == 0)
                                {
                                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                                    {
                                        NetworkLog.LogWarning($"{nameof(NetworkPrefab)} {nameof(NetworkPrefab.SourceHashToOverride)} is zero (entry will be ignored).");
                                    }
                                    removeEmptyPrefabs.Add(i);
                                    continue;
                                }
                                sourcePrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].SourceHashToOverride;
                                break;
                            }
                        case NetworkPrefabOverride.Prefab:
                            {
                                if (NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride == null)
                                {
                                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                                    {
                                        NetworkLog.LogWarning($"{nameof(NetworkPrefab)} {nameof(NetworkPrefab.SourcePrefabToOverride)} is null (entry will be ignored).");
                                    }
                                    Debug.LogWarning($"{nameof(NetworkPrefab)} override entry {NetworkConfig.NetworkPrefabs[i].SourceHashToOverride} will be removed and ignored.");
                                    removeEmptyPrefabs.Add(i);
                                    continue;
                                }
                                else
                                {
                                    networkObject = NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.GetComponent<NetworkObject>();
                                    if (networkObject == null)
                                    {
                                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                                        {
                                            NetworkLog.LogWarning($"{nameof(NetworkPrefab)} ({NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.name}) " +
                                                $"is missing a {nameof(NetworkObject)} component (entry will be ignored).");
                                        }
                                        Debug.LogWarning($"{nameof(NetworkPrefab)} override entry (\"{NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.name}\") will be removed and ignored.");
                                        removeEmptyPrefabs.Add(i);
                                        continue;
                                    }
                                    sourcePrefabGlobalObjectIdHash = networkObject.GlobalObjectIdHash;
                                }
                                break;
                            }
                    }

                    // Validate target prefab override values next
                    if (NetworkConfig.NetworkPrefabs[i].OverridingTargetPrefab == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogWarning($"{nameof(NetworkPrefab)} {nameof(NetworkPrefab.OverridingTargetPrefab)} is null!");
                        }
                        removeEmptyPrefabs.Add(i);
                        switch (NetworkConfig.NetworkPrefabs[i].Override)
                        {
                            case NetworkPrefabOverride.Hash:
                                {
                                    Debug.LogWarning($"{nameof(NetworkPrefab)} override entry {NetworkConfig.NetworkPrefabs[i].SourceHashToOverride} will be removed and ignored.");
                                    break;
                                }
                            case NetworkPrefabOverride.Prefab:
                                {
                                    Debug.LogWarning($"{nameof(NetworkPrefab)} override entry ({NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.name}) will be removed and ignored.");
                                    break;
                                }
                        }
                        continue;
                    }
                    else
                    {
                        targetPrefabGlobalObjectIdHash = NetworkConfig.NetworkPrefabs[i].OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;
                    }
                }

                // Assign the appropriate GlobalObjectIdHash to the appropriate NetworkPrefab
                if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(sourcePrefabGlobalObjectIdHash))
                {
                    if (NetworkConfig.NetworkPrefabs[i].Override == NetworkPrefabOverride.None)
                    {
                        NetworkConfig.NetworkPrefabOverrideLinks.Add(sourcePrefabGlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                    }
                    else
                    {
                        if (!NetworkConfig.OverrideToNetworkPrefab.ContainsKey(targetPrefabGlobalObjectIdHash))
                        {
                            switch (NetworkConfig.NetworkPrefabs[i].Override)
                            {
                                case NetworkPrefabOverride.Prefab:
                                    {
                                        NetworkConfig.NetworkPrefabOverrideLinks.Add(sourcePrefabGlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                                        NetworkConfig.OverrideToNetworkPrefab.Add(targetPrefabGlobalObjectIdHash, sourcePrefabGlobalObjectIdHash);
                                    }
                                    break;
                                case NetworkPrefabOverride.Hash:
                                    {
                                        NetworkConfig.NetworkPrefabOverrideLinks.Add(sourcePrefabGlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                                        NetworkConfig.OverrideToNetworkPrefab.Add(targetPrefabGlobalObjectIdHash, sourcePrefabGlobalObjectIdHash);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            // This can happen if a user tries to make several GlobalObjectIdHash values point to the same target
                            Debug.LogError($"{nameof(NetworkPrefab)} (\"{networkObject.name}\") has a duplicate {nameof(NetworkObject.GlobalObjectIdHash)} target entry value of: {targetPrefabGlobalObjectIdHash}! Removing entry from list!");
                            removeEmptyPrefabs.Add(i);
                        }
                    }
                }
                else
                {
                    // This should never happen, but in the case it somehow does log an error and remove the duplicate entry
                    Debug.LogError($"{nameof(NetworkPrefab)} ({networkObject.name}) has a duplicate {nameof(NetworkObject.GlobalObjectIdHash)} source entry value of: {sourcePrefabGlobalObjectIdHash}! Removing entry from list!");
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

        private void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // Ensures that the NetworkManager is cleaned up before OnDestroy is run on NetworkObjects and NetworkBehaviours when unloading a scene with a NetworkManager
        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == gameObject.scene)
            {
                OnDestroy();
            }
        }

        // Ensures that the NetworkManager is cleaned up before OnDestroy is run on NetworkObjects and NetworkBehaviours when quitting the application.
        private void OnApplicationQuit()
        {
            OnDestroy();
        }

        // Note that this gets also called manually by OnSceneUnloaded and OnApplicationQuit
        private void OnDestroy()
        {
            Shutdown();

            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        /// <summary>
        /// Globally shuts down the library.
        /// Disconnects clients if connected and stops server if running.
        /// </summary>
        public void Shutdown()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Shutdown));
            }

            if (IsServer)
            {
                // make sure all messages are flushed before transport disconnect clients
                if (MessageQueueContainer != null)
                {
                    MessageQueueContainer.ProcessAndFlushMessageQueue(
                        queueType: MessageQueueContainer.MessageQueueProcessingTypes.Send,
                        NetworkUpdateStage.PostLateUpdate); // flushing messages in case transport's disconnect
                }

                var disconnectedIds = new HashSet<ulong>();

                //Don't know if I have to disconnect the clients. I'm assuming the NetworkTransport does all the cleaning on shutdown. But this way the clients get a disconnect message from server (so long it does't get lost)

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
            }

            if (IsClient)
            {
                // Client only, send disconnect to server
                NetworkConfig.NetworkTransport.DisconnectLocalClient();
            }

            IsConnectedClient = false;
            IsServer = false;
            IsClient = false;

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

            NetworkConfig.NetworkTransport.OnTransportEvent -= HandleRawTransportPoll;

            if (SpawnManager != null)
            {
                SpawnManager.DestroyNonSceneObjects();
                SpawnManager.ServerResetShudownStateForSceneObjects();

                SpawnManager = null;
            }

            if (SceneManager != null)
            {
                // Let the NetworkSceneManager clean up its two SceneEvenData instances
                SceneManager.Dispose();
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

            // This is required for handling the potential scenario where multiple NetworkManager instances are created.
            // See MTT-860 for more information
            if (IsListening)
            {
                //The Transport is set during initialization, thus it is possible for the Transport to be null
                NetworkConfig?.NetworkTransport?.Shutdown();
            }

            IsListening = false;
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

            NetworkMetrics.DispatchFrame();
        }

        private void SendConnectionRequest()
        {
            var clientIds = new[] { ServerClientId };
            var context = MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.ConnectionRequest, NetworkChannel.Internal,
                clientIds, NetworkUpdateStage.EarlyUpdate);
            if (context != null)
            {
                using var nonNullContext = (InternalCommandContext)context;
                nonNullContext.NetworkWriter.WriteUInt64Packed(NetworkConfig.GetConfig());

                if (NetworkConfig.ConnectionApproval)
                {
                    nonNullContext.NetworkWriter.WriteByteArray(NetworkConfig.ConnectionData);
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

        private void HandleRawTransportPoll(NetworkEvent networkEvent, ulong clientId, NetworkChannel networkChannel,
            ArraySegment<byte> payload, float receiveTime)
        {
            NetworkMetrics.TrackTransportBytesReceived(payload.Count);

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
                        Shutdown();
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

        internal void HandleIncomingData(ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> data, float receiveTime)
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

            using var messageStream = m_InputBufferWrapper;
            // Client tried to send a network message that was not the connection request before he was accepted.

            if (MessageQueueContainer.IsUsingBatching())
            {
                m_MessageBatcher.ReceiveItems(messageStream, ReceiveCallback, clientId, receiveTime, networkChannel);
            }
            else
            {
                var messageType = (MessageQueueContainer.MessageType)messageStream.ReadByte();
                MessageHandler.MessageReceiveQueueItem(clientId, messageStream, receiveTime, messageType, networkChannel);
                NetworkMetrics.TrackNetworkMessageReceived(clientId, MessageQueueContainer.GetMessageTypeName(messageType), data.Count);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.End();
#endif
        }

        private void ReceiveCallback(NetworkBuffer messageBuffer, MessageQueueContainer.MessageType messageType, ulong clientId,
            float receiveTime, NetworkChannel receiveChannel)
        {
            MessageHandler.MessageReceiveQueueItem(clientId, messageBuffer, receiveTime, messageType, receiveChannel);
            NetworkMetrics.TrackNetworkMessageReceived(clientId, MessageQueueContainer.GetMessageTypeName(messageType), messageBuffer.Length);
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
            using var reader = PooledNetworkReader.Get(item.NetworkBuffer);
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
                s_InvokeRpc.End();
#endif
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
                using var nonNullContext = (InternalCommandContext)context;
                nonNullContext.NetworkWriter.WriteInt32Packed(NetworkTickSystem.ServerTime.Tick);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_SyncTime.End();
#endif
        }

        /// <summary>
        /// Server Side: Handles the approval of a client
        /// </summary>
        /// <param name="ownerClientId">client being approved</param>
        /// <param name="createPlayerObject">whether we want to create a player or not</param>
        /// <param name="playerPrefabHash">the GlobalObjectIdHash value for the Network Prefab to create as the player</param>
        /// <param name="approved">Is the player approved or not?</param>
        /// <param name="position">Used when createPlayerObject is true, position of the player when spawned </param>
        /// <param name="rotation">Used when createPlayerObject is true, rotation of the player when spawned</param>
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

                // Server doesn't send itself the connection approved message
                if (ownerClientId != ServerClientId)
                {
                    var context = MessageQueueContainer.EnterInternalCommandContext(MessageQueueContainer.MessageType.ConnectionApproved, NetworkChannel.Internal,
                        new ulong[] { ownerClientId }, NetworkUpdateStage.EarlyUpdate);

                    if (context != null)
                    {
                        using var nonNullContext = (InternalCommandContext)context;
                        nonNullContext.NetworkWriter.WriteUInt64Packed(ownerClientId);
                        nonNullContext.NetworkWriter.WriteInt32Packed(LocalTime.Tick);

                        // If scene management is disabled, then just serialize all client relative (observed) NetworkObjects
                        if (!NetworkConfig.EnableSceneManagement)
                        {
                            SpawnManager.SerializeObservedNetworkObjects(ownerClientId, nonNullContext.NetworkWriter);
                        }
                    }

                    // If scene management is enabled, then let NetworkSceneManager handle the initial scene and NetworkObject synchronization
                    if (NetworkConfig.EnableSceneManagement)
                    {
                        SceneManager.SynchronizeNetworkObjects(ownerClientId);
                    }
                    else
                    {
                        InvokeOnClientConnectedCallback(ownerClientId);
                    }
                }
                else // Server just adds itself as an observer to all spawned NetworkObjects
                {
                    InvokeOnClientConnectedCallback(ownerClientId);
                }

                if (!createPlayerObject || (playerPrefabHash == null && NetworkConfig.PlayerPrefab == null))
                {
                    return;
                }

                // Separating this into a contained function call for potential further future separation of when this notification is sent.
                ApprovedPlayerSpawn(ownerClientId, playerPrefabHash ?? NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash);
            }
            else
            {
                PendingClients.Remove(ownerClientId);
                NetworkConfig.NetworkTransport.DisconnectRemoteClient(ownerClientId);
            }
        }

        /// <summary>
        /// Spawns the newly approved player
        /// </summary>
        /// <param name="clientId">new player client identifier</param>
        /// <param name="playerPrefabHash">the prefab GlobalObjectIdHash value for this player</param>
        internal void ApprovedPlayerSpawn(ulong clientId, uint playerPrefabHash)
        {
            foreach (KeyValuePair<ulong, NetworkClient> clientPair in ConnectedClients)
            {
                if (clientPair.Key == clientId ||
                    clientPair.Key == ServerClientId || // Server already spawned it
                    ConnectedClients[clientId].PlayerObject == null)
                {
                    continue; //The new client.
                }

                var context = MessageQueueContainer.EnterInternalCommandContext(MessageQueueContainer.MessageType.CreateObject, NetworkChannel.Internal,
                    new[] { clientPair.Key }, NetworkUpdateLoop.UpdateStage);
                if (context != null)
                {
                    using var nonNullContext = (InternalCommandContext)context;
                    nonNullContext.NetworkWriter.WriteBool(true);
                    nonNullContext.NetworkWriter.WriteUInt64Packed(ConnectedClients[clientId].PlayerObject.NetworkObjectId);
                    nonNullContext.NetworkWriter.WriteUInt64Packed(clientId);

                    //Does not have a parent
                    nonNullContext.NetworkWriter.WriteBool(false);

                    // This is not a scene object
                    nonNullContext.NetworkWriter.WriteBool(false);

                    nonNullContext.NetworkWriter.WriteUInt32Packed(playerPrefabHash);

                    if (ConnectedClients[clientId].PlayerObject.IncludeTransformWhenSpawning == null || ConnectedClients[clientId].PlayerObject.IncludeTransformWhenSpawning(clientId))
                    {
                        nonNullContext.NetworkWriter.WriteBool(true);
                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.position.x);
                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.position.y);
                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.position.z);

                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.x);
                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.y);
                        nonNullContext.NetworkWriter.WriteSinglePacked(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.z);
                    }
                    else
                    {
                        nonNullContext.NetworkWriter.WriteBool(false);
                    }

                    nonNullContext.NetworkWriter.WriteBool(false); //No payload data

                    if (NetworkConfig.EnableNetworkVariable)
                    {
                        ConnectedClients[clientId].PlayerObject.WriteNetworkVariableData(nonNullContext.NetworkWriter.GetStream(), clientPair.Key);
                    }
                }
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
