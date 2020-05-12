namespace MLAPI.Transports
{
    /// <summary>
    /// Represents a netEvent when polling
    /// </summary>
    public enum NetEventType
    {
        /// <summary>
        /// New data is received
        /// </summary>
        Data,
        /// <summary>
        /// A client is connected, or client connected to server
        /// </summary>
        Connect,
        /// <summary>
        /// A client disconnected, or client disconnected from server
        /// </summary>
        Disconnect,
        /// <summary>
        /// A host migrated
        /// </summary>
        HostMigrate,
        /// <summary>
        /// No new event
        /// </summary>
        Nothing
    }
}
