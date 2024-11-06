// NetSim Implementation compilation boilerplate
// All references to UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED should be defined in the same way,
// as any discrepancies are likely to result in build failures
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && !UNITY_MP_TOOLS_NETSIM_DISABLED_IN_DEVELOP) || (!DEVELOPMENT_BUILD && UNITY_MP_TOOLS_NETSIM_ENABLED_IN_RELEASE)
#define UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using NetcodeNetworkEvent = Unity.Netcode.NetworkEvent;
using TransportNetworkEvent = Unity.Networking.Transport.NetworkEvent;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
#if UTP_TRANSPORT_2_0_ABOVE
using Unity.Networking.Transport.TLS;
#endif

#if !UTP_TRANSPORT_2_0_ABOVE
using NetworkEndpoint = Unity.Networking.Transport.NetworkEndPoint;
#endif

namespace Unity.Netcode.Transports.UTP
{
    /// <summary>
    /// Provides an interface that overrides the ability to create your own drivers and pipelines
    /// </summary>
    public interface INetworkStreamDriverConstructor
    {
        /// <summary>
        /// Creates the internal NetworkDriver
        /// </summary>
        /// <param name="transport">The owner transport</param>
        /// <param name="driver">The driver</param>
        /// <param name="unreliableFragmentedPipeline">The UnreliableFragmented NetworkPipeline</param>
        /// <param name="unreliableSequencedFragmentedPipeline">The UnreliableSequencedFragmented NetworkPipeline</param>
        /// <param name="reliableSequencedPipeline">The ReliableSequenced NetworkPipeline</param>
        void CreateDriver(
            UnityTransport transport,
            out NetworkDriver driver,
            out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline,
            out NetworkPipeline reliableSequencedPipeline);
    }

    /// <summary>
    /// Helper utility class to convert <see cref="Networking.Transport"/> error codes to human readable error messages.
    /// </summary>
    public static class ErrorUtilities
    {
        private static readonly FixedString128Bytes k_NetworkSuccess = "Success";
        private static readonly FixedString128Bytes k_NetworkIdMismatch = "Invalid connection ID {0}.";
        private static readonly FixedString128Bytes k_NetworkVersionMismatch = "Connection ID is invalid. Likely caused by sending on stale connection {0}.";
        private static readonly FixedString128Bytes k_NetworkStateMismatch = "Connection state is invalid. Likely caused by sending on connection {0} which is stale or still connecting.";
        private static readonly FixedString128Bytes k_NetworkPacketOverflow = "Packet is too large to be allocated by the transport.";
        private static readonly FixedString128Bytes k_NetworkSendQueueFull = "Unable to queue packet in the transport. Likely caused by send queue size ('Max Send Queue Size') being too small.";

        /// <summary>
        /// Convert a UTP error code to human-readable error message.
        /// </summary>
        /// <param name="error">UTP error code.</param>
        /// <param name="connectionId">ID of the connection on which the error occurred.</param>
        /// <returns>Human-readable error message.</returns>
        public static string ErrorToString(Networking.Transport.Error.StatusCode error, ulong connectionId)
        {
            return ErrorToString((int)error, connectionId);
        }

        internal static string ErrorToString(int error, ulong connectionId)
        {
            return ErrorToFixedString(error, connectionId).ToString();
        }

        internal static FixedString128Bytes ErrorToFixedString(int error, ulong connectionId)
        {
            switch ((Networking.Transport.Error.StatusCode)error)
            {
                case Networking.Transport.Error.StatusCode.Success:
                    return k_NetworkSuccess;
                case Networking.Transport.Error.StatusCode.NetworkIdMismatch:
                    return FixedString.Format(k_NetworkIdMismatch, connectionId);
                case Networking.Transport.Error.StatusCode.NetworkVersionMismatch:
                    return FixedString.Format(k_NetworkVersionMismatch, connectionId);
                case Networking.Transport.Error.StatusCode.NetworkStateMismatch:
                    return FixedString.Format(k_NetworkStateMismatch, connectionId);
                case Networking.Transport.Error.StatusCode.NetworkPacketOverflow:
                    return k_NetworkPacketOverflow;
                case Networking.Transport.Error.StatusCode.NetworkSendQueueFull:
                    return k_NetworkSendQueueFull;
                default:
                    return FixedString.Format("Unknown error code {0}.", error);
            }
        }
    }

    /// <summary>
    /// The Netcode for GameObjects NetworkTransport for UnityTransport.
    /// Note: This is highly recommended to use over UNet.
    /// </summary>
    [AddComponentMenu("Netcode/Unity Transport")]
    public partial class UnityTransport : NetworkTransport, INetworkStreamDriverConstructor
    {
        /// <summary>
        /// Enum type stating the type of protocol
        /// </summary>
        public enum ProtocolType
        {
            /// <summary>
            /// Unity Transport Protocol
            /// </summary>
            UnityTransport,
            /// <summary>
            /// Unity Transport Protocol over Relay
            /// </summary>
            RelayUnityTransport,
        }

        private enum State
        {
            Disconnected,
            Listening,
            Connected,
        }

        /// <summary>
        /// The default maximum (receive) packet queue size
        /// </summary>
        public const int InitialMaxPacketQueueSize = 128;

        /// <summary>
        /// The default maximum payload size
        /// </summary>
        public const int InitialMaxPayloadSize = 6 * 1024;

        /// <summary>
        /// The default maximum send queue size
        /// </summary>
        [Obsolete("MaxSendQueueSize is now determined dynamically (can still be set programmatically using the MaxSendQueueSize property). This initial value is not used anymore.", false)]
        public const int InitialMaxSendQueueSize = 16 * InitialMaxPayloadSize;

        // Maximum reliable throughput, assuming the full reliable window can be sent on every
        // frame at 60 FPS. This will be a large over-estimation in any realistic scenario.
        private const int k_MaxReliableThroughput = (NetworkParameterConstants.MTU * 64 * 60) / 1000; // bytes per millisecond

        private static ConnectionAddressData s_DefaultConnectionAddressData = new ConnectionAddressData { Address = "127.0.0.1", Port = 7777, ServerListenAddress = string.Empty };

#pragma warning disable IDE1006 // Naming Styles

        /// <summary>
        /// The global <see cref="INetworkStreamDriverConstructor"/> implementation
        /// </summary>
        public static INetworkStreamDriverConstructor s_DriverConstructor;
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>
        /// Returns either the global <see cref="INetworkStreamDriverConstructor"/> implementation or the current <see cref="UnityTransport"/> instance
        /// </summary>
        public INetworkStreamDriverConstructor DriverConstructor => s_DriverConstructor ?? this;

        [Tooltip("Which protocol should be selected (Relay/Non-Relay).")]
        [SerializeField]
        private ProtocolType m_ProtocolType;

#if UTP_TRANSPORT_2_0_ABOVE
        [Tooltip("Per default the client/server will communicate over UDP. Set to true to communicate with WebSocket.")]
        [SerializeField]
        private bool m_UseWebSockets = false;

        public bool UseWebSockets
        {
            get => m_UseWebSockets;
            set => m_UseWebSockets = value;
        }

        /// <summary>
        /// Per default the client/server communication will not be encrypted. Select true to enable DTLS for UDP and TLS for Websocket.
        /// </summary>
        [Tooltip("Per default the client/server communication will not be encrypted. Select true to enable DTLS for UDP and TLS for Websocket.")]
        [SerializeField]
        private bool m_UseEncryption = false;
        public bool UseEncryption
        {
            get => m_UseEncryption;
            set => m_UseEncryption = value;
        }
#endif

        [Tooltip("The maximum amount of packets that can be in the internal send/receive queues. Basically this is how many packets can be sent/received in a single update/frame.")]
        [SerializeField]
        private int m_MaxPacketQueueSize = InitialMaxPacketQueueSize;

        /// <summary>The maximum amount of packets that can be in the internal send/receive queues.</summary>
        /// <remarks>Basically this is how many packets can be sent/received in a single update/frame.</remarks>
        public int MaxPacketQueueSize
        {
            get => m_MaxPacketQueueSize;
            set => m_MaxPacketQueueSize = value;
        }

