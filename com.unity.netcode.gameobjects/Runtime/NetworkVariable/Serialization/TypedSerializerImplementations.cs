using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Netcode
{
    /// <summary>
    ///     Packing serializer for shorts
    /// </summary>
    internal class ShortSerializer : INetworkVariableSerializer<short>
    {
        public NetworkVariableType Type => NetworkVariableType.Short;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref short value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref short value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref short value, ref short previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref short value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref short value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref short value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref short value, ref short previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref short value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<short>.ReadWithAllocator(FastBufferReader reader, out short value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in short value, ref short duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Packing serializer for shorts
    /// </summary>
    internal class UshortSerializer : INetworkVariableSerializer<ushort>
    {
        public NetworkVariableType Type => NetworkVariableType.UShort;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref ushort value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref ushort value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref ushort value, ref ushort previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref ushort value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref ushort value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref ushort value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref ushort value, ref ushort previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref ushort value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<ushort>.ReadWithAllocator(FastBufferReader reader, out ushort value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in ushort value, ref ushort duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Packing serializer for ints
    /// </summary>
    internal class IntSerializer : INetworkVariableSerializer<int>
    {
        public NetworkVariableType Type => NetworkVariableType.Int;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref int value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref int value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref int value, ref int previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref int value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref int value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref int value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref int value, ref int previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref int value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<int>.ReadWithAllocator(FastBufferReader reader, out int value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in int value, ref int duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Packing serializer for ints
    /// </summary>
    internal class UintSerializer : INetworkVariableSerializer<uint>
    {
        public NetworkVariableType Type => NetworkVariableType.UInt;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref uint value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref uint value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref uint value, ref uint previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref uint value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref uint value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref uint value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref uint value, ref uint previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref uint value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<uint>.ReadWithAllocator(FastBufferReader reader, out uint value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in uint value, ref uint duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Packing serializer for longs
    /// </summary>
    internal class LongSerializer : INetworkVariableSerializer<long>
    {
        public NetworkVariableType Type => NetworkVariableType.Long;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref long value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref long value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref long value, ref long previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref long value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref long value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref long value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref long value, ref long previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref long value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<long>.ReadWithAllocator(FastBufferReader reader, out long value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in long value, ref long duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Packing serializer for longs
    /// </summary>
    internal class UlongSerializer : INetworkVariableSerializer<ulong>
    {
        public NetworkVariableType Type => NetworkVariableType.ULong;
        public bool IsDistributedAuthorityOptimized => true;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref ulong value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref ulong value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref ulong value, ref ulong previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref ulong value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref ulong value)
        {
            BytePacker.WriteValueBitPacked(writer, value);
        }

        public void Read(FastBufferReader reader, ref ulong value)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref ulong value, ref ulong previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref ulong value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<ulong>.ReadWithAllocator(FastBufferReader reader, out ulong value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in ulong value, ref ulong duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Basic serializer for unmanaged types.
    ///     This covers primitives, built-in unity types, and IForceSerializeByMemcpy
    ///     Since all of those ultimately end up calling WriteUnmanagedSafe, this simplifies things
    ///     by calling that directly - thus preventing us from having to have a specific T that meets
    ///     the specific constraints that the various generic WriteValue calls require.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class UnmanagedTypeSerializer<T> : INetworkVariableSerializer<T> where T : unmanaged
    {
        public NetworkVariableType Type => NetworkVariableType.Unmanaged;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref T value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref T value)
        {
            writer.WriteUnmanagedSafe(value);
        }

        public void Read(FastBufferReader reader, ref T value)
        {
            reader.ReadUnmanagedSafe(out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref T value)
        {
            Read(reader, ref value);
        }

        void INetworkVariableSerializer<T>.ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in T value, ref T duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    internal class ListSerializer<T> : INetworkVariableSerializer<List<T>>
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref List<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref List<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref List<T> value, ref List<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref List<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref List<T> value)
        {
            var isNull = value == null;
            writer.WriteValueSafe(isNull);
            if (!isNull)
            {
                BytePacker.WriteValuePacked(writer, value.Count);
                foreach (var item in value)
                {
                    var reffable = item;
                    NetworkVariableSerialization<T>.Write(writer, ref reffable);
                }
            }
        }

        public void Read(FastBufferReader reader, ref List<T> value)
        {
            reader.ReadValueSafe(out bool isNull);
            if (isNull)
            {
                value = null;
            }
            else
            {
                if (value == null)
                {
                    value = new List<T>();
                }

                ByteUnpacker.ReadValuePacked(reader, out int len);
                if (len < value.Count)
                {
                    value.RemoveRange(len, value.Count - len);
                }

                for (var i = 0; i < len; ++i)
                {
                    // Read in place where possible
                    if (i < value.Count)
                    {
                        var item = value[i];
                        NetworkVariableSerialization<T>.Read(reader, ref item);
                        value[i] = item;
                    }
                    else
                    {
                        T item = default;
                        NetworkVariableSerialization<T>.Read(reader, ref item);
                        value.Add(item);
                    }
                }
            }
        }

        public void WriteDelta(FastBufferWriter writer, ref List<T> value, ref List<T> previousValue)
        {
            CollectionSerializationUtility.WriteListDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref List<T> value)
        {
            CollectionSerializationUtility.ReadListDelta(reader, ref value);
        }

        void INetworkVariableSerializer<List<T>>.ReadWithAllocator(FastBufferReader reader, out List<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in List<T> value, ref List<T> duplicatedValue)
        {
            if (duplicatedValue == null)
            {
                duplicatedValue = new List<T>();
            }

            duplicatedValue.Clear();
            foreach (var item in value)
            {
                // This handles the nested list scenario List<List<T>>
                T subValue = default;
                NetworkVariableSerialization<T>.Duplicate(item, ref subValue);
                duplicatedValue.Add(subValue);
            }
        }
    }

    internal class HashSetSerializer<T> : INetworkVariableSerializer<HashSet<T>> where T : IEquatable<T>
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref HashSet<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref HashSet<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref HashSet<T> value, ref HashSet<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref HashSet<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref HashSet<T> value)
        {
            var isNull = value == null;
            writer.WriteValueSafe(isNull);
            if (!isNull)
            {
                writer.WriteValueSafe(value.Count);
                foreach (var item in value)
                {
                    var reffable = item;
                    NetworkVariableSerialization<T>.Write(writer, ref reffable);
                }
            }
        }


        public void Read(FastBufferReader reader, ref HashSet<T> value)
        {
            reader.ReadValueSafe(out bool isNull);
            if (isNull)
            {
                value = null;
            }
            else
            {
                if (value == null)
                {
                    value = new HashSet<T>();
                }
                else
                {
                    value.Clear();
                }

                reader.ReadValueSafe(out int len);
                for (var i = 0; i < len; ++i)
                {
                    T item = default;
                    NetworkVariableSerialization<T>.Read(reader, ref item);
                    value.Add(item);
                }
            }
        }

        public void WriteDelta(FastBufferWriter writer, ref HashSet<T> value, ref HashSet<T> previousValue)
        {
            CollectionSerializationUtility.WriteHashSetDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref HashSet<T> value)
        {
            CollectionSerializationUtility.ReadHashSetDelta(reader, ref value);
        }

        void INetworkVariableSerializer<HashSet<T>>.ReadWithAllocator(FastBufferReader reader, out HashSet<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in HashSet<T> value, ref HashSet<T> duplicatedValue)
        {
            if (duplicatedValue == null)
            {
                duplicatedValue = new HashSet<T>();
            }

            duplicatedValue.Clear();
            foreach (var item in value)
            {
                // Handles nested HashSets
                T subValue = default;
                NetworkVariableSerialization<T>.Duplicate(item, ref subValue);
                duplicatedValue.Add(item);
            }
        }
    }


    internal class DictionarySerializer<TKey, TVal> : INetworkVariableSerializer<Dictionary<TKey, TVal>>
        where TKey : IEquatable<TKey>
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref Dictionary<TKey, TVal> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref Dictionary<TKey, TVal> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref Dictionary<TKey, TVal> value, ref Dictionary<TKey, TVal> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref Dictionary<TKey, TVal> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref Dictionary<TKey, TVal> value)
        {
            var isNull = value == null;
            writer.WriteValueSafe(isNull);
            if (!isNull)
            {
                writer.WriteValueSafe(value.Count);
                foreach (var item in value)
                {
                    var (key, val) = (item.Key, item.Value);
                    NetworkVariableSerialization<TKey>.Write(writer, ref key);
                    NetworkVariableSerialization<TVal>.Write(writer, ref val);
                }
            }
        }

        public void Read(FastBufferReader reader, ref Dictionary<TKey, TVal> value)
        {
            reader.ReadValueSafe(out bool isNull);
            if (isNull)
            {
                value = null;
            }
            else
            {
                if (value == null)
                {
                    value = new Dictionary<TKey, TVal>();
                }
                else
                {
                    value.Clear();
                }

                reader.ReadValueSafe(out int len);
                for (var i = 0; i < len; ++i)
                {
                    (TKey key, TVal val) = (default, default);
                    NetworkVariableSerialization<TKey>.Read(reader, ref key);
                    NetworkVariableSerialization<TVal>.Read(reader, ref val);
                    value.Add(key, val);
                }
            }
        }

        public void WriteDelta(FastBufferWriter writer, ref Dictionary<TKey, TVal> value, ref Dictionary<TKey, TVal> previousValue)
        {
            CollectionSerializationUtility.WriteDictionaryDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref Dictionary<TKey, TVal> value)
        {
            CollectionSerializationUtility.ReadDictionaryDelta(reader, ref value);
        }

        void INetworkVariableSerializer<Dictionary<TKey, TVal>>.ReadWithAllocator(FastBufferReader reader, out Dictionary<TKey, TVal> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in Dictionary<TKey, TVal> value, ref Dictionary<TKey, TVal> duplicatedValue)
        {
            if (duplicatedValue == null)
            {
                duplicatedValue = new Dictionary<TKey, TVal>();
            }

            duplicatedValue.Clear();
            foreach (var item in value)
            {
                // Handles nested dictionaries
                TKey subKey = default;
                TVal subValue = default;
                NetworkVariableSerialization<TKey>.Duplicate(item.Key, ref subKey);
                NetworkVariableSerialization<TVal>.Duplicate(item.Value, ref subValue);
                duplicatedValue.Add(subKey, subValue);
            }
        }
    }

    internal class UnmanagedArraySerializer<T> : INetworkVariableSerializer<NativeArray<T>> where T : unmanaged
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref NativeArray<T> value)
        {
            writer.WriteUnmanagedSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeArray<T> value)
        {
            value.Dispose();
            reader.ReadUnmanagedSafe(out value, Allocator.Persistent);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeArrayDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeArray<T> value)
        {
            CollectionSerializationUtility.ReadNativeArrayDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeArray<T>>.ReadWithAllocator(FastBufferReader reader, out NativeArray<T> value, Allocator allocator)
        {
            reader.ReadUnmanagedSafe(out value, allocator);
        }

        public void Duplicate(in NativeArray<T> value, ref NativeArray<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated || duplicatedValue.Length != value.Length)
            {
                if (duplicatedValue.IsCreated)
                {
                    duplicatedValue.Dispose();
                }

                duplicatedValue = new NativeArray<T>(value.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            duplicatedValue.CopyFrom(value);
        }
    }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
    internal class UnmanagedListSerializer<T> : INetworkVariableSerializer<NativeList<T>> where T : unmanaged
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeList<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeList<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref NativeList<T> value)
        {
            writer.WriteUnmanagedSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeList<T> value)
        {
            reader.ReadUnmanagedSafeInPlace(ref value);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeListDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeList<T> value)
        {
            CollectionSerializationUtility.ReadNativeListDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeList<T>>.ReadWithAllocator(FastBufferReader reader, out NativeList<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in NativeList<T> value, ref NativeList<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated)
            {
                duplicatedValue = new NativeList<T>(value.Length, Allocator.Persistent);
            }
            else if (value.Length != duplicatedValue.Length)
            {
                duplicatedValue.ResizeUninitialized(value.Length);
            }

            duplicatedValue.CopyFrom(value);
        }
    }


    internal class NativeHashSetSerializer<T> : INetworkVariableSerializer<NativeHashSet<T>> where T : unmanaged, IEquatable<T>
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeHashSet<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeHashSet<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeHashSet<T> value, ref NativeHashSet<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeHashSet<T> value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref NativeHashSet<T> value)
        {
            writer.WriteValueSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeHashSet<T> value)
        {
            reader.ReadValueSafeInPlace(ref value);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeHashSet<T> value, ref NativeHashSet<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeHashSetDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeHashSet<T> value)
        {
            CollectionSerializationUtility.ReadNativeHashSetDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeHashSet<T>>.ReadWithAllocator(FastBufferReader reader, out NativeHashSet<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in NativeHashSet<T> value, ref NativeHashSet<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated)
            {
                duplicatedValue = new NativeHashSet<T>(value.Capacity, Allocator.Persistent);
            }

            duplicatedValue.Clear();
            foreach (var item in value)
            {
                duplicatedValue.Add(item);
            }
        }
    }


    internal class NativeHashMapSerializer<TKey, TVal> : INetworkVariableSerializer<NativeHashMap<TKey, TVal>>
        where TKey : unmanaged, IEquatable<TKey>
        where TVal : unmanaged
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeHashMap<TKey, TVal> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeHashMap<TKey, TVal> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeHashMap<TKey, TVal> value, ref NativeHashMap<TKey, TVal> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeHashMap<TKey, TVal> value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref NativeHashMap<TKey, TVal> value)
        {
            writer.WriteValueSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeHashMap<TKey, TVal> value)
        {
            reader.ReadValueSafeInPlace(ref value);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeHashMap<TKey, TVal> value, ref NativeHashMap<TKey, TVal> previousValue)
        {
            CollectionSerializationUtility.WriteNativeHashMapDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeHashMap<TKey, TVal> value)
        {
            CollectionSerializationUtility.ReadNativeHashMapDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeHashMap<TKey, TVal>>.ReadWithAllocator(FastBufferReader reader, out NativeHashMap<TKey, TVal> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in NativeHashMap<TKey, TVal> value, ref NativeHashMap<TKey, TVal> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated)
            {
                duplicatedValue = new NativeHashMap<TKey, TVal>(value.Capacity, Allocator.Persistent);
            }

            duplicatedValue.Clear();
            foreach (var item in value)
            {
                duplicatedValue.Add(item.Key, item.Value);
            }
        }
    }
#endif

    /// <summary>
    /// Serializer for FixedStrings
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FixedStringSerializer<T> : INetworkVariableSerializer<T> where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref T value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref T value)
        {
            writer.WriteValueSafe(value);
        }

        public void Read(FastBufferReader reader, ref T value)
        {
            reader.ReadValueSafeInPlace(ref value);
        }

        // Because of how strings are generally used, it is likely that most strings will still write as full strings
        // instead of deltas. This actually adds one byte to the data to encode that it was serialized in full.
        // But the potential savings from a small change to a large string are valuable enough to be worth that extra
        // byte.
        public unsafe void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            using var changes = new ResizableBitVector(Allocator.Temp);
            var minLength = math.min(value.Length, previousValue.Length);
            var numChanges = 0;
            for (var i = 0; i < minLength; ++i)
            {
                var val = value[i];
                var prevVal = previousValue[i];
                if (val != prevVal)
                {
                    ++numChanges;
                    changes.Set(i);
                }
            }

            for (var i = previousValue.Length; i < value.Length; ++i)
            {
                ++numChanges;
                changes.Set(i);
            }

            if (changes.GetSerializedSize() + FastBufferWriter.GetWriteSize<byte>() * numChanges > FastBufferWriter.GetWriteSize<byte>() * value.Length)
            {
                // If there are too many changes, send the entire array.
                writer.WriteByteSafe(1); // Flag that we're sending the entire array
                writer.WriteValueSafe(value);
                return;
            }

            writer.WriteByteSafe(0); // Flag that we're sending a delta
            BytePacker.WriteValuePacked(writer, value.Length);
            writer.WriteValueSafe(changes);
            var ptr = value.GetUnsafePtr();
            for (var i = 0; i < value.Length; ++i)
            {
                if (changes.IsSet(i))
                {
                    writer.WriteByteSafe(ptr[i]);
                }
            }
        }

        public unsafe void ReadDelta(FastBufferReader reader, ref T value)
        {
            // Writing can use the NativeArray logic as it is, but reading is a little different.
            // Using the NativeArray logic for reading would result in length changes allocating a new NativeArray,
            // which is not what we want for FixedString. With FixedString, the actual size of the data does not change,
            // only an in-memory "length" value - so if the length changes, the only thing we want to do is change
            // that value, and otherwise read everything in-place.
            reader.ReadByteSafe(out var full);
            if (full == 1)
            {
                reader.ReadValueSafeInPlace(ref value);
                return;
            }

            ByteUnpacker.ReadValuePacked(reader, out int length);
            var changes = new ResizableBitVector(Allocator.Temp);
            using var toDispose = changes;
            {
                reader.ReadNetworkSerializableInPlace(ref changes);

                value.Length = length;

                var ptr = value.GetUnsafePtr();
                for (var i = 0; i < value.Length; ++i)
                {
                    if (changes.IsSet(i))
                    {
                        reader.ReadByteSafe(out ptr[i]);
                    }
                }
            }
        }

        void INetworkVariableSerializer<T>.ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in T value, ref T duplicatedValue)
        {
            duplicatedValue = value;
        }
    }

    /// <summary>
    ///     Serializer for FixedStrings
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FixedStringArraySerializer<T> : INetworkVariableSerializer<NativeArray<T>> where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref NativeArray<T> value)
        {
            writer.WriteValueSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeArray<T> value)
        {
            value.Dispose();
            reader.ReadValueSafe(out value, Allocator.Persistent);
        }


        public void WriteDelta(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeArrayDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeArray<T> value)
        {
            CollectionSerializationUtility.ReadNativeArrayDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeArray<T>>.ReadWithAllocator(FastBufferReader reader, out NativeArray<T> value, Allocator allocator)
        {
            reader.ReadValueSafe(out value, allocator);
        }

        public void Duplicate(in NativeArray<T> value, ref NativeArray<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated || duplicatedValue.Length != value.Length)
            {
                if (duplicatedValue.IsCreated)
                {
                    duplicatedValue.Dispose();
                }

                duplicatedValue = new NativeArray<T>(value.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            duplicatedValue.CopyFrom(value);
        }
    }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
    /// <summary>
    ///     Serializer for FixedStrings
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FixedStringListSerializer<T> : INetworkVariableSerializer<NativeList<T>> where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeList<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeList<T> value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref NativeList<T> value)
        {
            writer.WriteValueSafe(value);
        }

        public void Read(FastBufferReader reader, ref NativeList<T> value)
        {
            reader.ReadValueSafeInPlace(ref value);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeListDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeList<T> value)
        {
            CollectionSerializationUtility.ReadNativeListDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeList<T>>.ReadWithAllocator(FastBufferReader reader, out NativeList<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in NativeList<T> value, ref NativeList<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated)
            {
                duplicatedValue = new NativeList<T>(value.Length, Allocator.Persistent);
            }
            else if (value.Length != duplicatedValue.Length)
            {
                duplicatedValue.ResizeUninitialized(value.Length);
            }

            duplicatedValue.CopyFrom(value);
        }
    }
