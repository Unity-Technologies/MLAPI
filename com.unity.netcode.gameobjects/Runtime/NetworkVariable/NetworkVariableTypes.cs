#if UNITY_EDITOR
#endif

namespace Unity.Netcode
{
    /// <summary>
    ///     Enum representing the different types of Network Variables that can be sent over the network.
    ///     The values cannot be changed, as they are used to serialize and deserialize variables on the DA server.
    ///     Adding new variables should be done by adding new values to the end of the enum
    ///     using the next free value.
    /// </summary>
    /// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    /// Add any new Variable types to this table at the END with incremented index value
    /// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    internal enum NetworkVariableType : byte
    {
        /// <summary>
        ///     Value
        ///     Used for all of the basic NetworkVariables that contain a single value
        /// </summary>
        Value = 0,
        /// <summary>
        ///     For any type that is not known at runtime
        /// </summary>
        Unknown = 1,
        /// <summary>
        ///     NetworkList
        /// </summary>
        NetworkList = 2,

        // The following types are valid types inside of NetworkVariable collections
        Short = 11,
        UShort = 12,
        Int = 13,
        UInt = 14,
        Long = 15,
        ULong = 16,
        Unmanaged = 17,
    }
}
