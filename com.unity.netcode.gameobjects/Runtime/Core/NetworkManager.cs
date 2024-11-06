using System;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode
{
    /// <summary>
    /// The main component of the library
    /// </summary>
    [AddComponentMenu("Netcode/Network Manager", -100)]
    public class NetworkManager : MonoBehaviour, INetworkUpdateSystem
    {
        /// <summary>
        /// Subscribe to this static event to get notifications when a <see cref="NetworkManager"/> instance has been instantiated.
        /// </summary>
        public static event Action<NetworkManager> OnInstantiated;

        /// <summary>
        /// Subscribe to this static event to get notifications when a <see cref="NetworkManager"/> instance is being destroyed.
        /// </summary>
        public static event Action<NetworkManager> OnDestroying;


#if UNITY_EDITOR
        // Inspector view expand/collapse settings for this derived child class
        [HideInInspector]
        public bool NetworkManagerExpanded;
#endif

        // TODO: Deprecate...
        // The following internal values are not used, but because ILPP makes them public in the assembly, they cannot
        // be removed thanks to our semver validation.
#pragma warning disable IDE1006 // disable naming rule violation check

        // RuntimeAccessModifiersILPP will make this `public`
        internal delegate void RpcReceiveHandler(NetworkBehaviour behaviour, FastBufferReader reader, __RpcParams parameters);

        // RuntimeAccessModifiersILPP will make this `public`
        internal static readonly Dictionary<uint, RpcReceiveHandler> __rpc_func_table = new Dictionary<uint, RpcReceiveHandler>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR || UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE
        // RuntimeAccessModifiersILPP will make this `public`
        internal static readonly Dictionary<uint, string> __rpc_name_table = new Dictionary<uint, string>();
#endif

#pragma warning restore IDE1006 // restore naming rule violation check

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static List<Type> s_SerializedType = new List<Type>();
        // This is used to control the serialized type not optimized messaging for integration test purposes
        internal static bool DisableNotOptimizedSerializedType;
        /// <summary>
        /// Until all serialized types are optimized for the distributed authority network topology,
        /// this will handle the notification to the user that the type being serialized is not yet
        /// optimized but will only log the message once to prevent log spamming.
        /// </summary>
        internal static void LogSerializedTypeNotOptimized<T>()
        {
            if (DisableNotOptimizedSerializedType)
            {
                return;
            }
            var type = typeof(T);
            if (!s_SerializedType.Contains(type))
            {
                s_SerializedType.Add(type);
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    Debug.LogWarning($"[{type.Name}] Serialized type has not been optimized for use with Distributed Authority!");
                }
            }
        }
#endif

        internal static bool IsDistributedAuthority;

        /// <summary>
        /// Distributed Authority Mode
        /// Returns true if the current session is running in distributed authority mode.
        /// </summary>
        public bool DistributedAuthorityMode { get; private set; }

        /// <summary>
        /// Distributed Authority Mode
        /// Gets whether the NetworkManager is connected to a distributed authority state service.
        /// <see cref="NetworkClient.DAHost"/> to determine if the instance is mocking the state service.
        /// </summary>
        public bool CMBServiceConnection
        {
            get
            {
                return NetworkConfig.UseCMBService;
            }
        }

        /// <summary>
        /// Distributed Authority Mode
        /// When enabled, the player prefab will be automatically spawned on the newly connected client-side.
        /// </summary>
        /// <remarks>
        /// Refer to <see cref="NetworkConfig.AutoSpawnPlayerPrefabClientSide"/> to enable/disable automatic spawning of the player prefab.
        /// Alternately, override the <see cref="FetchLocalPlayerPrefabToSpawn"/> to control what prefab the player should spawn.
        /// </remarks>
        public bool AutoSpawnPlayerPrefabClientSide
        {
            get
            {
                return NetworkConfig.AutoSpawnPlayerPrefabClientSide;
            }
        }

        /// <summary>
        /// Distributed Authority Mode
        /// Delegate definition for <see cref="FetchLocalPlayerPrefabToSpawn"/>
        /// </summary>
        /// <returns>Player Prefab <see cref="GameObject"/></returns>
        public delegate GameObject OnFetchLocalPlayerPrefabToSpawnDelegateHandler();

        /// <summary>
        /// Distributed Authority Mode
        /// When a callback is assigned, this provides control over what player prefab a client will be using.
        /// This is invoked only when <see cref="NetworkConfig.AutoSpawnPlayerPrefabClientSide"/> is enabled.
        /// </summary>
        public OnFetchLocalPlayerPrefabToSpawnDelegateHandler OnFetchLocalPlayerPrefabToSpawn;

        internal GameObject FetchLocalPlayerPrefabToSpawn()
        {
            if (!AutoSpawnPlayerPrefabClientSide)
            {
                Debug.LogError($"[{nameof(FetchLocalPlayerPrefabToSpawn)}] Invoked when {nameof(NetworkConfig.AutoSpawnPlayerPrefabClientSide)} was not set! Check call paths!");
                return null;
            }
            if (OnFetchLocalPlayerPrefabToSpawn == null && NetworkConfig.PlayerPrefab == null)
            {
                return null;
            }

            if (OnFetchLocalPlayerPrefabToSpawn != null)
            {
                return OnFetchLocalPlayerPrefabToSpawn();
            }
            return NetworkConfig.PlayerPrefab;
        }

        /// <summary>
        /// Distributed Authority Mode
        /// Gets whether the current NetworkManager is running as a mock distributed authority state service (DAHost)
        /// </summary>
        public bool DAHost
        {
            get
            {
                return LocalClient.DAHost;
            }
        }

        // DANGO-TODO-MVP: Remove these properties once the service handles object distribution
        internal ulong ClientToRedistribute;
        internal bool RedistributeToClient;
        internal int TickToRedistribute;

        internal List<NetworkObject> DeferredDespawnObjects = new List<NetworkObject>();

        public ulong CurrentSessionOwner { get; internal set; }

        /// <summary>
        /// Delegate declaration for <see cref="OnSessionOwnerPromoted"/>
        /// </summary>
        /// <param name="sessionOwnerPromoted">the new session owner client identifier</param>
        public delegate void OnSessionOwnerPromotedDelegateHandler(ulong sessionOwnerPromoted);

        /// <summary>
        /// Network Topology: Distributed Authority
        /// When a new session owner is promoted, this event is triggered on all connected clients
        /// </summary>
        public event OnSessionOwnerPromotedDelegateHandler OnSessionOwnerPromoted;

        internal void SetSessionOwner(ulong sessionOwner)
        {
            var previousSessionOwner = CurrentSessionOwner;
            CurrentSessionOwner = sessionOwner;
            LocalClient.IsSessionOwner = LocalClientId == sessionOwner;
            if (LocalClient.IsSessionOwner)
            {
                foreach (var networkObjectEntry in SpawnManager.SpawnedObjects)
                {
                    var networkObject = networkObjectEntry.Value;
                    if (networkObject.IsSceneObject == null || !networkObject.IsSceneObject.Value)
                    {
                        continue;
                    }
                    if (networkObject.OwnerClientId != LocalClientId)
                    {
                        SpawnManager.ChangeOwnership(networkObject, LocalClientId, true);
                    }
                }
            }

            OnSessionOwnerPromoted?.Invoke(sessionOwner);
        }

        internal void PromoteSessionOwner(ulong clientId)
        {
            if (!DistributedAuthorityMode)
            {
                NetworkLog.LogErrorServer($"[SceneManagement][NotDA] Invoking promote session owner while not in distributed authority mode!");
                return;
            }
            if (!DAHost)
            {
                NetworkLog.LogErrorServer($"[SceneManagement][NotDAHost] Client is attempting to promote another client as the session owner!");
                return;
            }
            SetSessionOwner(clientId);
            var sessionOwnerMessage = new SessionOwnerMessage()
            {
                SessionOwner = clientId,
            };
            var clients = ConnectionManager.ConnectedClientIds.Where(c => c != LocalClientId).ToArray();
            foreach (var targetClient in clients)
            {
                ConnectionManager.SendMessage(ref sessionOwnerMessage, NetworkDelivery.ReliableSequenced, targetClient);
            }
        }

        internal Dictionary<ulong, NetworkObject> NetworkTransformUpdate = new Dictionary<ulong, NetworkObject>();
#if COM_UNITY_MODULES_PHYSICS
        internal Dictionary<ulong, NetworkObject> NetworkTransformFixedUpdate = new Dictionary<ulong, NetworkObject>();
#endif

        internal void NetworkTransformRegistration(NetworkObject networkObject, bool onUpdate = true, bool register = true)
        {
            if (onUpdate)
            {
                if (register)
                {
                    if (!NetworkTransformUpdate.ContainsKey(networkObject.NetworkObjectId))
                    {
                        NetworkTransformUpdate.Add(networkObject.NetworkObjectId, networkObject);
                    }
                }
                else
                {
                    NetworkTransformUpdate.Remove(networkObject.NetworkObjectId);
                }
            }
#if COM_UNITY_MODULES_PHYSICS
            else
            {
                if (register)
                {
                    if (!NetworkTransformFixedUpdate.ContainsKey(networkObject.NetworkObjectId))
                    {
                        NetworkTransformFixedUpdate.Add(networkObject.NetworkObjectId, networkObject);
                    }
                }
                else
                {
                    NetworkTransformFixedUpdate.Remove(networkObject.NetworkObjectId);
                }
            }
#endif
        }

        private void UpdateTopology()
        {
            var transportTopology = IsListening ? NetworkConfig.NetworkTransport.CurrentTopology() : NetworkConfig.NetworkTopology;
            if (transportTopology != NetworkConfig.NetworkTopology)
            {
                NetworkLog.LogErrorServer($"[Topology Mismatch] Transport detected an issue with the topology ({transportTopology} | {NetworkConfig.NetworkTopology}) usage or setting! Disconnecting from session.");
                Shutdown();
            }
            else
            {
                IsDistributedAuthority = DistributedAuthorityMode = transportTopology == NetworkTopologyTypes.DistributedAuthority;
            }
        }

        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            switch (updateStage)
            {
                case NetworkUpdateStage.EarlyUpdate:
                    {
                        UpdateTopology();
                        ConnectionManager.ProcessPendingApprovals();
                        ConnectionManager.PollAndHandleNetworkEvents();

                        DeferredMessageManager.ProcessTriggers(IDeferredNetworkMessageManager.TriggerType.OnNextFrame, 0);

                        AnticipationSystem.SetupForUpdate();
                        MessageManager.ProcessIncomingMessageQueue();
                        MessageManager.CleanupDisconnectedClients();
                        AnticipationSystem.ProcessReanticipation();
                    }
                    break;
#if COM_UNITY_MODULES_PHYSICS
                case NetworkUpdateStage.FixedUpdate:
                    {
                        foreach (var networkObjectEntry in NetworkTransformFixedUpdate)
                        {
                            // if not active or not spawned then skip
                            if (!networkObjectEntry.Value.gameObject.activeInHierarchy || !networkObjectEntry.Value.IsSpawned)
                            {
                                continue;
                            }

                            foreach (var networkTransformEntry in networkObjectEntry.Value.NetworkTransforms)
                            {
                                // only update if enabled
                                if (networkTransformEntry.enabled)
                                {
                                    networkTransformEntry.OnFixedUpdate();
                                }
                            }
                        }
                    }
                    break;
#endif
                case NetworkUpdateStage.PreUpdate:
                    {
                        NetworkTimeSystem.UpdateTime();
                        AnticipationSystem.Update();
                    }
                    break;
                case NetworkUpdateStage.PreLateUpdate:
                    {
                        // Non-physics based non-authority NetworkTransforms update their states after all other components
                        foreach (var networkObjectEntry in NetworkTransformUpdate)
                        {
                            // if not active or not spawned then skip
                            if (!networkObjectEntry.Value.gameObject.activeInHierarchy || !networkObjectEntry.Value.IsSpawned)
                            {
                                continue;
                            }

                            foreach (var networkTransformEntry in networkObjectEntry.Value.NetworkTransforms)
                            {
                                // only update if enabled
                                if (networkTransformEntry.enabled)
                                {
                                    networkTransformEntry.OnUpdate();
                                }
                            }
                        }
                    }
                    break;
                case NetworkUpdateStage.PostScriptLateUpdate:
                    {
                        AnticipationSystem.Sync();
                        AnticipationSystem.SetupForRender();
                    }
                    break;
                case NetworkUpdateStage.PostLateUpdate:
                    {
                        // Handle deferred despawning
                        if (DistributedAuthorityMode)
                        {
                            SpawnManager.DeferredDespawnUpdate(ServerTime);
                        }

                        // Update any NetworkObject's registered to notify of scene migration changes.
                        NetworkObject.UpdateNetworkObjectSceneChanges();

                        // This should be invoked just prior to the MessageManager processes its outbound queue.
                        SceneManager.CheckForAndSendNetworkObjectSceneChanged();

                        // Process outbound messages
                        MessageManager.ProcessSendQueues();

                        // Metrics update needs to be driven by NetworkConnectionManager's update to assure metrics are dispatched after the send queue is processed.
                        MetricsManager.UpdateMetrics();

                        // TODO: Determine a better way to handle this
                        NetworkObject.VerifyParentingStatus();

                        // This is "ok" to invoke when not processing messages since it is just cleaning up messages that never got handled within their timeout period.
                        DeferredMessageManager.CleanupStaleTriggers();

                        // DANGO-TODO-MVP: Remove this once the service handles object distribution
                        // NOTE: This needs to be the last thing done and should happen exactly at this point
                        // in the update
                        if (RedistributeToClient && ServerTime.Tick <= TickToRedistribute)
                        {
                            RedistributeToClient = false;
                            SpawnManager.DistributeNetworkObjects(ClientToRedistribute);
                            ClientToRedistribute = 0;
                        }

                        if (m_ShuttingDown)
                        {
                            // Host-server will disconnect any connected clients prior to finalizing its shutdown
                            if (IsServer)
                            {
                                ProcessServerShutdown();
                            }
                            else
                            {
                                // Clients just disconnect immediately
                                ShutdownInternal();
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Used to provide a graceful shutdown sequence
        /// </summary>
        internal enum ServerShutdownStates
        {
            None,
            WaitForClientDisconnects,
            InternalShutdown,
            ShuttingDown,
        };

        internal ServerShutdownStates ServerShutdownState;
        private float m_ShutdownTimeout;

        /// <summary>
        /// This is a "soft shutdown" where the host or server will disconnect
        /// all clients, with a provided reasons, prior to invoking its final
        /// internal shutdown.
        /// </summary>
        internal void ProcessServerShutdown()
        {
            var minClientCount = IsHost ? 2 : 1;
            switch (ServerShutdownState)
            {
                case ServerShutdownStates.None:
                    {
                        if (ConnectedClients.Count >= minClientCount)
                        {
                            var hostServer = IsHost ? "host" : "server";
                            var disconnectReason = $"Disconnected due to {hostServer} shutting down.";
                            for (int i = ConnectedClientsIds.Count - 1; i >= 0; i--)
                            {
                                var clientId = ConnectedClientsIds[i];
                                if (clientId == ServerClientId)
                                {
                                    continue;
                                }
                                ConnectionManager.DisconnectClient(clientId, disconnectReason);
                            }
                            ServerShutdownState = ServerShutdownStates.WaitForClientDisconnects;
                            m_ShutdownTimeout = Time.realtimeSinceStartup + 5.0f;
                        }
                        else
                        {
                            ServerShutdownState = ServerShutdownStates.InternalShutdown;
                            ProcessServerShutdown();
                        }
                        break;
                    }
                case ServerShutdownStates.WaitForClientDisconnects:
                    {
                        if (ConnectedClients.Count < minClientCount || m_ShutdownTimeout < Time.realtimeSinceStartup)
                        {
                            ServerShutdownState = ServerShutdownStates.InternalShutdown;
                            ProcessServerShutdown();
                        }
                        break;
                    }
                case ServerShutdownStates.InternalShutdown:
                    {
                        ServerShutdownState = ServerShutdownStates.ShuttingDown;
                        ShutdownInternal();
                        break;
                    }
            }
        }

        /// <summary>
        /// The client id used to represent the server
        /// </summary>
        public const ulong ServerClientId = 0;

        /// <summary>
        /// Returns ServerClientId if IsServer or LocalClientId if not
        /// </summary>
        public ulong LocalClientId
        {
            get => ConnectionManager.LocalClient.ClientId;
            internal set => ConnectionManager.LocalClient.ClientId = value;
        }

        /// <summary>
        /// Gets a dictionary of connected clients and their clientId keys.
        /// </summary>
        public IReadOnlyDictionary<ulong, NetworkClient> ConnectedClients => ConnectionManager.ConnectedClients;

        /// <summary>
        /// Gets a list of connected clients.
        /// </summary>
        public IReadOnlyList<NetworkClient> ConnectedClientsList => ConnectionManager.ConnectedClientsList;

        /// <summary>
        /// Gets a list of just the IDs of all connected clients.
        /// </summary>
        public IReadOnlyList<ulong> ConnectedClientsIds => ConnectionManager.ConnectedClientIds;

        /// <summary>
        /// Gets the local <see cref="NetworkClient"/> for this client.
        /// </summary>
        public NetworkClient LocalClient => ConnectionManager.LocalClient;

        /// <summary>
        /// Gets a dictionary of the clients that have been accepted by the transport but are still pending by the Netcode. This is only populated on the server.
        /// </summary>
        // See NetworkConnectionManager.AddPendingClient and NetworkConnectionManager.RemovePendingClient to see how this is now populated
        public readonly Dictionary<ulong, PendingClient> PendingClients = new Dictionary<ulong, PendingClient>();

        /// <summary>
        /// Gets Whether or not a server is running
        /// </summary>
        public bool IsServer => ConnectionManager.LocalClient.IsServer;

        /// <summary>
        /// Gets whether or not the current server (local or remote) is a host - i.e., also a client
        /// </summary>
        public bool ServerIsHost => ConnectionManager.ConnectedClientIds.Contains(ServerClientId);

        /// <summary>
        /// Gets Whether or not a client is running
        /// </summary>
        public bool IsClient => ConnectionManager.LocalClient.IsClient;

        /// <summary>
        /// Gets if we are running as host
        /// </summary>
        public bool IsHost => ConnectionManager.LocalClient.IsHost;

        /// <summary>
        /// When disconnected from the server, the server may send a reason. If a reason was sent, this property will
        /// tell client code what the reason was. It should be queried after the OnClientDisconnectCallback is called
        /// </summary>
        public string DisconnectReason => ConnectionManager.DisconnectReason;

        /// <summary>
        /// Is true when a server or host is listening for connections.
        /// Is true when a client is connecting or connected to a network session.
        /// Is false when not listening, connecting, or connected.
        /// </summary>
        public bool IsListening
        {
            get => ConnectionManager.IsListening;
            internal set => ConnectionManager.IsListening = value;
        }

        /// <summary>
        /// When true, the client is connected, approved, and synchronized with
        /// the server.
        /// <see cref="NetworkClient.IsConnected"/> <br />
        /// <see cref="NetworkClient.IsApproved"/> <br />
        /// </summary>
        public bool IsConnectedClient
        {
            get => ConnectionManager.LocalClient.IsConnected;
            internal set => ConnectionManager.LocalClient.IsConnected = value;
        }

        /// <summary>
        /// Is true when the client has been approved.
        /// </summary>
        /// <remarks>
        /// This only reflects the client's approved status and does not mean the client
        /// has finished the connection and synchronization process. The server-host will
        /// always be approved upon being starting the <see cref="NetworkManager"/>
        /// <see cref="NetworkClient.IsConnectedClient"/>
        /// </remarks>
        public bool IsApproved
        {
            get => ConnectionManager.LocalClient.IsApproved;
            internal set => ConnectionManager.LocalClient.IsApproved = value;
        }

        /// <summary>
        /// The callback to invoke if the <see cref="NetworkTransport"/> fails.
        /// </summary>
        /// <remarks>
        /// A failure of the transport is always followed by the <see cref="NetworkManager"/> shutting down. Recovering
        /// from a transport failure would normally entail reconfiguring the transport (e.g. re-authenticating, or
        /// recreating a new service allocation depending on the transport) and restarting the client/server/host.
        /// </remarks>
        public event Action OnTransportFailure
        {
            add => ConnectionManager.OnTransportFailure += value;
            remove => ConnectionManager.OnTransportFailure -= value;
        }

        public delegate void ReanticipateDelegate(double lastRoundTripTime);

        /// <summary>
        /// This callback is called after all individual OnReanticipate calls on AnticipatedNetworkVariable
        /// and AnticipatedNetworkTransform values have been invoked. The first parameter is a hash set of
        /// all the variables that have been changed on this frame (you can detect a particular variable by
        /// checking if the set contains it), while the second parameter is a set of all anticipated network
        /// transforms that have been changed. Both are passed as their base class type.
        ///
        /// The third parameter is the local time corresponding to the current authoritative server state
        /// (i.e., to determine the amount of time that needs to be re-simulated, you will use
        /// NetworkManager.LocalTime.Time - authorityTime).
        /// </summary>
        public event ReanticipateDelegate OnReanticipate
        {
            add => AnticipationSystem.OnReanticipate += value;
            remove => AnticipationSystem.OnReanticipate -= value;
        }

        /// <summary>
        /// The callback to invoke during connection approval. Allows client code to decide whether or not to allow incoming client connection
        /// </summary>
        public Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback
        {
            get => ConnectionManager.ConnectionApprovalCallback;
            set
            {
                if (value != null && value.GetInvocationList().Length > 1)
                {
                    throw new InvalidOperationException($"Only one {nameof(ConnectionApprovalCallback)} can be registered at a time.");
                }

                ConnectionManager.ConnectionApprovalCallback = value;
            }
        }

        /// <summary>
        /// The callback to invoke once a client connects. This callback is only ran on the server and on the local client that connects.
        ///
        /// It is recommended to use OnConnectionEvent instead, as this will eventually be deprecated
        /// </summary>
        public event Action<ulong> OnClientConnectedCallback
        {
            add => ConnectionManager.OnClientConnectedCallback += value;
            remove => ConnectionManager.OnClientConnectedCallback -= value;
        }

        /// <summary>
        /// The callback to invoke when a client disconnects. This callback is only ran on the server and on the local client that disconnects.
        ///
        /// It is recommended to use OnConnectionEvent instead, as this will eventually be deprecated
        /// </summary>
        public event Action<ulong> OnClientDisconnectCallback
        {
            add => ConnectionManager.OnClientDisconnectCallback += value;
            remove => ConnectionManager.OnClientDisconnectCallback -= value;
        }

        /// <summary>
        /// The callback to invoke on any connection event. See <see cref="ConnectionEvent"/> and <see cref="ConnectionEventData"/>
        /// for more info.
        /// </summary>
        public event Action<NetworkManager, ConnectionEventData> OnConnectionEvent
        {
            add => ConnectionManager.OnConnectionEvent += value;
            remove => ConnectionManager.OnConnectionEvent -= value;
        }

        /// <summary>
        /// The current host name we are connected to, used to validate certificate
        /// </summary>
        public string ConnectedHostname => string.Empty;

        /// <summary>
        /// Connection Approval Response
        /// </summary>
        public class ConnectionApprovalResponse
        {
            /// <summary>
            /// Whether or not the client was approved
            /// </summary>
            public bool Approved;

            /// <summary>
            /// If true, a player object will be created. Otherwise the client will have no object.
            /// </summary>
            public bool CreatePlayerObject;

            /// <summary>
            /// The prefabHash to use for the client. If createPlayerObject is false, this is ignored. If playerPrefabHash is null, the default player prefab is used.
            /// </summary>
            public uint? PlayerPrefabHash;

            /// <summary>
            /// The position to spawn the client at. If null, the prefab position is used.
            /// </summary>
            public Vector3? Position;

            /// <summary>
            /// The rotation to spawn the client with. If null, the prefab position is used.
            /// </summary>
            public Quaternion? Rotation;

            /// <summary>
            /// If the Approval decision cannot be made immediately, the client code can set Pending to true, keep a reference to the ConnectionApprovalResponse object and write to it later. Client code must exercise care to setting all the members to the value it wants before marking Pending to false, to indicate completion. If the field is set as Pending = true, we'll monitor the object until it gets set to not pending anymore and use the parameters then.
            /// </summary>
            public bool Pending;

            /// <summary>
            /// Optional reason. If Approved is false, this reason will be sent to the client so they know why they
            /// were not approved.
            /// </summary>
            public string Reason;
        }

        /// <summary>
        /// Connection Approval Request
        /// </summary>
        public struct ConnectionApprovalRequest
        {
            /// <summary>
            /// The connection data payload
            /// </summary>
            public byte[] Payload;

            /// <summary>
            /// The Network Id of the client we are about to handle
            /// </summary>
            public ulong ClientNetworkId;
        }

        /// <summary>
        /// Can be used to determine if the <see cref="NetworkManager"/> is currently shutting itself down
        /// </summary>
        public bool ShutdownInProgress => m_ShuttingDown;

        private bool m_ShuttingDown;

        /// <summary>
        /// The current netcode project configuration
        /// </summary>
        [HideInInspector]
        public NetworkConfig NetworkConfig;

        /// <summary>
        /// The local <see cref="NetworkTime"/>
        /// </summary>
        public NetworkTime LocalTime => NetworkTickSystem?.LocalTime ?? default;

        /// <summary>
        /// The <see cref="NetworkTime"/> on the server
        /// </summary>
        public NetworkTime ServerTime => NetworkTickSystem?.ServerTime ?? default;

        /// <summary>
        /// Gets or sets if the application should be set to run in background
        /// </summary>
        [HideInInspector]
        public bool RunInBackground = true;

        /// <summary>
        /// The log level to use
        /// </summary>
        [HideInInspector]
        public LogLevel LogLevel = LogLevel.Normal;

        /// <summary>
        /// The singleton instance of the NetworkManager
        /// </summary>
        public static NetworkManager Singleton { get; private set; }

        internal static event Action OnSingletonReady;

        /// <summary>
        /// This callback is invoked when the local server is started and listening for incoming connections.
        /// </summary>
        public event Action OnServerStarted = null;

        /// <summary>
        /// The callback to invoke once the local client is ready
        /// </summary>
        public event Action OnClientStarted = null;

        /// <summary>
        /// This callback is invoked once the local server is stopped.
        /// </summary>
        /// <param name="arg1">The first parameter of this event will be set to <see cref="true"/> when stopping a host instance and <see cref="false"/> when stopping a server instance.</param>
        public event Action<bool> OnServerStopped = null;

        /// <summary>
        /// The callback to invoke once the local client stops
        /// </summary>
        /// <remarks>The parameter states whether the client was running in host mode</remarks>
        /// <param name="arg1">The first parameter of this event will be set to <see cref="true"/> when stopping the host client and <see cref="false"/> when stopping a standard client instance.</param>
        public event Action<bool> OnClientStopped = null;

        /// <summary>
        /// The <see cref="NetworkPrefabHandler"/> instance created after starting the <see cref="NetworkManager"/>
        /// </summary>
        public NetworkPrefabHandler PrefabHandler
        {
            get
            {
                if (m_PrefabHandler == null)
                {
                    m_PrefabHandler = new NetworkPrefabHandler();
                    m_PrefabHandler.Initialize(this);
                }

                return m_PrefabHandler;
            }
        }

        private NetworkPrefabHandler m_PrefabHandler;

        /// <summary>
        /// Gets the SpawnManager for this NetworkManager
        /// </summary>
        public NetworkSpawnManager SpawnManager { get; private set; }

        internal IDeferredNetworkMessageManager DeferredMessageManager { get; private set; }

        // This erroneously tries to simplify these method references but the docs do not pick it up correctly
        // because they try to resolve it on the field rather than the class of the same name.
#pragma warning disable IDE0001
        /// <summary>
        /// Provides access to the various <see cref="SendTo"/> targets at runtime, as well as
        /// runtime-bound targets like <see cref="Unity.Netcode.RpcTarget.Single"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Group(NativeArray{ulong})"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Group(NativeList{ulong})"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Group(ulong[])"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Group{T}(T)"/>, <see cref="Unity.Netcode.RpcTarget.Not(ulong)"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Not(NativeArray{ulong})"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Not(NativeList{ulong})"/>,
        /// <see cref="Unity.Netcode.RpcTarget.Not(ulong[])"/>, and
        /// <see cref="Unity.Netcode.RpcTarget.Not{T}(T)"/>
        /// </summary>
#pragma warning restore IDE0001
        public RpcTarget RpcTarget;

        /// <summary>
        /// Gets the CustomMessagingManager for this NetworkManager
        /// </summary>
        public CustomMessagingManager CustomMessagingManager { get; private set; }

        /// <summary>
        /// The <see cref="NetworkSceneManager"/> instance created after starting the <see cref="NetworkManager"/>
        /// </summary>
        public NetworkSceneManager SceneManager { get; private set; }

        internal NetworkBehaviourUpdater BehaviourUpdater { get; set; }

        /// <summary>
        /// Accessor property for the <see cref="NetworkTimeSystem"/> of the NetworkManager.
        /// Prefer the use of the LocalTime and ServerTime properties
        /// </summary>
        public NetworkTimeSystem NetworkTimeSystem { get; private set; }

        /// <summary>
        /// Accessor property for the <see cref="NetworkTickSystem"/> of the NetworkManager.
        /// </summary>
        public NetworkTickSystem NetworkTickSystem { get; private set; }

        internal AnticipationSystem AnticipationSystem { get; private set; }

        /// <summary>
        /// Used for time mocking in tests
        /// </summary>
        internal IRealTimeProvider RealTimeProvider { get; private set; }

        internal INetworkMetrics NetworkMetrics => MetricsManager.NetworkMetrics;
        internal NetworkMetricsManager MetricsManager = new NetworkMetricsManager();
        internal NetworkConnectionManager ConnectionManager = new NetworkConnectionManager();
        internal NetworkMessageManager MessageManager = null;

        internal struct Override<T>
        {
            private T m_Value;
            public bool Overidden { get; private set; }
            internal T Value
            {
                get { return Overidden ? m_Value : default(T); }
                set { Overidden = true; m_Value = value; }
            }
        };

        internal Override<ushort> PortOverride;


#if UNITY_EDITOR
        internal static INetworkManagerHelper NetworkManagerHelper;

        /// <summary>
        /// Interface for NetworkManagerHelper
        /// </summary>
        internal interface INetworkManagerHelper
        {
            bool NotifyUserOfNestedNetworkManager(NetworkManager networkManager, bool ignoreNetworkManagerCache = false, bool editorTest = false);
            void CheckAndNotifyUserNetworkObjectRemoved(NetworkManager networkManager, bool editorTest = false);
        }

        internal delegate void ResetNetworkManagerDelegate(NetworkManager manager);

        internal static ResetNetworkManagerDelegate OnNetworkManagerReset;

        private void Reset()
        {
            OnNetworkManagerReset?.Invoke(this);
        }

        protected virtual void OnValidateComponent()
        {

        }

        private PackageInfo GetPackageInfo(string packageName)
        {
            return AssetDatabase.FindAssets("package").Select(AssetDatabase.GUIDToAssetPath).Where(x => AssetDatabase.LoadAssetAtPath<TextAsset>(x) != null).Select(PackageInfo.FindForAssetPath).Where(x => x != null).First(x => x.name == packageName);
        }

        internal void OnValidate()
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
            NetworkConfig.Prefabs.NetworkPrefabOverrideLinks.Clear();

            var prefabs = NetworkConfig.Prefabs.Prefabs;
            // Check network prefabs and assign to dictionary for quick look up
            for (int i = 0; i < prefabs.Count; i++)
            {
                var networkPrefab = prefabs[i];
                var networkPrefabGo = networkPrefab?.Prefab;
                if (networkPrefabGo == null)
                {
                    continue;
                }

                var networkObject = networkPrefabGo.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogError($"Cannot register {NetworkPrefabHandler.PrefabDebugHelper(networkPrefab)}, it does not have a {nameof(NetworkObject)} component at its root");
                    }

                    continue;
                }

                {
                    var childNetworkObjects = new List<NetworkObject>();
                    networkPrefabGo.GetComponentsInChildren(true, childNetworkObjects);
                    if (childNetworkObjects.Count > 1) // total count = 1 root NetworkObject + n child NetworkObjects
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning($"{NetworkPrefabHandler.PrefabDebugHelper(networkPrefab)} has child {nameof(NetworkObject)}(s) but they will not be spawned across the network (unsupported {nameof(NetworkPrefab)} setup)");
                        }
                    }
                }
            }

            try
            {
                OnValidateComponent();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void ModeChanged(PlayModeStateChange change)
        {
            if (IsListening && change == PlayModeStateChange.ExitingPlayMode)
            {
                // Make sure we are not holding onto anything in case domain reload is disabled
                ShutdownInternal();
            }
        }
#endif

        /// <summary>
        /// Determines if the NetworkManager's GameObject is parented under another GameObject and
        /// notifies the user that this is not allowed for the NetworkManager.
        /// </summary>
        internal bool NetworkManagerCheckForParent(bool ignoreNetworkManagerCache = false)
        {
#if UNITY_EDITOR
            var isParented = NetworkManagerHelper.NotifyUserOfNestedNetworkManager(this, ignoreNetworkManagerCache);
#else
            var isParented = transform.root != transform;
            if (isParented)
            {
                throw new Exception(GenerateNestedNetworkManagerMessage(transform));
            }
#endif
            return isParented;
        }

        internal static string GenerateNestedNetworkManagerMessage(Transform transform)
        {
            return $"{transform.name} is nested under {transform.root.name}. NetworkManager cannot be nested.\n";
        }

        /// <summary>
        /// Handle runtime detection for parenting the NetworkManager's GameObject under another GameObject
        /// </summary>
        private void OnTransformParentChanged()
        {
            NetworkManagerCheckForParent();
        }

        /// <summary>
        /// Set this NetworkManager instance as the static NetworkManager singleton
        /// </summary>
        public void SetSingleton()
        {
            Singleton = this;

            OnSingletonReady?.Invoke();
        }

        private void Awake()
        {
            NetworkConfig?.InitializePrefabs();

            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += ModeChanged;
#endif
            // Notify we have instantiated a new instance of NetworkManager.
            OnInstantiated?.Invoke(this);
        }

        private void OnEnable()
        {
            if (RunInBackground)
            {
                Application.runInBackground = true;
            }

            if (Singleton == null)
            {
                SetSingleton();
            }

            if (!NetworkManagerCheckForParent())
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// <see cref="NetworkPrefabHandler.GetNetworkPrefabOverride(GameObject)"/>
        /// </summary>
        /// <param name="gameObject">the <see cref="GameObject"/> to be checked for a <see cref="NetworkManager"/> defined NetworkPrefab override</param>
        /// <returns>a <see cref="GameObject"/> that is either the override or if no overrides exist it returns the same as the one passed in as a parameter</returns>
        public GameObject GetNetworkPrefabOverride(GameObject gameObject) => PrefabHandler.GetNetworkPrefabOverride(gameObject);

        /// <summary>
        /// <see cref="NetworkPrefabHandler.AddNetworkPrefab(GameObject)"/>
        /// </summary>
        /// <param name="prefab"></param>
        /// <exception cref="Exception"></exception>
        public void AddNetworkPrefab(GameObject prefab) => PrefabHandler.AddNetworkPrefab(prefab);

        /// <summary>
        /// <see cref="NetworkPrefabHandler.RemoveNetworkPrefab(GameObject)"/>
        /// </summary>
        /// <param name="prefab"></param>
        public void RemoveNetworkPrefab(GameObject prefab) => PrefabHandler.RemoveNetworkPrefab(prefab);

        /// <summary>
        /// Sets the maximum size of a single non-fragmented message (or message batch) passed through the transport.
        /// This should represent the transport's default MTU size, minus any transport-level overhead.
        /// This value will be used for any remote endpoints that haven't had per-endpoint MTUs set.
        /// This value is also used as the size of the temporary buffer used when serializing
        /// a single message (to avoid serializing multiple times when sending to multiple endpoints),
        /// and thus should be large enough to ensure it can hold each message type.
        /// This value defaults to 1296.
        /// </summary>
        /// <param name="size"></param>
        public int MaximumTransmissionUnitSize
        {
            set => MessageManager.NonFragmentedMessageMaxSize = value & ~7; // Round down to nearest word aligned size
            get => MessageManager.NonFragmentedMessageMaxSize;
        }

        /// <summary>
        /// Set the maximum transmission unit for a specific peer.
        /// This determines the maximum size of a message batch that can be sent to that client.
        /// If not set for any given client, <see cref="MaximumTransmissionUnitSize"/> will be used instead.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="size"></param>
        public void SetPeerMTU(ulong clientId, int size)
        {
            MessageManager.PeerMTUSizes[clientId] = size;
        }

        /// <summary>
        /// Queries the current MTU size for a client.
        /// If no MTU has been set for that client, will return <see cref="MaximumTransmissionUnitSize"/>
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public int GetPeerMTU(ulong clientId)
        {
            if (MessageManager.PeerMTUSizes.TryGetValue(clientId, out var ret))
            {
                return ret;
            }

            return MessageManager.NonFragmentedMessageMaxSize;
        }

        /// <summary>
        /// Sets the maximum size of a message (or message batch) passed through the transport with the ReliableFragmented delivery.
        /// Warning: setting this value too low may result in the SDK becoming non-functional with projects that have a large number of NetworkBehaviours or NetworkVariables, as the SDK relies on the transport's ability to fragment some messages when they grow beyond the MTU size.
        /// </summary>
        /// <param name="size"></param>
        public int MaximumFragmentedMessageSize
        {
            set => MessageManager.FragmentedMessageMaxSize = value;
            get => MessageManager.FragmentedMessageMaxSize;
        }

        public virtual void Initialize(bool server)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!DisableNotOptimizedSerializedType)
            {
                s_SerializedType.Clear();
            }
#endif

#if COM_UNITY_MODULES_PHYSICS
            NetworkTransformFixedUpdate.Clear();
#endif
            NetworkTransformUpdate.Clear();

            UpdateTopology();

            // Make sure the ServerShutdownState is reset when initializing
            if (server)
            {
                ServerShutdownState = ServerShutdownStates.None;
            }

            // Don't allow the user to start a network session if the NetworkManager is
            // still parented under another GameObject
            if (NetworkManagerCheckForParent(true))
            {
                return;
            }

            ParseCommandLineOptions();

            if (NetworkConfig.NetworkTransport == null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError("No transport has been selected!");
                }

                return;
            }

            // Logging initializes first for any logging during systems initialization
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Initialize));
            }

            this.RegisterNetworkUpdate(NetworkUpdateStage.EarlyUpdate);
