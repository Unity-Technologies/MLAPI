using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// Interface used by NetworkVariables to serialize them with additional information for the DA runtime
    /// </summary>
    ///
    /// <typeparam name="T"></typeparam>
    internal interface IDistributedAuthoritySerializer<T>
    {
        /// <summary>
        /// The Type tells the DA server how to parse this type.
        /// The user should never be able to override this value, as it is meaningful for the DA server
        /// </summary>
        public NetworkVariableType Type { get; }
        public bool IsDistributedAuthorityOptimized { get; }
        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value);
        public void ReadDistributedAuthority(FastBufferReader reader, ref T value);
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value);
    }


    /// <typeparam name="T"></typeparam>
    internal interface INetworkVariableSerializer<T> : IDistributedAuthoritySerializer<T>
    {
        // Write has to be taken by ref here because of INetworkSerializable
        // Open Instance Delegates (pointers to methods without an instance attached to them)
        // require the first parameter passed to them (the instance) to be passed by ref.
        // So foo.Bar() becomes BarDelegate(ref foo);
        // Taking T as an in parameter like we do in other places would require making a copy
        // of it to pass it as a ref parameter.,

        public void Write(FastBufferWriter writer, ref T value);
        public void Read(FastBufferReader reader, ref T value);
        public void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue);
        public void ReadDelta(FastBufferReader reader, ref T value);
        internal void ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator);
        public void Duplicate(in T value, ref T duplicatedValue);
    }
}
