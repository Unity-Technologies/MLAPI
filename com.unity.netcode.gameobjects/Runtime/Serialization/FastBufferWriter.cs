using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Optimized class used for writing values into a byte stream
    /// <seealso cref="FastBufferReader"/>
    /// <seealso cref="BytePacker"/>
    /// <seealso cref="ByteUnpacker"/>
    /// </summary>
    public struct FastBufferWriter : IDisposable
    {
        internal struct WriterHandle
        {
            internal unsafe byte* BufferPointer;
            internal int Position;
            internal int Length;
            internal int Capacity;
            internal int MaxCapacity;
            internal Allocator Allocator;
            internal bool BufferGrew;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            internal int AllowedWriteMark;
            internal bool InBitwiseContext;
#endif
        }

        internal unsafe WriterHandle* Handle;

        private static byte[] s_ByteArrayCache = new byte[65535];

        /// <summary>
        /// The current write position
        /// </summary>
        public unsafe int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->Position;
        }

        /// <summary>
        /// The current total buffer size
        /// </summary>
        public unsafe int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->Capacity;
        }

        /// <summary>
        /// The maximum possible total buffer size
        /// </summary>
        public unsafe int MaxCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->MaxCapacity;
        }

        /// <summary>
        /// The total amount of bytes that have been written to the stream
        /// </summary>
        public unsafe int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handle->Position > Handle->Length ? Handle->Position : Handle->Length;
        }

        /// <summary>
        /// Gets a value indicating whether the writer has been initialized and a handle allocated.
        /// </summary>
        public unsafe bool IsInitialized => Handle != null;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CommitBitwiseWrites(int amount)
        {
            Handle->Position += amount;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->InBitwiseContext = false;
#endif
        }

        /// <summary>
        /// Create a FastBufferWriter.
        /// </summary>
        /// <param name="size">Size of the buffer to create</param>
        /// <param name="allocator">Allocator to use in creating it</param>
        /// <param name="maxSize">Maximum size the buffer can grow to. If less than size, buffer cannot grow.</param>
        public unsafe FastBufferWriter(int size, Allocator allocator, int maxSize = -1)
        {
            // Allocating both the Handle struct and the buffer in a single allocation - sizeof(WriterHandle) + size
            // The buffer for the initial allocation is the next block of memory after the handle itself.
            // If the buffer grows, a new buffer will be allocated and the handle pointer pointed at the new location...
            // The original buffer won't be deallocated until the writer is destroyed since it's part of the handle allocation.
            Handle = (WriterHandle*)UnsafeUtility.Malloc(sizeof(WriterHandle) + size, UnsafeUtility.AlignOf<WriterHandle>(), allocator);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnsafeUtility.MemSet(Handle, 0, sizeof(WriterHandle) + size);
#endif
            Handle->BufferPointer = (byte*)(Handle + 1);
            Handle->Position = 0;
            Handle->Length = 0;
            Handle->Capacity = size;
            Handle->Allocator = allocator;
            Handle->MaxCapacity = maxSize < size ? size : maxSize;
            Handle->BufferGrew = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->AllowedWriteMark = 0;
            Handle->InBitwiseContext = false;
#endif
        }

        /// <summary>
        /// <see cref="IDisposable"/> implementation that frees the allocated buffer
        /// </summary>
        public unsafe void Dispose()
        {
            if (Handle->BufferGrew)
            {
                UnsafeUtility.Free(Handle->BufferPointer, Handle->Allocator);
            }
            UnsafeUtility.Free(Handle, Handle->Allocator);
            Handle = null;
        }

        /// <summary>
        /// Move the write position in the stream.
        /// Note that moving forward past the current length will extend the buffer's Length value even if you don't write.
        /// </summary>
        /// <param name="where">Absolute value to move the position to, truncated to Capacity</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Seek(int where)
        {
            // This avoids us having to synchronize length all the time.
            // Writing things is a much more common operation than seeking
            // or querying length. The length here is a high watermark of
            // what's been written. So before we seek, if the current position
            // is greater than the length, we update that watermark.
            // When querying length later, we'll return whichever of the two
            // values is greater, thus if we write past length, length increases
            // because position increases, and if we seek backward, length remembers
            // the position it was in.
            // Seeking forward will not update the length.
            where = Math.Min(where, Handle->Capacity);
            if (Handle->Position > Handle->Length && where < Handle->Position)
            {
                Handle->Length = Handle->Position;
            }

            Handle->Position = where;
        }

        /// <summary>
        /// Truncate the stream by setting Length to the specified value.
        /// If Position is greater than the specified value, it will be moved as well.
        /// </summary>
        /// <param name="where">The value to truncate to. If -1, the current position will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Truncate(int where = -1)
        {
            if (where == -1)
            {
                where = Position;
            }

            if (Handle->Position > where)
            {
                Handle->Position = where;
            }

            if (Handle->Length > where)
            {
                Handle->Length = where;
            }
        }

        /// <summary>
        /// Retrieve a BitWriter to be able to perform bitwise operations on the buffer.
        /// No bytewise operations can be performed on the buffer until bitWriter.Dispose() has been called.
        /// At the end of the operation, FastBufferWriter will remain byte-aligned.
        /// </summary>
        /// <returns>A BitWriter</returns>
        public unsafe BitWriter EnterBitwiseContext()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->InBitwiseContext = true;
