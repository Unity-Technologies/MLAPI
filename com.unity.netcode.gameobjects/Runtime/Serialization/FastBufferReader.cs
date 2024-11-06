using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Optimized class used for reading values from a byte stream
    /// <seealso cref="FastBufferWriter"/>
    /// <seealso cref="BytePacker"/>
    /// <seealso cref="ByteUnpacker"/>
    /// </summary>
    public struct FastBufferReader : IDisposable
    {
        internal struct ReaderHandle
        {
            internal unsafe byte* BufferPointer;
            internal int Position;
            internal int Length;
            internal Allocator Allocator;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            internal int AllowedReadMark;
            internal bool InBitwiseContext;
#endif
        }

        internal unsafe ReaderHandle* Handle;

        /// <summary>
        /// Get the current read position
        /// </summary>
        public unsafe int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->Position;
        }

        /// <summary>
        /// Get the total length of the buffer
        /// </summary>
        public unsafe int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->Length;
        }

        /// <summary>
        /// Gets a value indicating whether the reader has been initialized and a handle allocated.
        /// </summary>
        public unsafe bool IsInitialized => Handle != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CommitBitwiseReads(int amount)
        {
            Handle->Position += amount;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->InBitwiseContext = false;
#endif
        }

        private static unsafe ReaderHandle* CreateHandle(byte* buffer, int length, int offset, Allocator copyAllocator, Allocator internalAllocator)
        {
            ReaderHandle* readerHandle;
            if (copyAllocator == Allocator.None)
            {
                readerHandle = (ReaderHandle*)UnsafeUtility.Malloc(sizeof(ReaderHandle), UnsafeUtility.AlignOf<byte>(), internalAllocator);
                readerHandle->BufferPointer = buffer;
                readerHandle->Position = offset;
            }
            else
            {
                readerHandle = (ReaderHandle*)UnsafeUtility.Malloc(sizeof(ReaderHandle) + length, UnsafeUtility.AlignOf<byte>(), copyAllocator);
                UnsafeUtility.MemCpy(readerHandle + 1, buffer + offset, length);
                readerHandle->BufferPointer = (byte*)(readerHandle + 1);
                readerHandle->Position = 0;
            }

            readerHandle->Length = length;

            // If the copyAllocator provided is Allocator.None, there is a chance that the internalAllocator was provided
            // When we dispose, we are really only interested in disposing Allocator.Persistent and Allocator.TempJob
            // as disposing Allocator.Temp and Allocator.None would do nothing. Therefore, make sure we dispose the readerHandle with the right Allocator label
            readerHandle->Allocator = copyAllocator == Allocator.None ? internalAllocator : copyAllocator;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            readerHandle->AllowedReadMark = 0;
            readerHandle->InBitwiseContext = false;
#endif
            return readerHandle;
        }

        /// <summary>
        /// Create a FastBufferReader from a NativeArray.
        ///
        /// A new buffer will be created using the given <param name="copyAllocator"></param> and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// The exception to this is when the <param name="copyAllocator"></param> passed in is Allocator.None. In this scenario,
        /// ownership of the data remains with the caller and the reader will point at it directly.
        /// When created with Allocator.None, FastBufferReader will allocate some internal data using
        /// Allocator.Temp so it should be treated as if it's a ref struct and not allowed to outlive
        /// the context in which it was created (it should neither be returned from that function nor
        /// stored anywhere in heap memory). This is true, unless the <param name="internalAllocator"></param> param is explicitly set
        /// to i.e.: Allocator.Persistent in which case it would allow the internal data to Persist for longer, but the caller
        /// should manually call Dispose() when it is no longer needed.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length"></param>
        /// <param name="offset"></param>
        /// <param name="internalAllocator">The allocator type used for internal data when this reader points directly at a buffer owned by someone else</param>
        public unsafe FastBufferReader(NativeArray<byte> buffer, Allocator copyAllocator, int length = -1, int offset = 0, Allocator internalAllocator = Allocator.Temp)
        {
            Handle = CreateHandle((byte*)buffer.GetUnsafePtr(), length == -1 ? buffer.Length : length, offset, copyAllocator, internalAllocator);
        }

        /// <summary>
        /// Create a FastBufferReader from an ArraySegment.
        ///
        /// A new buffer will be created using the given allocator and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// Allocator.None is not supported for byte[]. If you need this functionality, use a fixed() block
        /// and ensure the FastBufferReader isn't used outside that block.
        /// </summary>
        /// <param name="buffer">The buffer to copy from</param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length">The number of bytes to copy (all if this is -1)</param>
        /// <param name="offset">The offset of the buffer to start copying from</param>
        public unsafe FastBufferReader(ArraySegment<byte> buffer, Allocator copyAllocator, int length = -1, int offset = 0)
        {
            if (copyAllocator == Allocator.None)
            {
                throw new NotSupportedException("Allocator.None cannot be used with managed source buffers.");
            }
            fixed (byte* data = buffer.Array)
            {
                Handle = CreateHandle(data, length == -1 ? buffer.Count : length, offset, copyAllocator, Allocator.Temp);
            }
        }

        /// <summary>
        /// Create a FastBufferReader from an existing byte array.
        ///
        /// A new buffer will be created using the given allocator and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// Allocator.None is not supported for byte[]. If you need this functionality, use a fixed() block
        /// and ensure the FastBufferReader isn't used outside that block.
        /// </summary>
        /// <param name="buffer">The buffer to copy from</param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length">The number of bytes to copy (all if this is -1)</param>
        /// <param name="offset">The offset of the buffer to start copying from</param>
        public unsafe FastBufferReader(byte[] buffer, Allocator copyAllocator, int length = -1, int offset = 0)
        {
            if (copyAllocator == Allocator.None)
            {
                throw new NotSupportedException("Allocator.None cannot be used with managed source buffers.");
            }
            fixed (byte* data = buffer)
            {
                Handle = CreateHandle(data, length == -1 ? buffer.Length : length, offset, copyAllocator, Allocator.Temp);
            }
        }

        /// <summary>
        /// Create a FastBufferReader from an existing byte buffer.
        ///
        /// A new buffer will be created using the given <param name="copyAllocator"></param> and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// The exception to this is when the <param name="copyAllocator"></param> passed in is Allocator.None. In this scenario,
        /// ownership of the data remains with the caller and the reader will point at it directly.
        /// When created with Allocator.None, FastBufferReader will allocate some internal data using
        /// Allocator.Temp, so it should be treated as if it's a ref struct and not allowed to outlive
        /// the context in which it was created (it should neither be returned from that function nor
        /// stored anywhere in heap memory). This is true, unless the <param name="internalAllocator"></param> param is explicitly set
        /// to i.e.: Allocator.Persistent in which case it would allow the internal data to Persist for longer, but the caller
        /// should manually call Dispose() when it is no longer needed.
        /// </summary>
        /// <param name="buffer">The buffer to copy from</param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length">The number of bytes to copy</param>
        /// <param name="offset">The offset of the buffer to start copying from</param>
        /// <param name="internalAllocator">The allocator type used for internal data when this reader points directly at a buffer owned by someone else</param>
        public unsafe FastBufferReader(byte* buffer, Allocator copyAllocator, int length, int offset = 0, Allocator internalAllocator = Allocator.Temp)
        {
            Handle = CreateHandle(buffer, length, offset, copyAllocator, internalAllocator);
        }

        /// <summary>
        /// Create a FastBufferReader from a FastBufferWriter.
        ///
        /// A new buffer will be created using the given <param name="copyAllocator"></param> and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// The exception to this is when the <param name="copyAllocator"></param> passed in is Allocator.None. In this scenario,
        /// ownership of the data remains with the caller and the reader will point at it directly.
        /// When created with Allocator.None, FastBufferReader will allocate some internal data using
        /// Allocator.Temp, so it should be treated as if it's a ref struct and not allowed to outlive
        /// the context in which it was created (it should neither be returned from that function nor
        /// stored anywhere in heap memory). This is true, unless the <param name="internalAllocator"></param> param is explicitly set
        /// to i.e.: Allocator.Persistent in which case it would allow the internal data to Persist for longer, but the caller
        /// should manually call Dispose() when it is no longer needed.
        /// </summary>
        /// <param name="writer">The writer to copy from</param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length">The number of bytes to copy (all if this is -1)</param>
        /// <param name="offset">The offset of the buffer to start copying from</param>
        /// <param name="internalAllocator">The allocator type used for internal data when this reader points directly at a buffer owned by someone else</param>
        public unsafe FastBufferReader(FastBufferWriter writer, Allocator copyAllocator, int length = -1, int offset = 0, Allocator internalAllocator = Allocator.Temp)
        {
            Handle = CreateHandle(writer.GetUnsafePtr(), length == -1 ? writer.Length : length, offset, copyAllocator, internalAllocator);
        }

        /// <summary>
        /// Create a FastBufferReader from another existing FastBufferReader. This is typically used when you
        /// want to change the copyAllocator that a reader is allocated to - for example, upgrading a Temp reader to
        /// a Persistent one to be processed later.
        ///
        /// A new buffer will be created using the given <param name="copyAllocator"></param> and the value will be copied in.
        /// FastBufferReader will then own the data.
        ///
        /// The exception to this is when the <param name="copyAllocator"></param> passed in is Allocator.None. In this scenario,
        /// ownership of the data remains with the caller and the reader will point at it directly.
        /// When created with Allocator.None, FastBufferReader will allocate some internal data using
        /// Allocator.Temp, so it should be treated as if it's a ref struct and not allowed to outlive
        /// the context in which it was created (it should neither be returned from that function nor
        /// stored anywhere in heap memory).
        /// </summary>
        /// <param name="reader">The reader to copy from</param>
        /// <param name="copyAllocator">The allocator type used for internal data when copying an existing buffer if other than Allocator.None is specified, that memory will be owned by this FastBufferReader instance</param>
        /// <param name="length">The number of bytes to copy (all if this is -1)</param>
        /// <param name="offset">The offset of the buffer to start copying from</param>
        /// <param name="internalAllocator">The allocator type used for internal data when this reader points directly at a buffer owned by someone else</param>
        public unsafe FastBufferReader(FastBufferReader reader, Allocator copyAllocator, int length = -1, int offset = 0, Allocator internalAllocator = Allocator.Temp)
        {
            Handle = CreateHandle(reader.GetUnsafePtr(), length == -1 ? reader.Length : length, offset, copyAllocator, internalAllocator);
        }

        /// <summary>
        /// <see cref="IDisposable"/> implementation that frees the allocated buffer
        /// </summary>
        public unsafe void Dispose()
        {
            if (Handle == null)
            {
                return;
            }

            UnsafeUtility.Free(Handle, Handle->Allocator);
            Handle = null;
        }

        /// <summary>
        /// Move the read position in the stream
        /// </summary>
        /// <param name="where">Absolute value to move the position to, truncated to Length</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Seek(int where)
        {
            Handle->Position = Math.Min(Length, where);
        }

        /// <summary>
        /// Mark that some bytes are going to be read via GetUnsafePtr().
        /// </summary>
        /// <param name="amount">Amount that will be read</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void MarkBytesRead(int amount)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + amount > Handle->AllowedReadMark)
            {
                throw new OverflowException("Attempted to read without first calling TryBeginRead()");
            }
