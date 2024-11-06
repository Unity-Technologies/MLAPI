using System;
using UnityEngine;

namespace Unity.Netcode
{
    public struct NetworkVariableUpdateTraits
    {
        [Tooltip("The minimum amount of time that must pass between sending updates. If this amount of time has not passed since the last update, dirtiness will be ignored.")]
        public float MinSecondsBetweenUpdates;

        [Tooltip("The maximum amount of time that a variable can be dirty without sending an update. If this amount of time has passed since the last update, an update will be sent even if the dirtiness threshold has not been met.")]
        public float MaxSecondsBetweenUpdates;
    }

    /// <summary>
    /// Interface for network value containers
    /// </summary>
    public abstract class NetworkVariableBase : IDisposable
    {
        [SerializeField]
        internal NetworkVariableUpdateTraits UpdateTraits = default;

        [NonSerialized]
        internal double LastUpdateSent;

        /// <summary>
        /// The delivery type (QoS) to send data with
        /// </summary>
        internal const NetworkDelivery Delivery = NetworkDelivery.ReliableFragmentedSequenced;

        /// <summary>
        /// Maintains a link to the associated NetworkBehaviour
        /// </summary>
        private protected NetworkBehaviour m_NetworkBehaviour;

        private NetworkManager m_InternalNetworkManager;

        internal virtual NetworkVariableType Type => NetworkVariableType.Unknown;

        internal string GetWritePermissionError()
        {
            return $"|Client-{m_NetworkManager.LocalClientId}|{m_NetworkBehaviour.name}|{Name}| Write permissions ({WritePerm}) for this client instance is not allowed!";
        }

        internal void LogWritePermissionError()
        {
            Debug.LogError(GetWritePermissionError());
        }

        private protected NetworkManager m_NetworkManager
        {
            get
            {
                if (m_InternalNetworkManager == null && m_NetworkBehaviour && m_NetworkBehaviour.NetworkObject?.NetworkManager)
                {
                    m_InternalNetworkManager = m_NetworkBehaviour.NetworkObject?.NetworkManager;
                }
                return m_InternalNetworkManager;
            }
        }

        public NetworkBehaviour GetBehaviour()
        {
            return m_NetworkBehaviour;
        }

        /// <summary>
        /// Initializes the NetworkVariable
        /// </summary>
        /// <param name="networkBehaviour">The NetworkBehaviour the NetworkVariable belongs to</param>
        public void Initialize(NetworkBehaviour networkBehaviour)
        {
            m_InternalNetworkManager = null;
            m_NetworkBehaviour = networkBehaviour;
            if (m_NetworkBehaviour && m_NetworkBehaviour.NetworkObject?.NetworkManager)
            {
                m_InternalNetworkManager = m_NetworkBehaviour.NetworkObject?.NetworkManager;
                // When in distributed authority mode, there is no such thing as server write permissions
                InternalWritePerm = m_InternalNetworkManager.DistributedAuthorityMode ? NetworkVariableWritePermission.Owner : InternalWritePerm;

                if (m_NetworkBehaviour.NetworkManager.NetworkTimeSystem != null)
                {
                    UpdateLastSentTime();
                }
            }

            OnInitialize();
        }

        /// <summary>
        /// Called on initialization
        /// </summary>
        public virtual void OnInitialize()
        {

        }

        /// <summary>
        /// Sets the update traits for this network variable to determine how frequently it will send updates.
        /// </summary>
        /// <param name="traits"></param>
        public void SetUpdateTraits(NetworkVariableUpdateTraits traits)
        {
            UpdateTraits = traits;
        }

        /// <summary>
        /// Check whether or not this variable has changed significantly enough to send an update.
        /// If not, no update will be sent even if the variable is dirty, unless the time since last update exceeds
        /// the <see cref="UpdateTraits"/>' <see cref="NetworkVariableUpdateTraits.MaxSecondsBetweenUpdates"/>.
        /// </summary>
        /// <returns></returns>
        public virtual bool ExceedsDirtinessThreshold()
        {
            return true;
        }

        /// <summary>
        /// The default read permissions
        /// </summary>
        public const NetworkVariableReadPermission DefaultReadPerm = NetworkVariableReadPermission.Everyone;

        /// <summary>
        /// The default write permissions
        /// </summary>
        public const NetworkVariableWritePermission DefaultWritePerm = NetworkVariableWritePermission.Server;

