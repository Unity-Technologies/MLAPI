using System;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// The generic transport class all Netcode for GameObjects network transport implementations
    /// derive from.  Use this class to add a custom transport.
    /// <seealso cref="Transports.UTP.UnityTransport"> for an example of how a transport is integrated</seealso>
    /// </summary>
    public abstract class NetworkTransport : MonoBehaviour
    {
        /// <summary>
        /// A constant `clientId` that represents the server
        /// When this value is found in methods such as `Send`, it should be treated as a placeholder that means "the server"
        /// </summary>
        public abstract ulong ServerClientId { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:NetworkTransport"/> is supported in the current runtime context
        /// This is used by multiplex adapters
        /// </summary>
        /// <value><c>true</c> if is supported; otherwise, <c>false</c>.</value>
        public virtual bool IsSupported => true;

        internal INetworkMetrics NetworkMetrics;

        /// <summary>
        /// Delegate for transport network events
        /// </summary>
        public delegate void TransportEventDelegate(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime);

        /// <summary>
        /// Occurs when the transport has a new transport network event.
        /// Can be used to make an event based transport instead of a poll based.
        /// Invocation has to occur on the Unity thread in the Update loop.
        /// </summary>
        public event TransportEventDelegate OnTransportEvent;

        /// <summary>
        /// Invokes the <see cref="OnTransportEvent"/>. Invokation has to occur on the Unity thread in the Update loop.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="clientId">The clientId this event is for</param>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="receiveTime">The time the event was received, as reported by Time.realtimeSinceStartup.</param>
        protected void InvokeOnTransportEvent(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            OnTransportEvent?.Invoke(eventType, clientId, payload, receiveTime);
        }

        /// <summary>
        /// Send a payload to the specified clientId, data and networkDelivery.
        /// </summary>
        /// <param name="clientId">The clientId to send to</param>
        /// <param name="payload">The data to send</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public abstract void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery);

        /// <summary>
        /// Polls for incoming events, with an extra output parameter to report the precise time the event was received.
        /// </summary>
        /// <param name="clientId">The clientId this event is for</param>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="receiveTime">The time the event was received, as reported by Time.realtimeSinceStartup.</param>
        /// <returns>Returns the event type</returns>
        public abstract NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime);

        /// <summary>
        /// Connects client to the server
        /// </summary>
        /// <returns>Returns success or failure</returns>
        public abstract bool StartClient();

        /// <summary>
        /// Starts to listening for incoming clients
        /// </summary>
        /// <returns>Returns success or failure</returns>
        public abstract bool StartServer();

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
        /// <param name="clientId">The clientId to get the RTT from</param>
        /// <returns>Returns the round trip time in milliseconds </returns>
        public abstract ulong GetCurrentRtt(ulong clientId);

        /// <summary>
        /// Shuts down the transport
        /// </summary>
        public abstract void Shutdown();

        /// <summary>
        /// Initializes the transport
        /// </summary>
        /// /// <param name="networkManager">optionally pass in NetworkManager</param>
        public abstract void Initialize(NetworkManager networkManager = null);

        protected virtual NetworkTopologyTypes OnCurrentTopology()
        {
            return NetworkTopologyTypes.ClientServer;
        }

        internal NetworkTopologyTypes CurrentTopology()
        {
            return OnCurrentTopology();
        }
    }

    public enum NetworkTopologyTypes
    {
        ClientServer,
        DistributedAuthority
    }

#if UNITY_INCLUDE_TESTS
    public abstract class TestingNetworkTransport : NetworkTransport
    {

    }
#endif
}
