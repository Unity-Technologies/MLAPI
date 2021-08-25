namespace Unity.Netcode
{
    /// <summary>
    /// Permission type
    /// </summary>
    public enum NetworkVariableWritePermission
    {
        /// <summary>
        /// Server-only operation
        /// </summary>
        ServerOnly,

        /// <summary>
        /// Owner-ownly
        /// </summary>
        OwnerOnly,
    }

    public enum NetworkVariableReadPermission
    {
        /// <summary>
        /// Everyone
        /// </summary>
        Everyone,

        /// <summary>
        /// Owner-only
        /// </summary>
        OwnerOnly,

        /// <summary>
        /// Custom delegate
        /// </summary>
        Custom
    }
}