        /// <summary>
        /// The default constructor for <see cref="NetworkVariableBase"/> that can be used to create a
        /// custom NetworkVariable.
        /// </summary>
        /// <param name="readPerm">the <see cref="NetworkVariableReadPermission"/> access settings</param>
        /// <param name="writePerm">the <see cref="NetworkVariableWritePermission"/> access settings</param>
        protected NetworkVariableBase(
            NetworkVariableReadPermission readPerm = DefaultReadPerm,
            NetworkVariableWritePermission writePerm = DefaultWritePerm)
        {
            ReadPerm = readPerm;
            InternalWritePerm = writePerm;
        }

        /// <summary>
        /// The <see cref="m_IsDirty"/> property is used to determine if the
        /// value of the `NetworkVariable` has changed.
        /// </summary>
        private bool m_IsDirty;

        /// <summary>
        /// Gets or sets the name of the network variable's instance
        /// (MemberInfo) where it was declared.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// The read permission for this var
        /// </summary>
        public readonly NetworkVariableReadPermission ReadPerm;

        /// <summary>
        /// The write permission for this var
        /// </summary>
        public NetworkVariableWritePermission WritePerm
        {
            get
            {
                return InternalWritePerm;
            }
        }

        // We had to change the Write Permission in distributed authority.
        // (It is too bad we initially declared it as readonly)
        internal NetworkVariableWritePermission InternalWritePerm;

        /// <summary>
        /// Sets whether or not the variable needs to be delta synced
        /// </summary>
        /// <param name="isDirty">Whether or not the var is dirty</param>
        public virtual void SetDirty(bool isDirty)
        {
            m_IsDirty = isDirty;

            if (m_IsDirty)
            {
                MarkNetworkBehaviourDirty();
            }
        }

        internal bool CanSend()
        {
            // When connected to a service or not the server, always use the synchronized server time as opposed to the local time
            var time = m_InternalNetworkManager.CMBServiceConnection || !m_InternalNetworkManager.IsServer ? m_NetworkBehaviour.NetworkManager.ServerTime.Time : m_NetworkBehaviour.NetworkManager.NetworkTimeSystem.LocalTime;
            var timeSinceLastUpdate = time - LastUpdateSent;
            return
                (
                    UpdateTraits.MaxSecondsBetweenUpdates > 0 &&
                    timeSinceLastUpdate >= UpdateTraits.MaxSecondsBetweenUpdates
                ) ||
                (
                    timeSinceLastUpdate >= UpdateTraits.MinSecondsBetweenUpdates &&
                    ExceedsDirtinessThreshold()
                );
        }

        internal void UpdateLastSentTime()
        {
            // When connected to a service or not the server, always use the synchronized server time as opposed to the local time
            LastUpdateSent = m_InternalNetworkManager.CMBServiceConnection || !m_InternalNetworkManager.IsServer ? m_NetworkBehaviour.NetworkManager.ServerTime.Time : m_NetworkBehaviour.NetworkManager.NetworkTimeSystem.LocalTime;
        }

        internal static bool IgnoreInitializeWarning;

        protected void MarkNetworkBehaviourDirty()
        {
            if (m_NetworkBehaviour == null)
            {
                if (!IgnoreInitializeWarning)
                {
                    Debug.LogWarning($"NetworkVariable is written to, but doesn't know its NetworkBehaviour yet. " +
                                     "Are you modifying a NetworkVariable before the NetworkObject is spawned?");
                }
                return;
            }
            if (m_NetworkBehaviour.NetworkManager.ShutdownInProgress)
            {
                if (m_NetworkBehaviour.NetworkManager.LogLevel <= LogLevel.Developer)
                {
                    Debug.LogWarning($"NetworkVariable is written to during the NetworkManager shutdown! " +
                 "Are you modifying a NetworkVariable within a NetworkBehaviour.OnDestroy or NetworkBehaviour.OnDespawn method?");
                }
                return;
            }

            if (!m_NetworkBehaviour.NetworkManager.IsListening)
            {
                if (m_NetworkBehaviour.NetworkManager.LogLevel <= LogLevel.Developer)
                {
                    Debug.LogWarning($"NetworkVariable is written to after the NetworkManager has already shutdown! " +
                     "Are you modifying a NetworkVariable within a NetworkBehaviour.OnDestroy or NetworkBehaviour.OnDespawn method?");
                }
                return;
            }

            m_NetworkBehaviour.NetworkManager.BehaviourUpdater?.AddForUpdate(m_NetworkBehaviour.NetworkObject);
        }

        /// <summary>
        /// Resets the dirty state and marks the variable as synced / clean
        /// </summary>
        public virtual void ResetDirty()
        {
            m_IsDirty = false;
        }

