using Unity.Collections;

namespace Unity.Netcode
{
    /// <typeparam name="T"></typeparam>
    internal interface INetworkVariableSerializer<T>
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
