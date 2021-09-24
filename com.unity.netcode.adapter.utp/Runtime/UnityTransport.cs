using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodeNetworkEvent = Unity.Netcode.NetworkEvent;
using TransportNetworkEvent = Unity.Networking.Transport.NetworkEvent;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;

namespace Unity.Netcode
{
    /// <summary>
    /// Provides an interface that overrides the ability to create your own drivers and pipelines
    /// </summary>
    public interface INetworkStreamDriverConstructor
    {
        void CreateDriver(UnityTransport transport, out NetworkDriver driver, out NetworkPipeline unreliableSequencedPipeline, out NetworkPipeline reliableSequencedPipeline, out NetworkPipeline reliableSequencedFragmentedPipeline);
    }

    public class UnityTransport : NetworkTransport, INetworkStreamDriverConstructor
    {
        public enum ProtocolType
        {
            UnityTransport,
            RelayUnityTransport,
        }

        private enum State
        {
            Disconnected,
            Listening,
            Connected,
        }

        public const int MaximumMessageLength = 6 * 1024;

#pragma warning disable IDE1006 // Naming Styles
        public static INetworkStreamDriverConstructor s_DriverConstructor;
#pragma warning restore IDE1006 // Naming Styles
        public INetworkStreamDriverConstructor DriverConstructor => s_DriverConstructor != null ? s_DriverConstructor : this;

        [SerializeField] private ProtocolType m_ProtocolType;
        [SerializeField] private int m_MessageBufferSize = MaximumMessageLength;
        [SerializeField] private int m_ReciveQueueSize = 128;
        [SerializeField] private int m_SendQueueSize = 128;

        [Tooltip("The maximum size of the send queue for batching Netcode events")]
        [SerializeField] private int m_SendQueueBatchSize = 4096;

        [SerializeField] private string m_ServerAddress = "127.0.0.1";
        [SerializeField] private ushort m_ServerPort = 7777;

        private State m_State = State.Disconnected;
        private NetworkDriver m_Driver;
        private List<INetworkParameter> m_NetworkParameters;
        private byte[] m_MessageBuffer;
        private ulong m_ServerClientId;

        private NetworkPipeline m_UnreliableSequencedPipeline;
        private NetworkPipeline m_ReliableSequencedPipeline;
        private NetworkPipeline m_ReliableSequencedFragmentedPipeline;

        public override ulong ServerClientId => m_ServerClientId;

        public ProtocolType Protocol => m_ProtocolType;

        private RelayServerData m_RelayServerData;

#if UNITY_EDITOR
        private static int ClientPacketDelayMs => UnityEditor.EditorPrefs.GetInt($"NetcodeGameObjects_{Application.productName}_ClientDelay");
        private static int ClientPacketJitterMs => UnityEditor.EditorPrefs.GetInt($"NetcodeGameObjects_{Application.productName}_ClientJitter");
        private static int ClientPacketDropRate => UnityEditor.EditorPrefs.GetInt($"NetcodeGameObjects_{Application.productName}_ClientDropRate");
#elif DEVELOPMENT_BUILD
        public static int ClientPacketDelayMs = 0;
        public static int ClientPacketJitterMs = 0;
        public static int ClientPacketDropRate = 0;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public SimulatorUtility.Parameters ClientSimulatorParameters
        {
            get
            {
                var packetDelay = ClientPacketDelayMs;
                var jitter = ClientPacketJitterMs;
                if (jitter > packetDelay)
                {
                    jitter = packetDelay;
                }

                var packetDrop = ClientPacketDropRate;
                int networkRate = 60; // TODO: read from some better place
                // All 3 packet types every frame stored for maximum delay, doubled for safety margin
                int maxPackets = 2 * (networkRate * 3 * packetDelay + 999) / 1000;
                return new SimulatorUtility.Parameters
                {
                    MaxPacketSize = NetworkParameterConstants.MTU,
                    MaxPacketCount = maxPackets,
                    PacketDelayMs = packetDelay,
                    PacketJitterMs = jitter,
                    PacketDropPercentage = packetDrop
                };
            }
        }
#endif

        /// <summary>
        /// SendQueue dictionary is used to batch events instead of sending them immediately.
        /// </summary>
        private readonly Dictionary<SendTarget, SendQueue> m_SendQueue = new Dictionary<SendTarget, SendQueue>();