#endif

    /// <summary>
    ///     Serializer for unmanaged INetworkSerializable types
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class UnmanagedNetworkSerializableSerializer<T> : INetworkVariableSerializer<T> where T : unmanaged, INetworkSerializable
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref T value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref T value)
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerWriter>(new BufferSerializerWriter(writer));
            value.NetworkSerialize(bufferSerializer);
        }

        public void Read(FastBufferReader reader, ref T value)
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerReader>(new BufferSerializerReader(reader));
            value.NetworkSerialize(bufferSerializer);
        }

        public void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            if (UserNetworkVariableSerialization<T>.WriteDelta != null && UserNetworkVariableSerialization<T>.ReadDelta != null)
            {
                UserNetworkVariableSerialization<T>.WriteDelta(writer, value, previousValue);
                return;
            }

            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref T value)
        {
            if (UserNetworkVariableSerialization<T>.WriteDelta != null && UserNetworkVariableSerialization<T>.ReadDelta != null)
            {
                UserNetworkVariableSerialization<T>.ReadDelta(reader, ref value);
                return;
            }

            Read(reader, ref value);
        }

        void INetworkVariableSerializer<T>.ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in T value, ref T duplicatedValue)
        {
            duplicatedValue = value;
        }
    }


    /// <summary>
    ///     Serializer for unmanaged INetworkSerializable types
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class UnmanagedNetworkSerializableArraySerializer<T> : INetworkVariableSerializer<NativeArray<T>> where T : unmanaged, INetworkSerializable
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeArray<T> value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref NativeArray<T> value)
        {
            writer.WriteNetworkSerializable(value);
        }

        public void Read(FastBufferReader reader, ref NativeArray<T> value)
        {
            value.Dispose();
            reader.ReadNetworkSerializable(out value, Allocator.Persistent);
        }


        public void WriteDelta(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeArrayDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeArray<T> value)
        {
            CollectionSerializationUtility.ReadNativeArrayDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeArray<T>>.ReadWithAllocator(FastBufferReader reader, out NativeArray<T> value, Allocator allocator)
        {
            reader.ReadNetworkSerializable(out value, allocator);
        }

        public void Duplicate(in NativeArray<T> value, ref NativeArray<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated || duplicatedValue.Length != value.Length)
            {
                if (duplicatedValue.IsCreated)
                {
                    duplicatedValue.Dispose();
                }

                duplicatedValue = new NativeArray<T>(value.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            duplicatedValue.CopyFrom(value);
        }
    }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
    /// <summary>
    ///     Serializer for unmanaged INetworkSerializable types
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class UnmanagedNetworkSerializableListSerializer<T> : INetworkVariableSerializer<NativeList<T>> where T : unmanaged, INetworkSerializable
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref NativeList<T> value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref NativeList<T> value) => Read(reader, ref value);

        public void Write(FastBufferWriter writer, ref NativeList<T> value)
        {
            writer.WriteNetworkSerializable(value);
        }

        public void Read(FastBufferReader reader, ref NativeList<T> value)
        {
            reader.ReadNetworkSerializableInPlace(ref value);
        }

        public void WriteDelta(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue)
        {
            CollectionSerializationUtility.WriteNativeListDelta(writer, ref value, ref previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref NativeList<T> value)
        {
            CollectionSerializationUtility.ReadNativeListDelta(reader, ref value);
        }

        void INetworkVariableSerializer<NativeList<T>>.ReadWithAllocator(FastBufferReader reader, out NativeList<T> value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in NativeList<T> value, ref NativeList<T> duplicatedValue)
        {
            if (!duplicatedValue.IsCreated)
            {
                duplicatedValue = new NativeList<T>(value.Length, Allocator.Persistent);
            }
            else if (value.Length != duplicatedValue.Length)
            {
                duplicatedValue.ResizeUninitialized(value.Length);
            }

            duplicatedValue.CopyFrom(value);
        }
    }