        [Tooltip("The maximum size of an unreliable payload that can be handled by the transport. The memory for MaxPayloadSize is allocated once per connection and is released when the connection is closed.")]
        [SerializeField]
        private int m_MaxPayloadSize = InitialMaxPayloadSize;

        /// <summary>The maximum size of an unreliable payload that can be handled by the transport.</summary>
        /// <remarks>The memory for MaxPayloadSize is allocated once per connection and is released when the connection is closed.</remarks>
        public int MaxPayloadSize
        {
            get => m_MaxPayloadSize;
            set => m_MaxPayloadSize = value;
        }

        private int m_MaxSendQueueSize = 0;

        /// <summary>The maximum size in bytes of the transport send queue.</summary>
        /// <remarks>
        /// The send queue accumulates messages for batching and stores messages when other internal
        /// send queues are full. Note that there should not be any need to set this value manually
        /// since the send queue size is dynamically sized based on need.
        ///
        /// This value should only be set if you have particular requirements (e.g. if you want to
        /// limit the memory usage of the send queues). Note however that setting this value too low
        /// can easily lead to disconnections under heavy traffic.
        /// </remarks>
        public int MaxSendQueueSize
        {
            get => m_MaxSendQueueSize;
            set => m_MaxSendQueueSize = value;
        }

        [Tooltip("Timeout in milliseconds after which a heartbeat is sent if there is no activity.")]
        [SerializeField]
        private int m_HeartbeatTimeoutMS = NetworkParameterConstants.HeartbeatTimeoutMS;

        /// <summary>Timeout in milliseconds after which a heartbeat is sent if there is no activity.</summary>
        public int HeartbeatTimeoutMS
        {
            get => m_HeartbeatTimeoutMS;
            set => m_HeartbeatTimeoutMS = value;
        }

        [Tooltip("Timeout in milliseconds indicating how long we will wait until we send a new connection attempt.")]
        [SerializeField]
        private int m_ConnectTimeoutMS = NetworkParameterConstants.ConnectTimeoutMS;

        /// <summary>
        /// Timeout in milliseconds indicating how long we will wait until we send a new connection attempt.
        /// </summary>
        public int ConnectTimeoutMS
        {
            get => m_ConnectTimeoutMS;
            set => m_ConnectTimeoutMS = value;
        }

        [Tooltip("The maximum amount of connection attempts we will try before disconnecting.")]
        [SerializeField]
        private int m_MaxConnectAttempts = NetworkParameterConstants.MaxConnectAttempts;

        /// <summary>The maximum amount of connection attempts we will try before disconnecting.</summary>
        public int MaxConnectAttempts
        {
            get => m_MaxConnectAttempts;
            set => m_MaxConnectAttempts = value;
        }

        [Tooltip("Inactivity timeout after which a connection will be disconnected. The connection needs to receive data from the connected endpoint within this timeout. Note that with heartbeats enabled, simply not sending any data will not be enough to trigger this timeout (since heartbeats count as connection events).")]
        [SerializeField]
        private int m_DisconnectTimeoutMS = NetworkParameterConstants.DisconnectTimeoutMS;

        /// <summary>Inactivity timeout after which a connection will be disconnected.</summary>
        /// <remarks>
        /// The connection needs to receive data from the connected endpoint within this timeout.
        /// Note that with heartbeats enabled, simply not sending any data will not be enough to
        /// trigger this timeout (since heartbeats count as connection events).
        /// </remarks>
        public int DisconnectTimeoutMS
        {
            get => m_DisconnectTimeoutMS;
            set => m_DisconnectTimeoutMS = value;
        }

        /// <summary>
        /// Structure to store the address to connect to
        /// </summary>
        [Serializable]
        public struct ConnectionAddressData
        {
            /// <summary>
            /// IP address of the server (address to which clients will connect to).
            /// </summary>
            [Tooltip("IP address of the server (address to which clients will connect to).")]
            [SerializeField]
            public string Address;

            /// <summary>
            /// UDP port of the server.
            /// </summary>
            [Tooltip("UDP port of the server.")]
            [SerializeField]
            public ushort Port;

            /// <summary>
            /// IP address the server will listen on. If not provided, will use localhost.
            /// </summary>
            [Tooltip("IP address the server will listen on. If not provided, will use localhost.")]
            [SerializeField]
            public string ServerListenAddress;

            private static NetworkEndpoint ParseNetworkEndpoint(string ip, ushort port, bool silent = false)
            {
                NetworkEndpoint endpoint = default;

                if (!NetworkEndpoint.TryParse(ip, port, out endpoint, NetworkFamily.Ipv4) &&
                    !NetworkEndpoint.TryParse(ip, port, out endpoint, NetworkFamily.Ipv6))
                {
                    if (!silent)
                    {
                        Debug.LogError($"Invalid network endpoint: {ip}:{port}.");
                    }
                }

                return endpoint;
            }

            /// <summary>
            /// Endpoint (IP address and port) clients will connect to.
            /// </summary>
            public NetworkEndpoint ServerEndPoint => ParseNetworkEndpoint(Address, Port);

            /// <summary>
            /// Endpoint (IP address and port) server will listen/bind on.
            /// </summary>
            public NetworkEndpoint ListenEndPoint
            {
                get
                {
                    if (string.IsNullOrEmpty(ServerListenAddress))
                    {
                        var ep = NetworkEndpoint.LoopbackIpv4;

                        // If an address was entered and it's IPv6, switch to using ::1 as the
                        // default listen address. (Otherwise we always assume IPv4.)
                        if (!string.IsNullOrEmpty(Address) && ServerEndPoint.Family == NetworkFamily.Ipv6)
                        {
                            ep = NetworkEndpoint.LoopbackIpv6;
                        }

                        return ep.WithPort(Port);
                    }
                    else
                    {
                        return ParseNetworkEndpoint(ServerListenAddress, Port);
                    }
                }
            }

            public bool IsIpv6 => !string.IsNullOrEmpty(Address) && ParseNetworkEndpoint(Address, Port, true).Family == NetworkFamily.Ipv6;
        }


        /// <summary>
        /// The connection (address) data for this <see cref="UnityTransport"/> instance.
        /// This is where you can change IP Address, Port, or server's listen address.
        /// <see cref="ConnectionAddressData"/>
        /// </summary>
        public ConnectionAddressData ConnectionData = s_DefaultConnectionAddressData;

        /// <summary>
        /// Parameters for the Network Simulator
        /// </summary>
        [Serializable]
        public struct SimulatorParameters
        {
            /// <summary>
            /// Delay to add to every send and received packet (in milliseconds). Only applies in the editor and in development builds. The value is ignored in production builds.
            /// </summary>
            [Tooltip("Delay to add to every send and received packet (in milliseconds). Only applies in the editor and in development builds. The value is ignored in production builds.")]
            [SerializeField]
            public int PacketDelayMS;

            /// <summary>
            /// Jitter (random variation) to add/substract to the packet delay (in milliseconds). Only applies in the editor and in development builds. The value is ignored in production builds.
            /// </summary>
            [Tooltip("Jitter (random variation) to add/substract to the packet delay (in milliseconds). Only applies in the editor and in development builds. The value is ignored in production builds.")]
            [SerializeField]
            public int PacketJitterMS;

            /// <summary>
            /// Percentage of sent and received packets to drop. Only applies in the editor and in the editor and in developments builds.
            /// </summary>
            [Tooltip("Percentage of sent and received packets to drop. Only applies in the editor and in the editor and in developments builds.")]
            [SerializeField]
            public int PacketDropRate;
        }

        /// <summary>
        /// Can be used to simulate poor network conditions such as:
        /// - packet delay/latency
        /// - packet jitter (variances in latency, see: https://en.wikipedia.org/wiki/Jitter)
        /// - packet drop rate (packet loss)
        /// </summary>

#if UTP_TRANSPORT_2_0_ABOVE
        [Obsolete("DebugSimulator is no longer supported and has no effect. Use Network Simulator from the Multiplayer Tools package.", false)]
#endif
        public SimulatorParameters DebugSimulator = new SimulatorParameters
        {
            PacketDelayMS = 0,
            PacketJitterMS = 0,
            PacketDropRate = 0
        };