        private void InitDriver()
        {
            DriverConstructor.CreateDriver(this, out m_Driver, out m_UnreliableSequencedPipeline, out m_ReliableSequencedPipeline, out m_ReliableSequencedFragmentedPipeline);
        }

        private void DisposeDriver()
        {
            if (m_Driver.IsCreated)
            {
                m_Driver.Dispose();
            }
        }

        private NetworkPipeline SelectSendPipeline(NetworkDelivery delivery, int size)
        {
            switch (delivery)
            {
                case NetworkDelivery.Unreliable:
                    return NetworkPipeline.Null;

                case NetworkDelivery.UnreliableSequenced:
                    return m_UnreliableSequencedPipeline;

                case NetworkDelivery.Reliable:
                case NetworkDelivery.ReliableSequenced:
                    return m_ReliableSequencedPipeline;

                case NetworkDelivery.ReliableFragmentedSequenced:
                    // No need to send on the fragmented pipeline if data is smaller than MTU.
                    if (size < NetworkParameterConstants.MTU)
                    {
                        return m_ReliableSequencedPipeline;
                    }

                    return m_ReliableSequencedFragmentedPipeline;

                default:
                    Debug.LogError($"Unknown {nameof(NetworkDelivery)} value: {delivery}");
                    return NetworkPipeline.Null;
            }
        }

        private IEnumerator ClientBindAndConnect(SocketTask task)
        {
            var serverEndpoint = default(NetworkEndPoint);

            if (m_ProtocolType == ProtocolType.RelayUnityTransport)
            {
                //This comparison is currently slow since RelayServerData does not implement a custom comparison operator that doesn't use
                //reflection, but this does not live in the context of a performance-critical loop, it runs once at initial connection time.
                if (m_RelayServerData.Equals(default(RelayServerData)))
                {
                    Debug.LogError("You must call SetRelayServerData() at least once before calling StartRelayServer.");
                    task.IsDone = true;
                    task.Success = false;
                    yield break;
                }

                m_NetworkParameters.Add(new RelayNetworkParameter { ServerData = m_RelayServerData });
            }
            else
            {
                serverEndpoint = NetworkEndPoint.Parse(m_ServerAddress, m_ServerPort);
            }

            InitDriver();

            if (m_Driver.Bind(NetworkEndPoint.AnyIpv4) != 0)
            {
                Debug.LogError("Client failed to bind");
            }
            else
            {
                while (!m_Driver.Bound)
                {
                    yield return null;
                }

                var serverConnection = m_Driver.Connect(serverEndpoint);
                m_ServerClientId = ParseClientId(serverConnection);

                while (m_Driver.GetConnectionState(serverConnection) == NetworkConnection.State.Connecting)
                {
                    yield return null;
                }

                if (m_Driver.GetConnectionState(serverConnection) == NetworkConnection.State.Connected)
                {
                    task.Success = true;
                    m_State = State.Connected;
                }
                else
                {
                    Debug.LogError("Client failed to connect to server");
                }
            }

            task.IsDone = true;
        }

        private IEnumerator ServerBindAndListen(SocketTask task, NetworkEndPoint endPoint)
        {
            InitDriver();

            if (m_Driver.Bind(endPoint) != 0)
            {
                Debug.LogError("Server failed to bind");
            }
            else
            {
                while (!m_Driver.Bound)
                {
                    yield return null;
                }

                if (m_Driver.Listen() == 0)
                {
                    task.Success = true;
                    m_State = State.Listening;
                }
                else
                {
                    Debug.LogError("Server failed to listen");
                }
            }

            task.IsDone = true;
        }

        private static RelayAllocationId ConvertFromAllocationIdBytes(byte[] allocationIdBytes)
        {
            unsafe
            {
                fixed (byte* ptr = allocationIdBytes)
                {
                    return RelayAllocationId.FromBytePointer(ptr, allocationIdBytes.Length);
                }
            }
        }

        private static RelayHMACKey ConvertFromHMAC(byte[] hmac)
        {
            unsafe
            {
                fixed (byte* ptr = hmac)
                {
                    return RelayHMACKey.FromBytePointer(ptr, RelayHMACKey.k_Length);
                }
            }
        }