#if COM_UNITY_MODULES_PHYSICS
            this.RegisterNetworkUpdate(NetworkUpdateStage.FixedUpdate);
#endif
            this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);
            this.RegisterNetworkUpdate(NetworkUpdateStage.PostScriptLateUpdate);
            this.RegisterNetworkUpdate(NetworkUpdateStage.PreLateUpdate);
            this.RegisterNetworkUpdate(NetworkUpdateStage.PostLateUpdate);

            // ComponentFactory needs to set its defaults next
            ComponentFactory.SetDefaults();

            // UnityTransport dependencies are then initialized
            RealTimeProvider = ComponentFactory.Create<IRealTimeProvider>(this);
            MetricsManager.Initialize(this);

            {
                MessageManager = new NetworkMessageManager(new DefaultMessageSender(this), this);

                MessageManager.Hook(new NetworkManagerHooks(this));

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (NetworkConfig.NetworkProfilingMetrics)
                {
                    MessageManager.Hook(new ProfilingHooks());
                }
#endif

#if MULTIPLAYER_TOOLS
                if (NetworkConfig.NetworkMessageMetrics)
                {
                    MessageManager.Hook(new MetricHooks(this));
                }
#endif

                // Assures there is a server message queue available
                MessageManager.ClientConnected(ServerClientId);
            }

            // Now the connection manager can initialize (which initializes transport)
            ConnectionManager.Initialize(this);

            // The remaining systems can then be initialized
            NetworkTimeSystem = server ? NetworkTimeSystem.ServerTimeSystem() : new NetworkTimeSystem(1.0 / NetworkConfig.TickRate);
            NetworkTickSystem = NetworkTimeSystem.Initialize(this);
            AnticipationSystem = new AnticipationSystem(this);

            // Create spawn manager instance
            SpawnManager = new NetworkSpawnManager(this);

            DeferredMessageManager = ComponentFactory.Create<IDeferredNetworkMessageManager>(this);

            RpcTarget = new RpcTarget(this);

            CustomMessagingManager = new CustomMessagingManager(this);

            SceneManager = new NetworkSceneManager(this);

            BehaviourUpdater = new NetworkBehaviourUpdater();
            BehaviourUpdater.Initialize(this);

            NetworkConfig.InitializePrefabs();
            PrefabHandler.RegisterPlayerPrefab();
        }

        private enum StartType
        {
            Server,
            Host,
            Client
        }

        /// <summary>
        /// Determines if NetworkManager can start based on the current
        /// NetworkManager instance state(s)
        /// </summary>
        private bool CanStart(StartType type)
        {
            if (IsListening)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start " + type + " while an instance is already running");
                }

                return false;
            }

            // Only if it is starting as a server or host do we need to check this
            // Clients don't invoke the ConnectionApprovalCallback
            if (NetworkConfig.ConnectionApproval && type != StartType.Client)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("No ConnectionApproval callback defined. Connection approval will timeout");
                    }
                }
            }

            if (ConnectionApprovalCallback != null)
            {
                if (!NetworkConfig.ConnectionApproval)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("A ConnectionApproval callback is defined but ConnectionApproval is disabled. In order to use ConnectionApproval it has to be explicitly enabled ");
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Starts a server
        /// </summary>
        /// <returns>(<see cref="true"/>/<see cref="false"/>) returns true if <see cref="NetworkManager"/> started in server mode successfully.</returns>
        public bool StartServer()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartServer));
            }

            if (!CanStart(StartType.Server))
            {
                return false;
            }

            if (!ConnectionManager.LocalClient.SetRole(true, false, this))
            {
                return false;
            }
            ConnectionManager.LocalClient.ClientId = ServerClientId;

            Initialize(true);

            try
            {
                IsListening = NetworkConfig.NetworkTransport.StartServer();
                // If we failed to start then shutdown and notify user that the transport failed to start
                if (IsListening)
                {
                    SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

                    // Notify the server that everything should be synchronized/spawned at this time.
                    SpawnManager.NotifyNetworkObjectsSynchronized();
                    OnServerStarted?.Invoke();
                    ConnectionManager.LocalClient.IsApproved = true;
                    return true;
                }

                ConnectionManager.TransportFailureEventHandler(true);
            }
            catch (Exception)
            {
                ConnectionManager.LocalClient.SetRole(false, false);
                IsListening = false;
                throw;
            }

            return IsListening;
        }

        /// <summary>
        /// Starts a client
        /// </summary>
        /// <returns>(<see cref="true"/>/<see cref="false"/>) returns true if <see cref="NetworkManager"/> started in client mode successfully.</returns>
        public bool StartClient()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartClient));
            }

            if (!CanStart(StartType.Client))
            {
                return false;
            }

            if (!ConnectionManager.LocalClient.SetRole(false, true, this))
            {
                return false;
            }

            Initialize(false);

            try
            {
                IsListening = NetworkConfig.NetworkTransport.StartClient();
                // If we failed to start then shutdown and notify user that the transport failed to start
                if (!IsListening)
                {
                    ConnectionManager.TransportFailureEventHandler(true);
                }
                else
                {
                    OnClientStarted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ConnectionManager.LocalClient.SetRole(false, false);
                IsListening = false;
            }

            return IsListening;
        }

        /// <summary>
        /// Starts a Host
        /// </summary>
        /// <returns>(<see cref="true"/>/<see cref="false"/>) returns true if <see cref="NetworkManager"/> started in host mode successfully.</returns>
        public bool StartHost()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartHost));
            }

            if (!CanStart(StartType.Host))
            {
                return false;
            }

            if (!ConnectionManager.LocalClient.SetRole(true, true, this))
            {
                return false;
            }

            Initialize(true);
            try
            {
                IsListening = NetworkConfig.NetworkTransport.StartServer();
                // If we failed to start then shutdown and notify user that the transport failed to start
                if (!IsListening)
                {
                    ConnectionManager.TransportFailureEventHandler(true);
                }
                else
                {
                    // Finalize host-client and server creation logic
                    HostServerInitialize();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ConnectionManager.LocalClient.SetRole(false, false);
                IsListening = false;
            }

            return IsListening;
        }

        /// <summary>
        /// Handles the host client creation logic along with
        /// additional server creation logic
        /// </summary>
        private void HostServerInitialize()
        {
            LocalClientId = ServerClientId;
            NetworkMetrics.SetConnectionId(LocalClientId);
            MessageManager.SetLocalClientId(LocalClientId);

            if (NetworkConfig.ConnectionApproval && ConnectionApprovalCallback != null)
            {
                var response = new ConnectionApprovalResponse();
                ConnectionApprovalCallback(new ConnectionApprovalRequest { Payload = NetworkConfig.ConnectionData, ClientNetworkId = ServerClientId }, response);
                if (!response.Approved)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("You cannot decline the host connection. The connection was automatically approved.");
                    }
                }

                response.Approved = true;
                ConnectionManager.HandleConnectionApproval(ServerClientId, response);
            }
            else
            {
                var response = new ConnectionApprovalResponse
                {
                    Approved = true,
                    // Distributed authority always returns true since the client side handles spawning (whether automatically or manually)
                    CreatePlayerObject = DistributedAuthorityMode || NetworkConfig.PlayerPrefab != null,
                };
                ConnectionManager.HandleConnectionApproval(ServerClientId, response);
            }

            SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

            // Notify the host that everything should be synchronized/spawned at this time.
            SpawnManager.NotifyNetworkObjectsSynchronized();

            OnServerStarted?.Invoke();
            OnClientStarted?.Invoke();

            // This assures that any in-scene placed NetworkObject is spawned and
            // any associated NetworkBehaviours' netcode related properties are
            // set prior to invoking OnClientConnected.
            ConnectionManager.InvokeOnClientConnectedCallback(LocalClientId);
        }

        /// <summary>
        /// Disconnects the remote client.
        /// </summary>
        /// <param name="clientId">The ClientId to disconnect</param>
        public void DisconnectClient(ulong clientId) => ConnectionManager.DisconnectClient(clientId);

        /// <summary>
        /// Disconnects the remote client.
        /// </summary>
        /// <param name="clientId">The ClientId to disconnect</param>
        /// <param name="reason">Disconnection reason. If set, client will receive a DisconnectReasonMessage and have the
        /// reason available in the NetworkManager.DisconnectReason property</param>
        public void DisconnectClient(ulong clientId, string reason = null) => ConnectionManager.DisconnectClient(clientId, reason);

        /// <summary>
        /// Globally shuts down the library.
        /// Disconnects clients if connected and stops server if running.
        /// </summary>
        /// <param name="discardMessageQueue">
        /// If false, any messages that are currently in the incoming queue will be handled,
        /// and any messages in the outgoing queue will be sent, before the shutdown is processed.
        /// If true, NetworkManager will shut down immediately, and any unprocessed or unsent messages
        /// will be discarded.
        /// </param>
        public void Shutdown(bool discardMessageQueue = false)
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Shutdown));
            }

            // If we're not running, don't start shutting down, it would only cause an immediate
            // shutdown the next time the manager is started.
            if (IsServer || IsClient)
            {
                m_ShuttingDown = true;
                if (MessageManager != null)
                {
                    MessageManager.StopProcessing = discardMessageQueue;
                }
            }
        }

        // Ensures that the NetworkManager is cleaned up before OnDestroy is run on NetworkObjects and NetworkBehaviours when unloading a scene with a NetworkManager
        private void OnSceneUnloaded(Scene scene)
        {
            if (gameObject != null && scene == gameObject.scene)
            {
                OnDestroy();
            }
        }

        internal void ShutdownInternal()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(ShutdownInternal));
            }

            this.UnregisterAllNetworkUpdates();

            // Everything is shutdown in the order of their dependencies
            DeferredMessageManager?.CleanupAllTriggers();
            CustomMessagingManager = null;

            RpcTarget?.Dispose();
            RpcTarget = null;

            BehaviourUpdater?.Shutdown();
            BehaviourUpdater = null;

            // Shutdown connection manager last which shuts down transport
            ConnectionManager.Shutdown();

            if (MessageManager != null)
            {
                MessageManager.Dispose();
                MessageManager = null;
            }

            // We need to clean up NetworkObjects before we reset the IsServer
            // and IsClient properties. This provides consistency of these two
            // property values for NetworkObjects that are still spawned when
            // the shutdown cycle begins.
            SpawnManager?.DespawnAndDestroyNetworkObjects();
            SpawnManager?.ServerResetShudownStateForSceneObjects();
            SpawnManager = null;

            // Let the NetworkSceneManager clean up its two SceneEvenData instances
            SceneManager?.Dispose();
            SceneManager = null;
            IsListening = false;
            m_ShuttingDown = false;

            // Generate a local notification that the host client is disconnected
            if (IsHost)
            {
                ConnectionManager.InvokeOnClientDisconnectCallback(LocalClientId);
            }

            if (ConnectionManager.LocalClient.IsClient)
            {
                // If we were a client, we want to know if we were a host
                // client or not. (why we pass in "IsServer")
                OnClientStopped?.Invoke(ConnectionManager.LocalClient.IsServer);
            }

            if (ConnectionManager.LocalClient.IsServer)
            {
                // If we were a server, we want to know if we were a host
                // or not. (why we pass in "IsClient")
                OnServerStopped?.Invoke(ConnectionManager.LocalClient.IsClient);
            }

            // In the event shutdown is invoked within OnClientStopped or OnServerStopped, set it to false again
            m_ShuttingDown = false;

            // Reset the client's roles
            ConnectionManager.LocalClient.SetRole(false, false);

            // This cleans up the internal prefabs list
            NetworkConfig?.Prefabs?.Shutdown();

            // Reset the configuration hash for next session in the event
            // that the prefab list changes
            NetworkConfig?.ClearConfigHash();

            // Time & tick systems should be the last system shutdown so other systems
            // can unsubscribe from tick updates and such.
            NetworkTimeSystem?.Shutdown();
            NetworkTickSystem = null;
        }


        // Ensures that the NetworkManager is cleaned up before OnDestroy is run on NetworkObjects and NetworkBehaviours when quitting the application.
        private void OnApplicationQuit()
        {
            // Make sure ShutdownInProgress returns true during this time
            m_ShuttingDown = true;
            OnDestroy();
        }

        // Note that this gets also called manually by OnSceneUnloaded and OnApplicationQuit
        private void OnDestroy()
        {
            ShutdownInternal();

            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;

            // Notify we are destroying NetworkManager
            OnDestroying?.Invoke(this);

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        // Command line options
        private const string k_OverridePortArg = "-port";

        private string GetArg(string[] commandLineArgs, string arg)
        {
            var argIndex = Array.IndexOf(commandLineArgs, arg);
            if (argIndex >= 0 && argIndex < commandLineArgs.Length - 1)
            {
                return commandLineArgs[argIndex + 1];
            }

            return null;
        }

        private void ParseArg<T>(string arg, ref Override<T> value)
        {
            if (GetArg(Environment.GetCommandLineArgs(), arg) is string argValue)
            {
                value.Value = (T)Convert.ChangeType(argValue, typeof(T));
            }
        }

        private void ParseCommandLineOptions()
        {
#if UNITY_SERVER && UNITY_DEDICATED_SERVER_ARGUMENTS_PRESENT
            if ( UnityEngine.DedicatedServer.Arguments.Port != null)
            {
                PortOverride.Value = (ushort)UnityEngine.DedicatedServer.Arguments.Port;
            }
#else
            ParseArg(k_OverridePortArg, ref PortOverride);
#endif
        }
    }
}