        internal uint? DebugSimulatorRandomSeed { get; set; } = null;

        private struct PacketLossCache
        {
            public int PacketsReceived;
            public int PacketsDropped;
            public float PacketLoss;
        };

        internal static event Action<int, NetworkDriver> TransportInitialized;
        internal static event Action<int> TransportDisposed;
        public NetworkDriver NetworkDriver => m_Driver;

        protected NetworkDriver m_Driver;

        /// <summary>
        /// Gets a reference to the <see cref="Networking.Transport.NetworkDriver"/>.
        /// </summary>
        /// <returns>ref <see cref="Networking.Transport.NetworkDriver"/></returns>
        public ref NetworkDriver GetNetworkDriver()
        {
            return ref m_Driver;
        }

        /// <summary>
        /// Gets the local sytem's <see cref="NetworkEndpoint"/> that is assigned for the current network session.
        /// </summary>
        /// <remarks>
        /// If the driver is not created it will return an invalid <see cref="NetworkEndpoint"/>.
        /// </remarks>
        /// <returns><see cref="NetworkEndpoint"/></returns>
        public NetworkEndpoint GetLocalEndpoint()
        {
            if (m_Driver.IsCreated)
            {
                return m_Driver.GetLocalEndpoint();
            }
            return new NetworkEndpoint();
        }

        private PacketLossCache m_PacketLossCache = new PacketLossCache();

        private State m_State = State.Disconnected;
        private NetworkSettings m_NetworkSettings;
        private ulong m_ServerClientId;

        private NetworkPipeline m_UnreliableFragmentedPipeline;
        private NetworkPipeline m_UnreliableSequencedFragmentedPipeline;
        private NetworkPipeline m_ReliableSequencedPipeline;

        /// <summary>
        /// The client id used to represent the server.
        /// </summary>
        public override ulong ServerClientId => m_ServerClientId;

        /// <summary>
        /// The current ProtocolType used by the transport
        /// </summary>
        public ProtocolType Protocol => m_ProtocolType;

        private RelayServerData m_RelayServerData;

        /// <summary>
        /// NetworkManager associated to this transport instance
        /// </summary>
        protected NetworkManager m_NetworkManager;

        private IRealTimeProvider m_RealTimeProvider;

        /// <summary>
        /// SendQueue dictionary is used to batch events instead of sending them immediately.
        /// </summary>
        private readonly Dictionary<SendTarget, BatchedSendQueue> m_SendQueue = new Dictionary<SendTarget, BatchedSendQueue>();

        // Since reliable messages may be spread out over multiple transport payloads, it's possible
        // to receive only parts of a message in an update. We thus keep the reliable receive queues
        // around to avoid losing partial messages.
        private readonly Dictionary<ulong, BatchedReceiveQueue> m_ReliableReceiveQueues = new Dictionary<ulong, BatchedReceiveQueue>();

        private void InitDriver()
        {
            DriverConstructor.CreateDriver(
                this,
                out m_Driver,
                out m_UnreliableFragmentedPipeline,
                out m_UnreliableSequencedFragmentedPipeline,
                out m_ReliableSequencedPipeline);

            TransportInitialized?.Invoke(GetInstanceID(), NetworkDriver);
        }

        private void DisposeInternals()
        {
            if (m_Driver.IsCreated)
            {
                m_Driver.Dispose();
            }

            m_NetworkSettings.Dispose();

            foreach (var queue in m_SendQueue.Values)
            {
                queue.Dispose();
            }

            m_SendQueue.Clear();

            TransportDisposed?.Invoke(GetInstanceID());
        }

        private NetworkPipeline SelectSendPipeline(NetworkDelivery delivery)
        {
            switch (delivery)
            {
                case NetworkDelivery.Unreliable:
                    return m_UnreliableFragmentedPipeline;

                case NetworkDelivery.UnreliableSequenced:
                    return m_UnreliableSequencedFragmentedPipeline;

                case NetworkDelivery.Reliable:
                case NetworkDelivery.ReliableSequenced:
                case NetworkDelivery.ReliableFragmentedSequenced:
                    return m_ReliableSequencedPipeline;

                default:
                    Debug.LogError($"Unknown {nameof(NetworkDelivery)} value: {delivery}");
                    return NetworkPipeline.Null;
            }
        }

        private bool ClientBindAndConnect()
        {
            var serverEndpoint = default(NetworkEndpoint);

            if (m_ProtocolType == ProtocolType.RelayUnityTransport)
            {
                //This comparison is currently slow since RelayServerData does not implement a custom comparison operator that doesn't use
                //reflection, but this does not live in the context of a performance-critical loop, it runs once at initial connection time.
                if (m_RelayServerData.Equals(default(RelayServerData)))
                {
                    Debug.LogError("You must call SetRelayServerData() at least once before calling StartClient.");
                    return false;
                }

                m_NetworkSettings.WithRelayParameters(ref m_RelayServerData, m_HeartbeatTimeoutMS);
                serverEndpoint = m_RelayServerData.Endpoint;
            }
            else
            {
                serverEndpoint = ConnectionData.ServerEndPoint;
            }

            // Verify the endpoint is valid before proceeding
            if (serverEndpoint.Family == NetworkFamily.Invalid)
            {
                Debug.LogError($"Target server network address ({ConnectionData.Address}) is {nameof(NetworkFamily.Invalid)}!");
                return false;
            }

            InitDriver();

            var bindEndpoint = serverEndpoint.Family == NetworkFamily.Ipv6 ? NetworkEndpoint.AnyIpv6 : NetworkEndpoint.AnyIpv4;
            int result = m_Driver.Bind(bindEndpoint);
            if (result != 0)
            {
                Debug.LogError("Client failed to bind");
                return false;
            }

            var serverConnection = Connect(serverEndpoint);
            m_ServerClientId = ParseClientId(serverConnection);

            return true;
        }

        protected virtual NetworkConnection Connect(NetworkEndpoint serverEndpoint)
        {
            return m_Driver.Connect(serverEndpoint);
        }

        private bool ServerBindAndListen(NetworkEndpoint endPoint)
        {
            // Verify the endpoint is valid before proceeding
            if (endPoint.Family == NetworkFamily.Invalid)
            {
                Debug.LogError($"Network listen address ({ConnectionData.Address}) is {nameof(NetworkFamily.Invalid)}!");
                return false;
            }

            InitDriver();

            int result = m_Driver.Bind(endPoint);
            if (result != 0)
            {
                Debug.LogError("Server failed to bind. This is usually caused by another process being bound to the same port.");
                return false;
            }

            result = m_Driver.Listen();
            if (result != 0)
            {
                Debug.LogError("Server failed to listen.");
                return false;
            }

            m_State = State.Listening;
            return true;
        }

        private void SetProtocol(ProtocolType inProtocol)
        {
            m_ProtocolType = inProtocol;
        }

        /// <summary>Set the relay server data for the server.</summary>
        /// <param name="ipv4Address">IP address or hostname of the relay server.</param>
        /// <param name="port">UDP port of the relay server.</param>
        /// <param name="allocationIdBytes">Allocation ID as a byte array.</param>
        /// <param name="keyBytes">Allocation key as a byte array.</param>
        /// <param name="connectionDataBytes">Connection data as a byte array.</param>
        /// <param name="hostConnectionDataBytes">The HostConnectionData as a byte array.</param>
        /// <param name="isSecure">Whether the connection is secure (uses DTLS).</param>
        public void SetRelayServerData(string ipv4Address, ushort port, byte[] allocationIdBytes, byte[] keyBytes, byte[] connectionDataBytes, byte[] hostConnectionDataBytes = null, bool isSecure = false)
        {
            var hostConnectionData = hostConnectionDataBytes ?? connectionDataBytes;
            m_RelayServerData = new RelayServerData(ipv4Address, port, allocationIdBytes, connectionDataBytes, hostConnectionData, keyBytes, isSecure);
            SetProtocol(ProtocolType.RelayUnityTransport);
        }

