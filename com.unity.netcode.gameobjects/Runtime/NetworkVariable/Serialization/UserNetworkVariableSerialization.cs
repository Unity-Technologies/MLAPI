namespace Unity.Netcode
{
    /// <summary>
    /// This class is used to register user serialization with NetworkVariables for types
    /// that are serialized via user serialization, such as with FastBufferReader and FastBufferWriter
    /// extension methods. Finding those methods isn't achievable efficiently at runtime, so this allows
    /// users to tell NetworkVariable about those extension methods (or simply pass in a lambda)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class UserNetworkVariableSerialization<T>
    {
        /// <summary>
        /// The write value delegate handler definition
        /// </summary>
        /// <param name="writer">The <see cref="FastBufferWriter"/> to write the value of type `T`</param>
        /// <param name="value">The value of type `T` to be written</param>
        public delegate void WriteValueDelegate(FastBufferWriter writer, in T value);

        /// <summary>
        /// The write value delegate handler definition
        /// </summary>
        /// <param name="writer">The <see cref="FastBufferWriter"/> to write the value of type `T`</param>
        /// <param name="value">The value of type `T` to be written</param>
        public delegate void WriteDeltaDelegate(FastBufferWriter writer, in T value, in T previousValue);

        /// <summary>
        /// The read value delegate handler definition
        /// </summary>
        /// <param name="reader">The <see cref="FastBufferReader"/> to read the value of type `T`</param>
        /// <param name="value">The value of type `T` to be read</param>
        public delegate void ReadValueDelegate(FastBufferReader reader, out T value);

        /// <summary>
        /// The read value delegate handler definition
        /// </summary>
        /// <param name="reader">The <see cref="FastBufferReader"/> to read the value of type `T`</param>
        /// <param name="value">The value of type `T` to be read</param>
        public delegate void ReadDeltaDelegate(FastBufferReader reader, ref T value);

        /// <summary>
        /// The read value delegate handler definition
        /// </summary>
        /// <param name="reader">The <see cref="FastBufferReader"/> to read the value of type `T`</param>
        /// <param name="value">The value of type `T` to be read</param>
        public delegate void DuplicateValueDelegate(in T value, ref T duplicatedValue);

        /// <summary>
        /// Callback to write a value
        /// </summary>
        public static WriteValueDelegate WriteValue;

        /// <summary>
        /// Callback to read a value
        /// </summary>
        public static ReadValueDelegate ReadValue;

        /// <summary>
        /// Callback to write a delta between two values, based on computing the difference between the previous and
        /// current values.
        /// </summary>
        public static WriteDeltaDelegate WriteDelta;

        /// <summary>
        /// Callback to read a delta, applying only select changes to the current value.
        /// </summary>
        public static ReadDeltaDelegate ReadDelta;

        /// <summary>
        /// Callback to create a duplicate of a value, used to check for dirty status.
        /// </summary>
        public static DuplicateValueDelegate DuplicateValue;
    }
}