        private static RelayConnectionData ConvertConnectionData(byte[] connectionData)
        {
            unsafe
            {
                fixed (byte* ptr = connectionData)
                {
                    return RelayConnectionData.FromBytePointer(ptr, RelayConnectionData.k_Length);
                }
            }
        }

        public void SetRelayServerData(string ipv4Address, ushort port, byte[] allocationIdBytes, byte[] keyBytes,
            byte[] connectionDataBytes, byte[] hostConnectionDataBytes = null, bool isSecure = false)
        {
            RelayConnectionData hostConnectionData;

            var serverEndpoint = NetworkEndPoint.Parse(ipv4Address, port);
            var allocationId = ConvertFromAllocationIdBytes(allocationIdBytes);
            var key = ConvertFromHMAC(keyBytes);
            var connectionData = ConvertConnectionData(connectionDataBytes);

            if (hostConnectionDataBytes != null)
            {
                hostConnectionData = ConvertConnectionData(hostConnectionDataBytes);
            }
            else
            {
                hostConnectionData = connectionData;
            }

            m_RelayServerData = new RelayServerData(ref serverEndpoint, 0, ref allocationId, ref connectionData,
                ref hostConnectionData, ref key, isSecure);
            m_RelayServerData.ComputeNewNonce();
        }

        /// <summary>
        /// Sets IP and Port information. This will be ignored if using the Unity Relay and you should call <see cref="SetRelayServerData"/>
        /// </summary>
        public void SetConnectionData(string ipv4Address, ushort port)
        {
            m_ServerAddress = ipv4Address;
            m_ServerPort = port;
        }

        private IEnumerator StartRelayServer(SocketTask task)
        {
            //This comparison is currently slow since RelayServerData does not implement a custom comparison operator that doesn't use
            //reflection, but this does not live in the context of a performance-critical loop, it runs once at initial connection time.
            if (m_RelayServerData.Equals(default(RelayServerData)))
            {
                Debug.LogError("You must call SetRelayServerData() at least once before calling StartRelayServer.");
                task.IsDone = true;
                task.Success = false;
                yield break;
            }
            else
            {
                m_NetworkParameters.Add(new RelayNetworkParameter { ServerData = m_RelayServerData });

                yield return ServerBindAndListen(task, NetworkEndPoint.AnyIpv4);
            }
        }

        private bool AcceptConnection()
        {
            var connection = m_Driver.Accept();

            if (connection == default(NetworkConnection))
            {
                return false;
            }

            InvokeOnTransportEvent(NetcodeNetworkEvent.Connect,
                ParseClientId(connection),
                default(ArraySegment<byte>),
                Time.realtimeSinceStartup);

            return true;

        }

        private bool ProcessEvent()
        {
            var eventType = m_Driver.PopEvent(out var networkConnection, out var reader);

            switch (eventType)
            {
                case TransportNetworkEvent.Type.Connect:
                    InvokeOnTransportEvent(NetcodeNetworkEvent.Connect,
                        ParseClientId(networkConnection),
                        default(ArraySegment<byte>),
                        Time.realtimeSinceStartup);
                    return true;

                case TransportNetworkEvent.Type.Disconnect:
                    InvokeOnTransportEvent(NetcodeNetworkEvent.Disconnect,
                        ParseClientId(networkConnection),
                        default(ArraySegment<byte>),
                        Time.realtimeSinceStartup);
                    return true;

                case TransportNetworkEvent.Type.Data:
                    var isBatched = reader.ReadByte();
                    if (isBatched == 1)
                    {
                        while (reader.GetBytesRead() < reader.Length)
                        {
                            var payloadSize = reader.ReadInt();
                            ReadData(payloadSize, ref reader, ref networkConnection);
                        }
                    }
                    else // If is not batched, then read the entire buffer at once
                    {
                        var payloadSize = reader.ReadInt();

                        ReadData(payloadSize, ref reader, ref networkConnection);
                    }

                    return true;
            }

            return false;
        }