        /// <summary>Set the relay server data (using the lower-level Unity Transport data structure).</summary>
        /// <param name="serverData">Data for the Relay server to use.</param>
        public void SetRelayServerData(RelayServerData serverData)
        {
            m_RelayServerData = serverData;
            SetProtocol(ProtocolType.RelayUnityTransport);
        }

        /// <summary>Set the relay server data for the host.</summary>
        /// <param name="ipAddress">IP address or hostname of the relay server.</param>
        /// <param name="port">UDP port of the relay server.</param>
        /// <param name="allocationId">Allocation ID as a byte array.</param>
        /// <param name="key">Allocation key as a byte array.</param>
        /// <param name="connectionData">Connection data as a byte array.</param>
        /// <param name="isSecure">Whether the connection is secure (uses DTLS).</param>
        public void SetHostRelayData(string ipAddress, ushort port, byte[] allocationId, byte[] key, byte[] connectionData, bool isSecure = false)
        {
            SetRelayServerData(ipAddress, port, allocationId, key, connectionData, null, isSecure);
        }

        /// <summary>Set the relay server data for the host.</summary>
        /// <param name="ipAddress">IP address or hostname of the relay server.</param>
        /// <param name="port">UDP port of the relay server.</param>
        /// <param name="allocationId">Allocation ID as a byte array.</param>
        /// <param name="key">Allocation key as a byte array.</param>
        /// <param name="connectionData">Connection data as a byte array.</param>
        /// <param name="hostConnectionData">Host's connection data as a byte array.</param>
        /// <param name="isSecure">Whether the connection is secure (uses DTLS).</param>
        public void SetClientRelayData(string ipAddress, ushort port, byte[] allocationId, byte[] key, byte[] connectionData, byte[] hostConnectionData, bool isSecure = false)
        {
            SetRelayServerData(ipAddress, port, allocationId, key, connectionData, hostConnectionData, isSecure);
        }

        /// <summary>
        /// Sets IP and Port information. This will be ignored if using the Unity Relay and you should call <see cref="SetRelayServerData"/>
        /// </summary>
        /// <param name="ipv4Address">The remote IP address (despite the name, can be an IPv6 address)</param>
        /// <param name="port">The remote port</param>
        /// <param name="listenAddress">The local listen address</param>
        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null)
        {
            ConnectionData = new ConnectionAddressData
            {
                Address = ipv4Address,
                Port = port,
                ServerListenAddress = listenAddress ?? ipv4Address
            };

            SetProtocol(ProtocolType.UnityTransport);
        }

        /// <summary>
        /// Sets IP and Port information. This will be ignored if using the Unity Relay and you should call <see cref="SetRelayServerData"/>
        /// </summary>
        /// <param name="endPoint">The remote end point</param>
        /// <param name="listenEndPoint">The local listen endpoint</param>
        public void SetConnectionData(NetworkEndpoint endPoint, NetworkEndpoint listenEndPoint = default)
        {
            string serverAddress = endPoint.Address.Split(':')[0];

            string listenAddress = string.Empty;
            if (listenEndPoint != default)
            {
                listenAddress = listenEndPoint.Address.Split(':')[0];
                if (endPoint.Port != listenEndPoint.Port)
                {
                    Debug.LogError($"Port mismatch between server and listen endpoints ({endPoint.Port} vs {listenEndPoint.Port}).");
                }
            }

            SetConnectionData(serverAddress, endPoint.Port, listenAddress);
        }

        /// <summary>Set the parameters for the debug simulator.</summary>
        /// <param name="packetDelay">Packet delay in milliseconds.</param>
        /// <param name="packetJitter">Packet jitter in milliseconds.</param>
        /// <param name="dropRate">Packet drop percentage.</param>

#if UTP_TRANSPORT_2_0_ABOVE
        [Obsolete("SetDebugSimulatorParameters is no longer supported and has no effect. Use Network Simulator from the Multiplayer Tools package.", false)]
#endif

        public void SetDebugSimulatorParameters(int packetDelay, int packetJitter, int dropRate)
        {
            if (m_Driver.IsCreated)
            {
                Debug.LogError("SetDebugSimulatorParameters() must be called before StartClient() or StartServer().");
                return;
            }

            DebugSimulator = new SimulatorParameters
            {
                PacketDelayMS = packetDelay,
                PacketJitterMS = packetJitter,
                PacketDropRate = dropRate
            };
        }

        private bool StartRelayServer()
        {
            //This comparison is currently slow since RelayServerData does not implement a custom comparison operator that doesn't use
            //reflection, but this does not live in the context of a performance-critical loop, it runs once at initial connection time.
            if (m_RelayServerData.Equals(default(RelayServerData)))
            {
                Debug.LogError("You must call SetRelayServerData() at least once before calling StartServer.");
                return false;
            }
            else
            {
                m_NetworkSettings.WithRelayParameters(ref m_RelayServerData, m_HeartbeatTimeoutMS);
                return ServerBindAndListen(NetworkEndpoint.AnyIpv4);
            }
        }

        [BurstCompile]
        private struct SendBatchedMessagesJob : IJob
        {
            public NetworkDriver.Concurrent Driver;
            public SendTarget Target;
            public BatchedSendQueue Queue;
            public NetworkPipeline ReliablePipeline;
            public int MTU;

            public void Execute()
            {
                var clientId = Target.ClientId;
                var connection = ParseClientId(clientId);
                var pipeline = Target.NetworkPipeline;

                while (!Queue.IsEmpty)
                {
                    var result = Driver.BeginSend(pipeline, connection, out var writer);
                    if (result != (int)Networking.Transport.Error.StatusCode.Success)
                    {
                        Debug.LogError($"Error sending message: {ErrorUtilities.ErrorToFixedString(result, clientId)}");
                        return;
                    }

                    // We don't attempt to send entire payloads over the reliable pipeline. Instead we
                    // fragment it manually. This is safe and easy to do since the reliable pipeline
                    // basically implements a stream, so as long as we separate the different messages
                    // in the stream (the send queue does that automatically) we are sure they'll be
                    // reassembled properly at the other end. This allows us to lift the limit of ~44KB
                    // on reliable payloads (because of the reliable window size).
                    var written = pipeline == ReliablePipeline ? Queue.FillWriterWithBytes(ref writer, MTU) : Queue.FillWriterWithMessages(ref writer, MTU);

                    result = Driver.EndSend(writer);
                    if (result == written)
                    {
                        // Batched message was sent successfully. Remove it from the queue.
                        Queue.Consume(written);
                    }
                    else
                    {
                        // Some error occured. If it's just the UTP queue being full, then don't log
                        // anything since that's okay (the unsent message(s) are still in the queue
                        // and we'll retry sending them later). Otherwise log the error and remove the
                        // message from the queue (we don't want to resend it again since we'll likely
                        // just get the same error again).
                        if (result != (int)Networking.Transport.Error.StatusCode.NetworkSendQueueFull)
                        {
                            Debug.LogError($"Error sending the message: {ErrorUtilities.ErrorToFixedString(result, clientId)}");
                            Queue.Consume(written);
                        }

                        return;
                    }
                }
            }
        }

        // Send as many batched messages from the queue as possible.
        private void SendBatchedMessages(SendTarget sendTarget, BatchedSendQueue queue)
        {
            if (!m_Driver.IsCreated)
            {
                return;
            }

            var mtu = 0;
            if (m_NetworkManager)
            {
                var ngoClientId = m_NetworkManager.ConnectionManager.TransportIdToClientId(sendTarget.ClientId);
                mtu = m_NetworkManager.GetPeerMTU(ngoClientId);
            }

            new SendBatchedMessagesJob
            {
                Driver = m_Driver.ToConcurrent(),
                Target = sendTarget,
                Queue = queue,
                ReliablePipeline = m_ReliableSequencedPipeline,
                MTU = mtu,
            }.Run();
        }

        private bool AcceptConnection()
        {
            var connection = m_Driver.Accept();

            if (connection == default)
            {
                return false;
            }

            InvokeOnTransportEvent(NetcodeNetworkEvent.Connect,
                ParseClientId(connection),
                default,
                m_RealTimeProvider.RealTimeSinceStartup);

            return true;

        }

