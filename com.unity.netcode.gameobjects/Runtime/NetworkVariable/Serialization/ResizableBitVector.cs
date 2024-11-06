using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Netcode
{
    /// <summary>
    /// This is a simple resizable bit vector - i.e., a list of flags that use 1 bit each and can
    /// grow to an indefinite size. This is backed by a NativeList&lt;byte&gt; instead of a single
    /// integer value, allowing it to contain any size of memory. Contains built-in serialization support.
    /// </summary>
    internal struct ResizableBitVector : INetworkSerializable, IDisposable
    {
        private NativeList<byte> m_Bits;
        private const int k_Divisor = sizeof(byte) * 8;

        public ResizableBitVector(Allocator allocator)
        {
            m_Bits = new NativeList<byte>(allocator);
        }

        public void Dispose()
        {
            m_Bits.Dispose();
        }

        public int GetSerializedSize()
        {
            return sizeof(int) + m_Bits.Length;
        }

        private (int, int) GetBitData(int i)
        {
            var index = i / k_Divisor;
            var bitWithinIndex = i % k_Divisor;
            return (index, bitWithinIndex);
        }

        /// <summary>
        /// Set bit 'i' - i.e., bit 0 is 00000001, bit 1 is 00000010, and so on.
        /// There is no upper bound on i except for the memory available in the system.
        /// </summary>
        /// <param name="i"></param>
        public void Set(int i)
        {
            var (index, bitWithinIndex) = GetBitData(i);
            if (index >= m_Bits.Length)
            {
                m_Bits.Resize(index + 1, NativeArrayOptions.ClearMemory);
            }

            m_Bits[index] |= (byte)(1 << bitWithinIndex);
        }

        /// <summary>
        /// Unset bit 'i' - i.e., bit 0 is 00000001, bit 1 is 00000010, and so on.
        /// There is no upper bound on i except for the memory available in the system.
        /// Note that once a BitVector has grown to a certain size, it will not shrink back down,
        /// so if you set and unset every bit, it will still serialize at its high watermark size.
        /// </summary>
        /// <param name="i"></param>
        public void Unset(int i)
        {
            var (index, bitWithinIndex) = GetBitData(i);
            if (index >= m_Bits.Length)
            {
                return;
            }

            m_Bits[index] &= (byte)~(1 << bitWithinIndex);
        }

        /// <summary>
        /// Check if bit 'i' is set - i.e., bit 0 is 00000001, bit 1 is 00000010, and so on.
        /// There is no upper bound on i except for the memory available in the system.
        /// </summary>
        /// <param name="i"></param>
        public bool IsSet(int i)
        {
            var (index, bitWithinIndex) = GetBitData(i);
            if (index >= m_Bits.Length)
            {
                return false;
            }

            return (m_Bits[index] & (byte)(1 << bitWithinIndex)) != 0;
        }

        public unsafe void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            var length = m_Bits.Length;
            serializer.SerializeValue(ref length);
            m_Bits.ResizeUninitialized(length);
            var ptr = m_Bits.GetUnsafePtr();
            {
                if (serializer.IsReader)
                {
#if UTP_TRANSPORT_2_0_ABOVE
                    serializer.GetFastBufferReader().ReadBytesSafe(ptr, length);
#else
                    serializer.GetFastBufferReader().ReadBytesSafe((byte*)ptr, length);
#endif
                }
                else
                {
#if UTP_TRANSPORT_2_0_ABOVE
                    serializer.GetFastBufferWriter().WriteBytesSafe(ptr, length);
#else
                    serializer.GetFastBufferWriter().WriteBytesSafe((byte*)ptr, length);
#endif
                }
            }
        }
    }
}