#endif
            return new BitWriter(this);
        }

        internal unsafe void Grow(int additionalSizeRequired)
        {
            var desiredSize = Handle->Capacity * 2;
            while (desiredSize < Position + additionalSizeRequired)
            {
                desiredSize *= 2;
            }

            var newSize = Math.Min(desiredSize, Handle->MaxCapacity);
            byte* newBuffer = (byte*)UnsafeUtility.Malloc(newSize, UnsafeUtility.AlignOf<byte>(), Handle->Allocator);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnsafeUtility.MemSet(newBuffer, 0, newSize);
#endif
            UnsafeUtility.MemCpy(newBuffer, Handle->BufferPointer, Length);
            if (Handle->BufferGrew)
            {
                UnsafeUtility.Free(Handle->BufferPointer, Handle->Allocator);
            }

            Handle->BufferGrew = true;
            Handle->BufferPointer = newBuffer;
            Handle->Capacity = newSize;
        }

        /// <summary>
        /// Allows faster serialization by batching bounds checking.
        /// When you know you will be writing multiple fields back-to-back and you know the total size,
        /// you can call TryBeginWrite() once on the total size, and then follow it with calls to
        /// WriteValue() instead of WriteValueSafe() for faster serialization.
        ///
        /// Unsafe write operations will throw OverflowException in editor and development builds if you
        /// go past the point you've marked using TryBeginWrite(). In release builds, OverflowException will not be thrown
        /// for performance reasons, since the point of using TryBeginWrite is to avoid bounds checking in the following
        /// operations in release builds.
        /// </summary>
        /// <param name="bytes">Amount of bytes to write</param>
        /// <returns>True if the write is allowed, false otherwise</returns>
        /// <exception cref="InvalidOperationException">If called while in a bitwise context</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryBeginWrite(int bytes)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif
            if (Handle->Position + bytes > Handle->Capacity)
            {
                if (Handle->Position + bytes > Handle->MaxCapacity)
                {
                    return false;
                }

                if (Handle->Capacity < Handle->MaxCapacity)
                {
                    Grow(bytes);
                }
                else
                {
                    return false;
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->AllowedWriteMark = Handle->Position + bytes;
#endif
            return true;
        }

        /// <summary>
        /// Allows faster serialization by batching bounds checking.
        /// When you know you will be writing multiple fields back-to-back and you know the total size,
        /// you can call TryBeginWrite() once on the total size, and then follow it with calls to
        /// WriteValue() instead of WriteValueSafe() for faster serialization.
        ///
        /// Unsafe write operations will throw OverflowException in editor and development builds if you
        /// go past the point you've marked using TryBeginWrite(). In release builds, OverflowException will not be thrown
        /// for performance reasons, since the point of using TryBeginWrite is to avoid bounds checking in the following
        /// operations in release builds. Instead, attempting to write past the marked position in release builds
        /// will write to random memory and cause undefined behavior, likely including instability and crashes.
        /// </summary>
        /// <typeparam name="T">The value type to write</typeparam>
        /// <param name="value">The value of the type `T` you want to write</param>
        /// <returns>True if the write is allowed, false otherwise</returns>
        /// <exception cref="InvalidOperationException">If called while in a bitwise context</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryBeginWriteValue<T>(in T value) where T : unmanaged
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif
            int len = sizeof(T);
            if (Handle->Position + len > Handle->Capacity)
            {
                if (Handle->Position + len > Handle->MaxCapacity)
                {
                    return false;
                }

                if (Handle->Capacity < Handle->MaxCapacity)
                {
                    Grow(len);
                }
                else
                {
                    return false;
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Handle->AllowedWriteMark = Handle->Position + len;
#endif
            return true;
        }

        /// <summary>
        /// Internal version of TryBeginWrite.
        /// Differs from TryBeginWrite only in that it won't ever move the AllowedWriteMark backward.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryBeginWriteInternal(int bytes)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif
            if (Handle->Position + bytes > Handle->Capacity)
            {
                if (Handle->Position + bytes > Handle->MaxCapacity)
                {
                    return false;
                }

                if (Handle->Capacity < Handle->MaxCapacity)
                {
                    Grow(bytes);
                }
                else
                {
                    return false;
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->Position + bytes > Handle->AllowedWriteMark)
            {
                Handle->AllowedWriteMark = Handle->Position + bytes;
            }
#endif
            return true;
        }

        /// <summary>
        /// Returns an array representation of the underlying byte buffer.
        /// !!Allocates a new array!!
        /// </summary>
        /// <returns></returns>
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
        /// Uses a static cached array to create an array segment with no allocations.
        /// This array can only be used until the next time ToTempByteArray() is called on ANY FastBufferWriter,
        /// as the cached buffer is shared by all of them and will be overwritten.
        /// As such, this should be used with care.
        /// </summary>
        /// <returns></returns>
        internal unsafe ArraySegment<byte> ToTempByteArray()
        {
            var length = Length;
            if (length > s_ByteArrayCache.Length)
            {
                return new ArraySegment<byte>(ToArray(), 0, length);
            }

            fixed (byte* b = s_ByteArrayCache)
            {
                UnsafeUtility.MemCpy(b, Handle->BufferPointer, length);
            }

            return new ArraySegment<byte>(s_ByteArrayCache, 0, length);
        }

        /// <summary>
        /// Gets a direct pointer to the underlying buffer
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetUnsafePtr()
        {
            return Handle->BufferPointer;
        }

        /// <summary>
        /// Gets a direct pointer to the underlying buffer at the current read position
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetUnsafePtrAtCurrentPosition()
        {
            return Handle->BufferPointer + Handle->Position;
        }

        /// <summary>
        /// Get the required size to write a string
        /// </summary>
        /// <param name="s">The string to write</param>
        /// <param name="oneByteChars">Whether or not to use one byte per character. This will only allow ASCII</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWriteSize(string s, bool oneByteChars = false)
        {
            return SizeOfLengthField() + s.Length * (oneByteChars ? sizeof(byte) : sizeof(char));
        }

        /// <summary>
        /// Write an INetworkSerializable
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <typeparam name="T"></typeparam>
        public void WriteNetworkSerializable<T>(in T value) where T : INetworkSerializable
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerWriter>(new BufferSerializerWriter(this));
            value.NetworkSerialize(bufferSerializer);
        }

        /// <summary>
        /// Write an array of INetworkSerializables
        /// </summary>
        /// <param name="array">The value to write</param>
        /// <param name="count"></param>
        /// <param name="offset"></param>
        /// <typeparam name="T"></typeparam>
        public void WriteNetworkSerializable<T>(T[] array, int count = -1, int offset = 0) where T : INetworkSerializable
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            WriteLengthSafe(sizeInTs);
            foreach (var item in array)
            {
                WriteNetworkSerializable(item);
            }
        }

        /// <summary>
        /// Write a NativeArray of INetworkSerializables
        /// </summary>
        /// <param name="array">The value to write</param>
        /// <param name="count"></param>
        /// <param name="offset"></param>
        /// <typeparam name="T"></typeparam>
        public void WriteNetworkSerializable<T>(NativeArray<T> array, int count = -1, int offset = 0) where T : unmanaged, INetworkSerializable
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            WriteLengthSafe(sizeInTs);
            foreach (var item in array)
            {
                WriteNetworkSerializable(item);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Write a NativeList of INetworkSerializables
        /// </summary>
        /// <param name="array">The value to write</param>
        /// <param name="count"></param>
        /// <param name="offset"></param>
        /// <typeparam name="T"></typeparam>
        public void WriteNetworkSerializable<T>(NativeList<T> array, int count = -1, int offset = 0) where T : unmanaged, INetworkSerializable
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            WriteLengthSafe(sizeInTs);
            foreach (var item in array)
            {
                WriteNetworkSerializable(item);
            }
        }