        private void ReceiveMessages(ulong clientId, NetworkPipeline pipeline, DataStreamReader dataReader)
        {
            BatchedReceiveQueue queue;
            if (pipeline == m_ReliableSequencedPipeline)
            {
                if (m_ReliableReceiveQueues.TryGetValue(clientId, out queue))
                {
                    queue.PushReader(dataReader);
                }
                else
                {
                    queue = new BatchedReceiveQueue(dataReader);
                    m_ReliableReceiveQueues[clientId] = queue;
                }
            }
            else
            {
                queue = new BatchedReceiveQueue(dataReader);
            }

            while (!queue.IsEmpty)
            {
                var message = queue.PopMessage();
                if (message == default)
                {
                    // Only happens if there's only a partial message in the queue (rare).
                    break;
                }

                InvokeOnTransportEvent(NetcodeNetworkEvent.Data, clientId, message, m_RealTimeProvider.RealTimeSinceStartup);
            }
        }

        private bool ProcessEvent()
        {
            var eventType = m_Driver.PopEvent(out var networkConnection, out var reader, out var pipeline);
            var clientId = ParseClientId(networkConnection);

            switch (eventType)
            {
                case TransportNetworkEvent.Type.Connect:
                    {
                        InvokeOnTransportEvent(NetcodeNetworkEvent.Connect,
                            clientId,
                            default,
                            m_RealTimeProvider.RealTimeSinceStartup);

                        m_State = State.Connected;
                        return true;
                    }
                case TransportNetworkEvent.Type.Disconnect:
                    {
                        // Handle cases where we're a client receiving a Disconnect event. The
                        // meaning of the event depends on our current state. If we were connected
                        // then it means we got disconnected. If we were disconnected means that our
                        // connection attempt has failed.
                        if (m_State == State.Connected)
                        {
                            m_State = State.Disconnected;
                            m_ServerClientId = default;
                        }
                        else if (m_State == State.Disconnected)
                        {
                            Debug.LogError("Failed to connect to server.");
                            m_ServerClientId = default;
                        }

                        m_ReliableReceiveQueues.Remove(clientId);
                        ClearSendQueuesForClientId(clientId);

                        InvokeOnTransportEvent(NetcodeNetworkEvent.Disconnect,
                            clientId,
                            default,
                            m_RealTimeProvider.RealTimeSinceStartup);

                        return true;
                    }
                case TransportNetworkEvent.Type.Data:
                    {
                        ReceiveMessages(clientId, pipeline, reader);
                        return true;
                    }
            }

            return false;
        }

        private void Update()
        {
            if (m_Driver.IsCreated)
            {
                foreach (var kvp in m_SendQueue)
                {
                    SendBatchedMessages(kvp.Key, kvp.Value);
                }

                m_Driver.ScheduleUpdate().Complete();

                if (m_ProtocolType == ProtocolType.RelayUnityTransport && m_Driver.GetRelayConnectionStatus() == RelayConnectionStatus.AllocationInvalid)
                {
                    Debug.LogError("Transport failure! Relay allocation needs to be recreated, and NetworkManager restarted. " +
                        "Use NetworkManager.OnTransportFailure to be notified of such events programmatically.");

                    InvokeOnTransportEvent(NetcodeNetworkEvent.TransportFailure, 0, default, m_RealTimeProvider.RealTimeSinceStartup);
                    return;
                }

                while (AcceptConnection() && m_Driver.IsCreated)
                {
                    ;
                }

                while (ProcessEvent() && m_Driver.IsCreated)
                {
                    ;
                }

#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                if (m_NetworkManager)
                {
                    ExtractNetworkMetrics();
                }
#endif
            }
        }

        private void OnDestroy()
        {
            DisposeInternals();
        }

#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
        private void ExtractNetworkMetrics()
        {
            if (m_NetworkManager.IsServer)
            {
                var ngoConnectionIds = m_NetworkManager.ConnectedClients.Keys;
                foreach (var ngoConnectionId in ngoConnectionIds)
                {
                    if (ngoConnectionId == 0 && m_NetworkManager.IsHost)
                    {
                        continue;
                    }
                    var transportClientId = m_NetworkManager.ConnectionManager.ClientIdToTransportId(ngoConnectionId);
                    ExtractNetworkMetricsForClient(transportClientId);
                }
            }
            else
            {
                if (m_ServerClientId != 0)
                {
                    ExtractNetworkMetricsForClient(m_ServerClientId);
                }
            }
        }

        private void ExtractNetworkMetricsForClient(ulong transportClientId)
        {
            var networkConnection = ParseClientId(transportClientId);
            ExtractNetworkMetricsFromPipeline(m_UnreliableFragmentedPipeline, networkConnection);
            ExtractNetworkMetricsFromPipeline(m_UnreliableSequencedFragmentedPipeline, networkConnection);
            ExtractNetworkMetricsFromPipeline(m_ReliableSequencedPipeline, networkConnection);

            var rttValue = m_NetworkManager.IsServer ? 0 : ExtractRtt(networkConnection);
            NetworkMetrics.UpdateRttToServer(rttValue);

            var packetLoss = m_NetworkManager.IsServer ? 0 : ExtractPacketLoss(networkConnection);
            NetworkMetrics.UpdatePacketLoss(packetLoss);
        }

        private void ExtractNetworkMetricsFromPipeline(NetworkPipeline pipeline, NetworkConnection networkConnection)
        {
            if (m_Driver.GetConnectionState(networkConnection) != NetworkConnection.State.Connected)
            {
                return;
            }

            //Don't need to dispose of the buffers, they are filled with data pointers.
            m_Driver.GetPipelineBuffers(pipeline,
#if UTP_TRANSPORT_2_0_ABOVE
                NetworkPipelineStageId.Get<NetworkMetricsPipelineStage>(),
#else
                NetworkPipelineStageCollection.GetStageId(typeof(NetworkMetricsPipelineStage)),
#endif
                networkConnection,
                out _,
                out _,
                out var sharedBuffer);

            unsafe
            {
                var networkMetricsContext = (NetworkMetricsContext*)sharedBuffer.GetUnsafePtr();

                NetworkMetrics.TrackPacketSent(networkMetricsContext->PacketSentCount);
                NetworkMetrics.TrackPacketReceived(networkMetricsContext->PacketReceivedCount);

                networkMetricsContext->PacketSentCount = 0;
                networkMetricsContext->PacketReceivedCount = 0;
            }
        }
#endif

        private int ExtractRtt(NetworkConnection networkConnection)
        {
            if (m_Driver.GetConnectionState(networkConnection) != NetworkConnection.State.Connected)
            {
                return 0;
            }

            m_Driver.GetPipelineBuffers(m_ReliableSequencedPipeline,
#if UTP_TRANSPORT_2_0_ABOVE
                NetworkPipelineStageId.Get<ReliableSequencedPipelineStage>(),
#else
                NetworkPipelineStageCollection.GetStageId(typeof(ReliableSequencedPipelineStage)),
#endif
                networkConnection,
                out _,
                out _,
                out var sharedBuffer);

            unsafe
            {
                var sharedContext = (ReliableUtility.SharedContext*)sharedBuffer.GetUnsafePtr();

                return sharedContext->RttInfo.LastRtt;
            }
        }