#endif
            Handle->Position += amount;
        }

        /// <summary>
        /// Retrieve a BitReader to be able to perform bitwise operations on the buffer.
        /// No bytewise operations can be performed on the buffer until bitReader.Dispose() has been called.
        /// At the end of the operation, FastBufferReader will remain byte-aligned.
        /// </summary>
        /// <returns>A BitReader</returns>
        public unsafe BitReader EnterBitwiseContext()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->InBitwiseContext = true;
#endif
            return new BitReader(this);
        }

        /// <summary>
        /// Allows faster serialization by batching bounds checking.
        /// When you know you will be reading multiple fields back-to-back and you know the total size,
        /// you can call TryBeginRead() once on the total size, and then follow it with calls to
        /// ReadValue() instead of ReadValueSafe() for faster serialization.
        ///
        /// Unsafe read operations will throw OverflowException in editor and development builds if you
        /// go past the point you've marked using TryBeginRead(). In release builds, OverflowException will not be thrown
        /// for performance reasons, since the point of using TryBeginRead is to avoid bounds checking in the following
        /// operations in release builds.
        /// </summary>
        /// <param name="bytes">Amount of bytes to read</param>
        /// <returns>True if the read is allowed, false otherwise</returns>
        /// <exception cref="InvalidOperationException">If called while in a bitwise context</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryBeginRead(int bytes)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif
            if (Handle->Position + bytes > Handle->Length)
            {
                return false;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->AllowedReadMark = Handle->Position + bytes;
#endif
            return true;
        }

        /// <summary>
        /// Allows faster serialization by batching bounds checking.
        /// When you know you will be reading multiple fields back-to-back and you know the total size,
        /// you can call TryBeginRead() once on the total size, and then follow it with calls to
        /// ReadValue() instead of ReadValueSafe() for faster serialization.
        ///
        /// Unsafe read operations will throw OverflowException in editor and development builds if you
        /// go past the point you've marked using TryBeginRead(). In release builds, OverflowException will not be thrown
        /// for performance reasons, since the point of using TryBeginRead is to avoid bounds checking in the following
        /// operations in release builds.
        /// </summary>
        /// <typeparam name="T">the type `T` of the value you are trying to read</typeparam>
        /// <param name="value">The value you want to read</param>
        /// <returns>True if the read is allowed, false otherwise</returns>
        /// <exception cref="InvalidOperationException">If called while in a bitwise context</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryBeginReadValue<T>(in T value) where T : unmanaged
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif
            int len = sizeof(T);
            if (Handle->Position + len > Handle->Length)
            {
                return false;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->AllowedReadMark = Handle->Position + len;
#endif
            return true;
        }

        /// <summary>
        /// Internal version of TryBeginRead.
        /// Differs from TryBeginRead only in that it won't ever move the AllowedReadMark backward.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>true upon success</returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool TryBeginReadInternal(int bytes)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif
            if (Handle->Position + bytes > Handle->Length)
            {
                return false;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->Position + bytes > Handle->AllowedReadMark)
            {
                Handle->AllowedReadMark = Handle->Position + bytes;
            }