        private unsafe void ReadData(int size, ref DataStreamReader reader, ref NetworkConnection networkConnection)
        {
            if (size > m_MessageBufferSize)
            {
                Debug.LogError("The received message does not fit into the message buffer");
            }
            else
            {
                unsafe
                {
                    fixed (byte* buffer = &m_MessageBuffer[0])
                    {
                        reader.ReadBytes(buffer, size);
                    }
                }

                InvokeOnTransportEvent(NetcodeNetworkEvent.Data,
                    ParseClientId(networkConnection),
                    new ArraySegment<byte>(m_MessageBuffer, 0, size),
                    Time.realtimeSinceStartup
                );
            }
        }

        private void Update()
        {
            if (m_Driver.IsCreated)
            {
                FlushAllSendQueues();

                m_Driver.ScheduleUpdate().Complete();

                while (AcceptConnection() && m_Driver.IsCreated)
                {
                    ;
                }

                while (ProcessEvent() && m_Driver.IsCreated)
                {
                    ;
                }
            }

        }

        private void OnDestroy()
        {
            DisposeDriver();
        }

        private static unsafe ulong ParseClientId(NetworkConnection utpConnectionId)
        {
            return *(ulong*)&utpConnectionId;
        }

        private static unsafe NetworkConnection ParseClientId(ulong netcodeConnectionId)
        {
            return *(NetworkConnection*)&netcodeConnectionId;
        }

        public override void DisconnectLocalClient()
        {
            Debug.Assert(m_State == State.Connected, "DisconnectLocalClient should be called on a connected client");

            if (m_State == State.Connected)
            {
                m_Driver.Disconnect(ParseClientId(m_ServerClientId));
            }
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            Debug.Assert(m_State == State.Listening, "DisconnectRemoteClient should be called on a listening server");

            if (m_State == State.Listening)
            {
                var connection = ParseClientId(clientId);

                if (m_Driver.GetConnectionState(connection) != NetworkConnection.State.Disconnected)
                {
                    m_Driver.Disconnect(connection);
                }

                // we need to cleanup any SendQueues for this connectionID;
                var keys = new NativeList<SendTarget>(16, Allocator.Temp); // use nativelist and manual foreach to avoid allocations
                foreach (var key in m_SendQueue.Keys)
                {
                    if (key.ClientId == clientId)
                    {
                        keys.Add(key);
                    }
                }

                foreach (var queue in keys)
                {
                    m_SendQueue[queue].Dispose();
                    m_SendQueue.Remove(queue);
                }
                keys.Dispose();
            }
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Initialize()
        {
            Debug.Assert(sizeof(ulong) == UnsafeUtility.SizeOf<NetworkConnection>(),
                "Netcode connection id size does not match UTP connection id size");
            Debug.Assert(m_MessageBufferSize > 5, "Message buffer size must be greater than 5");

            m_NetworkParameters = new List<INetworkParameter>();

            // If we want to be able to actually handle messages MaximumMessageLength bytes in
            // size, we need to allow a bit more than that in FragmentationUtility since this needs
            // to account for headers and such. 128 bytes is plenty enough for such overhead.
            var maxFragmentationCapacity = MaximumMessageLength + 128;
            m_NetworkParameters.Add(new FragmentationUtility.Parameters() { PayloadCapacity = maxFragmentationCapacity });
            m_NetworkParameters.Add(new BaselibNetworkParameter()
            {
                maximumPayloadSize = (uint)m_MessageBufferSize,
                receiveQueueCapacity = m_ReciveQueueSize,
                sendQueueCapacity = m_SendQueueSize
            });

            m_MessageBuffer = new byte[m_MessageBufferSize];
        }

        public override NetcodeNetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = default;
            payload = default;
            receiveTime = default;
            return NetcodeNetworkEvent.Nothing;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            var size = payload.Count + 1 + 4; // 1 extra byte for the channel and another 4 for the count of the data
            var pipeline = SelectSendPipeline(networkDelivery, size);

            var sendTarget = new SendTarget(clientId, pipeline);
            if (!m_SendQueue.TryGetValue(sendTarget, out var queue))
            {
                queue = new SendQueue(m_SendQueueBatchSize);
                m_SendQueue.Add(sendTarget, queue);
            }

            var success = queue.AddEvent(payload);
            if (!success) // This would be false only when the SendQueue is full already or we are sending a super large message at once
            {
                // If we are in here data exceeded remaining queue size. This should not happen under normal operation.
                if (payload.Count > queue.Size)
                {
                    // If data is too large to be batched, flush it out immediately. This happens with large initial spawn packets from Netcode for Gameobjects.
                    Debug.LogWarning($"Sent {payload.Count} bytes based on delivery method: {networkDelivery}. Event size exceeds sendQueueBatchSize: ({m_SendQueueBatchSize}). This can be the initial payload!");
                    Debug.Assert(networkDelivery == NetworkDelivery.ReliableFragmentedSequenced); // Messages like this, should always be sent via the fragmented pipeline.
                    SendMessageInstantly(sendTarget.ClientId, payload, pipeline);
                }
                else
                {
                    // Since our queue buffer is full then send that right away, clear it and queue this new data
                    SendBatchedMessageAndClearQueue(sendTarget, queue);
                    Debug.Assert(queue.IsEmpty() == true);
                    queue.Clear();
                    queue.AddEvent(payload);
                }
            }
        }