        private float ExtractPacketLoss(NetworkConnection networkConnection)
        {
            if (m_Driver.GetConnectionState(networkConnection) != NetworkConnection.State.Connected)
            {
                return 0f;
            }

            m_Driver.GetPipelineBuffers(m_ReliableSequencedPipeline,
#if UTP_TRANSPORT_2_0_ABOVE
                NetworkPipelineStageId.Get<ReliableSequencedPipelineStage>(),
#else
                NetworkPipelineStageCollection.GetStageId(typeof(ReliableSequencedPipelineStage)),
#endif
                networkConnection,
                out _,
                out _,
                out var sharedBuffer);

            unsafe
            {
                var sharedContext = (ReliableUtility.SharedContext*)sharedBuffer.GetUnsafePtr();

                var packetReceivedDelta = (float)(sharedContext->stats.PacketsReceived - m_PacketLossCache.PacketsReceived);
                var packetDroppedDelta = (float)(sharedContext->stats.PacketsDropped - m_PacketLossCache.PacketsDropped);

                // There can be multiple update happening in a single frame where no packets have transitioned
                // In those situation we want to return the last packet loss value instead of 0 to avoid invalid swings
                if (packetDroppedDelta == 0 && packetReceivedDelta == 0)
                {
                    return m_PacketLossCache.PacketLoss;
                }

                m_PacketLossCache.PacketsReceived = sharedContext->stats.PacketsReceived;
                m_PacketLossCache.PacketsDropped = sharedContext->stats.PacketsDropped;

                m_PacketLossCache.PacketLoss = packetReceivedDelta > 0 ? packetDroppedDelta / packetReceivedDelta : 0;

                return m_PacketLossCache.PacketLoss;
            }
        }

        private static unsafe ulong ParseClientId(NetworkConnection utpConnectionId)
        {
            return *(ulong*)&utpConnectionId;
        }

        private static unsafe NetworkConnection ParseClientId(ulong netcodeConnectionId)
        {
            return *(NetworkConnection*)&netcodeConnectionId;
        }

        private void ClearSendQueuesForClientId(ulong clientId)
        {
            // NativeList and manual foreach avoids any allocations.
            using var keys = new NativeList<SendTarget>(16, Allocator.Temp);
            foreach (var key in m_SendQueue.Keys)
            {
                if (key.ClientId == clientId)
                {
                    keys.Add(key);
                }
            }

            foreach (var target in keys)
            {
                m_SendQueue[target].Dispose();
                m_SendQueue.Remove(target);
            }
        }