#endif

        /// <summary>
        /// Writes a string
        /// </summary>
        /// <param name="s">The string to write</param>
        /// <param name="oneByteChars">Whether or not to use one byte per character. This will only allow ASCII</param>
        public unsafe void WriteValue(string s, bool oneByteChars = false)
        {
            WriteLength((uint)s.Length);
            int target = s.Length;
            if (oneByteChars)
            {
                for (int i = 0; i < target; ++i)
                {
                    WriteByte((byte)s[i]);
                }
            }
            else
            {
                fixed (char* native = s)
                {
                    WriteBytes((byte*)native, target * sizeof(char));
                }
            }
        }

        /// <summary>
        /// Writes a string
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="s">The string to write</param>
        /// <param name="oneByteChars">Whether or not to use one byte per character. This will only allow ASCII</param>
        public unsafe void WriteValueSafe(string s, bool oneByteChars = false)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif

            int sizeInBytes = GetWriteSize(s, oneByteChars);

            if (!TryBeginWriteInternal(sizeInBytes))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }

            WriteLength((uint)s.Length);
            int target = s.Length;
            if (oneByteChars)
            {
                for (int i = 0; i < target; ++i)
                {
                    WriteByte((byte)s[i]);
                }
            }
            else
            {
                fixed (char* native = s)
                {
                    WriteBytes((byte*)native, target * sizeof(char));
                }
            }
        }

        /// <summary>
        /// Get the required size to write an unmanaged array
        /// </summary>
        /// <param name="array">The array to write</param>
        /// <param name="count">The amount of elements to write</param>
        /// <param name="offset">Where in the array to start</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetWriteSize<T>(T[] array, int count = -1, int offset = 0) where T : unmanaged
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            int sizeInBytes = sizeInTs * sizeof(T);
            return SizeOfLengthField() + sizeInBytes;
        }

        /// <summary>
        /// Get the required size to write a NativeArray
        /// </summary>
        /// <param name="array">The array to write</param>
        /// <param name="count">The amount of elements to write</param>
        /// <param name="offset">Where in the array to start</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetWriteSize<T>(NativeArray<T> array, int count = -1, int offset = 0) where T : unmanaged
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            int sizeInBytes = sizeInTs * sizeof(T);
            return SizeOfLengthField() + sizeInBytes;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Get the required size to write a NativeList
        /// </summary>
        /// <param name="array">The array to write</param>
        /// <param name="count">The amount of elements to write</param>
        /// <param name="offset">Where in the array to start</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetWriteSize<T>(NativeList<T> array, int count = -1, int offset = 0) where T : unmanaged
        {
            int sizeInTs = count != -1 ? count : array.Length - offset;
            int sizeInBytes = sizeInTs * sizeof(T);
            return SizeOfLengthField() + sizeInBytes;
        }
