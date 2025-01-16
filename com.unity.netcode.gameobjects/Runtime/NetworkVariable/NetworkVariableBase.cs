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

        // Determines if this NetworkVariable has been "initialized" to prevent initializing more than once which can happen when first
        // instantiated and spawned. If this NetworkVariable instance is on an in-scene placed NetworkObject =or= a pooled NetworkObject
        // that can persist between sessions and/or be recycled we need to reset the LastUpdateSent value prior to spawning otherwise
        // this NetworkVariableBase property instance will not update until the last session time used.
        internal bool HasBeenInitialized { get; private set; }

        internal string GetWritePermissionError()
        {
            return $"|Client-{m_NetworkManager.LocalClientId}|{m_NetworkBehaviour.name}|{Name}| Write permissions ({WritePerm}) for this client instance is not allowed!";
        }

        internal void LogWritePermissionError()
        {
            Debug.LogError(GetWritePermissionError());
        }

        private protected NetworkManager m_NetworkManager => m_InternalNetworkManager;

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
            // If we have already been initialized, then exit early.
            // This can happen on the very first instantiation and spawning of the associated NetworkObject
            if (HasBeenInitialized)
            {
                return;
            }

            // Throw an exception if there is an invalid NetworkBehaviour parameter
            if (!networkBehaviour)
            {
                throw new Exception($"[{GetType().Name}][Initialize] {nameof(NetworkBehaviour)} parameter passed in is null!");
            }
            m_NetworkBehaviour = networkBehaviour;

            // Throw an exception if there is no NetworkManager available
            if (!m_NetworkBehaviour.NetworkManager)
            {
                // Exit early if there has yet to be a NetworkManager assigned.
                // This is ok because Initialize is invoked multiple times until
                // it is considered "initialized".
                return;
            }

            if (!m_NetworkBehaviour.NetworkObject)
            {
                // Exit early if there has yet to be a NetworkObject assigned.
                // This is ok because Initialize is invoked multiple times until
                // it is considered "initialized".
                return;
            }

            if (!m_NetworkBehaviour.NetworkObject.NetworkManagerOwner)
            {
                // Exit early if there has yet to be a NetworkManagerOwner assigned
                // to the NetworkObject. This is ok because Initialize is invoked 
                // multiple times until it is considered "initialized".
                return;
            }
            m_InternalNetworkManager = m_NetworkBehaviour.NetworkObject.NetworkManagerOwner;

            // When in distributed authority mode, there is no such thing as server write permissions
            InternalWritePerm = m_InternalNetworkManager.DistributedAuthorityMode ? NetworkVariableWritePermission.Owner : InternalWritePerm;

            // Wrap potential user script override to catch and log any exceptions
            try
            {
                OnInitialize();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // Some unit tests don't operate with a running NetworkManager.
            // Only update the last time if there is a NetworkTimeSystem.
            if (m_InternalNetworkManager.NetworkTimeSystem != null)
            {
                // Update our last sent time relative to when this was initialized
                UpdateLastSentTime();

                // At this point, this instance is considered initialized
                HasBeenInitialized = true;
            }
            else if (m_InternalNetworkManager.LogLevel == LogLevel.Developer)
            {
                Debug.LogWarning($"[{m_NetworkBehaviour.name}][{m_NetworkBehaviour.GetType().Name}][{GetType().Name}][Initialize] {nameof(NetworkManager)} has no {nameof(NetworkTimeSystem)} assigned!");
            }
        }

        /// <summary>
        /// Deinitialize is invoked when a NetworkObject is despawned.
        /// This allows for a recyled NetworkObject (in-scene or pooled)
        /// to be properly initialized upon the next use/spawn.
        /// </summary>
        internal void Deinitialize()
        {
            // When despawned, reset the HasBeenInitialized so if the associated NetworkObject instance
            // is recylced (i.e. in-scene placed or pooled) it will re-initialize the LastUpdateSent time.
            HasBeenInitialized = false;
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
