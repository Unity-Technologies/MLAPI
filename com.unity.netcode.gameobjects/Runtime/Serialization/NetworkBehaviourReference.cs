using System;
using System.Runtime.CompilerServices;

namespace Unity.Netcode
{
    /// <summary>
    /// A helper struct for serializing <see cref="NetworkBehaviour"/>s over the network. Can be used in RPCs and <see cref="NetworkVariable{T}"/>.
    /// Note: network ids get recycled by the NetworkManager after a while. So a reference pointing to
    /// </summary>
    public struct NetworkBehaviourReference : INetworkSerializable, IEquatable<NetworkBehaviourReference>
    {
        private NetworkObjectReference m_NetworkObjectReference;
        private ushort m_NetworkBehaviourId;
        private static ushort s_NullId = ushort.MaxValue;


        /// <summary>
        /// Creates a new instance of the <see cref="NetworkBehaviourReference{T}"/> struct.
        /// </summary>
        /// <param name="networkBehaviour">The <see cref="NetworkBehaviour"/> to reference.</param>
        /// <exception cref="ArgumentException"></exception>
        public NetworkBehaviourReference(NetworkBehaviour networkBehaviour)
        {
            if (networkBehaviour == null)
            {
                m_NetworkObjectReference = new NetworkObjectReference((NetworkObject)null);
                m_NetworkBehaviourId = s_NullId;
                return;
            }
            if (networkBehaviour.NetworkObject == null)
            {
                throw new ArgumentException($"Cannot create {nameof(NetworkBehaviourReference)} from {nameof(NetworkBehaviour)} without a {nameof(NetworkObject)}.");
            }

            m_NetworkObjectReference = networkBehaviour.NetworkObject;
            m_NetworkBehaviourId = networkBehaviour.NetworkBehaviourId;
        }

        /// <summary>
        /// Tries to get the <see cref="NetworkBehaviour"/> referenced by this reference.
        /// </summary>
        /// <param name="networkBehaviour">The <see cref="NetworkBehaviour"/> which was found. Null if the corresponding <see cref="NetworkObject"/> was not found.</param>
        /// <param name="networkManager">The networkmanager. Uses <see cref="NetworkManager.Singleton"/> to resolve if null.</param>
        /// <returns>True if the <see cref="NetworkBehaviour"/> was found; False if the <see cref="NetworkBehaviour"/> was not found. This can happen if the corresponding <see cref="NetworkObject"/> has not been spawned yet. you can try getting the reference at a later point in time.</returns>
        public bool TryGet(out NetworkBehaviour networkBehaviour, NetworkManager networkManager = null)
        {
            networkBehaviour = GetInternal(this, networkManager);
            return networkBehaviour != null;
        }

        /// <summary>
        /// Tries to get the <see cref="NetworkBehaviour"/> referenced by this reference.
        /// </summary>
        /// <param name="networkBehaviour">The <see cref="NetworkBehaviour"/> which was found. Null if the corresponding <see cref="NetworkObject"/> was not found.</param>
        /// <param name="networkManager">The networkmanager. Uses <see cref="NetworkManager.Singleton"/> to resolve if null.</param>
        /// <typeparam name="T">The type of the networkBehaviour for convenience.</typeparam>
        /// <returns>True if the <see cref="NetworkBehaviour"/> was found; False if the <see cref="NetworkBehaviour"/> was not found. This can happen if the corresponding <see cref="NetworkObject"/> has not been spawned yet. you can try getting the reference at a later point in time.</returns>
        public bool TryGet<T>(out T networkBehaviour, NetworkManager networkManager = null) where T : NetworkBehaviour
        {
            networkBehaviour = GetInternal(this, networkManager) as T;
            return networkBehaviour != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static NetworkBehaviour GetInternal(NetworkBehaviourReference networkBehaviourRef, NetworkManager networkManager = null)
        {
            if (networkBehaviourRef.m_NetworkBehaviourId == s_NullId)
            {
                return null;
            }

            if (networkBehaviourRef.m_NetworkObjectReference.TryGet(out NetworkObject networkObject, networkManager))
            {
                return networkObject.GetNetworkBehaviourAtOrderIndex(networkBehaviourRef.m_NetworkBehaviourId);
            }

            return null;
        }

        /// <inheritdoc/>
        public bool Equals(NetworkBehaviourReference other)
        {
            return m_NetworkObjectReference.Equals(other.m_NetworkObjectReference) && m_NetworkBehaviourId == other.m_NetworkBehaviourId;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is NetworkBehaviourReference other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (m_NetworkObjectReference.GetHashCode() * 397) ^ m_NetworkBehaviourId.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            m_NetworkObjectReference.NetworkSerialize(serializer);
            serializer.SerializeValue(ref m_NetworkBehaviourId);
        }

        /// <summary>
        /// Implicitly convert <see cref="NetworkBehaviourReference"/> to <see cref="NetworkBehaviour"/>.
        /// </summary>
        /// <param name="networkBehaviourRef">The <see cref="NetworkBehaviourReference"/> to convert from.</param>
        /// <returns>The <see cref="NetworkBehaviour"/> this class is holding a reference to</returns>
        public static implicit operator NetworkBehaviour(NetworkBehaviourReference networkBehaviourRef) => GetInternal(networkBehaviourRef);

        /// <summary>
        /// Implicitly convert <see cref="NetworkBehaviour"/> to <see cref="NetworkBehaviourReference"/>.
        /// </summary>
        /// <param name="networkBehaviour">The <see cref="NetworkBehaviour"/> to convert from.</param>
        /// <returns>The <see cref="NetworkBehaviourReference"/> created from the <see cref="NetworkBehaviour"/> passed in as a parameter</returns>
        public static implicit operator NetworkBehaviourReference(NetworkBehaviour networkBehaviour) => new NetworkBehaviourReference(networkBehaviour);
    }
}