#endif

        /// <summary>
        /// Write a partial value. The specified number of bytes is written from the value and the rest is ignored.
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="bytesToWrite">Number of bytes</param>
        /// <param name="offsetBytes">Offset into the value to begin reading the bytes</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WritePartialValue<T>(T value, int bytesToWrite, int offsetBytes = 0) where T : unmanaged
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + bytesToWrite > Handle->AllowedWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWrite)}()");
            }
#endif

            byte* ptr = ((byte*)&value) + offsetBytes;
            byte* bufferPointer = Handle->BufferPointer + Handle->Position;
            UnsafeUtility.MemCpy(bufferPointer, ptr, bytesToWrite);

            Handle->Position += bytesToWrite;
        }

        /// <summary>
        /// Write a byte to the stream.
        /// </summary>
        /// <param name="value">Value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteByte(byte value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + 1 > Handle->AllowedWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWrite)}()");
            }
#endif
            Handle->BufferPointer[Handle->Position++] = value;
        }

        /// <summary>
        /// Write a byte to the stream.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">Value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteByteSafe(byte value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif

            if (!TryBeginWriteInternal(1))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }
            Handle->BufferPointer[Handle->Position++] = value;
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytes(byte* value, int size, int offset = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
            if (Handle->Position + size > Handle->AllowedWriteMark)
            {
                throw new OverflowException($"Attempted to write without first calling {nameof(TryBeginWrite)}(), Position+Size={Handle->Position + size} > AllowedWriteMark={Handle->AllowedWriteMark}");
            }
#endif
            UnsafeUtility.MemCpy((Handle->BufferPointer + Handle->Position), value + offset, size);
            Handle->Position += size;
        }

        /// <summary>
        /// Write multiple bytes to the stream
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytesSafe(byte* value, int size, int offset = 0)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Handle->InBitwiseContext)
            {
                throw new InvalidOperationException(
                    "Cannot use BufferWriter in bytewise mode while in a bitwise context.");
            }
