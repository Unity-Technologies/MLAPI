using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Netcode
{
    internal static class CollectionSerializationUtility
    {
        public static void WriteNativeArrayDelta<T>(FastBufferWriter writer, ref NativeArray<T> value, ref NativeArray<T> previousValue) where T : unmanaged
        {
            // This bit vector serializes the list of which fields have changed using 1 bit per field.
            // This will always be 1 bit per field of the whole array (rounded up to the nearest 8 bits)
            // even if there is only one change, so as compared to serializing the index with each item,
            // this will use more bandwidth when the overall bandwidth usage is small and the array is large,
            // but less when the overall bandwidth usage is large. So it optimizes for the worst case while accepting
            // some reduction in efficiency in the best case.
            using var changes = new ResizableBitVector(Allocator.Temp);
            int minLength = math.min(value.Length, previousValue.Length);
            var numChanges = 0;
            // Iterate the array, checking which values have changed and marking that in the bit vector
            for (var i = 0; i < minLength; ++i)
            {
                var val = value[i];
                var prevVal = previousValue[i];
                if (!NetworkVariableSerialization<T>.AreEqual(ref val, ref prevVal))
                {
                    ++numChanges;
                    changes.Set(i);
                }
            }

            // Mark any newly added items as well
            // We don't need to mark removed items because they are captured by serializing the length
            for (var i = previousValue.Length; i < value.Length; ++i)
            {
                ++numChanges;
                changes.Set(i);
            }

            // If the size of serializing the dela is greater than the size of serializing the whole array (i.e.,
            // because almost the entire array has changed and the overhead of the change set increases bandwidth),
            // then we just do a normal full serialization instead of a delta.
            if (changes.GetSerializedSize() + FastBufferWriter.GetWriteSize<T>() * numChanges > FastBufferWriter.GetWriteSize<T>() * value.Length)
            {
                // 1 = full serialization
                writer.WriteByteSafe(1);
                writer.WriteValueSafe(value);
                return;
            }
            // 0 = delta serialization
            writer.WriteByte(0);
            // Write the length, which will be used on the read side to resize the array
            BytePacker.WriteValuePacked(writer, value.Length);
            writer.WriteValueSafe(changes);
            unsafe
            {
                var ptr = (T*)value.GetUnsafePtr();
                var prevPtr = (T*)previousValue.GetUnsafePtr();
                for (int i = 0; i < value.Length; ++i)
                {
                    if (changes.IsSet(i))
                    {
                        if (i < previousValue.Length)
                        {
                            // If we have an item in the previous array for this index, we can do nested deltas!
                            NetworkVariableSerialization<T>.WriteDelta(writer, ref ptr[i], ref prevPtr[i]);
                        }
                        else
                        {
                            // If not, just write it normally
                            NetworkVariableSerialization<T>.Write(writer, ref ptr[i]);
                        }
                    }
                }
            }
        }
        public static void ReadNativeArrayDelta<T>(FastBufferReader reader, ref NativeArray<T> value) where T : unmanaged
        {
            // 1 = full serialization, 0 = delta serialization
            reader.ReadByteSafe(out byte full);
            if (full == 1)
            {
                // If we're doing full serialization, we fall back on reading the whole array.
                value.Dispose();
                reader.ReadValueSafe(out value, Allocator.Persistent);
                return;
            }
            // If not, first read the length and the change bits
            ByteUnpacker.ReadValuePacked(reader, out int length);
            var changes = new ResizableBitVector(Allocator.Temp);
            using var toDispose = changes;
            {
                reader.ReadNetworkSerializableInPlace(ref changes);

                // If the length has changed, we need to resize.
                // NativeArray is not resizeable, so we have to dispose and allocate a new one.
                var previousLength = value.Length;
                if (length != value.Length)
                {
                    var newArray = new NativeArray<T>(length, Allocator.Persistent);
                    unsafe
                    {
                        UnsafeUtility.MemCpy(newArray.GetUnsafePtr(), value.GetUnsafePtr(), math.min(newArray.Length * sizeof(T), value.Length * sizeof(T)));
                    }
                    value.Dispose();
                    value = newArray;
                }

                unsafe
                {
                    var ptr = (T*)value.GetUnsafePtr();
                    for (var i = 0; i < value.Length; ++i)
                    {
                        if (changes.IsSet(i))
                        {
                            if (i < previousLength)
                            {
                                // If we have an item to read a delta into, read it as a delta
                                NetworkVariableSerialization<T>.ReadDelta(reader, ref ptr[i]);
                            }
                            else
                            {
                                // If not, read as a standard element
                                NetworkVariableSerialization<T>.Read(reader, ref ptr[i]);
                            }
                        }
                    }
                }
            }
        }
        public static void WriteListDelta<T>(FastBufferWriter writer, ref List<T> value, ref List<T> previousValue)
        {
            // Lists can be null, so we have to handle that case.
            // We do that by marking this as a full serialization and using the existing null handling logic
            // in NetworkVariableSerialization<List<T>>
            if (value == null || previousValue == null)
            {
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<List<T>>.Write(writer, ref value);
                return;
            }
            // This bit vector serializes the list of which fields have changed using 1 bit per field.
            // This will always be 1 bit per field of the whole array (rounded up to the nearest 8 bits)
            // even if there is only one change, so as compared to serializing the index with each item,
            // this will use more bandwidth when the overall bandwidth usage is small and the array is large,
            // but less when the overall bandwidth usage is large. So it optimizes for the worst case while accepting
            // some reduction in efficiency in the best case.
            using var changes = new ResizableBitVector(Allocator.Temp);
            int minLength = math.min(value.Count, previousValue.Count);
            var numChanges = 0;
            // Iterate the list, checking which values have changed and marking that in the bit vector
            for (var i = 0; i < minLength; ++i)
            {
                var val = value[i];
                var prevVal = previousValue[i];
                if (!NetworkVariableSerialization<T>.AreEqual(ref val, ref prevVal))
                {
                    ++numChanges;
                    changes.Set(i);
                }
            }

            // Mark any newly added items as well
            // We don't need to mark removed items because they are captured by serializing the length
            for (var i = previousValue.Count; i < value.Count; ++i)
            {
                ++numChanges;
                changes.Set(i);
            }

            // If the size of serializing the dela is greater than the size of serializing the whole array (i.e.,
            // because almost the entire array has changed and the overhead of the change set increases bandwidth),
            // then we just do a normal full serialization instead of a delta.
            // In the case of List<T>, it's difficult to know exactly what the serialized size is going to be before
            // we serialize it, so we fudge it.
            if (numChanges >= value.Count * 0.9)
            {
                // 1 = full serialization
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<List<T>>.Write(writer, ref value);
                return;
            }

            // 0 = delta serialization
            writer.WriteByteSafe(0);
            // Write the length, which will be used on the read side to resize the list
            BytePacker.WriteValuePacked(writer, value.Count);
            writer.WriteValueSafe(changes);
            for (int i = 0; i < value.Count; ++i)
            {
                if (changes.IsSet(i))
                {
                    var reffable = value[i];
                    if (i < previousValue.Count)
                    {
                        // If we have an item in the previous array for this index, we can do nested deltas!
                        var prevReffable = previousValue[i];
                        NetworkVariableSerialization<T>.WriteDelta(writer, ref reffable, ref prevReffable);
                    }
                    else
                    {
                        // If not, just write it normally.
                        NetworkVariableSerialization<T>.Write(writer, ref reffable);
                    }
                }
            }
        }
        public static void ReadListDelta<T>(FastBufferReader reader, ref List<T> value)
        {
            // 1 = full serialization, 0 = delta serialization
            reader.ReadByteSafe(out byte full);
            if (full == 1)
            {
                // If we're doing full serialization, we fall back on reading the whole list.
                NetworkVariableSerialization<List<T>>.Read(reader, ref value);
                return;
            }
            // If not, first read the length and the change bits
            ByteUnpacker.ReadValuePacked(reader, out int length);
            var changes = new ResizableBitVector(Allocator.Temp);
            using var toDispose = changes;
            {
                reader.ReadNetworkSerializableInPlace(ref changes);

                // If the list shrank, we need to resize it down.
                // List<T> has no method to reserve space for future elements,
                // so if we have to grow it, we just do that using Add() below.
                if (length < value.Count)
                {
                    value.RemoveRange(length, value.Count - length);
                }

                for (var i = 0; i < length; ++i)
                {
                    if (changes.IsSet(i))
                    {
                        if (i < value.Count)
                        {
                            // If we have an item to read a delta into, read it as a delta
                            T item = value[i];
                            NetworkVariableSerialization<T>.ReadDelta(reader, ref item);
                            value[i] = item;
                        }
                        else
                        {
                            // If not, just read it as a standard item.
                            T item = default;
                            NetworkVariableSerialization<T>.Read(reader, ref item);
                            value.Add(item);
                        }
                    }
                }
            }
        }

        // For HashSet and Dictionary, we need to have some local space to hold lists we need to serialize.
        // We don't want to do allocations all the time and we know each one needs a maximum of three lists,
        // so we're going to keep static lists that we can reuse in these methods.
        private static class ListCache<T>
        {
            private static List<T> s_AddedList = new List<T>();
            private static List<T> s_RemovedList = new List<T>();
            private static List<T> s_ChangedList = new List<T>();

            public static List<T> GetAddedList()
            {
                s_AddedList.Clear();
                return s_AddedList;
            }
            public static List<T> GetRemovedList()
            {
                s_RemovedList.Clear();
                return s_RemovedList;
            }
            public static List<T> GetChangedList()
            {
                s_ChangedList.Clear();
                return s_ChangedList;
            }
        }

        public static void WriteHashSetDelta<T>(FastBufferWriter writer, ref HashSet<T> value, ref HashSet<T> previousValue) where T : IEquatable<T>
        {
            // HashSets can be null, so we have to handle that case.
            // We do that by marking this as a full serialization and using the existing null handling logic
            // in NetworkVariableSerialization<HashSet<T>>
            if (value == null || previousValue == null)
            {
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<HashSet<T>>.Write(writer, ref value);
                return;
            }
            // No changed array because a set can't have a "changed" element, only added and removed.
            var added = ListCache<T>.GetAddedList();
            var removed = ListCache<T>.GetRemovedList();
            // collect the new elements
            foreach (var item in value)
            {
                if (!previousValue.Contains(item))
                {
                    added.Add(item);
                }
            }

            // collect the removed elements
            foreach (var item in previousValue)
            {
                if (!value.Contains(item))
                {
                    removed.Add(item);
                }
            }

            // If we've got more changes than total items, we just do a full serialization
            if (added.Count + removed.Count >= value.Count)
            {
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<HashSet<T>>.Write(writer, ref value);
                return;
            }

            writer.WriteByteSafe(0);
            // Write out the added and removed arrays.
            writer.WriteValueSafe(added.Count);
            for (var i = 0; i < added.Count; ++i)
            {
                var item = added[i];
                NetworkVariableSerialization<T>.Write(writer, ref item);
            }
            writer.WriteValueSafe(removed.Count);
            for (var i = 0; i < removed.Count; ++i)
            {
                var item = removed[i];
                NetworkVariableSerialization<T>.Write(writer, ref item);
            }
        }

        public static void ReadHashSetDelta<T>(FastBufferReader reader, ref HashSet<T> value) where T : IEquatable<T>
        {
            // 1 = full serialization, 0 = delta serialization
            reader.ReadByteSafe(out byte full);
            if (full != 0)
            {
                NetworkVariableSerialization<HashSet<T>>.Read(reader, ref value);
                return;
            }
            // Read in the added and removed values
            reader.ReadValueSafe(out int addedCount);
            for (var i = 0; i < addedCount; ++i)
            {
                T item = default;
                NetworkVariableSerialization<T>.Read(reader, ref item);
                value.Add(item);
            }
            reader.ReadValueSafe(out int removedCount);
            for (var i = 0; i < removedCount; ++i)
            {
                T item = default;
                NetworkVariableSerialization<T>.Read(reader, ref item);
                value.Remove(item);
            }
        }
        public static void WriteDictionaryDelta<TKey, TVal>(FastBufferWriter writer, ref Dictionary<TKey, TVal> value, ref Dictionary<TKey, TVal> previousValue)
            where TKey : IEquatable<TKey>
        {
            if (value == null || previousValue == null)
            {
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<Dictionary<TKey, TVal>>.Write(writer, ref value);
                return;
            }
            var added = ListCache<KeyValuePair<TKey, TVal>>.GetAddedList();
            var changed = ListCache<KeyValuePair<TKey, TVal>>.GetRemovedList();
            var removed = ListCache<KeyValuePair<TKey, TVal>>.GetChangedList();
            // Collect items that have been added or have changed
            foreach (var item in value)
            {
                var val = item.Value;
                var hasPrevVal = previousValue.TryGetValue(item.Key, out var prevVal);
                if (!hasPrevVal)
                {
                    added.Add(item);
                }
                else if (!NetworkVariableSerialization<TVal>.AreEqual(ref val, ref prevVal))
                {
                    changed.Add(item);
                }
            }

            // collect the items that have been removed
            foreach (var item in previousValue)
            {
                if (!value.ContainsKey(item.Key))
                {
                    removed.Add(item);
                }
            }

            // If there are more changes than total values, just do a full serialization
            if (added.Count + removed.Count + changed.Count >= value.Count)
            {
                writer.WriteByteSafe(1);
                NetworkVariableSerialization<Dictionary<TKey, TVal>>.Write(writer, ref value);
                return;
            }

            writer.WriteByteSafe(0);
            // Else, write out the added, removed, and changed arrays
            writer.WriteValueSafe(added.Count);
            for (var i = 0; i < added.Count; ++i)
            {
                (var key, var val) = (added[i].Key, added[i].Value);
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
                NetworkVariableSerialization<TVal>.Write(writer, ref val);
            }
            writer.WriteValueSafe(removed.Count);
            for (var i = 0; i < removed.Count; ++i)
            {
                var key = removed[i].Key;
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
            }
            writer.WriteValueSafe(changed.Count);
            for (var i = 0; i < changed.Count; ++i)
            {
                (var key, var val) = (changed[i].Key, changed[i].Value);
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
                NetworkVariableSerialization<TVal>.Write(writer, ref val);
            }
        }

        public static void ReadDictionaryDelta<TKey, TVal>(FastBufferReader reader, ref Dictionary<TKey, TVal> value)
            where TKey : IEquatable<TKey>
        {
            // 1 = full serialization, 0 = delta serialization
            reader.ReadByteSafe(out byte full);
            if (full != 0)
            {
                NetworkVariableSerialization<Dictionary<TKey, TVal>>.Read(reader, ref value);
                return;
            }
            // Added
            reader.ReadValueSafe(out int length);
            for (var i = 0; i < length; ++i)
            {
                (TKey key, TVal val) = (default, default);
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                NetworkVariableSerialization<TVal>.Read(reader, ref val);
                value.Add(key, val);
            }
            // Removed
            reader.ReadValueSafe(out length);
            for (var i = 0; i < length; ++i)
            {
                TKey key = default;
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                value.Remove(key);
            }
            // Changed
            reader.ReadValueSafe(out length);
            for (var i = 0; i < length; ++i)
            {
                (TKey key, TVal val) = (default, default);
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                NetworkVariableSerialization<TVal>.Read(reader, ref val);
                value[key] = val;
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public static void WriteNativeListDelta<T>(FastBufferWriter writer, ref NativeList<T> value, ref NativeList<T> previousValue) where T : unmanaged
        {
            // See WriteListDelta and WriteNativeArrayDelta to understand most of this. It's basically the same,
            // just adjusted for the NativeList API
            using var changes = new ResizableBitVector(Allocator.Temp);
            int minLength = math.min(value.Length, previousValue.Length);
            var numChanges = 0;
            for (var i = 0; i < minLength; ++i)
            {
                var val = value[i];
                var prevVal = previousValue[i];
                if (!NetworkVariableSerialization<T>.AreEqual(ref val, ref prevVal))
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

            if (changes.GetSerializedSize() + FastBufferWriter.GetWriteSize<T>() * numChanges > FastBufferWriter.GetWriteSize<T>() * value.Length)
            {
                writer.WriteByteSafe(1);
                writer.WriteValueSafe(value);
                return;
            }

            writer.WriteByte(0);
            BytePacker.WriteValuePacked(writer, value.Length);
            writer.WriteValueSafe(changes);
            unsafe
            {
#if UTP_TRANSPORT_2_0_ABOVE
                var ptr = value.GetUnsafePtr();
                var prevPtr = previousValue.GetUnsafePtr();
#else
                var ptr = (T*)value.GetUnsafePtr();
                var prevPtr = (T*)previousValue.GetUnsafePtr();
#endif
                for (int i = 0; i < value.Length; ++i)
                {
                    if (changes.IsSet(i))
                    {
                        if (i < previousValue.Length)
                        {
                            NetworkVariableSerialization<T>.WriteDelta(writer, ref ptr[i], ref prevPtr[i]);
                        }
                        else
                        {
                            NetworkVariableSerialization<T>.Write(writer, ref ptr[i]);
                        }
                    }
                }
            }
        }
        public static void ReadNativeListDelta<T>(FastBufferReader reader, ref NativeList<T> value) where T : unmanaged
        {
            // See ReadListDelta and ReadNativeArrayDelta to understand most of this. It's basically the same,
            // just adjusted for the NativeList API
            reader.ReadByteSafe(out byte full);
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

                var previousLength = value.Length;
                // The one big difference between this and NativeArray/List is that NativeList supports
                // easy and fast resizing and reserving space.
                if (length != value.Length)
                {
                    value.Resize(length, NativeArrayOptions.UninitializedMemory);
                }

                unsafe
                {
#if UTP_TRANSPORT_2_0_ABOVE
                    var ptr = value.GetUnsafePtr();
#else
                    var ptr = (T*)value.GetUnsafePtr();
#endif
                    for (var i = 0; i < value.Length; ++i)
                    {
                        if (changes.IsSet(i))
                        {
                            if (i < previousLength)
                            {
                                NetworkVariableSerialization<T>.ReadDelta(reader, ref ptr[i]);
                            }
                            else
                            {
                                NetworkVariableSerialization<T>.Read(reader, ref ptr[i]);
                            }
                        }
                    }
                }
            }
        }

        public static unsafe void WriteNativeHashSetDelta<T>(FastBufferWriter writer, ref NativeHashSet<T> value, ref NativeHashSet<T> previousValue) where T : unmanaged, IEquatable<T>
        {
            // See WriteHashSet; this is the same algorithm, adjusted for the NativeHashSet API
            var added = stackalloc T[value.Count];
            var removed = stackalloc T[previousValue.Count];
            var addedCount = 0;
            var removedCount = 0;
            foreach (var item in value)
            {
                if (!previousValue.Contains(item))
                {
                    added[addedCount] = item;
                    ++addedCount;
                }
            }

            foreach (var item in previousValue)
            {
                if (!value.Contains(item))
                {
                    removed[removedCount] = item;
                    ++removedCount;
                }
            }
#if UTP_TRANSPORT_2_0_ABOVE
            if (addedCount + removedCount >= value.Count)
#else
            if (addedCount + removedCount >= value.Count())
#endif
            {
                writer.WriteByteSafe(1);
                writer.WriteValueSafe(value);
                return;
            }

            writer.WriteByteSafe(0);
            writer.WriteValueSafe(addedCount);
            for (var i = 0; i < addedCount; ++i)
            {
                NetworkVariableSerialization<T>.Write(writer, ref added[i]);
            }
            writer.WriteValueSafe(removedCount);
            for (var i = 0; i < removedCount; ++i)
            {
                NetworkVariableSerialization<T>.Write(writer, ref removed[i]);
            }
        }

        public static void ReadNativeHashSetDelta<T>(FastBufferReader reader, ref NativeHashSet<T> value) where T : unmanaged, IEquatable<T>
        {
            // See ReadHashSet; this is the same algorithm, adjusted for the NativeHashSet API
            reader.ReadByteSafe(out byte full);
            if (full != 0)
            {
                reader.ReadValueSafeInPlace(ref value);
                return;
            }
            reader.ReadValueSafe(out int addedCount);
            for (var i = 0; i < addedCount; ++i)
            {
                T item = default;
                NetworkVariableSerialization<T>.Read(reader, ref item);
                value.Add(item);
            }
            reader.ReadValueSafe(out int removedCount);
            for (var i = 0; i < removedCount; ++i)
            {
                T item = default;
                NetworkVariableSerialization<T>.Read(reader, ref item);
                value.Remove(item);
            }
        }

        public static unsafe void WriteNativeHashMapDelta<TKey, TVal>(FastBufferWriter writer, ref NativeHashMap<TKey, TVal> value, ref NativeHashMap<TKey, TVal> previousValue)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            // See WriteDictionary; this is the same algorithm, adjusted for the NativeHashMap API
#if UTP_TRANSPORT_2_0_ABOVE
            var added = stackalloc KVPair<TKey, TVal>[value.Count];
            var changed = stackalloc KVPair<TKey, TVal>[value.Count];
            var removed = stackalloc KVPair<TKey, TVal>[previousValue.Count];
#else
            var added = stackalloc KeyValue<TKey, TVal>[value.Count()];
            var changed = stackalloc KeyValue<TKey, TVal>[value.Count()];
            var removed = stackalloc KeyValue<TKey, TVal>[previousValue.Count()];
#endif
            var addedCount = 0;
            var changedCount = 0;
            var removedCount = 0;
            foreach (var item in value)
            {

                var hasPrevVal = previousValue.TryGetValue(item.Key, out var prevVal);
                if (!hasPrevVal)
                {
                    added[addedCount] = item;
                    ++addedCount;
                }
                else if (!NetworkVariableSerialization<TVal>.AreEqual(ref item.Value, ref prevVal))
                {
                    changed[changedCount] = item;
                    ++changedCount;
                }
            }

            foreach (var item in previousValue)
            {
                if (!value.ContainsKey(item.Key))
                {
                    removed[removedCount] = item;
                    ++removedCount;
                }
            }

#if UTP_TRANSPORT_2_0_ABOVE
            if (addedCount + removedCount + changedCount >= value.Count)
#else
            if (addedCount + removedCount + changedCount >= value.Count())
#endif
            {
                writer.WriteByteSafe(1);
                writer.WriteValueSafe(value);
                return;
            }

            writer.WriteByteSafe(0);
            writer.WriteValueSafe(addedCount);
            for (var i = 0; i < addedCount; ++i)
            {
                (var key, var val) = (added[i].Key, added[i].Value);
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
                NetworkVariableSerialization<TVal>.Write(writer, ref val);
            }
            writer.WriteValueSafe(removedCount);
            for (var i = 0; i < removedCount; ++i)
            {
                var key = removed[i].Key;
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
            }
            writer.WriteValueSafe(changedCount);
            for (var i = 0; i < changedCount; ++i)
            {
                (var key, var val) = (changed[i].Key, changed[i].Value);
                NetworkVariableSerialization<TKey>.Write(writer, ref key);
                NetworkVariableSerialization<TVal>.Write(writer, ref val);
            }
        }

        public static void ReadNativeHashMapDelta<TKey, TVal>(FastBufferReader reader, ref NativeHashMap<TKey, TVal> value)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            // See ReadDictionary; this is the same algorithm, adjusted for the NativeHashMap API
            reader.ReadByteSafe(out byte full);
            if (full != 0)
            {
                reader.ReadValueSafeInPlace(ref value);
                return;
            }
            // Added
            reader.ReadValueSafe(out int length);
            for (var i = 0; i < length; ++i)
            {
                (TKey key, TVal val) = (default, default);
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                NetworkVariableSerialization<TVal>.Read(reader, ref val);
                value.Add(key, val);
            }
            // Removed
            reader.ReadValueSafe(out length);
            for (var i = 0; i < length; ++i)
            {
                TKey key = default;
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                value.Remove(key);
            }
            // Changed
            reader.ReadValueSafe(out length);
            for (var i = 0; i < length; ++i)
            {
                (TKey key, TVal val) = (default, default);
                NetworkVariableSerialization<TKey>.Read(reader, ref key);
                NetworkVariableSerialization<TVal>.Read(reader, ref val);
                value[key] = val;
            }
        }
#endif
    }
}