        private void FlushSendQueuesForClientId(ulong clientId)
        {
            foreach (var kvp in m_SendQueue)
            {
                if (kvp.Key.ClientId == clientId)
                {
                    SendBatchedMessages(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Disconnects the local client from the remote
        /// </summary>
        public override void DisconnectLocalClient()
        {
            if (m_State == State.Connected)
            {
                FlushSendQueuesForClientId(m_ServerClientId);

                if (m_Driver.Disconnect(ParseClientId(m_ServerClientId)) == 0)
                {
                    m_State = State.Disconnected;

                    m_ReliableReceiveQueues.Remove(m_ServerClientId);
                    ClearSendQueuesForClientId(m_ServerClientId);

                    // If we successfully disconnect we dispatch a local disconnect message
                    // this how uNET and other transports worked and so this is just keeping with the old behavior
                    // should be also noted on the client this will call shutdown on the NetworkManager and the Transport
                    InvokeOnTransportEvent(NetcodeNetworkEvent.Disconnect,
                        m_ServerClientId,
                        default,
                        m_RealTimeProvider.RealTimeSinceStartup);
                }
            }
        }

        /// <summary>
        /// Disconnects a remote client from the server
        /// </summary>
        /// <param name="clientId">The client to disconnect</param>
        public override void DisconnectRemoteClient(ulong clientId)
        {
            Debug.Assert(m_State == State.Listening, "DisconnectRemoteClient should be called on a listening server");

            if (m_State == State.Listening)
            {
                FlushSendQueuesForClientId(clientId);

                m_ReliableReceiveQueues.Remove(clientId);
                ClearSendQueuesForClientId(clientId);

                var connection = ParseClientId(clientId);
                if (m_Driver.GetConnectionState(connection) != NetworkConnection.State.Disconnected)
                {
                    m_Driver.Disconnect(connection);
                }
            }
        }

        /// <summary>
        /// Gets the current RTT for a specific client
        /// </summary>
        /// <param name="clientId">The client RTT to get</param>
        /// <returns>The RTT</returns>
        public override ulong GetCurrentRtt(ulong clientId)
        {
            // We don't know if this is getting called from inside NGO (which presumably knows to
            // use the transport client ID) or from a user (which will be using the NGO client ID).
            // So we just try both cases (ExtractRtt returns 0 for invalid connections).

            if (m_NetworkManager != null)
            {
                var transportId = m_NetworkManager.ConnectionManager.ClientIdToTransportId(clientId);

                var rtt = ExtractRtt(ParseClientId(transportId));
                if (rtt > 0)
                {
                    return (ulong)rtt;
                }
            }

            return (ulong)ExtractRtt(ParseClientId(clientId));
        }

        /// <summary>
        /// Initializes the transport
        /// </summary>
        /// <param name="networkManager">The NetworkManager that initialized and owns the transport</param>
        public override void Initialize(NetworkManager networkManager = null)
        {
            Debug.Assert(sizeof(ulong) == UnsafeUtility.SizeOf<NetworkConnection>(), "Netcode connection id size does not match UTP connection id size");

            m_NetworkManager = networkManager;

            if (m_NetworkManager && m_NetworkManager.PortOverride.Overidden)
            {
                ConnectionData.Port = m_NetworkManager.PortOverride.Value;
            }

            m_RealTimeProvider = m_NetworkManager ? m_NetworkManager.RealTimeProvider : new RealTimeProvider();

            m_NetworkSettings = new NetworkSettings(Allocator.Persistent);

            // If the user sends a message of exactly m_MaxPayloadSize in length, we need to
            // account for the overhead of its length when we store it in the send queue.
            var fragmentationCapacity = m_MaxPayloadSize + BatchedSendQueue.PerMessageOverhead;
            m_NetworkSettings.WithFragmentationStageParameters(payloadCapacity: fragmentationCapacity);

            // Bump the reliable window size to its maximum size of 64. Since NGO makes heavy use of
            // reliable delivery, we're better off with the increased window size compared to the
            // extra 4 bytes of header that this costs us.
            //
            // We also increase the maximum resend timeout since the default one in UTP is very
            // aggressive (optimized for latency and low bandwidth). With NGO, it's too low and
            // we sometimes notice a lot of useless resends, especially if using Relay. (We can
            // only do this with UTP 2.0 because 1.X doesn't support that parameter.)
            m_NetworkSettings.WithReliableStageParameters(
                windowSize: 64
#if UTP_TRANSPORT_2_0_ABOVE
                ,
                maximumResendTime: m_ProtocolType == ProtocolType.RelayUnityTransport ? 750 : 500
#endif
            );

#if !UTP_TRANSPORT_2_0_ABOVE && !UNITY_WEBGL
            m_NetworkSettings.WithBaselibNetworkInterfaceParameters(
                receiveQueueCapacity: m_MaxPacketQueueSize,
                sendQueueCapacity: m_MaxPacketQueueSize);
#endif
        }

        /// <summary>
        /// Polls for incoming events, with an extra output parameter to report the precise time the event was received.
        /// </summary>
        /// <param name="clientId">The clientId this event is for</param>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="receiveTime">The time the event was received, as reported by m_RealTimeProvider.RealTimeSinceStartup.</param>
        /// <returns>Returns the event type</returns>
        public override NetcodeNetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = default;
            payload = default;
            receiveTime = default;
            return NetcodeNetworkEvent.Nothing;
        }

        /// <summary>
        /// Send a payload to the specified clientId, data and networkDelivery.
        /// </summary>
        /// <param name="clientId">The clientId to send to</param>
        /// <param name="payload">The data to send</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            var pipeline = SelectSendPipeline(networkDelivery);

            if (pipeline != m_ReliableSequencedPipeline && payload.Count > m_MaxPayloadSize)
            {
                Debug.LogError($"Unreliable payload of size {payload.Count} larger than configured 'Max Payload Size' ({m_MaxPayloadSize}).");
                return;
            }

            var sendTarget = new SendTarget(clientId, pipeline);
            if (!m_SendQueue.TryGetValue(sendTarget, out var queue))
            {
                // The maximum size of a send queue is determined according to the disconnection
                // timeout. The idea being that if the send queue contains enough reliable data that
                // sending it all out would take longer than the disconnection timeout, then there's
                // no point storing even more in the queue (it would be like having a ping higher
                // than the disconnection timeout, which is far into the realm of unplayability).
                //
                // The throughput used to determine what consists the maximum send queue size is
                // the maximum theoritical throughput of the reliable pipeline assuming we only send
                // on each update at 60 FPS, which turns out to be around 2.688 MB/s.
                //
                // Note that we only care about reliable throughput for send queues because that's
                // the only case where a full send queue causes a connection loss. Full unreliable
                // send queues are dealt with by flushing it out to the network or simply dropping
                // new messages if that fails.
                var maxCapacity = m_MaxSendQueueSize > 0 ? m_MaxSendQueueSize : m_DisconnectTimeoutMS * k_MaxReliableThroughput;

                queue = new BatchedSendQueue(Math.Max(maxCapacity, m_MaxPayloadSize));
                m_SendQueue.Add(sendTarget, queue);
            }

            if (!queue.PushMessage(payload))
            {
                if (pipeline == m_ReliableSequencedPipeline)
                {
                    // If the message is sent reliably, then we're over capacity and we can't
                    // provide any reliability guarantees anymore. Disconnect the client since at
                    // this point they're bound to become desynchronized.

                    var ngoClientId = m_NetworkManager?.ConnectionManager.TransportIdToClientId(clientId) ?? clientId;
                    Debug.LogError($"Couldn't add payload of size {payload.Count} to reliable send queue. " +
                        $"Closing connection {ngoClientId} as reliability guarantees can't be maintained.");

                    if (clientId == m_ServerClientId)
                    {
                        DisconnectLocalClient();
                    }
                    else
                    {
                        DisconnectRemoteClient(clientId);

                        // DisconnectRemoteClient doesn't notify SDK of disconnection.
                        InvokeOnTransportEvent(NetcodeNetworkEvent.Disconnect,
                            clientId,
                            default(ArraySegment<byte>),
                            m_RealTimeProvider.RealTimeSinceStartup);
                    }
                }
                else
                {
                    // If the message is sent unreliably, we can always just flush everything out
                    // to make space in the send queue. This is an expensive operation, but a user
                    // would need to send A LOT of unreliable traffic in one update to get here.

                    m_Driver.ScheduleFlushSend(default).Complete();
                    SendBatchedMessages(sendTarget, queue);

                    // Don't check for failure. If it still doesn't work, there's nothing we can do
                    // at this point and the message is lost (it was sent unreliable anyway).
                    queue.PushMessage(payload);
                }
            }
        }

        /// <summary>
        /// Connects client to the server
        /// Note:
        /// When this method returns false it could mean:
        /// - You are trying to start a client that is already started
        /// - It failed during the initial port binding when attempting to begin to connect
        /// </summary>
        /// <returns>true if the client was started and false if it failed to start the client</returns>
        public override bool StartClient()
        {
            if (m_Driver.IsCreated)
            {
                return false;
            }

            var succeeded = ClientBindAndConnect();
            if (!succeeded && m_Driver.IsCreated)
            {
                m_Driver.Dispose();
            }
            return succeeded;
        }

        /// <summary>
        /// Starts to listening for incoming clients
        /// Note:
        /// When this method returns false it could mean:
        /// - You are trying to start a client that is already started
        /// - It failed during the initial port binding when attempting to begin to connect
        /// </summary>
        /// <returns>true if the server was started and false if it failed to start the server</returns>
        public override bool StartServer()
        {
            if (m_Driver.IsCreated)
            {
                return false;
            }

            bool succeeded;
            switch (m_ProtocolType)
            {
                case ProtocolType.UnityTransport:
                    succeeded = ServerBindAndListen(ConnectionData.ListenEndPoint);
                    if (!succeeded && m_Driver.IsCreated)
                    {
                        m_Driver.Dispose();
                    }
                    return succeeded;
                case ProtocolType.RelayUnityTransport:
                    succeeded = StartRelayServer();
                    if (!succeeded && m_Driver.IsCreated)
                    {
                        m_Driver.Dispose();
                    }
                    return succeeded;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Shuts down the transport
        /// </summary>
        public override void Shutdown()
        {
            if (m_Driver.IsCreated)
            {
                // Flush all send queues to the network. NGO can be configured to flush its message
                // queue on shutdown. But this only calls the Send() method, which doesn't actually
                // get anything to the network.
                foreach (var kvp in m_SendQueue)
                {
                    SendBatchedMessages(kvp.Key, kvp.Value);
                }

                // The above flush only puts the message in UTP internal buffers, need an update to
                // actually get the messages on the wire. (Normally a flush send would be sufficient,
                // but there might be disconnect messages and those require an update call.)
                m_Driver.ScheduleUpdate().Complete();
            }

            DisposeInternals();

            m_ReliableReceiveQueues.Clear();

            // We must reset this to zero because UTP actually re-uses clientIds if there is a clean disconnect
            m_ServerClientId = 0;
        }

#if UTP_TRANSPORT_2_0_ABOVE
        private void ConfigureSimulatorForUtp2()
        {
            // As DebugSimulator is deprecated, the 'packetDelayMs', 'packetJitterMs' and 'packetDropPercentage'
            // parameters are set to the default and are supposed to be changed using Network Simulator tool instead.
            m_NetworkSettings.WithSimulatorStageParameters(
                maxPacketCount: 300, // TODO Is there any way to compute a better value?
                maxPacketSize: NetworkParameterConstants.MTU,
                packetDelayMs: 0,
                packetJitterMs: 0,
                packetDropPercentage: 0,
                randomSeed: DebugSimulatorRandomSeed ?? (uint)System.Diagnostics.Stopwatch.GetTimestamp()
                , mode: ApplyMode.AllPackets
            );

            m_NetworkSettings.WithNetworkSimulatorParameters();
        }
#else
        private void ConfigureSimulatorForUtp1()
        {
            m_NetworkSettings.WithSimulatorStageParameters(
                maxPacketCount: 300, // TODO Is there any way to compute a better value?
                maxPacketSize: NetworkParameterConstants.MTU,
                packetDelayMs: DebugSimulator.PacketDelayMS,
                packetJitterMs: DebugSimulator.PacketJitterMS,
                packetDropPercentage: DebugSimulator.PacketDropRate,
                randomSeed: DebugSimulatorRandomSeed ?? (uint)System.Diagnostics.Stopwatch.GetTimestamp()
            );
        }
#endif

        protected override NetworkTopologyTypes OnCurrentTopology()
        {
            return m_NetworkManager != null ? m_NetworkManager.NetworkConfig.NetworkTopology : NetworkTopologyTypes.ClientServer;
        }

        private string m_ServerPrivateKey;
        private string m_ServerCertificate;

        private string m_ServerCommonName;
        private string m_ClientCaCertificate;

        /// <summary>Set the server parameters for encryption.</summary>
        /// <remarks>
        /// The public certificate and private key are expected to be in the PEM format, including
        /// the begin/end markers like <c>-----BEGIN CERTIFICATE-----</c>.
        /// </remarks>
        /// <param name="serverCertificate">Public certificate for the server (PEM format).</param>
        /// <param name="serverPrivateKey">Private key for the server (PEM format).</param>
        public void SetServerSecrets(string serverCertificate, string serverPrivateKey)
        {
            m_ServerPrivateKey = serverPrivateKey;
            m_ServerCertificate = serverCertificate;
        }

        /// <summary>Set the client parameters for encryption.</summary>
        /// <remarks>
        /// <para>
        /// If the CA certificate is not provided, validation will be done against the OS/browser
        /// certificate store. This is what you'd want if using certificates from a known provider.
        /// For self-signed certificates, the CA certificate needs to be provided.
        /// </para>
        /// <para>
        /// The CA certificate (if provided) is expected to be in the PEM format, including the
        /// begin/end markers like <c>-----BEGIN CERTIFICATE-----</c>.
        /// </para>
        /// </remarks>
        /// <param name="serverCommonName">Common name of the server (typically hostname).</param>
        /// <param name="caCertificate">CA certificate used to validate the server's authenticity.</param>
        public void SetClientSecrets(string serverCommonName, string caCertificate = null)
        {
            m_ServerCommonName = serverCommonName;
            m_ClientCaCertificate = caCertificate;
        }

        /// <summary>
        /// Creates the internal NetworkDriver
        /// </summary>
        /// <param name="transport">The owner transport</param>
        /// <param name="driver">The driver</param>
        /// <param name="unreliableFragmentedPipeline">The UnreliableFragmented NetworkPipeline</param>
        /// <param name="unreliableSequencedFragmentedPipeline">The UnreliableSequencedFragmented NetworkPipeline</param>
        /// <param name="reliableSequencedPipeline">The ReliableSequenced NetworkPipeline</param>
        public void CreateDriver(UnityTransport transport, out NetworkDriver driver,
            out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline,
            out NetworkPipeline reliableSequencedPipeline)
        {
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7 && !UTP_TRANSPORT_2_0_ABOVE
            NetworkPipelineStageCollection.RegisterPipelineStage(new NetworkMetricsPipelineStage());
#endif

#if UTP_TRANSPORT_2_0_ABOVE && UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED
            ConfigureSimulatorForUtp2();
#elif !UTP_TRANSPORT_2_0_ABOVE && (UNITY_EDITOR || DEVELOPMENT_BUILD)
            ConfigureSimulatorForUtp1();
#endif

            m_NetworkSettings.WithNetworkConfigParameters(
                maxConnectAttempts: transport.m_MaxConnectAttempts,
                connectTimeoutMS: transport.m_ConnectTimeoutMS,
                disconnectTimeoutMS: transport.m_DisconnectTimeoutMS,
#if UTP_TRANSPORT_2_0_ABOVE
                sendQueueCapacity: m_MaxPacketQueueSize,
                receiveQueueCapacity: m_MaxPacketQueueSize,
#endif
                heartbeatTimeoutMS: transport.m_HeartbeatTimeoutMS);

#if UNITY_WEBGL && !UNITY_EDITOR
            if (m_NetworkManager.IsServer && m_ProtocolType != ProtocolType.RelayUnityTransport)
            {
                throw new Exception("WebGL as a server is not supported by Unity Transport, outside the Editor.");
            }
#endif

#if UTP_TRANSPORT_2_0_ABOVE
            if (m_UseEncryption)
            {
                if (m_ProtocolType == ProtocolType.RelayUnityTransport)
                {
                    if (m_RelayServerData.IsSecure == 0)
                    {
                        // log an error because we have mismatched configuration
                        Debug.LogError("Mismatched security configuration, between Relay and local NetworkManager settings");
                    }

                    // No need to to anything else if using Relay because UTP will handle the
                    // configuration of the security parameters on its own.
                }
                else
                {
                    if (m_NetworkManager.IsServer)
                    {
                        if (string.IsNullOrEmpty(m_ServerCertificate) || string.IsNullOrEmpty(m_ServerPrivateKey))
                        {
                            throw new Exception("In order to use encrypted communications, when hosting, you must set the server certificate and key.");
                        }

                        m_NetworkSettings.WithSecureServerParameters(m_ServerCertificate, m_ServerPrivateKey);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(m_ServerCommonName))
                        {
                            throw new Exception("In order to use encrypted communications, clients must set the server common name.");
                        }
                        else if (string.IsNullOrEmpty(m_ClientCaCertificate))
                        {
                            m_NetworkSettings.WithSecureClientParameters(m_ServerCommonName);
                        }
                        else
                        {
                            m_NetworkSettings.WithSecureClientParameters(m_ClientCaCertificate, m_ServerCommonName);
                        }
                    }
                }
            }
#endif

#if UTP_TRANSPORT_2_1_ABOVE
            if (m_ProtocolType == ProtocolType.RelayUnityTransport)
            {
                if (m_UseWebSockets && m_RelayServerData.IsWebSocket == 0)
                {
                    Debug.LogError("Transport is configured to use WebSockets, but Relay server data isn't. Be sure to use \"wss\" as the connection type when creating the server data (instead of \"dtls\" or \"udp\").");
                }

                if (!m_UseWebSockets && m_RelayServerData.IsWebSocket != 0)
                {
                    Debug.LogError("Relay server data indicates usage of WebSockets, but \"Use WebSockets\" checkbox isn't checked under \"Unity Transport\" component.");
                }
            }
#endif

#if UTP_TRANSPORT_2_0_ABOVE
            if (m_UseWebSockets)
            {
                driver = NetworkDriver.Create(new WebSocketNetworkInterface(), m_NetworkSettings);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogWarning($"WebSockets were used even though they're not selected in NetworkManager. You should check {nameof(UseWebSockets)}', on the Unity Transport component, to silence this warning.");
                driver = NetworkDriver.Create(new WebSocketNetworkInterface(), m_NetworkSettings);
#else
                driver = NetworkDriver.Create(new UDPNetworkInterface(), m_NetworkSettings);
#endif
            }
#else
            driver = NetworkDriver.Create(m_NetworkSettings);
#endif

#if MULTIPLAYER_TOOLS_1_0_0_PRE_7 && UTP_TRANSPORT_2_0_ABOVE
            driver.RegisterPipelineStage(new NetworkMetricsPipelineStage());
#endif

#if !UTP_TRANSPORT_2_0_ABOVE
            SetupPipelinesForUtp1(driver,
                out unreliableFragmentedPipeline,
                out unreliableSequencedFragmentedPipeline,
                out reliableSequencedPipeline);
#else
            SetupPipelinesForUtp2(driver,
                out unreliableFragmentedPipeline,
                out unreliableSequencedFragmentedPipeline,
                out reliableSequencedPipeline);
#endif
        }

#if !UTP_TRANSPORT_2_0_ABOVE
        private void SetupPipelinesForUtp1(NetworkDriver driver,
            out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline,
            out NetworkPipeline reliableSequencedPipeline)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugSimulator.PacketDelayMS > 0 || DebugSimulator.PacketDropRate > 0)
            {
                unreliableFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
                unreliableSequencedFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(UnreliableSequencedPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
                reliableSequencedPipeline = driver.CreatePipeline(
                    typeof(ReliableSequencedPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
            }
            else
#endif
            {
                unreliableFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
                unreliableSequencedFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(UnreliableSequencedPipelineStage)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
                reliableSequencedPipeline = driver.CreatePipeline(
                    typeof(ReliableSequencedPipelineStage)
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                    , typeof(NetworkMetricsPipelineStage)
#endif
                );
            }
        }
#else
        private void SetupPipelinesForUtp2(NetworkDriver driver,
            out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline,
            out NetworkPipeline reliableSequencedPipeline)
        {

            unreliableFragmentedPipeline = driver.CreatePipeline(
                typeof(FragmentationPipelineStage)
#if UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED
                , typeof(SimulatorPipelineStage)
#endif
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                , typeof(NetworkMetricsPipelineStage)
#endif
            );

            unreliableSequencedFragmentedPipeline = driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(UnreliableSequencedPipelineStage)
#if UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED
                , typeof(SimulatorPipelineStage)
#endif
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                , typeof(NetworkMetricsPipelineStage)
#endif
            );

            reliableSequencedPipeline = driver.CreatePipeline(
                typeof(ReliableSequencedPipelineStage)
#if UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED
                , typeof(SimulatorPipelineStage)
#endif
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
                , typeof(NetworkMetricsPipelineStage)
#endif
            );
        }
#endif
        // -------------- Utility Types -------------------------------------------------------------------------------


        /// <summary>
        /// Cached information about reliability mode with a certain client
        /// </summary>
        private struct SendTarget : IEquatable<SendTarget>
        {
            public readonly ulong ClientId;
            public readonly NetworkPipeline NetworkPipeline;

            public SendTarget(ulong clientId, NetworkPipeline networkPipeline)
            {
                ClientId = clientId;
                NetworkPipeline = networkPipeline;
            }

            public bool Equals(SendTarget other)
            {
                return ClientId == other.ClientId && NetworkPipeline.Equals(other.NetworkPipeline);
            }

            public override bool Equals(object obj)
            {
                return obj is SendTarget other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ClientId.GetHashCode() * 397) ^ NetworkPipeline.GetHashCode();
                }
            }
        }
    }
}