#endif

            if (!TryBeginWriteInternal(size))
            {
                throw new OverflowException($"Writing past the end of the buffer, size is {size} bytes but remaining capacity is {Handle->Capacity - Handle->Position} bytes");
            }
            UnsafeUtility.MemCpy((Handle->BufferPointer + Handle->Position), value + offset, size);
            Handle->Position += size;
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytes(byte[] value, int size = -1, int offset = 0)
        {
            fixed (byte* ptr = value)
            {
                WriteBytes(ptr, size == -1 ? value.Length : size, offset);
            }
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytes(NativeArray<byte> value, int size = -1, int offset = 0)
        {
            byte* ptr = (byte*)value.GetUnsafePtr();
            WriteBytes(ptr, size == -1 ? value.Length : size, offset);
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytes(NativeList<byte> value, int size = -1, int offset = 0)
        {
#if UTP_TRANSPORT_2_0_ABOVE
            byte* ptr = value.GetUnsafePtr();
#else
            byte* ptr = (byte*)value.GetUnsafePtr();
#endif
            WriteBytes(ptr, size == -1 ? value.Length : size, offset);
        }

        /// <summary>
        /// Write multiple bytes to the stream
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytesSafe(byte[] value, int size = -1, int offset = 0)
        {
            fixed (byte* ptr = value)
            {
                WriteBytesSafe(ptr, size == -1 ? value.Length : size, offset);
            }
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytesSafe(NativeArray<byte> value, int size = -1, int offset = 0)
        {
            byte* ptr = (byte*)value.GetUnsafePtr();
            WriteBytesSafe(ptr, size == -1 ? value.Length : size, offset);
        }

        /// <summary>
        /// Write multiple bytes to the stream
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="offset">Offset into the buffer to begin writing</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBytesSafe(NativeList<byte> value, int size = -1, int offset = 0)
        {
#if UTP_TRANSPORT_2_0_ABOVE
            byte* ptr = value.GetUnsafePtr();
#else
            byte* ptr = (byte*)value.GetUnsafePtr();
#endif
            WriteBytesSafe(ptr, size == -1 ? value.Length : size, offset);
        }

        /// <summary>
        /// Copy the contents of this writer into another writer.
        /// The contents will be copied from the beginning of this writer to its current position.
        /// They will be copied to the other writer starting at the other writer's current position.
        /// </summary>
        /// <param name="other">Writer to copy to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(FastBufferWriter other)
        {
            other.WriteBytes(Handle->BufferPointer, Handle->Position);
        }

        /// <summary>
        /// Copy the contents of another writer into this writer.
        /// The contents will be copied from the beginning of the other writer to its current position.
        /// They will be copied to this writer starting at this writer's current position.
        /// </summary>
        /// <param name="other">Writer to copy to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyFrom(FastBufferWriter other)
        {
            WriteBytes(other.Handle->BufferPointer, other.Handle->Position);
        }

        /// <summary>
        /// Get the write size for any general unmanaged value
        /// The ForStructs value here makes this the lowest-priority overload so other versions
        /// will be prioritized over this if they match
        /// </summary>
        /// <param name="value"></param>
        /// <param name="unused"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetWriteSize<T>(in T value, ForStructs unused = default) where T : unmanaged
        {
            return sizeof(T);
        }

        /// <summary>
        /// Get the write size for a FixedString
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetWriteSize<T>(in T value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return SizeOfLengthField() + value.Length;
        }

        /// <summary>
        /// Get the write size for an array of FixedStrings
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetWriteSize<T>(in NativeArray<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var size = SizeOfLengthField();
            foreach (var item in value)
            {
                size += SizeOfLengthField() + item.Length;
            }

            return size;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Get the write size for an array of FixedStrings
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetWriteSize<T>(in NativeList<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var size = SizeOfLengthField();
            foreach (var item in value)
            {
                size += SizeOfLengthField() + item.Length;
            }

            return size;
        }
#endif

        /// <summary>
        /// Get the size required to write an unmanaged value of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static unsafe int GetWriteSize<T>() where T : unmanaged
        {
            return sizeof(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanaged<T>(in T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                byte* bytes = (byte*)ptr;
                WriteBytes(bytes, sizeof(T));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanagedSafe<T>(in T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                byte* bytes = (byte*)ptr;
                WriteBytesSafe(bytes, sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SizeOfLengthField() => sizeof(uint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteLengthSafe(uint length) => WriteUnmanagedSafe(length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteLength(uint length) => WriteUnmanaged(length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteLengthSafe(int length)
        {
            if (length < 0)
            {
                throw new InvalidCastException("Cannot write negative length");
            }
            WriteLengthSafe((uint)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteLength(int length) => WriteLength((uint)length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanaged<T>(T[] value) where T : unmanaged
        {
            WriteLength(value.Length);
            fixed (T* ptr = value)
            {
                byte* bytes = (byte*)ptr;
                WriteBytes(bytes, sizeof(T) * value.Length);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanagedSafe<T>(T[] value) where T : unmanaged
        {
            WriteLengthSafe(value.Length);
            fixed (T* ptr = value)
            {
                byte* bytes = (byte*)ptr;
                WriteBytesSafe(bytes, sizeof(T) * value.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanaged<T>(NativeArray<T> value) where T : unmanaged
        {
            WriteLength(value.Length);
            var ptr = (T*)value.GetUnsafePtr();
            {
                byte* bytes = (byte*)ptr;
                WriteBytes(bytes, sizeof(T) * value.Length);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanagedSafe<T>(NativeArray<T> value) where T : unmanaged
        {
            WriteLengthSafe(value.Length);
            var ptr = (T*)value.GetUnsafePtr();
            {
                byte* bytes = (byte*)ptr;
                WriteBytesSafe(bytes, sizeof(T) * value.Length);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanaged<T>(NativeList<T> value) where T : unmanaged
        {
            WriteLength(value.Length);
#if UTP_TRANSPORT_2_0_ABOVE
            var ptr = value.GetUnsafePtr();
#else
            var ptr = (T*)value.GetUnsafePtr();
#endif
            {
                byte* bytes = (byte*)ptr;
                WriteBytes(bytes, sizeof(T) * value.Length);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanagedSafe<T>(NativeList<T> value) where T : unmanaged
        {
            WriteLengthSafe(value.Length);
#if UTP_TRANSPORT_2_0_ABOVE
            var ptr = value.GetUnsafePtr();
#else
            var ptr = (T*)value.GetUnsafePtr();
#endif
            {
                byte* bytes = (byte*)ptr;
                WriteBytesSafe(bytes, sizeof(T) * value.Length);
            }
        }
#endif

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForPrimitives
        {

        }

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForEnums
        {

        }

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForStructs
        {

        }

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForNetworkSerializable
        {

        }

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForFixedStrings
        {

        }

        /// <summary>
        /// This empty struct exists to allow overloading WriteValue based on generic constraints.
        /// At the bytecode level, constraints aren't included in the method signature, so if multiple
        /// methods exist with the same signature, it causes a compile error because they would end up
        /// being emitted as the same method, even if the constraints are different.
        /// Adding an empty struct with a default value gives them different signatures in the bytecode,
        /// which then allows the compiler to do overload resolution based on the generic constraints
        /// without the user having to pass the struct in themselves.
        /// </summary>
        public struct ForGeneric
        {

        }

        /// <summary>
        /// Write a NetworkSerializable value
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in T value, ForNetworkSerializable unused = default) where T : INetworkSerializable => WriteNetworkSerializable(value);

        /// <summary>
        /// Write a NetworkSerializable array
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(T[] value, ForNetworkSerializable unused = default) where T : INetworkSerializable => WriteNetworkSerializable(value);

        /// <summary>
        /// Write a NetworkSerializable value
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in T value, ForNetworkSerializable unused = default) where T : INetworkSerializable => WriteNetworkSerializable(value);

        /// <summary>
        /// Write a NetworkSerializable array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(T[] value, ForNetworkSerializable unused = default) where T : INetworkSerializable => WriteNetworkSerializable(value);

        /// <summary>
        /// Write a struct
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in T value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => WriteUnmanaged(value);

        /// <summary>
        /// Write a struct array
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(T[] value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => WriteUnmanaged(value);

        /// <summary>
        /// Write a struct NativeArray
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(NativeArray<T> value, ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.Write(this, ref value);
            }
            else
            {
                WriteUnmanaged(value);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Write a struct NativeList
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(NativeList<T> value, ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeList<T>>.Serializer.Write(this, ref value);
            }
            else
            {
                WriteUnmanaged(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteValueSafe<T>(NativeHashSet<T> value) where T : unmanaged, IEquatable<T>
        {
#if UTP_TRANSPORT_2_0_ABOVE
            WriteLengthSafe(value.Count);
#else
            WriteLengthSafe(value.Count());
#endif
            foreach (var item in value)
            {
                var iReffable = item;
                NetworkVariableSerialization<T>.Write(this, ref iReffable);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteValueSafe<TKey, TVal>(NativeHashMap<TKey, TVal> value)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
#if UTP_TRANSPORT_2_0_ABOVE
            WriteLengthSafe(value.Count);
#else
            WriteLengthSafe(value.Count());
#endif
            foreach (var item in value)
            {
                (var key, var val) = (item.Key, item.Value);
                NetworkVariableSerialization<TKey>.Write(this, ref key);
                NetworkVariableSerialization<TVal>.Write(this, ref val);
            }
        }
#endif

        /// <summary>
        /// Write a struct
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in T value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a struct array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(T[] value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a struct NativeArray
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(NativeArray<T> value, ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeArray<T>>.Serializer.Write(this, ref value);
            }
            else
            {
                WriteUnmanagedSafe(value);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Write a struct NativeList
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(NativeList<T> value, ForGeneric unused = default) where T : unmanaged
        {
            if (typeof(INetworkSerializable).IsAssignableFrom(typeof(T)))
            {
                // This calls WriteNetworkSerializable in a way that doesn't require
                // any boxing.
                NetworkVariableSerialization<NativeList<T>>.Serializer.Write(this, ref value);
            }
            else
            {
                WriteUnmanagedSafe(value);
            }
        }
#endif

        /// <summary>
        /// Write a primitive value (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in T value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => WriteUnmanaged(value);

        /// <summary>
        /// Write a primitive value array (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(T[] value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => WriteUnmanaged(value);

        /// <summary>
        /// Write a primitive value (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in T value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a primitive value (int, bool, etc)
        /// Accepts any value that implements the given interfaces, but is not guaranteed to work correctly
        /// on values that are not primitives.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(T[] value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write an enum value
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in T value, ForEnums unused = default) where T : unmanaged, Enum => WriteUnmanaged(value);

        /// <summary>
        /// Write an enum array
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(T[] value, ForEnums unused = default) where T : unmanaged, Enum => WriteUnmanaged(value);

        /// <summary>
        /// Write an enum value
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in T value, ForEnums unused = default) where T : unmanaged, Enum => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write an enum array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(T[] value, ForEnums unused = default) where T : unmanaged, Enum => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector2
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Vector2 value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector2 array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Vector2[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector3
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Vector3 value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector3 array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Vector3[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector2Int
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Vector2Int value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector2Int array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Vector2Int[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector3Int
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Vector3Int value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector3Int array
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Vector3Int[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector4
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Vector4 value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector4
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Vector4[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Quaternion
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Quaternion value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Quaternion array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Quaternion[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Color
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Color value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Color array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Color[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Color32
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Color32 value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Color32 array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Color32[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Ray
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Ray value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Ray array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Ray[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Ray2D
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(in Ray2D value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Ray2D array
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue(Ray2D[] value) => WriteUnmanaged(value);

        /// <summary>
        /// Write a Vector2
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Vector2 value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector2 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Vector2[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector3
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Vector3 value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector3 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Vector3[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector2Int
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Vector2Int value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector2Int array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Vector2Int[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector3Int
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Vector3Int value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector3Int array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Vector3Int[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector4
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Vector4 value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Vector4 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Vector4[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Quaternion
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Quaternion value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Quaternion array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Quaternion[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Color
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Color value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Collor array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Color[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Color32
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Color32 value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Color32 array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Color32[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Ray
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Ray value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Ray array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Ray[] value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Ray2D
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(in Ray2D value) => WriteUnmanagedSafe(value);

        /// <summary>
        /// Write a Ray2D array
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the values to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe(Ray2D[] value) => WriteUnmanagedSafe(value);

        // There are many FixedString types, but all of them share the interfaces INativeList<bool> and IUTF8Bytes.
        // INativeList<bool> provides the Length property
        // IUTF8Bytes provides GetUnsafePtr()
        // Those two are necessary to serialize FixedStrings efficiently
        // - otherwise we'd just be memcpying the whole thing even if
        // most of it isn't used.

        /// <summary>
        /// Write a FixedString value. Writes only the part of the string that's actually used.
        /// When calling TryBeginWrite, ensure you calculate the write size correctly (preferably by calling
        /// FastBufferWriter.GetWriteSize())
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteValue<T>(in T value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            // BytePacker.WriteValuePacked(this, value.Length);
            WriteLength(value.Length);
            // This avoids a copy on the string, which could be costly for FixedString4096Bytes
            // Otherwise, GetUnsafePtr() is an impure function call and will result in a copy
            // for `in` parameters.
            fixed (T* ptr = &value)
            {
                WriteBytes(ptr->GetUnsafePtr(), value.Length);
            }
        }

        /// <summary>
        /// Write an array of FixedString values. Writes only the part of each string that's actually used.
        /// When calling TryBeginWrite, ensure you calculate the write size correctly (preferably by calling
        /// FastBufferWriter.GetWriteSize())
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(T[] value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }

        /// <summary>
        /// Write a NativeArray of FixedString values. Writes only the part of each string that's actually used.
        /// When calling TryBeginWrite, ensure you calculate the write size correctly (preferably by calling
        /// FastBufferWriter.GetWriteSize())
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in NativeArray<T> value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Write a NativeList of FixedString values. Writes only the part of each string that's actually used.
        /// When calling TryBeginWrite, ensure you calculate the write size correctly (preferably by calling
        /// FastBufferWriter.GetWriteSize())
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(in NativeList<T> value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }
#endif


        /// <summary>
        /// Write a FixedString value. Writes only the part of the string that's actually used.
        ///
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in T value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (!TryBeginWriteInternal(SizeOfLengthField() + value.Length))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }
            WriteValue(value);
        }

        /// <summary>
        /// Write a NativeArray of FixedString values. Writes only the part of each string that's actually used.
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(T[] value, ForFixedStrings unused = default)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (!TryBeginWriteInternal(GetWriteSize(value)))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }

        /// <summary>
        /// Write a NativeArray of FixedString values. Writes only the part of each string that's actually used.
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in NativeArray<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (!TryBeginWriteInternal(GetWriteSize(value)))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Write a NativeList of FixedString values. Writes only the part of each string that's actually used.
        /// "Safe" version - automatically performs bounds checking. Less efficient than bounds checking
        /// for multiple writes at once by calling TryBeginWrite.
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <param name="unused">An unused parameter used for enabling overload resolution based on generic constraints</param>
        /// <typeparam name="T">The type being serialized</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSafe<T>(in NativeList<T> value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (!TryBeginWriteInternal(GetWriteSize(value)))
            {
                throw new OverflowException("Writing past the end of the buffer");
            }
            WriteLength(value.Length);
            foreach (var str in value)
            {
                WriteValue(str);
            }
        }
#endif
    }
}