#endif

    /// <summary>
    ///     Serializer for managed INetworkSerializable types, which differs from the unmanaged implementation in that it
    ///     has to be null-aware
    ///     <typeparam name="T"></typeparam>
    internal class ManagedNetworkSerializableSerializer<T> : INetworkVariableSerializer<T> where T : class, INetworkSerializable, new()
    {
        public NetworkVariableType Type => NetworkVariableType.Value;
        public bool IsDistributedAuthorityOptimized => false;

        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value)
        {
            Write(writer, ref value);
        }

        public void ReadDistributedAuthority(FastBufferReader reader, ref T value)
        {
            Read(reader, ref value);
        }
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue) => Write(writer, ref value);
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value) => Read(reader, ref value);
        public void Write(FastBufferWriter writer, ref T value)
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerWriter>(new BufferSerializerWriter(writer));
            var isNull = value == null;
            bufferSerializer.SerializeValue(ref isNull);
            if (!isNull)
            {
                value.NetworkSerialize(bufferSerializer);
            }
        }

        public void Read(FastBufferReader reader, ref T value)
        {
            var bufferSerializer = new BufferSerializer<BufferSerializerReader>(new BufferSerializerReader(reader));
            var isNull = false;
            bufferSerializer.SerializeValue(ref isNull);
            if (isNull)
            {
                value = null;
            }
            else
            {
                if (value == null)
                {
                    value = new T();
                }

                value.NetworkSerialize(bufferSerializer);
            }
        }

        public void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            if (UserNetworkVariableSerialization<T>.WriteDelta != null && UserNetworkVariableSerialization<T>.ReadDelta != null)
            {
                UserNetworkVariableSerialization<T>.WriteDelta(writer, value, previousValue);
                return;
            }

            Write(writer, ref value);
        }

        public void ReadDelta(FastBufferReader reader, ref T value)
        {
            if (UserNetworkVariableSerialization<T>.WriteDelta != null && UserNetworkVariableSerialization<T>.ReadDelta != null)
            {
                UserNetworkVariableSerialization<T>.ReadDelta(reader, ref value);
                return;
            }

            Read(reader, ref value);
        }

        void INetworkVariableSerializer<T>.ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator)
        {
            throw new NotImplementedException();
        }


        public void Duplicate(in T value, ref T duplicatedValue)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp, int.MaxValue);
            var refValue = value;
            Write(writer, ref refValue);

            using var reader = new FastBufferReader(writer, Allocator.None);
            Read(reader, ref duplicatedValue);
        }
    }
}