        /// <summary>
        /// Only used during the NetworkBehaviourUpdater pass and only used for NetworkVariable.
        /// This is to bypass duplication of the "original internal value" for collections.
        /// </summary>
        internal bool NetworkUpdaterCheck;

        /// <summary>
        /// Gets Whether or not the container is dirty
        /// </summary>
        /// <returns>Whether or not the container is dirty</returns>
        public virtual bool IsDirty()
        {
            return m_IsDirty;
        }

        /// <summary>
        /// Gets if a specific client has permission to read the var or not
        /// </summary>
        /// <param name="clientId">The client id</param>
        /// <returns>Whether or not the client has permission to read</returns>
        public bool CanClientRead(ulong clientId)
        {
            if (!m_NetworkBehaviour)
            {
                return false;
            }

            // When in distributed authority mode, everyone can read (but only the owner can write)
            if (m_NetworkManager != null && m_NetworkManager.DistributedAuthorityMode)
            {
                return true;
            }
            switch (ReadPerm)
            {
                default:
                case NetworkVariableReadPermission.Everyone:
                    return true;
                case NetworkVariableReadPermission.Owner:
                    return clientId == m_NetworkBehaviour.NetworkObject.OwnerClientId || NetworkManager.ServerClientId == clientId;
            }
        }

        /// <summary>
        /// Gets if a specific client has permission to write the var or not
        /// </summary>
        /// <param name="clientId">The client id</param>
        /// <returns>Whether or not the client has permission to write</returns>
        public bool CanClientWrite(ulong clientId)
        {
            if (!m_NetworkBehaviour)
            {
                return false;
            }

            switch (WritePerm)
            {
                default:
                case NetworkVariableWritePermission.Server:
                    return clientId == NetworkManager.ServerClientId;
                case NetworkVariableWritePermission.Owner:
                    return clientId == m_NetworkBehaviour.NetworkObject.OwnerClientId;
            }
        }

        /// <summary>
        /// Returns the ClientId of the owning client
        /// </summary>
        internal ulong OwnerClientId()
        {
            return m_NetworkBehaviour.NetworkObject.OwnerClientId;
        }

        /// <summary>
        /// Writes the dirty changes, that is, the changes since the variable was last dirty, to the writer
        /// </summary>
        /// <param name="writer">The stream to write the dirty changes to</param>
        public abstract void WriteDelta(FastBufferWriter writer);

        /// <summary>
        /// Writes the complete state of the variable to the writer
        /// </summary>
        /// <param name="writer">The stream to write the state to</param>
        public abstract void WriteField(FastBufferWriter writer);

        /// <summary>
        /// Reads the complete state from the reader and applies it
        /// </summary>
        /// <param name="reader">The stream to read the state from</param>
        public abstract void ReadField(FastBufferReader reader);
        /// <summary>
        /// Reads delta from the reader and applies them to the internal value
        /// </summary>
        /// <param name="reader">The stream to read the delta from</param>
        /// <param name="keepDirtyDelta">Whether or not the delta should be kept as dirty or consumed</param>
        public abstract void ReadDelta(FastBufferReader reader, bool keepDirtyDelta);

        /// <summary>
        /// This should be always invoked (client & server) to assure the previous values are set
        /// !! IMPORTANT !!
        /// When a server forwards delta updates to connected clients, it needs to preserve the previous dirty value(s)
        /// until it is done serializing all valid NetworkVariable field deltas (relative to each client). This is invoked 
        /// after it is done forwarding the deltas at the end of the <see cref="NetworkVariableDeltaMessage.Handle(ref NetworkContext)"/> method.
        /// </summary>
        internal virtual void PostDeltaRead()
        {
        }

        /// <summary>
        /// There are scenarios, specifically with collections, where a client could be synchronizing and
        /// some NetworkVariables have pending updates. To avoid duplicating entries, this is invoked only
        /// when sending the full synchronization information.
        /// </summary>
        /// <remarks>
        /// Derrived classes should send the previous value for synchronization so when the updated value
        /// is sent (after synchronizing the client) it will apply the updates.
        /// </remarks>
        /// <param name="writer"></param>
        internal virtual void WriteFieldSynchronization(FastBufferWriter writer)
        {
            WriteField(writer);
        }

        /// <summary>
        /// Virtual <see cref="IDisposable"/> implementation
        /// </summary>
        public virtual void Dispose()
        {
            m_InternalNetworkManager = null;
        }
    }
}