#endif
            return true;
        }

        /// <summary>
        /// Returns an array representation of the underlying byte buffer.
        /// !!Allocates a new array!!
        /// </summary>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte[] ToArray()
        {
            byte[] ret = new byte[Length];
            fixed (byte* b = ret)
            {
                UnsafeUtility.MemCpy(b, Handle->BufferPointer, Length);
            }
            return ret;
        }

        /// <summary>
        /// Gets a direct pointer to the underlying buffer
        /// </summary>
        /// <returns><see cref="byte"/> pointer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetUnsafePtr()
        {
            return Handle->BufferPointer;
        }

        /// <summary>
        /// Gets a direct pointer to the underlying buffer at the current read position
        /// </summary>
        /// <returns><see cref="byte"/> pointer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetUnsafePtrAtCurrentPosition()
        {
            return Handle->BufferPointer + Handle->Position;
        }

        /// <summary>
        /// Read an INetworkSerializable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">INetworkSerializable instance</param>
        /// <exception cref="NotImplementedException"></exception>
        public void ReadNetworkSerializable<T>(out T value) where T : INetworkSerializable, new()
        {
            value = new T();
            var bufferSerializer = new BufferSerializer<BufferSerializerReader>(new BufferSerializerReader(this));
            value.NetworkSerialize(bufferSerializer);
        }

        /// <summary>
        /// Read an array of INetworkSerializables
        /// </summary>
        /// <param name="value">INetworkSerializable instance</param>
        /// <typeparam name="T">the array to read the values of type `T` into</typeparam>
        /// <exception cref="NotImplementedException"></exception>
        public void ReadNetworkSerializable<T>(out T[] value) where T : INetworkSerializable, new()
        {
            ReadValueSafe(out int size);
            value = new T[size];
            for (var i = 0; i < size; ++i)
            {
                ReadNetworkSerializable(out value[i]);
            }
        }

        /// <summary>
        /// Read a NativeArray of INetworkSerializables
        /// </summary>
        /// <param name="value">INetworkSerializable instance</param>
        /// <param name="allocator">The allocator to use to construct the resulting NativeArray</param>
        /// <typeparam name="T">the array to read the values of type `T` into</typeparam>
        /// <exception cref="NotImplementedException"></exception>
        public void ReadNetworkSerializable<T>(out NativeArray<T> value, Allocator allocator) where T : unmanaged, INetworkSerializable
        {
            ReadValueSafe(out int size);
            value = new NativeArray<T>(size, allocator);
            for (var i = 0; i < size; ++i)
            {
                ReadNetworkSerializable(out T item);
                value[i] = item;
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Read a NativeList of INetworkSerializables
        /// </summary>
        /// <param name="value">INetworkSerializable instance</param>
        /// <typeparam name="T">the array to read the values of type `T` into</typeparam>
        /// <exception cref="NotImplementedException"></exception>
        public void ReadNetworkSerializableInPlace<T>(ref NativeList<T> value) where T : unmanaged, INetworkSerializable
        {
            ReadValueSafe(out int size);
            value.Resize(size, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < size; ++i)
            {
                ReadNetworkSerializable(out value.ElementAt(i));
            }
        }
#endif

        /// <summary>
        /// Read an INetworkSerializable in-place, without constructing a new one
        /// Note that this will NOT check for null before calling NetworkSerialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">INetworkSerializable instance</param>
        /// <exception cref="NotImplementedException"></exception>
        public void ReadNetworkSerializableInPlace<T>(ref T value) where T : INetworkSerializable
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerReader>(new BufferSerializerReader(this));
            value.NetworkSerialize(bufferSerializer);
        }

        /// <summary>
        /// Reads a string
        /// NOTE: ALLOCATES
        /// </summary>
        /// <param name="s">Stores the read string</param>
        /// <param name="oneByteChars">Whether or not to use one byte per character. This will only allow ASCII</param>
        public unsafe void ReadValue(out string s, bool oneByteChars = false)
        {
            ReadLength(out int length);
            s = "".PadRight(length);
            int target = s.Length;
            fixed (char* native = s)
            {
                if (oneByteChars)
                {
                    for (int i = 0; i < target; ++i)
                    {
                        ReadByte(out byte b);
                        native[i] = (char)b;
                    }
                }
                else
                {
                    ReadBytes((byte*)native, target * sizeof(char));
                }
            }
        }

        /// <summary>
        /// Reads a string.
        /// NOTE: ALLOCATES
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="s">Stores the read string</param>
        /// <param name="oneByteChars">Whether or not to use one byte per character. This will only allow ASCII</param>
        public unsafe void ReadValueSafe(out string s, bool oneByteChars = false)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif

            if (!TryBeginReadInternal(SizeOfLengthField()))
            {
                throw new OverflowException("Reading past the end of the buffer");
            }

            ReadLength(out int length);

            if (!TryBeginReadInternal(length * (oneByteChars ? 1 : sizeof(char))))
            {
                throw new OverflowException("Reading past the end of the buffer");
            }
            s = "".PadRight(length);
            int target = s.Length;
            fixed (char* native = s)
            {
                if (oneByteChars)
                {
                    for (int i = 0; i < target; ++i)
                    {
                        ReadByte(out byte b);
                        native[i] = (char)b;
                    }
                }
                else
                {
                    ReadBytes((byte*)native, target * sizeof(char));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SizeOfLengthField() => sizeof(uint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadLengthSafe(out uint length) => ReadUnmanagedSafe(out length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadLength(out uint length) => ReadUnmanaged(out length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadLengthSafe(out int length)
        {
            ReadLengthSafe(out uint temp);
            if (temp > int.MaxValue)
            {
                throw new InvalidCastException("length value outside of int32 range");
            }
            length = (int)temp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadLength(out int length)
        {
            ReadLength(out uint temp);
            length = (int)temp;
        }

        /// <summary>
        /// Read a partial value. The value is zero-initialized and then the specified number of bytes is read into it.
        /// </summary>
        /// <param name="value">Value to read</param>
        /// <param name="bytesToRead">Number of bytes</param>
        /// <param name="offsetBytes">Offset into the value to write the bytes</param>
        /// <typeparam name="T">the type value to read the value into</typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadPartialValue<T>(out T value, int bytesToRead, int offsetBytes = 0) where T : unmanaged
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + bytesToRead > Handle->AllowedReadMark)
            {
                throw new OverflowException($"Attempted to read without first calling {nameof(TryBeginRead)}()");
            }
#endif

            var val = new T();
            byte* ptr = ((byte*)&val) + offsetBytes;
            byte* bufferPointer = Handle->BufferPointer + Handle->Position;
            UnsafeUtility.MemCpy(ptr, bufferPointer, bytesToRead);

            Handle->Position += bytesToRead;
            value = val;
        }

        /// <summary>
        /// Read a byte to the stream.
        /// </summary>
        /// <param name="value">Stores the read value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadByte(out byte value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + 1 > Handle->AllowedReadMark)
            {
                throw new OverflowException($"Attempted to read without first calling {nameof(TryBeginRead)}()");
            }
#endif
            value = Handle->BufferPointer[Handle->Position++];
        }

        /// <summary>
        /// Read a byte to the stream.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">Stores the read value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadByteSafe(out byte value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif

            if (!TryBeginReadInternal(1))
            {
                throw new OverflowException("Reading past the end of the buffer");
            }
            value = Handle->BufferPointer[Handle->Position++];
        }

        /// <summary>
        /// Read multiple bytes to the stream
        /// </summary>
        /// <param name="value">Pointer to the destination buffer</param>
        /// <param name="size">Number of bytes to read - MUST BE &lt;= BUFFER SIZE</param>
        /// <param name="offset">Offset of the byte buffer to store into</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadBytes(byte* value, int size, int offset = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + size > Handle->AllowedReadMark)
            {
                throw new OverflowException($"Attempted to read without first calling {nameof(TryBeginRead)}()");
            }
#endif
            UnsafeUtility.MemCpy(value + offset, (Handle->BufferPointer + Handle->Position), size);
            Handle->Position += size;
        }

        /// <summary>
        /// Read multiple bytes to the stream
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">Pointer to the destination buffer</param>
        /// <param name="size">Number of bytes to read - MUST BE &lt;= BUFFER SIZE</param>
        /// <param name="offset">Offset of the byte buffer to store into</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadBytesSafe(byte* value, int size, int offset = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferReader in bytewise mode while in a bitwise context.");
            }
#endif

            if (!TryBeginReadInternal(size))
            {
                throw new OverflowException("Reading past the end of the buffer");
            }
            UnsafeUtility.MemCpy(value + offset, (Handle->BufferPointer + Handle->Position), size);
            Handle->Position += size;
        }

        /// <summary>
        /// Read multiple bytes from the stream
        /// </summary>
        /// <param name="value">Pointer to the destination buffer</param>
        /// <param name="size">Number of bytes to read - MUST BE &lt;= BUFFER SIZE</param>
        /// <param name="offset">Offset of the byte buffer to store into</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadBytes(ref byte[] value, int size, int offset = 0)
        {
            fixed (byte* ptr = value)
            {
                ReadBytes(ptr, size, offset);
            }
        }

        /// <summary>
        /// Read multiple bytes from the stream
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">Pointer to the destination buffer</param>
        /// <param name="size">Number of bytes to read - MUST BE &lt;= BUFFER SIZE</param>
        /// <param name="offset">Offset of the byte buffer to store into</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadBytesSafe(ref byte[] value, int size, int offset = 0)
        {
            fixed (byte* ptr = value)
            {
                ReadBytesSafe(ptr, size, offset);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanaged<T>(out T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                byte* bytes = (byte*)ptr;
                ReadBytes(bytes, sizeof(T));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanagedSafe<T>(out T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                byte* bytes = (byte*)ptr;
                ReadBytesSafe(bytes, sizeof(T));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanaged<T>(out T[] value) where T : unmanaged
        {
            ReadLength(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value = new T[sizeInTs];
            fixed (T* ptr = value)
            {
                byte* bytes = (byte*)ptr;
                ReadBytes(bytes, sizeInBytes);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanagedSafe<T>(out T[] value) where T : unmanaged
        {
            ReadLengthSafe(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value = new T[sizeInTs];
            fixed (T* ptr = value)
            {
                byte* bytes = (byte*)ptr;
                ReadBytesSafe(bytes, sizeInBytes);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanaged<T>(out NativeArray<T> value, Allocator allocator) where T : unmanaged
        {
            ReadLength(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value = new NativeArray<T>(sizeInTs, allocator);
            byte* bytes = (byte*)value.GetUnsafePtr();
            ReadBytes(bytes, sizeInBytes);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanagedSafe<T>(out NativeArray<T> value, Allocator allocator) where T : unmanaged
        {
            ReadLengthSafe(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value = new NativeArray<T>(sizeInTs, allocator);
            byte* bytes = (byte*)value.GetUnsafePtr();
            ReadBytesSafe(bytes, sizeInBytes);
        }
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanagedInPlace<T>(ref NativeList<T> value) where T : unmanaged
        {
            ReadLength(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value.Resize(sizeInTs, NativeArrayOptions.UninitializedMemory);
            byte* bytes = (byte*)value.GetUnsafePtr();
            ReadBytes(bytes, sizeInBytes);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ReadUnmanagedSafeInPlace<T>(ref NativeList<T> value) where T : unmanaged
        {
            ReadLengthSafe(out int sizeInTs);
            int sizeInBytes = sizeInTs * sizeof(T);
            value.Resize(sizeInTs, NativeArrayOptions.UninitializedMemory);
            byte* bytes = (byte*)value.GetUnsafePtr();
            ReadBytesSafe(bytes, sizeInBytes);
        }
#endif

        /// <summary>
        /// Read a NetworkSerializable value
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T value, FastBufferWriter.ForNetworkSerializable unused = default) where T : INetworkSerializable, new() => ReadNetworkSerializable(out value);

        /// <summary>
        /// Read a NetworkSerializable array
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T[] value, FastBufferWriter.ForNetworkSerializable unused = default) where T : INetworkSerializable, new() => ReadNetworkSerializable(out value);

        /// <summary>
        /// Read a NetworkSerializable value
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T value, FastBufferWriter.ForNetworkSerializable unused = default) where T : INetworkSerializable, new() => ReadNetworkSerializable(out value);

        /// <summary>
        /// Read a NetworkSerializable array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T[] value, FastBufferWriter.ForNetworkSerializable unused = default) where T : INetworkSerializable, new() => ReadNetworkSerializable(out value);

        /// <summary>
        /// Read a NetworkSerializable NativeArray
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="allocator">The allocator to use to construct the resulting NativeArray</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out NativeArray<T> value, Allocator allocator, FastBufferWriter.ForNetworkSerializable unused = default) where T : unmanaged, INetworkSerializable => ReadNetworkSerializable(out value, allocator);


        /// <summary>
        /// Read a struct
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T value, FastBufferWriter.ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => ReadUnmanaged(out value);

        /// <summary>
        /// Read a struct array
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T[] value, FastBufferWriter.ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => ReadUnmanaged(out value);

        /// <summary>
        /// Read a struct NativeArray
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="allocator">The allocator to use to construct the resulting NativeArray</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out NativeArray<T> value, Allocator allocator, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.ReadWithAllocator(this, out value, allocator);
            }
            else
            {
                ReadUnmanaged(out value, allocator);
            }
        }

        /// <summary>
        /// Read a struct NativeArray using a Temp allocator. Equivalent to ReadValue(out value, Allocator.Temp)
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueTemp<T>(out NativeArray<T> value, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.ReadWithAllocator(this, out value, Allocator.Temp);
            }
            else
            {
                ReadUnmanaged(out value, Allocator.Temp);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Read a struct NativeList
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueInPlace<T>(ref NativeList<T> value, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeList<T>>.Serializer.Read(this, ref value);
            }
            else
            {
                ReadUnmanagedInPlace(ref value);
            }
        }
#endif

        /// <summary>
        /// Read a struct
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T value, FastBufferWriter.ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a struct array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T[] value, FastBufferWriter.ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a struct NativeArray
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="allocator">The allocator to use to construct the resulting NativeArray</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out NativeArray<T> value, Allocator allocator, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.ReadWithAllocator(this, out value, allocator);
            }
            else
            {
                ReadUnmanagedSafe(out value, allocator);
            }
        }

        /// <summary>
        /// Read a struct NativeArray using a Temp allocator. Equivalent to ReadValueSafe(out value, Allocator.Temp)
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafeTemp<T>(out NativeArray<T> value, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.ReadWithAllocator(this, out value, Allocator.Temp);
            }
            else
            {
                ReadUnmanagedSafe(out value, Allocator.Temp);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Read a struct NativeList
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafeInPlace<T>(ref NativeList<T> value, FastBufferWriter.ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeList<T>>.Serializer.Read(this, ref value);
            }
            else
            {
                ReadUnmanagedSafeInPlace(ref value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadValueSafeInPlace<T>(ref NativeHashSet<T> value) where T : unmanaged, IEquatable<T>
        {
            ReadLengthSafe(out int length);
            value.Clear();
            for (var i = 0; i < length; ++i)
            {
                T val = default;
                NetworkVariableSerialization<T>.Read(this, ref val);
                value.Add(val);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadValueSafeInPlace<TKey, TVal>(ref NativeHashMap<TKey, TVal> value)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            ReadLengthSafe(out int length);
            value.Clear();
            for (var i = 0; i < length; ++i)
            {
                TKey key = default;
                TVal val = default;
                NetworkVariableSerialization<TKey>.Read(this, ref key);
                NetworkVariableSerialization<TVal>.Read(this, ref val);
                value[key] = val;
            }
        }
#endif

        /// <summary>
        /// Read a primitive value (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T value, FastBufferWriter.ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => ReadUnmanaged(out value);

        /// <summary>
        /// Read a primitive value array (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T[] value, FastBufferWriter.ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => ReadUnmanaged(out value);

        /// <summary>
        /// Read a primitive value (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T value, FastBufferWriter.ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a primitive value (int, bool, etc) array
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T[] value, FastBufferWriter.ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read an enum value
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T value, FastBufferWriter.ForEnums unused = default) where T : unmanaged, Enum => ReadUnmanaged(out value);

        /// <summary>
        /// Read an enum array
        /// </summary>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(out T[] value, FastBufferWriter.ForEnums unused = default) where T : unmanaged, Enum => ReadUnmanaged(out value);


        /// <summary>
        /// Read an enum value
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T value, FastBufferWriter.ForEnums unused = default) where T : unmanaged, Enum => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read an enum array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <typeparam name="T">The type being serialized</typeparam>
        /// <param name="value">The values to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T[] value, FastBufferWriter.ForEnums unused = default) where T : unmanaged, Enum => ReadUnmanagedSafe(out value);


        /// <summary>
        /// Read a Vector2
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector2 value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector2 array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector2[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector3
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector3 value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector3 array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector3[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector2Int
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector2Int value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector2Int array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector2Int[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector3Int
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector3Int value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector3Int array
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector3Int[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector4
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector4 value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Vector4
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Vector4[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Quaternion
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Quaternion value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Quaternion array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Quaternion[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Color
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Color value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Color array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Color[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Color32
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Color32 value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Color32 array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Color32[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Ray
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Ray value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Ray array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Ray[] value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Ray2D
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Ray2D value) => ReadUnmanaged(out value);

        /// <summary>
        /// Read a Ray2D array
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue(out Ray2D[] value) => ReadUnmanaged(out value);


        /// <summary>
        /// Read a Vector2
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector2 value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector2 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector2[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector3
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector3 value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector3 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector3[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector2Int
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector2Int value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector2Int array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector2Int[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector3Int
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector3Int value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector3Int array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector3Int[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector4
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector4 value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Vector4 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Vector4[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Quaternion
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Quaternion value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Quaternion array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Quaternion[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Color
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Color value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Collor array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Color[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Color32
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Color32 value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Color32 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Color32[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Ray
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Ray value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Ray array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Ray[] value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Ray2D
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Ray2D value) => ReadUnmanagedSafe(out value);

        /// <summary>
        /// Read a Ray2D array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the values to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe(out Ray2D[] value) => ReadUnmanagedSafe(out value);

        // There are many FixedString types, but all of them share the interfaces INativeList<bool> and IUTF8Bytes.
        // INativeList<bool> provides the Length property
        // IUTF8Bytes provides GetUnsafePtr()
        // Those two are necessary to serialize FixedStrings efficiently
        // - otherwise we'd just be memcpying the whole thing even if
        // most of it isn't used.

        /// <summary>
        /// Read a FixedString value.
        /// This method is a little difficult to use, since you have to know the size of the string before
        /// reading it, but is useful when the string is a known, fixed size. Note that the size of the
        /// string is also encoded, so the size to call TryBeginRead on is actually the fixed size (in bytes)
        /// plus sizeof(uint)
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadValue<T>(out T value, FastBufferWriter.ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLength(out int length);
            value = new T
            {
                Length = length
            };
            ReadBytes(value.GetUnsafePtr(), length);
        }


        /// <summary>
        /// Read a FixedString value.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadValueSafe<T>(out T value, FastBufferWriter.ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value = new T
            {
                Length = length
            };
            ReadBytesSafe(value.GetUnsafePtr(), length);
        }


        /// <summary>
        /// Read a FixedString value.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadValueSafeInPlace<T>(ref T value, FastBufferWriter.ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value.Length = length;
            ReadBytesSafe(value.GetUnsafePtr(), length);
        }

        /// <summary>
        /// Read a FixedString NativeArray.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="allocator">The allocator to use to construct the resulting NativeArray</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadValueSafe<T>(out NativeArray<T> value, Allocator allocator)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value = new NativeArray<T>(length, allocator);
            var ptr = (T*)value.GetUnsafePtr();
            for (var i = 0; i < length; ++i)
            {
                ReadValueSafeInPlace(ref ptr[i]);
            }
        }

        /// <summary>
        /// Read a FixedString NativeArray using a Temp allocator. Equivalent to ReadValueSafe(out value, Allocator.Temp)
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadValueSafeTemp<T>(out NativeArray<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value = new NativeArray<T>(length, Allocator.Temp);
            var ptr = (T*)value.GetUnsafePtr();
            for (var i = 0; i < length; ++i)
            {
                ReadValueSafeInPlace(ref ptr[i]);
            }
        }

        /// <summary>
        /// Read a FixedString NativeArray using a Temp allocator. Equivalent to ReadValueSafe(out value, Allocator.Temp)
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafe<T>(out T[] value, FastBufferWriter.ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value = new T[length];
            for (var i = 0; i < length; ++i)
            {
                ReadValueSafeInPlace(ref value[i]);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Read a FixedString NativeList.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple reads at once by calling TryBeginRead.
        /// </summary>
        /// <param name="value">the value to read</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueSafeInPlace<T>(ref NativeList<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ReadLengthSafe(out int length);
            value.Resize(length, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < length; ++i)
            {
                ReadValueSafeInPlace(ref value.ElementAt(i));
            }
        }
#endif
    }
}
