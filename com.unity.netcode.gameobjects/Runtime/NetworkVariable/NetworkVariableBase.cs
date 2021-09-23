using System;

namespace Unity.Netcode
{
    /// <summary>
    /// Interface for network value containers
    /// </summary>
    public abstract class NetworkVariableBase : IDisposable
    {
        /// <summary>
        /// The delivery type (QoS) to send data with
        /// </summary>
        internal const NetworkDelivery Delivery = NetworkDelivery.ReliableSequenced;

        private protected NetworkBehaviour m_NetworkBehaviour;

        public NetworkBehaviour NetworkBehaviour => m_NetworkBehaviour;

        public void Initialize(NetworkBehaviour networkBehaviour)
        {
            m_NetworkBehaviour = networkBehaviour;
            OnInitialize();
        }

        protected virtual void OnInitialize()
        {

        }

        protected NetworkVariableBase(NetworkVariableReadPermission readPermIn = NetworkVariableReadPermission.Everyone)
        {
            ReadPerm = readPermIn;
        }

        private protected bool m_IsDirty;

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
        /// Sets whether or not the variable needs to be delta synced
        /// </summary>
        public virtual void SetDirty(bool isDirty)
        {
            m_IsDirty = isDirty;
        }

        /// <summary>
        /// Resets the dirty state and marks the variable as synced / clean
        /// </summary>
        public virtual void ResetDirty()
        {
            m_IsDirty = false;
        }

        /// <summary>
        /// Gets Whether or not the container is dirty
        /// </summary>
        /// <returns>Whether or not the container is dirty</returns>
        public virtual bool IsDirty()
        {
            return m_IsDirty;
        }

        public virtual bool ShouldWrite(ulong clientId, bool isServer)
        {
            return IsDirty() && isServer && CanClientRead(clientId);
        }

        /// <summary>
        /// Gets Whether or not a specific client can read to the varaible
        /// </summary>
        /// <param name="clientId">The clientId of the remote client</param>
        /// <returns>Whether or not the client can read to the variable</returns>
        public bool CanClientRead(ulong clientId)
        {
            switch (ReadPerm)
            {
                case NetworkVariableReadPermission.Everyone:
                    return true;
                case NetworkVariableReadPermission.OwnerOnly:
                    return m_NetworkBehaviour.OwnerClientId == clientId;
            }
            return true;
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

        public virtual void Dispose()
        {
        }
    }
}
