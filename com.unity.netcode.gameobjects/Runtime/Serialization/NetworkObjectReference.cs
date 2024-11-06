using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// A helper struct for serializing <see cref="NetworkObject"/>s over the network. Can be used in RPCs and <see cref="NetworkVariable{T}"/>.
    /// </summary>
    public struct NetworkObjectReference : INetworkSerializable, IEquatable<NetworkObjectReference>
    {
        private ulong m_NetworkObjectId;
        private static ulong s_NullId = ulong.MaxValue;

        /// <summary>
        /// The <see cref="NetworkObject.NetworkObjectId"/> of the referenced <see cref="NetworkObject"/>.
        /// </summary>
        public ulong NetworkObjectId
        {
            get => m_NetworkObjectId;
            internal set => m_NetworkObjectId = value;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkObjectReference"/> struct.
        /// </summary>
        /// <param name="networkObject">The <see cref="NetworkObject"/> to reference.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public NetworkObjectReference(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                m_NetworkObjectId = s_NullId;
                return;
            }

            if (networkObject.IsSpawned == false)
            {
                throw new ArgumentException($"{nameof(NetworkObjectReference)} can only be created from spawned {nameof(NetworkObject)}s.");
            }

            m_NetworkObjectId = networkObject.NetworkObjectId;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkObjectReference"/> struct.
        /// </summary>
        /// <param name="gameObject">The GameObject from which the <see cref="NetworkObject"/> component will be referenced.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public NetworkObjectReference(GameObject gameObject)
        {
            if (gameObject == null)
            {
                m_NetworkObjectId = s_NullId;
                return;
            }

            var networkObject = gameObject.GetComponent<NetworkObject>();
            if (!networkObject)
            {
                throw new ArgumentException($"Cannot create {nameof(NetworkObjectReference)} from {nameof(GameObject)} without a {nameof(NetworkObject)} component.");
            }

            if (networkObject.IsSpawned == false)
            {
                throw new ArgumentException($"{nameof(NetworkObjectReference)} can only be created from spawned {nameof(NetworkObject)}s.");
            }

            m_NetworkObjectId = networkObject.NetworkObjectId;
        }

        /// <summary>
        /// Tries to get the <see cref="NetworkObject"/> referenced by this reference.
        /// </summary>
        /// <param name="networkObject">The <see cref="NetworkObject"/> which was found. Null if no object was found.</param>
        /// <param name="networkManager">The networkmanager. Uses <see cref="NetworkManager.Singleton"/> to resolve if null.</param>
        /// <returns>True if the <see cref="NetworkObject"/> was found; False if the <see cref="NetworkObject"/> was not found. This can happen if the <see cref="NetworkObject"/> has not been spawned yet. you can try getting the reference at a later point in time.</returns>
        public bool TryGet(out NetworkObject networkObject, NetworkManager networkManager = null)
        {
            networkObject = Resolve(this, networkManager);
            return networkObject != null;
        }

        /// <summary>
        /// Resolves the corresponding <see cref="NetworkObject"/> for this reference.
        /// </summary>
        /// <param name="networkObjectRef">The reference.</param>
        /// <param name="networkManager">The networkmanager. Uses <see cref="NetworkManager.Singleton"/> to resolve if null.</param>
        /// <returns>The resolved <see cref="NetworkObject"/>. Returns null if the networkobject was not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static NetworkObject Resolve(NetworkObjectReference networkObjectRef, NetworkManager networkManager = null)
        {
            if (networkObjectRef.m_NetworkObjectId == s_NullId)
            {
                return null;
            }
            networkManager = networkManager ?? NetworkManager.Singleton;
            networkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectRef.m_NetworkObjectId, out NetworkObject networkObject);

            return networkObject;
        }

        /// <inheritdoc/>
        public bool Equals(NetworkObjectReference other)
        {
            return m_NetworkObjectId == other.m_NetworkObjectId;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is NetworkObjectReference other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return m_NetworkObjectId.GetHashCode();
        }

        /// <inheritdoc/>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_NetworkObjectId);
        }

        /// <summary>
        /// Implicitly convert <see cref="NetworkObjectReference"/> to <see cref="NetworkObject"/>.
        /// </summary>
        /// <param name="networkObjectRef">The <see cref="NetworkObjectReference"/> to convert from.</param>
        /// <returns>The <see cref="NetworkObject"/> the <see cref="NetworkObjectReference"/> is referencing</returns>
        public static implicit operator NetworkObject(NetworkObjectReference networkObjectRef) => Resolve(networkObjectRef);

        /// <summary>
        /// Implicitly convert <see cref="NetworkObject"/> to <see cref="NetworkObjectReference"/>.
        /// </summary>
        /// <param name="networkObject">The <see cref="NetworkObject"/> to convert from.</param>
        /// <returns>The <see cref="NetworkObjectReference"/> created from the <see cref="NetworkObject"/> parameter</returns>
        public static implicit operator NetworkObjectReference(NetworkObject networkObject) => new NetworkObjectReference(networkObject);

        /// <summary>
        /// Implicitly convert <see cref="NetworkObjectReference"/> to <see cref="GameObject"/>.
        /// </summary>
        /// <param name="networkObjectRef">The <see cref="NetworkObjectReference"/> to convert from.</param>
        /// <returns>This returns the <see cref="GameObject"/> that the <see cref="NetworkObject"/> is attached to and is referenced by the <see cref="NetworkObjectReference"/> passed in as a parameter</returns>
        public static implicit operator GameObject(NetworkObjectReference networkObjectRef)
        {
            var networkObject = Resolve(networkObjectRef);
            if (networkObject != null)
            {
                return networkObject.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Implicitly convert <see cref="GameObject"/> to <see cref="NetworkObject"/>.
        /// </summary>
        /// <param name="gameObject">The <see cref="GameObject"/> to convert from.</param>
        /// <returns>The <see cref="NetworkObjectReference"/> created from the <see cref="GameObject"/> parameter that has a <see cref="NetworkObject"/> component attached to it</returns>
        public static implicit operator NetworkObjectReference(GameObject gameObject) => new NetworkObjectReference(gameObject);
    }
}