        private unsafe void SendBatchedMessage(ulong clientId, ref NativeArray<byte> data, NetworkPipeline pipeline)
        {
            var payloadSize = data.Length + 1; // One extra byte to mark whether this message is batched or not
            var result = m_Driver.BeginSend(pipeline, ParseClientId(clientId), out var writer, payloadSize);

            if (result == 0)
            {
                if (data.IsCreated)
                {
                    // This 1 byte indicates whether the message has been batched or not, in this case it is
                    writer.WriteByte(1);
                    writer.WriteBytes(data);
                }

                result = m_Driver.EndSend(writer);
                if (result == payloadSize) // If the whole data fit, then we are done here
                {
                    return;
                }
            }

            Debug.LogError($"Error sending the message {result}");
        }

        private unsafe void SendMessageInstantly(ulong clientId, ArraySegment<byte> data,
            NetworkPipeline pipeline)
        {
            var payloadSize =
                data.Count + 1 + 4; // 1 byte to indicate if the message is batched and 4 for the payload size
            var result = m_Driver.BeginSend(pipeline, ParseClientId(clientId), out var writer, payloadSize);
            if (result == 0)
            {
                if (data.Array != null)
                {
                    writer.WriteByte(0); // This 1 byte indicates whether the message has been batched or not, in this case is not, as is sent instantly
                    writer.WriteInt(data.Count);

                    // Note: we are not writing the one byte for the channel and the other 4 for the data count as it will be handled by the queue
                    unsafe
                    {
                        fixed (byte* dataPtr = &data.Array[data.Offset])
                        {
                            writer.WriteBytes(dataPtr, data.Count);
                        }
                    }
                }

                result = m_Driver.EndSend(writer);
                if (result == payloadSize) // If the whole data fit, then we are done here
                {
                    return;
                }
            }

            Debug.LogError($"Error sending the message {result}");
        }

        /// <summary>
        /// Flushes all send queues.
        /// </summary>
        private void FlushAllSendQueues()
        {
            foreach (var kvp in m_SendQueue)
            {
                if (kvp.Value.IsEmpty())
                {
                    continue;
                }

                SendBatchedMessageAndClearQueue(kvp.Key, kvp.Value);
            }
        }

        private void SendBatchedMessageAndClearQueue(SendTarget sendTarget, SendQueue sendQueue)
        {
            NetworkPipeline pipeline = sendTarget.NetworkPipeline;
            var payloadSize = sendQueue.Count + 1; // 1 extra byte to tell whether the message is batched or not
            if (payloadSize > NetworkParameterConstants.MTU) // If this is bigger than MTU then force it to be sent via the FragmentedReliableSequencedPipeline
            {
                pipeline = SelectSendPipeline(NetworkDelivery.ReliableFragmentedSequenced, payloadSize);
            }

            var sendBuffer = sendQueue.GetData();
            SendBatchedMessage(sendTarget.ClientId, ref sendBuffer, pipeline);
            sendQueue.Clear();
        }

        public override SocketTasks StartClient()
        {
            if (m_Driver.IsCreated)
            {
                return SocketTask.Fault.AsTasks();
            }

            var task = SocketTask.Working;
            StartCoroutine(ClientBindAndConnect(task));
            return task.AsTasks();
        }

