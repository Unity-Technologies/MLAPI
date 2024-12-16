using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Netcode
{
    /// <summary>
    /// Helper class for doing bitwise writes for a FastBufferWriter.
    /// Ensures all bitwise writes end on proper byte alignment so FastBufferWriter doesn't have to be concerned
    /// with misaligned writes.
    /// </summary>
    public ref struct BitWriter
    {
        private FastBufferWriter m_Writer;
        private unsafe byte* m_BufferPointer;
        private readonly int m_Position;
        private int m_BitPosition;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private int m_AllowedBitwiseWriteMark;
#endif
        private const int k_BitsPerByte = 8;

        /// <summary>
        /// Whether or not the current BitPosition is evenly divisible by 8. I.e. whether or not the BitPosition is at a byte boundary.
        /// </summary>
        public bool BitAligned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (m_BitPosition & 7) == 0;
        }

        private int BytePosition => m_BitPosition >> 3;

        internal unsafe BitWriter(FastBufferWriter writer)
        {
            m_Writer = writer;
            m_BufferPointer = writer.Handle->BufferPointer + writer.Handle->Position;
            m_Position = writer.Handle->Position;
            m_BitPosition = 0;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_AllowedBitwiseWriteMark = (m_Writer.Handle->AllowedWriteMark - m_Writer.Handle->Position) * k_BitsPerByte;
#endif
        }

        /// <summary>
        /// Pads the written bit count to byte alignment and commits the write back to the writer
        /// </summary>
        public void Dispose()
        {
            var bytesWritten = m_BitPosition >> 3;
            if (!BitAligned)
            {
                // Accounting for the partial write
                ++bytesWritten;
            }

            m_Writer.CommitBitwiseWrites(bytesWritten);
        }

        /// <summary>
        /// Allows faster serialization by batching bounds checking.
        /// When you know you will be writing multiple fields back-to-back and you know the total size,
        /// you can call TryBeginWriteBits() once on the total size, and then follow it with calls to
        /// WriteBit() or WriteBits().
        ///
        /// Bitwise write operations will throw OverflowException in editor and development builds if you
        /// go past the point you've marked using TryBeginWriteBits(). In release builds, OverflowException will not be thrown
        /// for performance reasons, since the point of using TryBeginWrite is to avoid bounds checking in the following
        /// operations in release builds. Instead, attempting to write past the marked position in release builds
        /// will write to random memory and cause undefined behavior, likely including instability and crashes.
        /// </summary>
        /// <param name="bitCount">Number of bits you want to write, in total</param>
        /// <returns>True if you can write, false if that would exceed buffer bounds</returns>
        public unsafe bool TryBeginWriteBits(int bitCount)
        {
            var newBitPosition = m_BitPosition + bitCount;
            var totalBytesWrittenInBitwiseContext = newBitPosition >> 3;
            if ((newBitPosition & 7) != 0)
            {
                // Accounting for the partial write
                ++totalBytesWrittenInBitwiseContext;
            }

            if (m_Position + totalBytesWrittenInBitwiseContext > m_Writer.Handle->Capacity)
            {
                if (m_Position + totalBytesWrittenInBitwiseContext > m_Writer.Handle->MaxCapacity)
                {
                    return false;
                }
                if (m_Writer.Handle->Capacity < m_Writer.Handle->MaxCapacity)
                {
                    m_Writer.Grow(totalBytesWrittenInBitwiseContext);
                    m_BufferPointer = m_Writer.Handle->BufferPointer + m_Writer.Handle->Position;
                }
                else
                {
                    return false;
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_AllowedBitwiseWriteMark = newBitPosition;
#endif
            return true;
        }

        /// <summary>
        /// Write s certain amount of bits to the stream.
        /// </summary>
        /// <param name="value">Value to get bits from.</param>
        /// <param name="bitCount">Amount of bits to write</param>
        public unsafe void WriteBits(ulong value, uint bitCount)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (bitCount > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot write more than 64 bits from a 64-bit value!");
            }

            int checkPos = (int)(m_BitPosition + bitCount);
            if (checkPos > m_AllowedBitwiseWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWriteBits)}()");
            }
#endif

            int wholeBytes = (int)bitCount / k_BitsPerByte;
            byte* asBytes = (byte*)&value;
            if (BitAligned)
            {
                if (wholeBytes != 0)
                {
                    WritePartialValue(value, wholeBytes);
                }
            }
            else
            {
                for (var i = 0; i < wholeBytes; ++i)
                {
                    WriteMisaligned(asBytes[i]);
                }
            }

            for (var count = wholeBytes * k_BitsPerByte; count < bitCount; ++count)
            {
                WriteBit((value & (1UL << count)) != 0);
            }
        }

        /// <summary>
        /// Write bits to stream.
        /// </summary>
        /// <param name="value">Value to get bits from.</param>
        /// <param name="bitCount">Amount of bits to write.</param>
        public void WriteBits(byte value, uint bitCount)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            int checkPos = (int)(m_BitPosition + bitCount);
            if (checkPos > m_AllowedBitwiseWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWriteBits)}()");
            }
#endif

            for (int i = 0; i < bitCount; ++i)
            {
                WriteBit(((value >> i) & 1) != 0);
            }
        }

        /// <summary>
        /// Write a single bit to the buffer
        /// </summary>
        /// <param name="bit">Value of the bit. True represents 1, False represents 0</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBit(bool bit)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            int checkPos = (m_BitPosition + 1);
            if (checkPos > m_AllowedBitwiseWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWriteBits)}()");
            }
#endif

            int offset = m_BitPosition & 7;
            int pos = BytePosition;
            ++m_BitPosition;
            m_BufferPointer[pos] = (byte)(bit ? (m_BufferPointer[pos] & ~(1 << offset)) | (1 << offset) : (m_BufferPointer[pos] & ~(1 << offset)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WritePartialValue<T>(T value, int bytesToWrite, int offsetBytes = 0) where T : unmanaged
        {
            byte* ptr = ((byte*)&value) + offsetBytes;
            byte* bufferPointer = m_BufferPointer + BytePosition;
            UnsafeUtility.MemCpy(bufferPointer, ptr, bytesToWrite);

            m_BitPosition += bytesToWrite * k_BitsPerByte;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteMisaligned(byte value)
        {
            int off = m_BitPosition & 7;
            int pos = m_BitPosition >> 3;
            int shift1 = 8 - off;
            m_BufferPointer[pos + 1] = (byte)((m_BufferPointer[pos + 1] & (0xFF << off)) | (value >> shift1));
            m_BufferPointer[pos] = (byte)((m_BufferPointer[pos] & (0xFF >> shift1)) | (value << off));

            m_BitPosition += 8;
        }
    }
}
