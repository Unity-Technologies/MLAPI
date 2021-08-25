namespace Unity.Netcode
{
    /// <summary>
    /// Delegate type for permission checking
    /// </summary>
    /// <param name="clientId">The clientId whose permissions to check</param>
    public delegate bool NetworkVariablePermissionsDelegate(ulong clientId);

    /// <summary>
    /// The settings class used by the build in NetworkVar implementations
    /// </summary>
    public class NetworkVariableSettings
    {
        /// <summary>
        /// Defines the write permissions for this var
        /// </summary>
        public NetworkVariableWritePermission WritePermission = NetworkVariableWritePermission.ServerOnly;
        /// <summary>
        /// Defines the read permissions for this var
        /// </summary>
        public NetworkVariableReadPermission ReadPermission = NetworkVariableReadPermission.Everyone;
        /// <summary>
        /// The delegate used to evaluate write permission when the "Custom" mode is used
        /// </summary>
        public NetworkVariablePermissionsDelegate WritePermissionCallback = null;
        /// <summary>
        /// The delegate used to evaluate read permission when the "Custom" mode is used
        /// </summary>
        public NetworkVariablePermissionsDelegate ReadPermissionCallback = null;
        /// <summary>
        /// The maximum times per second this var will be synced.
        /// A value of 0 will cause the variable to sync as soon as possible after being changed.
        /// A value of less than 0 will cause the variable to sync only at once at spawn and not update again.
        /// </summary>
        public double SendTickrate = 0;

        /// <summary>
        /// The name of the channel to use for this variable.
        /// Variables with different channels will be split into different packets
        /// </summary>
        public NetworkChannel SendNetworkChannel = NetworkChannel.NetworkVariable;

        /// <summary>
        /// Constructs a new NetworkVariableSettings instance
        /// </summary>
        public NetworkVariableSettings() { }
    }
}