        public override SocketTasks StartServer()
        {
            if (m_Driver.IsCreated)
            {
                return SocketTask.Fault.AsTasks();
            }

            var task = SocketTask.Working;
            switch (m_ProtocolType)
            {
                case ProtocolType.UnityTransport:
                    StartCoroutine(ServerBindAndListen(task, NetworkEndPoint.Parse(m_ServerAddress, m_ServerPort)));
                    break;
                case ProtocolType.RelayUnityTransport:
                    StartCoroutine(StartRelayServer(task));
                    break;
            }

            return task.AsTasks();
        }

        public override void Shutdown()
        {
            DisposeDriver();

            foreach (var queue in m_SendQueue.Values)
            {
                queue.Dispose();
            }

            // make sure we don't leak queues when we shutdown
            m_SendQueue.Clear();
        }

        public void CreateDriver(UnityTransport transport, out NetworkDriver driver, out NetworkPipeline unreliableSequencedPipeline, out NetworkPipeline reliableSequencedPipeline, out NetworkPipeline reliableSequencedFragmentedPipeline)
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var netParams = new NetworkConfigParameter
            {
                maxConnectAttempts = NetworkParameterConstants.MaxConnectAttempts,
                connectTimeoutMS = NetworkParameterConstants.ConnectTimeoutMS,
                disconnectTimeoutMS = NetworkParameterConstants.DisconnectTimeoutMS,
                maxFrameTimeMS = 100
            };

            var simulatorParams = ClientSimulatorParameters;
            transport.m_NetworkParameters.Insert(0, simulatorParams);
            transport.m_NetworkParameters.Insert(0, netParams);
#endif
            if (transport.m_NetworkParameters.Count > 0)
            {
                driver = NetworkDriver.Create(transport.m_NetworkParameters.ToArray());
            }
            else
            {
                driver = NetworkDriver.Create();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (simulatorParams.PacketDelayMs > 0 || simulatorParams.PacketDropInterval > 0)
            {
                unreliableSequencedPipeline = driver.CreatePipeline(
                    typeof(UnreliableSequencedPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend));
                reliableSequencedPipeline = driver.CreatePipeline(
                    typeof(ReliableSequencedPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend));
                reliableSequencedFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(ReliableSequencedPipelineStage),
                    typeof(SimulatorPipelineStage),
                    typeof(SimulatorPipelineStageInSend));
            }
            else
#endif
            {
                unreliableSequencedPipeline = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
                reliableSequencedPipeline = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
                reliableSequencedFragmentedPipeline = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage)
                );
            }
        }


        // -------------- Utility Types -------------------------------------------------------------------------------

        /// <summary>
        /// Memory Stream controller to store several events into one single buffer
        /// </summary>
        private class SendQueue : IDisposable
        {
            private NativeArray<byte> m_Array;
            private DataStreamWriter m_Stream;

            /// <summary>
            /// The size of the send queue.
            /// </summary>
            public int Size { get; }

            public SendQueue(int size)
            {
                Size = size;
                m_Array = new NativeArray<byte>(size, Allocator.Persistent);
                m_Stream = new DataStreamWriter(m_Array);
            }

            /// <summary>
            /// Ads an event to the send queue.
            /// </summary>
            /// <param name="data">The data to send.</param>
            /// <returns>True if the event was added successfully to the queue. False if there was no space in the queue.</returns>
            internal bool AddEvent(ArraySegment<byte> data)
            {
                // Check if we are about to write more than the buffer can fit
                // Note: 4 bytes for the count of data
                if (m_Stream.Length + data.Count + 4 > Size)
                {
                    return false;
                }

                m_Stream.WriteInt(data.Count);

                unsafe
                {
                    fixed (byte* byteData = data.Array)
                    {
                        m_Stream.WriteBytes(byteData, data.Count);
                    }
                }

                return true;
            }

            internal void Clear()
            {
                m_Stream.Clear();
            }

            internal bool IsEmpty()
            {
                return m_Stream.Length == 0;
            }

            internal int Count => m_Stream.Length;

            internal NativeArray<byte> GetData()
            {
                return m_Stream.AsNativeArray();
            }

            public void Dispose()
            {
                m_Array.Dispose();
            }
        }

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
