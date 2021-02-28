#pragma warning disable 618
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System;
using System.Collections.Generic;
using MLAPI.Exceptions;
using MLAPI.Logging;
using MLAPI.Profiling;
using MLAPI.Transports.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MLAPI.Transports.UNET
{
    public class UnetTransport : NetworkTransport, ITransportProfilerData
    {
        public enum SendMode
        {
            Immediately,
            Queued
        }

        static readonly ProfilingDataStore k_TransportProfilerData = new ProfilingDataStore();
        public static bool profilerEnabled;

        // Inspector / settings
        public int MessageBufferSize = 1024 * 5;
        public int MaxConnections = 100;
        public int MaxSentMessageQueueSize = 128;

        [SerializeField]
        string m_ConnectAddress = "127.0.0.1";
        [SerializeField]
        ushort m_ConnectPort = 7777;
        public int ServerListenPort = 7777;
        public int ServerWebsocketListenPort = 8887;
        public bool SupportWebsocket = false;

        // user-definable channels.  To add your own channel, do something of the form:
        //  #define MY_CHANNEL 0
        //  ...
        //  transport.Channels.Add(
        //     new UnetChannel()
        //       {
        //         Id = Channel.ChannelUnused + MY_CHANNEL,  <<-- must offset from reserved channel offset in MLAPI SDK
        //         Type = QosType.Unreliable
        //       }
        //  );
        public List<UnetChannel> Channels = new List<UnetChannel>();

        // Relay
        public bool UseMLAPIRelay = false;
        public string MLAPIRelayAddress = "184.72.104.138";
        public int MLAPIRelayPort = 8888;

        public SendMode MessageSendMode = SendMode.Immediately;

        // Runtime / state
        private byte[] messageBuffer;
        private WeakReference temporaryBufferReference;

        // Lookup / translation
        private readonly Dictionary<NetworkChannel, int> channelNameToId = new Dictionary<NetworkChannel, int>();
        private readonly Dictionary<int, NetworkChannel> channelIdToName = new Dictionary<int, NetworkChannel>();
        private int serverConnectionId;
        private int serverHostId;

        private SocketTask connectTask;
        public override ulong ServerClientId => GetMLAPIClientId(0, 0, true);

        /// <inheritdoc />
        public override string NetworkAddress { get { return m_ConnectAddress; } set { m_ConnectAddress = value; } }

        /// <inheritdoc />
        public override ushort NetworkPort { get { return m_ConnectPort; } set { m_ConnectPort = value; } }

        protected void LateUpdate()
        {
            if (UnityEngine.Networking.NetworkTransport.IsStarted && MessageSendMode == SendMode.Queued)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsList.Count; i++)
                    {
                        SendQueued(NetworkManager.Singleton.ConnectedClientsList[i].ClientId);
                    }
                }
                else
                {
                    SendQueued(NetworkManager.Singleton.LocalClientId);
                }
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkChannel networkChannel)
        {
            if (profilerEnabled)
            {
                k_TransportProfilerData.Increment(ProfilerConstants.NumberOfTransportSends);
            }

            GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

            int channelId = 0;

            if (channelNameToId.ContainsKey(networkChannel))
            {
                channelId = channelNameToId[networkChannel];
            }
            else
            {
                channelId = channelNameToId[NetworkChannel.Internal];
            }

            byte[] buffer;

            if (data.Offset > 0)
            {
                // UNET cant handle this, do a copy

                if (messageBuffer.Length >= data.Count)
                {
                    buffer = messageBuffer;
                }
                else
                {
                    object bufferRef = null;
                    if (temporaryBufferReference != null && ((bufferRef = temporaryBufferReference.Target) != null) && ((byte[])bufferRef).Length >= data.Count)
                    {
                        buffer = (byte[])bufferRef;
                    }
                    else
                    {
                        buffer = new byte[data.Count];
                        temporaryBufferReference = new WeakReference(buffer);
                    }
                }

                Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);
            }
            else
            {
                buffer = data.Array;
            }

            if (MessageSendMode == SendMode.Queued)
            {
                RelayTransport.QueueMessageForSending(hostId, connectionId, channelId, buffer, data.Count, out byte error);
            }
            else
            {
                RelayTransport.Send(hostId, connectionId, channelId, buffer, data.Count, out byte error);
            }
        }

        public void SendQueued(ulong clientId)
        {
            if (profilerEnabled)
            {
                k_TransportProfilerData.Increment(ProfilerConstants.NumberOfTransportSendQueues);
            }

            GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

            RelayTransport.SendQueuedMessages(hostId, connectionId, out byte error);
        }

        public override NetworkEvent PollEvent(out ulong clientId, out NetworkChannel networkChannel, out ArraySegment<byte> payload, out float receiveTime)
        {
            NetworkEventType eventType = RelayTransport.Receive(out int hostId, out int connectionId, out int channelId, messageBuffer, messageBuffer.Length, out int receivedSize, out byte error);

            clientId = GetMLAPIClientId((byte)hostId, (ushort)connectionId, false);

            receiveTime = UnityEngine.Time.realtimeSinceStartup;

            NetworkError networkError = (NetworkError)error;

            if (networkError == NetworkError.MessageToLong)
            {
                byte[] tempBuffer;

                if (temporaryBufferReference != null && temporaryBufferReference.IsAlive && ((byte[])temporaryBufferReference.Target).Length >= receivedSize)
                {
                    tempBuffer = (byte[])temporaryBufferReference.Target;
                }
                else
                {
                    tempBuffer = new byte[receivedSize];
                    temporaryBufferReference = new WeakReference(tempBuffer);
                }

                eventType = RelayTransport.Receive(out hostId, out connectionId, out channelId, tempBuffer, tempBuffer.Length, out receivedSize, out error);
                payload = new ArraySegment<byte>(tempBuffer, 0, receivedSize);
            }
            else
            {
                payload = new ArraySegment<byte>(messageBuffer, 0, receivedSize);
            }

            if (channelIdToName.ContainsKey(channelId))
            {
                networkChannel = channelIdToName[channelId];
            }
            else
            {
                networkChannel = NetworkChannel.Internal;
            }

            if (connectTask != null && hostId == serverHostId && connectionId == serverConnectionId)
            {
                if (eventType == NetworkEventType.ConnectEvent)
                {
                    // We just got a response to our connect request.
                    connectTask.Message = null;
                    connectTask.SocketError = networkError == NetworkError.Ok ? System.Net.Sockets.SocketError.Success : System.Net.Sockets.SocketError.SocketError;
                    connectTask.State = null;
                    connectTask.Success = networkError == NetworkError.Ok;
                    connectTask.TransportCode = (byte)networkError;
                    connectTask.TransportException = null;
                    connectTask.IsDone = true;

                    connectTask = null;
                }
                else if (eventType == NetworkEventType.DisconnectEvent)
                {
                    // We just got a response to our connect request.
                    connectTask.Message = null;
                    connectTask.SocketError = System.Net.Sockets.SocketError.SocketError;
                    connectTask.State = null;
                    connectTask.Success = false;
                    connectTask.TransportCode = (byte)networkError;
                    connectTask.TransportException = null;
                    connectTask.IsDone = true;

                    connectTask = null;
                }
            }

            if (networkError == NetworkError.Timeout)
            {
                // In UNET. Timeouts are not disconnects. We have to translate that here.
                eventType = NetworkEventType.DisconnectEvent;
            }

            // Translate NetworkEventType to NetEventType
            switch (eventType)
            {
                case NetworkEventType.DataEvent:
                    return NetworkEvent.Data;
                case NetworkEventType.ConnectEvent:
                    return NetworkEvent.Connect;
                case NetworkEventType.DisconnectEvent:
                    return NetworkEvent.Disconnect;
                case NetworkEventType.Nothing:
                    return NetworkEvent.Nothing;
                case NetworkEventType.BroadcastEvent:
                    return NetworkEvent.Nothing;
            }

            return NetworkEvent.Nothing;
        }

        public override SocketTasks StartClient()
        {
            SocketTask task = SocketTask.Working;

            serverHostId = RelayTransport.AddHost(new HostTopology(GetConfig(), 1), false);
            serverConnectionId = RelayTransport.Connect(serverHostId, m_ConnectAddress, m_ConnectPort, 0, out byte error);

            NetworkError connectError = (NetworkError)error;

            switch (connectError)
            {
                case NetworkError.Ok:
                    task.Success = true;
                    task.TransportCode = error;
                    task.SocketError = System.Net.Sockets.SocketError.Success;
                    task.IsDone = false;

                    // We want to continue to wait for the successful connect
                    connectTask = task;
                    break;
                default:
                    task.Success = false;
                    task.TransportCode = error;
                    task.SocketError = System.Net.Sockets.SocketError.SocketError;
                    task.IsDone = true;
                    break;
            }

            return task.AsTasks();
        }

        public override SocketTasks StartServer()
        {
            HostTopology topology = new HostTopology(GetConfig(), MaxConnections);

            if (SupportWebsocket)
            {
                if (!UseMLAPIRelay)
                {
                    int websocketHostId = UnityEngine.Networking.NetworkTransport.AddWebsocketHost(topology, ServerWebsocketListenPort);
                }
                else
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error) NetworkLog.LogError("Cannot create websocket host when using MLAPI relay");
                }
            }

            int normalHostId = RelayTransport.AddHost(topology, ServerListenPort, true);

            return SocketTask.Done.AsTasks();
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

            RelayTransport.Disconnect((int)hostId, (int)connectionId, out byte error);
        }

        public override void DisconnectLocalClient()
        {
            RelayTransport.Disconnect(serverHostId, serverConnectionId, out byte error);
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

            if (UseMLAPIRelay)
            {
                return 0;
            }
            else
            {
                return (ulong)UnityEngine.Networking.NetworkTransport.GetCurrentRTT((int)hostId, (int)connectionId, out byte error);
            }
        }

        public override void Shutdown()
        {
            channelIdToName.Clear();
            channelNameToId.Clear();
            UnityEngine.Networking.NetworkTransport.Shutdown();
        }

        public override void Init()
        {
            UpdateRelay();

            messageBuffer = new byte[MessageBufferSize];

            k_TransportProfilerData.Clear();

            UnityEngine.Networking.NetworkTransport.Init();
        }

        public ulong GetMLAPIClientId(byte hostId, ushort connectionId, bool isServer)
        {
            if (isServer)
            {
                return 0;
            }
            else
            {
                return ((ulong)connectionId | (ulong)hostId << 16) + 1;
            }
        }

        public void GetUnetConnectionDetails(ulong clientId, out byte hostId, out ushort connectionId)
        {
            if (clientId == 0)
            {
                hostId = (byte)serverHostId;
                connectionId = (ushort)serverConnectionId;
            }
            else
            {
                hostId = (byte)((clientId - 1) >> 16);
                connectionId = (ushort)((clientId - 1));
            }
        }

        public ConnectionConfig GetConfig()
        {
            ConnectionConfig config = new ConnectionConfig();

            // MLAPI built-in channels
            for (int i = 0; i < MLAPI_CHANNELS.Length; i++)
            {
                int channelId = AddMLAPIChannel(MLAPI_CHANNELS[i].Delivery, config);

                channelIdToName.Add(channelId, MLAPI_CHANNELS[i].Channel);
                channelNameToId.Add(MLAPI_CHANNELS[i].Channel, channelId);
            }

            // Custom user-added channels
            for (int i = 0; i < Channels.Count; i++)
            {
                int channelId = AddUNETChannel(Channels[i].Type, config);

                if (channelNameToId.ContainsKey(Channels[i].Id))
                {
                    throw new InvalidChannelException("Channel " + channelId + " already exists");
                }

                channelIdToName.Add(channelId, Channels[i].Id);
                channelNameToId.Add(Channels[i].Id, channelId);
            }

            config.MaxSentMessageQueueSize = (ushort)MaxSentMessageQueueSize;

            return config;
        }

        public int AddMLAPIChannel(NetworkDelivery type, ConnectionConfig config)
        {
            switch (type)
            {
                case NetworkDelivery.Unreliable:
                    return config.AddChannel(QosType.Unreliable);
                case NetworkDelivery.Reliable:
                    return config.AddChannel(QosType.Reliable);
                case NetworkDelivery.ReliableSequenced:
                    return config.AddChannel(QosType.ReliableSequenced);
                case NetworkDelivery.ReliableFragmentedSequenced:
                    return config.AddChannel(QosType.ReliableFragmentedSequenced);
                case NetworkDelivery.UnreliableSequenced:
                    return config.AddChannel(QosType.UnreliableSequenced);
            }

            return 0;
        }

        public int AddUNETChannel(QosType type, ConnectionConfig config)
        {
            switch (type)
            {
                case QosType.Unreliable:
                    return config.AddChannel(QosType.Unreliable);
                case QosType.UnreliableFragmented:
                    return config.AddChannel(QosType.UnreliableFragmented);
                case QosType.UnreliableSequenced:
                    return config.AddChannel(QosType.UnreliableSequenced);
                case QosType.Reliable:
                    return config.AddChannel(QosType.Reliable);
                case QosType.ReliableFragmented:
                    return config.AddChannel(QosType.ReliableFragmented);
                case QosType.ReliableSequenced:
                    return config.AddChannel(QosType.ReliableSequenced);
                case QosType.StateUpdate:
                    return config.AddChannel(QosType.StateUpdate);
                case QosType.ReliableStateUpdate:
                    return config.AddChannel(QosType.ReliableStateUpdate);
                case QosType.AllCostDelivery:
                    return config.AddChannel(QosType.AllCostDelivery);
                case QosType.UnreliableFragmentedSequenced:
                    return config.AddChannel(QosType.UnreliableFragmentedSequenced);
                case QosType.ReliableFragmentedSequenced:
                    return config.AddChannel(QosType.ReliableFragmentedSequenced);
            }

            return 0;
        }

        private void UpdateRelay()
        {
            RelayTransport.Enabled = UseMLAPIRelay;
            RelayTransport.RelayAddress = MLAPIRelayAddress;
            RelayTransport.RelayPort = (ushort)MLAPIRelayPort;
        }

        public void BeginNewTick()
        {
            k_TransportProfilerData.Clear();
        }

        public IReadOnlyDictionary<string, int> GetTransportProfilerData()
        {
            return k_TransportProfilerData.GetReadonly();
        }
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore 618
