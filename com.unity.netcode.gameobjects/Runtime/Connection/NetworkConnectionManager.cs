using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Unity.Netcode
{

    public enum ConnectionEvent
    {
        ClientConnected,
        PeerConnected,
        ClientDisconnected,
        PeerDisconnected
    }

    public struct ConnectionEventData
    {
        public ConnectionEvent EventType;

        /// <summary>
        /// The client ID for the client that just connected
        /// For the <see cref="ConnectionEvent.ClientConnected"/> and <see cref="ConnectionEvent.ClientDisconnected"/>
        /// events on the client side, this will be LocalClientId.
        /// On the server side, this will be the ID of the client that just connected.
        ///
        /// For the <see cref="ConnectionEvent.PeerConnected"/> and <see cref="ConnectionEvent.PeerDisconnected"/>
        /// events on the client side, this will be the client ID assigned by the server to the remote peer.
        /// </summary>
        public ulong ClientId;

        /// <summary>
        /// This is only populated in <see cref="ConnectionEvent.ClientConnected"/> on the client side, and
        /// contains the list of other peers who were present before you connected. In all other situations,
        /// this array will be uninitialized.
        /// </summary>
        public NativeArray<ulong> PeerClientIds;
    }

    /// <summary>
    /// The NGO connection manager handles:
    /// - Client Connections
    /// - Client Approval
    /// - Processing <see cref="NetworkEvent"/>s.
    /// - Client Disconnection
    /// </summary>
    public sealed class NetworkConnectionManager
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_TransportPollMarker = new ProfilerMarker($"{nameof(NetworkManager)}.TransportPoll");
        private static ProfilerMarker s_TransportConnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportConnect");
        private static ProfilerMarker s_HandleIncomingData = new ProfilerMarker($"{nameof(NetworkManager)}.{nameof(NetworkMessageManager.HandleIncomingData)}");
        private static ProfilerMarker s_TransportDisconnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportDisconnect");
#endif

        /// <summary>
        /// When disconnected from the server, the server may send a reason. If a reason was sent, this property will
        /// tell client code what the reason was. It should be queried after the OnClientDisconnectCallback is called
        /// </summary>
        public string DisconnectReason { get; internal set; }

        /// <summary>
        /// The callback to invoke once a client connects. This callback is only ran on the server and on the local client that connects.
        /// </summary>
        public event Action<ulong> OnClientConnectedCallback = null;

        /// <summary>
        /// The callback to invoke when a client disconnects. This callback is only ran on the server and on the local client that disconnects.
        /// </summary>
        public event Action<ulong> OnClientDisconnectCallback = null;

        /// <summary>
        /// The callback to invoke once a peer connects. This callback is only ran on the server and on the local client that connects.
        /// </summary>
        public event Action<NetworkManager, ConnectionEventData> OnConnectionEvent = null;


        internal void InvokeOnClientConnectedCallback(ulong clientId)
        {
            try
            {
                OnClientConnectedCallback?.Invoke(clientId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (!NetworkManager.IsServer)
            {
                var peerClientIds = new NativeArray<ulong>(Math.Max(NetworkManager.ConnectedClientsIds.Count - 1, 0), Allocator.Temp);
                // `using var peerClientIds` or `using(peerClientIds)` renders it immutable...
                using var sentinel = peerClientIds;

                var idx = 0;
                foreach (var peerId in NetworkManager.ConnectedClientsIds)
                {
                    if (peerId == NetworkManager.LocalClientId)
                    {
                        continue;
                    }

                    // This assures if the server has not timed out prior to the client synchronizing that it doesn't exceed the allocated peer count.
                    if (peerClientIds.Length > idx)
                    {
                        peerClientIds[idx] = peerId;
                        ++idx;
                    }
                }

                try
                {
                    OnConnectionEvent?.Invoke(NetworkManager, new ConnectionEventData { ClientId = NetworkManager.LocalClientId, EventType = ConnectionEvent.ClientConnected, PeerClientIds = peerClientIds });
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            else
            {
                try
                {
                    OnConnectionEvent?.Invoke(NetworkManager, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.ClientConnected });
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal void InvokeOnClientDisconnectCallback(ulong clientId)
        {
            try
            {
                OnClientDisconnectCallback?.Invoke(clientId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            try
            {
                OnConnectionEvent?.Invoke(NetworkManager, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.ClientDisconnected });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal void InvokeOnPeerConnectedCallback(ulong clientId)
        {
            try
            {
                OnConnectionEvent?.Invoke(NetworkManager, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.PeerConnected });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
        internal void InvokeOnPeerDisconnectedCallback(ulong clientId)
        {
            try
            {
                OnConnectionEvent?.Invoke(NetworkManager, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.PeerDisconnected });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// The callback to invoke if the <see cref="NetworkTransport"/> fails.
        /// </summary>
        /// <remarks>
        /// A failure of the transport is always followed by the <see cref="NetworkManager"/> shutting down. Recovering
        /// from a transport failure would normally entail reconfiguring the transport (e.g. re-authenticating, or
        /// recreating a new service allocation depending on the transport) and restarting the client/server/host.
        /// </remarks>
        public event Action OnTransportFailure;

        /// <summary>
        /// Is true when a server or host is listening for connections.
        /// Is true when a client is connecting or connected to a network session.
        /// Is false when not listening, connecting, or connected.
        /// </summary>
        public bool IsListening { get; internal set; }

        internal NetworkManager NetworkManager;
        internal NetworkMessageManager MessageManager;

        internal NetworkClient LocalClient = new NetworkClient();
        internal Dictionary<ulong, NetworkManager.ConnectionApprovalResponse> ClientsToApprove = new Dictionary<ulong, NetworkManager.ConnectionApprovalResponse>();

        internal Dictionary<ulong, NetworkClient> ConnectedClients = new Dictionary<ulong, NetworkClient>();
        internal Dictionary<ulong, ulong> ClientIdToTransportIdMap = new Dictionary<ulong, ulong>();
        internal Dictionary<ulong, ulong> TransportIdToClientIdMap = new Dictionary<ulong, ulong>();
        internal List<NetworkClient> ConnectedClientsList = new List<NetworkClient>();
        internal List<ulong> ConnectedClientIds = new List<ulong>();
        internal Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> ConnectionApprovalCallback;

        /// <summary>
        /// Use <see cref="AddPendingClient(ulong)"/> and <see cref="RemovePendingClient(ulong)"/> to add or remove
        /// Use <see cref="PendingClients"/> to internally access the pending client dictionary
        /// </summary>
        private Dictionary<ulong, PendingClient> m_PendingClients = new Dictionary<ulong, PendingClient>();

        internal IReadOnlyDictionary<ulong, PendingClient> PendingClients => m_PendingClients;

        internal Coroutine LocalClientApprovalCoroutine;

        /// <summary>
        /// Client-Side:
        /// Starts the client-side approval timeout coroutine
        /// </summary>
        /// <param name="clientId"></param>
        internal void StartClientApprovalCoroutine(ulong clientId)
        {
            LocalClientApprovalCoroutine = NetworkManager.StartCoroutine(ApprovalTimeout(clientId));
        }

        /// <summary>
        /// Client-Side:
        /// Stops the client-side approval timeout when it is approved.
        /// <see cref="ConnectionApprovedMessage.Handle(ref NetworkContext)"/>
        /// </summary>
        internal void StopClientApprovalCoroutine()
        {
            if (LocalClientApprovalCoroutine != null)
            {
                NetworkManager.StopCoroutine(LocalClientApprovalCoroutine);
                LocalClientApprovalCoroutine = null;
            }
        }

        /// <summary>
        /// Server-Side:
        /// Handles the issue with populating NetworkManager.PendingClients
        /// </summary>
        internal void AddPendingClient(ulong clientId)
        {
            m_PendingClients.Add(clientId, new PendingClient()
            {
                ClientId = clientId,
                ConnectionState = PendingClient.State.PendingConnection,
                ApprovalCoroutine = NetworkManager.StartCoroutine(ApprovalTimeout(clientId))
            });

            NetworkManager.PendingClients.Add(clientId, PendingClients[clientId]);
        }

        /// <summary>
        /// Server-Side:
        /// Handles the issue with depopulating NetworkManager.PendingClients
        /// </summary>
        internal void RemovePendingClient(ulong clientId)
        {
            if (m_PendingClients.ContainsKey(clientId) && m_PendingClients[clientId].ApprovalCoroutine != null)
            {
                NetworkManager.StopCoroutine(m_PendingClients[clientId].ApprovalCoroutine);
            }
            m_PendingClients.Remove(clientId);
            NetworkManager.PendingClients.Remove(clientId);
        }

        /// <summary>
        /// Used to generate client identifiers
        /// </summary>
        private ulong m_NextClientId = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong TransportIdToClientId(ulong transportId)
        {
            if (transportId == GetServerTransportId())
            {
                return NetworkManager.ServerClientId;
            }

            if (TransportIdToClientIdMap.TryGetValue(transportId, out var clientId))
            {
                return clientId;
            }

            if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
            {
                NetworkLog.LogWarning($"Trying to get the NGO client ID map for the transport ID ({transportId}) but did not find the map entry! Returning default transport ID value.");
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong ClientIdToTransportId(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                return GetServerTransportId();
            }

            if (ClientIdToTransportIdMap.TryGetValue(clientId, out var transportClientId))
            {
                return transportClientId;
            }

            if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
            {
                NetworkLog.LogWarning($"Trying to get the transport client ID map for the NGO client ID ({clientId}) but did not find the map entry! Returning default transport ID value.");
            }

            return default;
        }

        /// <summary>
        /// Gets the networkId of the server
        /// </summary>
        internal ulong ServerTransportId => GetServerTransportId();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetServerTransportId()
        {
            if (NetworkManager != null)
            {
                var transport = NetworkManager.NetworkConfig.NetworkTransport;
                if (transport != null)
                {
                    return transport.ServerClientId;
                }

                throw new NullReferenceException($"The transport in the active {nameof(NetworkConfig)} is null");
            }

            throw new Exception($"There is no {nameof(NetworkManager)} assigned to this instance!");
        }

        /// <summary>
        /// Handles cleaning up the transport id/client id tables after receiving a disconnect event from transport
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong TransportIdCleanUp(ulong transportId)
        {
            // This check is for clients that attempted to connect but failed.
            // When this happens, the client will not have an entry within the m_TransportIdToClientIdMap or m_ClientIdToTransportIdMap lookup tables so we exit early and just return 0 to be used for the disconnect event.
            if (!LocalClient.IsServer && !TransportIdToClientIdMap.ContainsKey(transportId))
            {
                return NetworkManager.LocalClientId;
            }

            var clientId = TransportIdToClientId(transportId);
            TransportIdToClientIdMap.Remove(transportId);
            ClientIdToTransportIdMap.Remove(clientId);
            return clientId;
        }

        internal void PollAndHandleNetworkEvents()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportPollMarker.Begin();
#endif
            NetworkEvent networkEvent;
            do
            {
                networkEvent = NetworkManager.NetworkConfig.NetworkTransport.PollEvent(out ulong transportClientId, out ArraySegment<byte> payload, out float receiveTime);
                HandleNetworkEvent(networkEvent, transportClientId, payload, receiveTime);
                if (networkEvent == NetworkEvent.Disconnect || networkEvent == NetworkEvent.TransportFailure)
                {
                    break;
                }
                // Only do another iteration if: there are no more messages AND (there is no limit to max events or we have processed less than the maximum)
            } while (NetworkManager.IsListening && networkEvent != NetworkEvent.Nothing);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportPollMarker.End();
#endif
        }

        /// <summary>
        /// Event driven NetworkTransports (like UnityTransport) NetworkEvent handling
        /// </summary>
        /// <remarks>
        /// Polling NetworkTransports invoke this directly
        /// </remarks>
        internal void HandleNetworkEvent(NetworkEvent networkEvent, ulong transportClientId, ArraySegment<byte> payload, float receiveTime)
        {
            switch (networkEvent)
            {
                case NetworkEvent.Connect:
                    ConnectEventHandler(transportClientId);
                    break;
                case NetworkEvent.Data:
                    DataEventHandler(transportClientId, ref payload, receiveTime);
                    break;
                case NetworkEvent.Disconnect:
                    DisconnectEventHandler(transportClientId);
                    break;
                case NetworkEvent.TransportFailure:
                    TransportFailureEventHandler();
                    break;
            }
        }

        /// <summary>
        /// Handles a <see cref="NetworkEvent.Connect"/> event.
        /// </summary>
        internal void ConnectEventHandler(ulong transportClientId)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportConnect.Begin();
#endif
            // Assumptions:
            // - When server receives a connection, it *must be* a client
            // - When client receives one, it *must be* the server
            // Client's can't connect to or talk to other clients.
            // Server is a sentinel so only one exists, if we are server, we can't be connecting to it.
            var clientId = transportClientId;
            if (LocalClient.IsServer)
            {
                clientId = m_NextClientId++;
            }
            else
            {
                clientId = NetworkManager.ServerClientId;
            }

            ClientIdToTransportIdMap[clientId] = transportClientId;
            TransportIdToClientIdMap[transportClientId] = clientId;
            MessageManager.ClientConnected(clientId);

            if (LocalClient.IsServer)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    var hostServer = NetworkManager.IsHost ? "Host" : "Server";
                    NetworkLog.LogInfo($"[{hostServer}-Side] Transport connection established with pending Client-{clientId}.");
                }
                AddPendingClient(clientId);
            }
            else
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    var serverOrService = NetworkManager.DistributedAuthorityMode ? NetworkManager.CMBServiceConnection ? "service" : "DAHost" : "server";
                    NetworkLog.LogInfo($"[Approval Pending][Client] Transport connection with {serverOrService} established! Awaiting connection approval...");
                }

                SendConnectionRequest();
                StartClientApprovalCoroutine(clientId);
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportConnect.End();
#endif
        }

        /// <summary>
        /// Handles a <see cref="NetworkEvent.Data"/> event.
        /// </summary>
        internal void DataEventHandler(ulong transportClientId, ref ArraySegment<byte> payload, float receiveTime)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.Begin();
#endif
            var clientId = TransportIdToClientId(transportClientId);
            MessageManager.HandleIncomingData(clientId, payload, receiveTime);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.End();
#endif
        }

        /// <summary>
        /// Handles a <see cref="NetworkEvent.Disconnect"/> event.
        /// </summary>
        internal void DisconnectEventHandler(ulong transportClientId)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportDisconnect.Begin();
#endif
            var clientId = TransportIdCleanUp(transportClientId);
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo($"Disconnect Event From {clientId}");
            }

            // If we are a client and we have gotten the ServerClientId back, then use our assigned local id as the client that was
            // disconnected (either the user disconnected or the server disconnected, but the client that disconnected is the LocalClientId)
            if (!NetworkManager.IsServer && clientId == NetworkManager.ServerClientId)
            {
                clientId = NetworkManager.LocalClientId;
            }

            // Process the incoming message queue so that we get everything from the server disconnecting us or, if we are the server, so we got everything from that client.
            MessageManager.ProcessIncomingMessageQueue();

            if (LocalClient.IsServer)
            {
                // We need to process the disconnection before notifying
                OnClientDisconnectFromServer(clientId);

                // Now notify the client has disconnected
                InvokeOnClientDisconnectCallback(clientId);

                if (LocalClient.IsHost)
                {
                    InvokeOnPeerDisconnectedCallback(clientId);
                }
            }
            else
            {
                // Notify local client of disconnection
                InvokeOnClientDisconnectCallback(clientId);

                // As long as we are not in the middle of a shutdown
                if (!NetworkManager.ShutdownInProgress)
                {
                    // We must pass true here and not process any sends messages as we are no longer connected.
                    // Otherwise, attempting to process messages here can cause an exception within UnityTransport
                    // as the client ID is no longer valid.
                    NetworkManager.Shutdown(true);
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_TransportDisconnect.End();
#endif
        }

        /// <summary>
        /// Handles a <see cref="NetworkEvent.TransportFailure"/> event.
        /// </summary>
        internal void TransportFailureEventHandler(bool duringStart = false)
        {
            var clientSeverOrHost = LocalClient.IsServer ? LocalClient.IsHost ? "Host" : "Server" : "Client";
            var whenFailed = duringStart ? "start failure" : "failure";
            NetworkLog.LogError($"{clientSeverOrHost} is shutting down due to network transport {whenFailed} of {NetworkManager.NetworkConfig.NetworkTransport.GetType().Name}!");
            OnTransportFailure?.Invoke();

            // If we had a transport failure when trying to start, reset the local client roles and directly invoke the internal shutdown.
            if (duringStart)
            {
                LocalClient.SetRole(false, false);
                NetworkManager.ShutdownInternal();
            }
            else
            {
                // Otherwise, stop processing messages and shutdown the normal way
                NetworkManager.Shutdown(true);
            }
        }

        /// <summary>
        /// Client-Side:
        /// Upon transport connecting, the client will send a connection request
        /// </summary>
        private void SendConnectionRequest()
        {
            var message = new ConnectionRequestMessage
            {
                CMBServiceConnection = NetworkManager.CMBServiceConnection,
                // Since only a remote client will send a connection request, we should always force the rebuilding of the NetworkConfig hash value
                ConfigHash = NetworkManager.NetworkConfig.GetConfig(false),
                ShouldSendConnectionData = NetworkManager.NetworkConfig.ConnectionApproval,
                ConnectionData = NetworkManager.NetworkConfig.ConnectionData,
                MessageVersions = new NativeArray<MessageVersionData>(MessageManager.MessageHandlers.Length, Allocator.Temp)
            };

            if (NetworkManager.CMBServiceConnection)
            {
                message.ClientConfig.TickRate = NetworkManager.NetworkConfig.TickRate;
                message.ClientConfig.EnableSceneManagement = NetworkManager.NetworkConfig.EnableSceneManagement;
            }

            for (int index = 0; index < MessageManager.MessageHandlers.Length; index++)
            {
                if (MessageManager.MessageTypes[index] != null)
                {
                    var type = MessageManager.MessageTypes[index];
                    message.MessageVersions[index] = new MessageVersionData
                    {
                        Hash = XXHash.Hash32(type.FullName),
                        Version = MessageManager.GetLocalVersion(type)
                    };
                }
            }

            SendMessage(ref message, NetworkDelivery.ReliableSequenced, NetworkManager.ServerClientId);
            message.MessageVersions.Dispose();
        }

        /// <summary>
        /// Approval time out coroutine
        /// </summary>
        private IEnumerator ApprovalTimeout(ulong clientId)
        {
            var timeStarted = LocalClient.IsServer ? NetworkManager.LocalTime.TimeAsFloat : NetworkManager.RealTimeProvider.RealTimeSinceStartup;
            var timedOut = false;
            var connectionApproved = false;
            var connectionNotApproved = false;
            var timeoutMarker = timeStarted + NetworkManager.NetworkConfig.ClientConnectionBufferTimeout;

            while (NetworkManager.IsListening && !NetworkManager.ShutdownInProgress && !timedOut && !connectionApproved)
            {
                yield return null;
                // Check if we timed out
                timedOut = timeoutMarker < (LocalClient.IsServer ? NetworkManager.LocalTime.TimeAsFloat : NetworkManager.RealTimeProvider.RealTimeSinceStartup);

                if (LocalClient.IsServer)
                {
                    // When the client is no longer in the pending clients list and is in the connected clients list it has been approved
                    connectionApproved = !PendingClients.ContainsKey(clientId) && ConnectedClients.ContainsKey(clientId);

                    // For the server side, if the client is in neither list then it was declined or the client disconnected
                    connectionNotApproved = !PendingClients.ContainsKey(clientId) && !ConnectedClients.ContainsKey(clientId);
                }
                else
                {
                    connectionApproved = NetworkManager.LocalClient.IsApproved;
                }
            }

            // Exit coroutine if we are no longer listening or a shutdown is in progress (client or server)
            if (!NetworkManager.IsListening || NetworkManager.ShutdownInProgress)
            {
                yield break;
            }

            // If the client timed out or was not approved
            if (timedOut || connectionNotApproved)
            {
                // Timeout
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    if (timedOut)
                    {
                        if (LocalClient.IsServer)
                        {
                            // Log a warning that the transport detected a connection but then did not receive a follow up connection request message.
                            // (hacking or something happened to the server's network connection)
                            NetworkLog.LogWarning($"Server detected a transport connection from Client-{clientId}, but timed out waiting for the connection request message.");
                        }
                        else
                        {
                            // We only provide informational logging for the client side
                            NetworkLog.LogInfo("Timed out waiting for the server to approve the connection request.");
                        }
                    }
                    else if (connectionNotApproved)
                    {
                        NetworkLog.LogInfo($"Client-{clientId} was either denied approval or disconnected while being approved.");
                    }
                }

                if (LocalClient.IsServer)
                {
                    DisconnectClient(clientId);
                }
                else
                {
                    NetworkManager.Shutdown(true);
                }
            }
        }

        /// <summary>
        /// Server-Side:
        /// Handles approval while processing a client connection request
        /// </summary>
        internal void ApproveConnection(ref ConnectionRequestMessage connectionRequestMessage, ref NetworkContext context)
        {
            // Note: Delegate creation allocates.
            // Note: ToArray() also allocates. :(
            var response = new NetworkManager.ConnectionApprovalResponse();
            ClientsToApprove[context.SenderId] = response;

            ConnectionApprovalCallback(
                new NetworkManager.ConnectionApprovalRequest
                {
                    Payload = connectionRequestMessage.ConnectionData,
                    ClientNetworkId = context.SenderId
                }, response);
        }

        /// <summary>
        /// Server-Side:
        /// Processes pending approvals and removes any stale pending clients
        /// </summary>
        internal void ProcessPendingApprovals()
        {
            List<ulong> senders = null;

            foreach (var responsePair in ClientsToApprove)
            {
                var response = responsePair.Value;
                var senderId = responsePair.Key;

                if (!response.Pending)
                {
                    try
                    {
                        HandleConnectionApproval(senderId, response);

                        senders ??= new List<ulong>();
                        senders.Add(senderId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            if (senders != null)
            {
                foreach (var sender in senders)
                {
                    ClientsToApprove.Remove(sender);
                }
            }
        }

        /// <summary>
        /// Adding this because message hooks cannot happen fast enough under certain scenarios
        /// where the message is sent and responded to before the hook is in place.
        /// </summary>
        internal bool MockSkippingApproval;

        /// <summary>
        /// Server Side: Handles the approval of a client
        /// </summary>
        /// <remarks>
        /// This will spawn the player prefab as well as start client synchronization if <see cref="NetworkConfig.EnableSceneManagement"/> is enabled
        /// </remarks>
        internal void HandleConnectionApproval(ulong ownerClientId, NetworkManager.ConnectionApprovalResponse response)
        {
            LocalClient.IsApproved = response.Approved;
            if (response.Approved)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"[Server-Side] Pending Client-{ownerClientId} connection approved!");
                }
                // The client was approved, stop the server-side approval time out coroutine
                RemovePendingClient(ownerClientId);

                var client = AddClient(ownerClientId);

                // Server-side spawning (only if there is a prefab hash or player prefab provided)
                if (!NetworkManager.DistributedAuthorityMode && response.CreatePlayerObject && (response.PlayerPrefabHash.HasValue || NetworkManager.NetworkConfig.PlayerPrefab != null))
                {
                    var playerObject = response.PlayerPrefabHash.HasValue ? NetworkManager.SpawnManager.GetNetworkObjectToSpawn(response.PlayerPrefabHash.Value, ownerClientId, response.Position ?? null, response.Rotation ?? null)
                    : NetworkManager.SpawnManager.GetNetworkObjectToSpawn(NetworkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash, ownerClientId, response.Position ?? null, response.Rotation ?? null);

                    // Spawn the player NetworkObject locally
                    NetworkManager.SpawnManager.SpawnNetworkObjectLocally(
                        playerObject,
                        NetworkManager.SpawnManager.GetNetworkObjectId(),
                        sceneObject: false,
                        playerObject: true,
                        ownerClientId,
                        destroyWithScene: false);

                    client.AssignPlayerObject(ref playerObject);
                }

                // Server doesn't send itself the connection approved message
                if (ownerClientId != NetworkManager.ServerClientId)
                {
                    var message = new ConnectionApprovedMessage
                    {
                        OwnerClientId = ownerClientId,
                        NetworkTick = NetworkManager.LocalTime.Tick,
                        IsDistributedAuthority = NetworkManager.DistributedAuthorityMode,
                        ConnectedClientIds = new NativeArray<ulong>(ConnectedClientIds.Count, Allocator.Temp)
                    };

                    var i = 0;
                    foreach (var clientId in ConnectedClientIds)
                    {
                        message.ConnectedClientIds[i] = clientId;
                        ++i;
                    }

                    if (!NetworkManager.NetworkConfig.EnableSceneManagement)
                    {
                        // Update the observed spawned NetworkObjects for the newly connected player when scene management is disabled
                        NetworkManager.SpawnManager.UpdateObservedNetworkObjects(ownerClientId);
                        if (NetworkManager.SpawnManager.SpawnedObjectsList.Count != 0)
                        {
                            message.SpawnedObjectsList = NetworkManager.SpawnManager.SpawnedObjectsList;
                        }
                    }

                    message.MessageVersions = new NativeArray<MessageVersionData>(MessageManager.MessageHandlers.Length, Allocator.Temp);
                    for (int index = 0; index < MessageManager.MessageHandlers.Length; index++)
                    {
                        if (MessageManager.MessageTypes[index] != null)
                        {
                            var type = MessageManager.MessageTypes[index];
                            message.MessageVersions[index] = new MessageVersionData
                            {
                                Hash = XXHash.Hash32(type.FullName),
                                Version = MessageManager.GetLocalVersion(type)
                            };
                        }
                    }
                    if (!MockSkippingApproval)
                    {
                        SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, ownerClientId);
                    }
                    else
                    {
                        NetworkLog.LogInfo("Mocking server not responding with connection approved...");
                    }
                    message.MessageVersions.Dispose();
                    message.ConnectedClientIds.Dispose();
                    if (MockSkippingApproval)
                    {
                        return;
                    }

                    // If scene management is disabled, then we are done and notify the local host-server the client is connected
                    if (!NetworkManager.NetworkConfig.EnableSceneManagement)
                    {
                        NetworkManager.ConnectedClients[ownerClientId].IsConnected = true;
                        InvokeOnClientConnectedCallback(ownerClientId);
                        if (LocalClient.IsHost)
                        {
                            InvokeOnPeerConnectedCallback(ownerClientId);
                        }
                        NetworkManager.SpawnManager.DistributeNetworkObjects(ownerClientId);

                    }
                    else // Otherwise, let NetworkSceneManager handle the initial scene and NetworkObject synchronization
                    {
                        if (NetworkManager.DistributedAuthorityMode && NetworkManager.LocalClient.IsSessionOwner)
                        {
                            NetworkManager.SceneManager.SynchronizeNetworkObjects(ownerClientId);
                        }
                        else if (!NetworkManager.DistributedAuthorityMode)
                        {
                            NetworkManager.SceneManager.SynchronizeNetworkObjects(ownerClientId);
                        }
                    }
                }
                else // Server just adds itself as an observer to all spawned NetworkObjects
                {
                    LocalClient = client;
                    NetworkManager.SpawnManager.UpdateObservedNetworkObjects(ownerClientId);
                    LocalClient.IsConnected = true;
                    // If running mock service, then set the instance as the default session owner
                    if (NetworkManager.DistributedAuthorityMode && NetworkManager.DAHost)
                    {
                        NetworkManager.SetSessionOwner(NetworkManager.LocalClientId);
                        NetworkManager.SceneManager.InitializeScenesLoaded();
                    }

                    if (NetworkManager.DistributedAuthorityMode && NetworkManager.AutoSpawnPlayerPrefabClientSide)
                    {
                        CreateAndSpawnPlayer(ownerClientId);
                    }
                }

                // Exit early if no player object was spawned
                if (!response.CreatePlayerObject || (response.PlayerPrefabHash == null && NetworkManager.NetworkConfig.PlayerPrefab == null))
                {
                    return;
                }

                // Players are always spawned by their respective client, exit early. (DAHost mode anyway, CMB Service will never spawn player prefab)
                if (NetworkManager.DistributedAuthorityMode)
                {
                    return;
                }
                // Separating this into a contained function call for potential further future separation of when this notification is sent.
                ApprovedPlayerSpawn(ownerClientId, response.PlayerPrefabHash ?? NetworkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash);
            }
            else
            {
                if (!string.IsNullOrEmpty(response.Reason))
                {
                    var disconnectReason = new DisconnectReasonMessage
                    {
                        Reason = response.Reason
                    };
                    SendMessage(ref disconnectReason, NetworkDelivery.Reliable, ownerClientId);
                    MessageManager.ProcessSendQueues();
                }
                DisconnectRemoteClient(ownerClientId);
            }
        }

        /// <summary>
        /// Client-Side Spawning in distributed authority mode uses this to spawn the player.
        /// </summary>
        internal void CreateAndSpawnPlayer(ulong ownerId)
        {
            if (NetworkManager.DistributedAuthorityMode && NetworkManager.AutoSpawnPlayerPrefabClientSide)
            {
                var playerPrefab = NetworkManager.FetchLocalPlayerPrefabToSpawn();
                if (playerPrefab != null)
                {
                    var globalObjectIdHash = playerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;
                    var networkObject = NetworkManager.SpawnManager.GetNetworkObjectToSpawn(globalObjectIdHash, ownerId, playerPrefab.transform.position, playerPrefab.transform.rotation);
                    networkObject.IsSceneObject = false;
                    networkObject.SpawnAsPlayerObject(ownerId, networkObject.DestroyWithScene);
                }
            }
        }

        /// <summary>
        /// Spawns the newly approved player
        /// </summary>
        /// <param name="clientId">new player client identifier</param>
        /// <param name="playerPrefabHash">the prefab GlobalObjectIdHash value for this player</param>
        internal void ApprovedPlayerSpawn(ulong clientId, uint playerPrefabHash)
        {
            foreach (var clientPair in ConnectedClients)
            {
                if (clientPair.Key == clientId ||
                    clientPair.Key == NetworkManager.ServerClientId || // Server already spawned it
                    ConnectedClients[clientId].PlayerObject == null ||
                    !ConnectedClients[clientId].PlayerObject.Observers.Contains(clientPair.Key))
                {
                    continue; //The new client.
                }

                var message = new CreateObjectMessage
                {
                    ObjectInfo = ConnectedClients[clientId].PlayerObject.GetMessageSceneObject(clientPair.Key),
                    IncludesSerializedObject = true,
                };

                message.ObjectInfo.Hash = playerPrefabHash;
                message.ObjectInfo.IsSceneObject = false;
                message.ObjectInfo.HasParent = false;
                message.ObjectInfo.IsPlayerObject = true;
                message.ObjectInfo.OwnerClientId = clientId;
                var size = SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, clientPair.Key);
                NetworkManager.NetworkMetrics.TrackObjectSpawnSent(clientPair.Key, ConnectedClients[clientId].PlayerObject, size);
            }
        }

        /// <summary>
        /// Server-Side:
        /// Creates a new <see cref="NetworkClient"/> and handles updating the associated
        /// connected clients lists.
        /// </summary>
        internal NetworkClient AddClient(ulong clientId)
        {
            if (ConnectedClients.ContainsKey(clientId) && ConnectedClientIds.Contains(clientId) && ConnectedClientsList.Contains(ConnectedClients[clientId]))
            {
                return ConnectedClients[clientId];
            }

            var networkClient = LocalClient;

            // If this is not the local client then create a new one
            if (clientId != NetworkManager.LocalClientId)
            {
                networkClient = new NetworkClient();
            }
            networkClient.SetRole(clientId == NetworkManager.ServerClientId, isClient: true, NetworkManager);
            networkClient.ClientId = clientId;
            if (!ConnectedClients.ContainsKey(clientId))
            {
                ConnectedClients.Add(clientId, networkClient);
            }
            if (!ConnectedClientsList.Contains(networkClient))
            {
                ConnectedClientsList.Add(networkClient);
            }

            if (NetworkManager.LocalClientId != clientId)
            {
                if ((!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer) ||
                    (NetworkManager.DistributedAuthorityMode && NetworkManager.NetworkConfig.EnableSceneManagement && NetworkManager.DAHost && NetworkManager.LocalClient.IsSessionOwner))
                {
                    var message = new ClientConnectedMessage { ClientId = clientId };
                    NetworkManager.MessageManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, ConnectedClientIds.Where((c) => c != NetworkManager.LocalClientId).ToArray());
                }
                else if (NetworkManager.DistributedAuthorityMode && NetworkManager.NetworkConfig.EnableSceneManagement && NetworkManager.DAHost && !NetworkManager.LocalClient.IsSessionOwner)
                {
                    var message = new ClientConnectedMessage
                    {
                        ShouldSynchronize = true,
                        ClientId = clientId
                    };
                    NetworkManager.MessageManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, NetworkManager.CurrentSessionOwner);
                }
            }
            if (!ConnectedClientIds.Contains(clientId))
            {
                ConnectedClientIds.Add(clientId);
            }

            var distributedAuthority = NetworkManager.DistributedAuthorityMode;
            var sessionOwnerId = NetworkManager.CurrentSessionOwner;
            var isSessionOwner = NetworkManager.LocalClient.IsSessionOwner;
            foreach (var networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject.SpawnWithObservers)
                {
                    // Don't add the client to the observers if hidden from the session owner
                    if (networkObject.IsOwner && distributedAuthority && !isSessionOwner && !networkObject.Observers.Contains(sessionOwnerId))
                    {
                        continue;
                    }
                    networkObject.Observers.Add(clientId);
                }
            }

            return networkClient;
        }

        /// <summary>
        /// Invoked on clients when another client disconnects
        /// </summary>
        /// <param name="clientId">the client identifier to remove</param>
        internal void RemoveClient(ulong clientId)
        {
            if (ConnectedClientIds.Contains(clientId))
            {
                ConnectedClientIds.Remove(clientId);
            }
            if (ConnectedClients.ContainsKey(clientId))
            {
                ConnectedClientsList.Remove(ConnectedClients[clientId]);
            }

            ConnectedClients.Remove(clientId);

            foreach (var networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                networkObject.Observers.Remove(clientId);
            }
        }

        /// <summary>
        /// DANGO-TODO: Until we have the CMB Server end-to-end with all features verified working via integration tests,
        /// I am keeping this debug toggle available. (NSS)
        /// </summary>
        internal bool EnableDistributeLogging;

        /// <summary>
        /// Server-Side:
        /// Invoked when a client is disconnected from a server-host
        /// </summary>
        internal void OnClientDisconnectFromServer(ulong clientId)
        {
            if (!LocalClient.IsServer)
            {
                throw new Exception("[OnClientDisconnectFromServer] Was invoked by non-server instance!");
            }

            // If we are shutting down and this is the server or host disconnecting, then ignore
            // clean up as everything that needs to be destroyed will be during shutdown.

            if (NetworkManager.ShutdownInProgress && clientId == NetworkManager.ServerClientId)
            {
                return;
            }

            if (ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
            {
                var playerObject = networkClient.PlayerObject;
                if (playerObject != null)
                {
                    if (!playerObject.DontDestroyWithOwner)
                    {
                        // DANGO-TODO: This is something that would be best for CMB Service to handle as it is part of the disconnection process
                        // If a player NetworkObject is being despawned, make sure to remove all children if they are marked to not be destroyed
                        // with the owner.
                        if (NetworkManager.DistributedAuthorityMode && NetworkManager.DAHost)
                        {
                            // Remove any children from the player object if they are not going to be destroyed with the owner
                            var childNetworkObjects = playerObject.GetComponentsInChildren<NetworkObject>();
                            foreach (var child in childNetworkObjects)
                            {
                                // TODO: We have always just removed all children, but we might think about changing this to preserve the nested child
                                // hierarchy.
                                if (child.DontDestroyWithOwner && child.transform.transform.parent != null)
                                {
                                    // If we are here, then we are running in DAHost mode and have the authority to remove the child from its parent
                                    child.AuthorityAppliedParenting = true;
                                    child.TryRemoveParentCachedWorldPositionStays();
                                }
                            }
                        }

                        if (NetworkManager.PrefabHandler.ContainsHandler(playerObject.GlobalObjectIdHash))
                        {
                            if (NetworkManager.DAHost && NetworkManager.DistributedAuthorityMode)
                            {
                                NetworkManager.SpawnManager.DespawnObject(playerObject, true, NetworkManager.DistributedAuthorityMode);
                            }
                            else
                            {
                                NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(playerObject);
                            }
                        }
                        else if (playerObject.IsSpawned)
                        {
                            // Call despawn to assure NetworkBehaviour.OnNetworkDespawn is invoked on the server-side (when the client side disconnected).
                            // This prevents the issue (when just destroying the GameObject) where any NetworkBehaviour component(s) destroyed before the NetworkObject would not have OnNetworkDespawn invoked.
                            NetworkManager.SpawnManager.DespawnObject(playerObject, true, NetworkManager.DistributedAuthorityMode);
                        }
                    }
                    else if (!NetworkManager.ShutdownInProgress)
                    {
                        if (!NetworkManager.ShutdownInProgress)
                        {
                            playerObject.RemoveOwnership();
                        }
                    }
                }

                // Get the NetworkObjects owned by the disconnected client
                var clientOwnedObjects = NetworkManager.SpawnManager.SpawnedObjectsList.Where((c) => c.OwnerClientId == clientId).ToList();
                if (clientOwnedObjects == null)
                {
                    // This could happen if a client is never assigned a player object and is disconnected
                    // Only log this in verbose/developer mode
                    if (NetworkManager.LogLevel == LogLevel.Developer)
                    {
                        NetworkLog.LogWarning($"ClientID {clientId} disconnected with (0) zero owned objects!  Was a player prefab not assigned?");
                    }
                }
                else
                {
                    // Handle changing ownership and prefab handlers
                    var clientCounter = 0;
                    var predictedClientCount = ConnectedClientsList.Count - 1;
                    var remainingClients = NetworkManager.DistributedAuthorityMode ? ConnectedClientsList.Where((c) => c.ClientId != clientId).ToList() : null;
                    for (int i = clientOwnedObjects.Count - 1; i >= 0; i--)
                    {
                        var ownedObject = clientOwnedObjects[i];
                        if (ownedObject != null)
                        {
                            if (!ownedObject.DontDestroyWithOwner)
                            {
                                if (NetworkManager.PrefabHandler.ContainsHandler(clientOwnedObjects[i].GlobalObjectIdHash))
                                {
                                    NetworkManager.SpawnManager.DespawnObject(ownedObject, true, true);
                                    NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(clientOwnedObjects[i]);
                                }
                                else
                                {
                                    NetworkManager.SpawnManager.DespawnObject(ownedObject, true, true);
                                }
                            }
                            else if (!NetworkManager.ShutdownInProgress)
                            {
                                // NOTE: All of the below code only handles ownership transfer.
                                // For client-server, we just remove the ownership.
                                // For distributed authority, we need to change ownership based on parenting
                                if (NetworkManager.DistributedAuthorityMode)
                                {
                                    // Only NetworkObjects that have the OwnershipStatus.Distributable flag set and no parent
                                    // (ownership is transferred to all children) will have their ownership redistributed.
                                    if (ownedObject.IsOwnershipDistributable && ownedObject.GetCachedParent() == null)
                                    {
                                        if (ownedObject.IsOwnershipLocked)
                                        {
                                            ownedObject.SetOwnershipLock(false);
                                        }

                                        // DANGO-TODO: We will want to match how the CMB service handles this. For now, we just try to evenly distribute
                                        // ownership.
                                        var targetOwner = NetworkManager.ServerClientId;
                                        if (predictedClientCount > 1)
                                        {
                                            clientCounter++;
                                            clientCounter = clientCounter % predictedClientCount;
                                            targetOwner = remainingClients[clientCounter].ClientId;
                                        }
                                        if (EnableDistributeLogging)
                                        {
                                            Debug.Log($"[Disconnected][Client-{clientId}][NetworkObjectId-{ownedObject.NetworkObjectId} Distributed to Client-{targetOwner}");
                                        }
                                        NetworkManager.SpawnManager.ChangeOwnership(ownedObject, targetOwner, true);
                                        // DANGO-TODO: Should we try handling inactive NetworkObjects?
                                        // Ownership gets passed down to all children
                                        var childNetworkObjects = ownedObject.GetComponentsInChildren<NetworkObject>();
                                        foreach (var childObject in childNetworkObjects)
                                        {
                                            // We already changed ownership for this
                                            if (childObject == ownedObject)
                                            {
                                                continue;
                                            }
                                            // If the client owner disconnected, it is ok to unlock this at this point in time.
                                            if (childObject.IsOwnershipLocked)
                                            {
                                                childObject.SetOwnershipLock(false);
                                            }

                                            NetworkManager.SpawnManager.ChangeOwnership(childObject, targetOwner, true);
                                            if (EnableDistributeLogging)
                                            {
                                                Debug.Log($"[Disconnected][Client-{clientId}][Child of {ownedObject.NetworkObjectId}][NetworkObjectId-{ownedObject.NetworkObjectId} Distributed to Client-{targetOwner}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    ownedObject.RemoveOwnership();
                                }
                            }
                        }
                    }
                }

                // TODO: Could(should?) be replaced with more memory per client, by storing the visibility
                foreach (var sobj in NetworkManager.SpawnManager.SpawnedObjectsList)
                {
                    sobj.Observers.Remove(clientId);
                }

                if (ConnectedClients.ContainsKey(clientId))
                {
                    ConnectedClientsList.Remove(ConnectedClients[clientId]);
                    ConnectedClients.Remove(clientId);
                }

                ConnectedClientIds.Remove(clientId);
                var message = new ClientDisconnectedMessage { ClientId = clientId };
                MessageManager?.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, ConnectedClientIds);

                // Used for testing/validation purposes only
#if ENABLE_DAHOST_AUTOPROMOTE_SESSION_OWNER
                if (NetworkManager.DistributedAuthorityMode && !NetworkManager.ShutdownInProgress && NetworkManager.IsListening)
                {
                    var newSessionOwner = NetworkManager.LocalClientId;
                    if (ConnectedClientIds.Count > 1)
                    {
                        var lowestRTT = ulong.MaxValue;
                        var unityTransport = NetworkManager.NetworkConfig.NetworkTransport as Transports.UTP.UnityTransport;

                        foreach (var identifier in ConnectedClientIds)
                        {
                            if (identifier == NetworkManager.LocalClientId)
                            {
                                continue;
                            }
                            var rtt = unityTransport.GetCurrentRtt(identifier);
                            if (rtt < lowestRTT)
                            {
                                newSessionOwner = identifier;
                                lowestRTT = rtt;
                            }
                        }
                    }

                    var sessionOwnerMessage = new SessionOwnerMessage()
                    {
                        SessionOwner = newSessionOwner,
                    };
                    MessageManager?.SendMessage(ref sessionOwnerMessage, NetworkDelivery.ReliableFragmentedSequenced, ConnectedClientIds);
                    NetworkManager.SetSessionOwner(newSessionOwner);
                }
#endif
            }

            // If the client ID transport map exists
            if (ClientIdToTransportIdMap.ContainsKey(clientId))
            {
                var transportId = ClientIdToTransportId(clientId);
                NetworkManager.NetworkConfig.NetworkTransport.DisconnectRemoteClient(transportId);

                InvokeOnClientDisconnectCallback(clientId);

                if (LocalClient.IsHost)
                {
                    InvokeOnPeerDisconnectedCallback(clientId);
                }

                // Clean up the transport to client (and vice versa) mappings
                TransportIdCleanUp(transportId);
            }

            // Assure the client id is no longer in the pending clients list
            // and stop the server-side client approval timeout since the client
            // is no longer connected.
            RemovePendingClient(clientId);

            // Handle cleaning up the server-side client send queue
            MessageManager.ClientDisconnected(clientId);
        }

        /// <summary>
        /// Server-Side:
        /// Invoked when disconnecting a remote client
        /// </summary>
        internal void DisconnectRemoteClient(ulong clientId)
        {
            MessageManager.ProcessSendQueues();
            OnClientDisconnectFromServer(clientId);
        }

        /// <summary>
        /// Server-Side:
        /// Invoked when disconnecting a remote client with the option to provide
        /// a reason.
        /// </summary>
        internal void DisconnectClient(ulong clientId, string reason = null)
        {
            if (!LocalClient.IsServer)
            {
                if (NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
                {
                    throw new NotServerException($"Only server can disconnect remote clients. Please use `{nameof(Shutdown)}()` instead.");
                }
                else
                {
                    Debug.LogWarning($"Currently, clients cannot disconnect other clients from a distributed authority session. Please use `{nameof(Shutdown)}()` instead.");
                    return;
                }
            }

            if (clientId == NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"Disconnecting the local server-host client is not allowed. Use NetworkManager.Shutdown instead.");
                return;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                var disconnectReason = new DisconnectReasonMessage
                {
                    Reason = reason
                };
                SendMessage(ref disconnectReason, NetworkDelivery.Reliable, clientId);
            }

            DisconnectRemoteClient(clientId);
        }

        /// <summary>
        /// Should be invoked when starting a server-host or client
        /// </summary>
        /// <param name="networkManager"></param>
        internal void Initialize(NetworkManager networkManager)
        {
            // Prepare for a new session
            LocalClient.IsApproved = false;
            m_PendingClients.Clear();
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();
            ConnectedClientIds.Clear();
            ClientIdToTransportIdMap.Clear();
            TransportIdToClientIdMap.Clear();
            ClientsToApprove.Clear();
            NetworkObject.OrphanChildren.Clear();
            DisconnectReason = string.Empty;

            NetworkManager = networkManager;
            MessageManager = networkManager.MessageManager;

            NetworkManager.NetworkConfig.NetworkTransport.NetworkMetrics = NetworkManager.MetricsManager.NetworkMetrics;

            NetworkManager.NetworkConfig.NetworkTransport.OnTransportEvent += HandleNetworkEvent;
            NetworkManager.NetworkConfig.NetworkTransport.Initialize(networkManager);
        }

        /// <summary>
        /// Should be called when shutting down the NetworkManager
        /// </summary>
        internal void Shutdown()
        {
            if (LocalClient.IsServer)
            {
                // Build a list of all client ids to be disconnected
                var disconnectedIds = new HashSet<ulong>();

                //Don't know if I have to disconnect the clients. I'm assuming the NetworkTransport does all the cleaning on shutdown. But this way the clients get a disconnect message from server (so long it does't get lost)
                var serverTransportId = NetworkManager.NetworkConfig.NetworkTransport.ServerClientId;
                foreach (KeyValuePair<ulong, NetworkClient> pair in ConnectedClients)
                {
                    if (!disconnectedIds.Contains(pair.Key))
                    {
                        if (pair.Key == serverTransportId)
                        {
                            continue;
                        }

                        disconnectedIds.Add(pair.Key);
                    }
                }

                foreach (KeyValuePair<ulong, PendingClient> pair in PendingClients)
                {
                    if (!disconnectedIds.Contains(pair.Key))
                    {
                        if (pair.Key == serverTransportId)
                        {
                            continue;
                        }

                        disconnectedIds.Add(pair.Key);
                    }
                }

                foreach (var clientId in disconnectedIds)
                {
                    DisconnectRemoteClient(clientId);
                }

                // make sure all messages are flushed before transport disconnects clients
                MessageManager?.ProcessSendQueues();
            }
            else if (NetworkManager != null && NetworkManager.IsListening && LocalClient.IsClient)
            {
                // make sure all messages are flushed before disconnecting
                MessageManager?.ProcessSendQueues();

                // Client only, send disconnect and if transport throws and exception, log the exception and continue the shutdown sequence (or forever be shutting down)
                try
                {
                    NetworkManager.NetworkConfig.NetworkTransport.DisconnectLocalClient();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            LocalClient.IsApproved = false;
            LocalClient.IsConnected = false;
            ConnectedClients.Clear();
            ConnectedClientIds.Clear();
            ConnectedClientsList.Clear();

            if (NetworkManager != null && NetworkManager.NetworkConfig?.NetworkTransport != null)
            {
                NetworkManager.NetworkConfig.NetworkTransport.OnTransportEvent -= HandleNetworkEvent;
            }

            // This is required for handling the potential scenario where multiple NetworkManager instances are created.
            // See MTT-860 for more information
            if (IsListening)
            {
                //The Transport is set during initialization, thus it is possible for the Transport to be null
                var transport = NetworkManager.NetworkConfig?.NetworkTransport;
                if (transport != null)
                {
                    transport.Shutdown();

                    if (NetworkManager.LogLevel <= LogLevel.Developer)
                    {
                        NetworkLog.LogInfo($"{nameof(NetworkConnectionManager)}.{nameof(Shutdown)}() -> {nameof(IsListening)} && {nameof(NetworkManager.NetworkConfig.NetworkTransport)} != null -> {nameof(NetworkTransport)}.{nameof(NetworkTransport.Shutdown)}()");
                    }
                }
            }
        }

        internal unsafe int SendMessage<TMessageType, TClientIdListType>(ref TMessageType message, NetworkDelivery delivery, in TClientIdListType clientIds)
            where TMessageType : INetworkMessage
            where TClientIdListType : IReadOnlyList<ulong>
        {
            // Prevent server sending to itself
            if (LocalClient.IsServer)
            {
                ulong* nonServerIds = stackalloc ulong[clientIds.Count];
                int newIdx = 0;
                for (int idx = 0; idx < clientIds.Count; ++idx)
                {
                    if (clientIds[idx] == NetworkManager.ServerClientId)
                    {
                        continue;
                    }

                    nonServerIds[newIdx++] = clientIds[idx];
                }

                if (newIdx == 0)
                {
                    return 0;
                }

                return MessageManager.SendMessage(ref message, delivery, nonServerIds, newIdx);
            }

            // else
            if (clientIds.Count != 1 || clientIds[0] != NetworkManager.ServerClientId)
            {
                throw new ArgumentException($"Clients may only send messages to {nameof(NetworkManager.ServerClientId)}");
            }

            return MessageManager.SendMessage(ref message, delivery, clientIds);
        }

        internal unsafe int SendMessage<T>(ref T message, NetworkDelivery delivery, ulong* clientIds, int numClientIds)
            where T : INetworkMessage
        {
            // Prevent server sending to itself
            if (LocalClient.IsServer)
            {
                ulong* nonServerIds = stackalloc ulong[numClientIds];
                int newIdx = 0;
                for (int idx = 0; idx < numClientIds; ++idx)
                {
                    if (clientIds[idx] == NetworkManager.ServerClientId)
                    {
                        continue;
                    }

                    nonServerIds[newIdx++] = clientIds[idx];
                }

                if (newIdx == 0)
                {
                    return 0;
                }

                return MessageManager.SendMessage(ref message, delivery, nonServerIds, newIdx);
            }

            // else
            if (numClientIds != 1 || clientIds[0] != NetworkManager.ServerClientId)
            {
                throw new ArgumentException($"Clients may only send messages to {nameof(NetworkManager.ServerClientId)}");
            }

            return MessageManager.SendMessage(ref message, delivery, clientIds, numClientIds);
        }

        internal unsafe int SendMessage<T>(ref T message, NetworkDelivery delivery, in NativeArray<ulong> clientIds)
            where T : INetworkMessage
        {
            return SendMessage(ref message, delivery, (ulong*)clientIds.GetUnsafePtr(), clientIds.Length);
        }

        internal int SendMessage<T>(ref T message, NetworkDelivery delivery, ulong clientId)
            where T : INetworkMessage
        {
            // Prevent server sending to itself or if there is no MessageManager yet then exit early
            if ((LocalClient.IsServer && clientId == NetworkManager.ServerClientId) || MessageManager == null)
            {
                return 0;
            }

            if (!LocalClient.IsServer && clientId != NetworkManager.ServerClientId)
            {
                throw new ArgumentException($"Clients may only send messages to {nameof(NetworkManager.ServerClientId)}");
            }

            return MessageManager.SendMessage(ref message, delivery, clientId);
        }
    }
}
