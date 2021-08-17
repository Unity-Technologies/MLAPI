using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode
{
    public enum NetworkChannel : byte
    {
        Internal,
        TimeSync,
        ReliableRpc,
        UnreliableRpc,
        SyncChannel,
        DefaultMessage,
        PositionUpdate,
        AnimationUpdate,
        NavAgentState,
        NavAgentCorrection,
        NetworkVariable, //todo: this channel will be used for snapshotting and should then go from reliable to unreliable
        SnapshotExchange,
        ChannelUnused, // <<-- must be present, and must be last
    };

    /// <summary>
    /// A network transport
    /// </summary>
    public abstract class NetworkTransport : MonoBehaviour
    {
        /// <summary>
        /// Delegate used to request channels on the underlying transport.
        /// </summary>
        public delegate void RequestChannelsDelegate(List<TransportChannel> channels);

        /// <summary>
        /// Delegate called when the transport wants to know what channels to register.
        /// </summary>
        public event RequestChannelsDelegate OnChannelRegistration;

        /// <summary>
        /// A constant clientId that represents the server.
        /// When this value is found in methods such as Send, it should be treated as a placeholder that means "the server"
        /// </summary>
        public abstract ulong ServerClientId { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Transport"/> is supported in the current runtime context.
        /// This is used by multiplex adapters.
        /// </summary>
        /// <value><c>true</c> if is supported; otherwise, <c>false</c>.</value>
        public virtual bool IsSupported => true;

        private TransportChannel[] m_ChannelsCache = null;

        internal void ResetChannelCache()
        {
            m_ChannelsCache = null;
        }

        public TransportChannel[] NETCODE_CHANNELS
        {
            get
            {
                if (m_ChannelsCache == null)
                {
                    var transportChannels = new List<TransportChannel>();

                    OnChannelRegistration?.Invoke(transportChannels);

                    m_ChannelsCache = new TransportChannel[NETCODE_INTERNAL_CHANNELS.Length + transportChannels.Count];

                    for (int i = 0; i < NETCODE_INTERNAL_CHANNELS.Length; i++)
                    {
                        m_ChannelsCache[i] = NETCODE_INTERNAL_CHANNELS[i];
                    }

                    for (int i = 0; i < transportChannels.Count; i++)
                    {
                        m_ChannelsCache[i + NETCODE_INTERNAL_CHANNELS.Length] = transportChannels[i];
                    }
                }

                return m_ChannelsCache;
            }
        }

        /// <summary>
        /// The channels the Netcode will use when sending internal messages.
        /// </summary>
#pragma warning disable IDE1006 // disable naming rule violation check
        private readonly TransportChannel[] NETCODE_INTERNAL_CHANNELS =
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            new TransportChannel(NetworkChannel.Internal, NetworkDelivery.ReliableFragmentedSequenced),
            new TransportChannel(NetworkChannel.ReliableRpc, NetworkDelivery.ReliableSequenced),
            new TransportChannel(NetworkChannel.UnreliableRpc, NetworkDelivery.UnreliableSequenced),
            new TransportChannel(NetworkChannel.TimeSync, NetworkDelivery.Unreliable),
            new TransportChannel(NetworkChannel.SyncChannel, NetworkDelivery.Unreliable),
            new TransportChannel(NetworkChannel.DefaultMessage, NetworkDelivery.Reliable),
            new TransportChannel(NetworkChannel.PositionUpdate, NetworkDelivery.UnreliableSequenced),
            new TransportChannel(NetworkChannel.AnimationUpdate, NetworkDelivery.ReliableSequenced),
            new TransportChannel(NetworkChannel.NavAgentState, NetworkDelivery.ReliableSequenced),
            new TransportChannel(NetworkChannel.NavAgentCorrection, NetworkDelivery.UnreliableSequenced),
            // todo: Currently, fragmentation support needed to deal with oversize packets encounterable with current pre-snapshot code".
            // todo: once we have snapshotting able to deal with missing frame, this should be unreliable
            new TransportChannel(NetworkChannel.NetworkVariable, NetworkDelivery.ReliableFragmentedSequenced),
            new TransportChannel(NetworkChannel.SnapshotExchange, NetworkDelivery.ReliableFragmentedSequenced), // todo: temporary until we separate snapshots in chunks
        };

        /// <summary>
        /// Delegate for transport events.
        /// </summary>
        public delegate void TransportEventDelegate(NetworkEvent type, ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> payload, float receiveTime);

        /// <summary>
        /// Occurs when the transport has a new transport event. Can be used to make an event based transport instead of a poll based.
        /// Invokation has to occur on the Unity thread in the Update loop.
        /// </summary>
        public event TransportEventDelegate OnTransportEvent;

        /// <summary>
        /// Invokes the <see cref="OnTransportEvent"/>. Invokation has to occur on the Unity thread in the Update loop.
        /// </summary>
        /// <param name="type">The event type</param>
        /// <param name="clientId">The clientId this event is for</param>
        /// <param name="channelName">The channel the data arrived at. This is usually used when responding to things like RPCs</param>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="receiveTime">The time the event was received, as reported by Time.realtimeSinceStartup.</param>
        protected void InvokeOnTransportEvent(NetworkEvent type, ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> payload, float receiveTime)
        {
            OnTransportEvent?.Invoke(type, clientId, networkChannel, payload, receiveTime);
        }

        /// <summary>
        /// Send a payload to the specified clientId, data and channelName.
        /// </summary>
        /// <param name="clientId">The clientId to send to</param>
        /// <param name="data">The data to send</param>
        /// <param name="channelName">The channel to send data to</param>
        public abstract void Send(ulong clientId, ArraySegment<byte> data, NetworkChannel networkChannel);

        /// <summary>
        /// Polls for incoming events, with an extra output parameter to report the precise time the event was received.
        /// </summary>
        /// <param name="clientId">The clientId this event is for</param>
        /// <param name="channelName">The channel the data arrived at. This is usually used when responding to things like RPCs</param>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="receiveTime">The time the event was received, as reported by Time.realtimeSinceStartup.</param>
        /// <returns>Returns the event type</returns>
        public abstract NetworkEvent PollEvent(out ulong clientId, out NetworkChannel networkChannel, out ArraySegment<byte> payload, out float receiveTime);

        /// <summary>
        /// Connects client to server
        /// </summary>
        public abstract SocketTasks StartClient();

        /// <summary>
        /// Starts to listen for incoming clients.
        /// </summary>
        public abstract SocketTasks StartServer();

        /// <summary>
        /// Disconnects a client from the server
        /// </summary>
        /// <param name="clientId">The clientId to disconnect</param>
        public abstract void DisconnectRemoteClient(ulong clientId);

        /// <summary>
        /// Disconnects the local client from the server
        /// </summary>
        public abstract void DisconnectLocalClient();

        /// <summary>
        /// Gets the round trip time for a specific client. This method is optional
        /// </summary>
        /// <param name="clientId">The clientId to get the rtt from</param>
        /// <returns>Returns the round trip time in milliseconds </returns>
        public abstract ulong GetCurrentRtt(ulong clientId);

        /// <summary>
        /// Shuts down the transport
        /// </summary>
        public abstract void Shutdown();

        /// <summary>
        /// Initializes the transport
        /// </summary>
        public abstract void Init();
    }
}
