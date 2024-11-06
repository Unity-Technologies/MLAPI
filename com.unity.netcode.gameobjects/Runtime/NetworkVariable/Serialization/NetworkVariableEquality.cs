using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;

namespace Unity.Netcode
{
    internal static class NetworkVariableEquality<T>
    {
        // Compares two values of the same unmanaged type by underlying memory
        // Ignoring any overridden value checks
        // Size is fixed
        internal static unsafe bool ValueEquals<TValueType>(ref TValueType a, ref TValueType b) where TValueType : unmanaged
        {
            // get unmanaged pointers
            var aptr = UnsafeUtility.AddressOf(ref a);
            var bptr = UnsafeUtility.AddressOf(ref b);

            // compare addresses
            return UnsafeUtility.MemCmp(aptr, bptr, sizeof(TValueType)) == 0;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        // Compares two values of the same unmanaged type by underlying memory
        // Ignoring any overridden value checks
        // Size is fixed
        internal static unsafe bool ValueEqualsList<TValueType>(ref NativeList<TValueType> a, ref NativeList<TValueType> b) where TValueType : unmanaged
        {
            if (a.IsCreated != b.IsCreated)
            {
                return false;
            }

            if (!a.IsCreated)
            {
                return true;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

#if UTP_TRANSPORT_2_0_ABOVE
            var aptr = a.GetUnsafePtr();
            var bptr = b.GetUnsafePtr();
#else
            var aptr = (TValueType*)a.GetUnsafePtr();
            var bptr = (TValueType*)b.GetUnsafePtr();
#endif

            return UnsafeUtility.MemCmp(aptr, bptr, sizeof(TValueType) * a.Length) == 0;
        }
#endif

        // Compares two values of the same unmanaged type by underlying memory
        // Ignoring any overridden value checks
        // Size is fixed
        internal static unsafe bool ValueEqualsArray<TValueType>(ref NativeArray<TValueType> a, ref NativeArray<TValueType> b) where TValueType : unmanaged
        {
            if (a.IsCreated != b.IsCreated)
            {
                return false;
            }

            if (!a.IsCreated)
            {
                return true;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            var aptr = (TValueType*)a.GetUnsafePtr();
            var bptr = (TValueType*)b.GetUnsafePtr();
            return UnsafeUtility.MemCmp(aptr, bptr, sizeof(TValueType) * a.Length) == 0;
        }

        internal static bool EqualityEqualsObject<TValueType>(ref TValueType a, ref TValueType b) where TValueType : class, IEquatable<TValueType>
        {
            if (a == null)
            {
                return b == null;
            }

            if (b == null)
            {
                return false;
            }

            return a.Equals(b);
        }

        internal static bool EqualityEquals<TValueType>(ref TValueType a, ref TValueType b) where TValueType : unmanaged, IEquatable<TValueType>
        {
            return a.Equals(b);
        }

        internal static bool EqualityEqualsList<TValueType>(ref List<TValueType> a, ref List<TValueType> b)
        {
            if (a == null != (b == null))
            {
                return false;
            }

            if (a == null)
            {
                return true;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; ++i)
            {
                var aItem = a[i];
                var bItem = b[i];
                if (!NetworkVariableSerialization<TValueType>.AreEqual(ref aItem, ref bItem))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool EqualityEqualsHashSet<TValueType>(ref HashSet<TValueType> a, ref HashSet<TValueType> b) where TValueType : IEquatable<TValueType>
        {
            if (a == null != (b == null))
            {
                return false;
            }

            if (a == null)
            {
                return true;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var item in a)
            {
                if (!b.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }

        // Compares two values of the same unmanaged type by underlying memory
        // Ignoring any overridden value checks
        // Size is fixed
        internal static unsafe bool EqualityEqualsArray<TValueType>(ref NativeArray<TValueType> a, ref NativeArray<TValueType> b) where TValueType : unmanaged, IEquatable<TValueType>
        {
            if (a.IsCreated != b.IsCreated)
            {
                return false;
            }

            if (!a.IsCreated)
            {
                return true;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            var aptr = (TValueType*)a.GetUnsafePtr();
            var bptr = (TValueType*)b.GetUnsafePtr();
            for (var i = 0; i < a.Length; ++i)
            {
                if (!EqualityEquals(ref aptr[i], ref bptr[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool ClassEquals<TValueType>(ref TValueType a, ref TValueType b) where TValueType : class
        {
            return a == b;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        // Compares two values of the same unmanaged type by underlying memory
        // Ignoring any overridden value checks
        // Size is fixed
        internal static unsafe bool EqualityEqualsNativeList<TValueType>(ref NativeList<TValueType> a, ref NativeList<TValueType> b) where TValueType : unmanaged, IEquatable<TValueType>
        {
            if (a.IsCreated != b.IsCreated)
            {
                return false;
            }

            if (!a.IsCreated)
            {
                return true;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

#if UTP_TRANSPORT_2_0_ABOVE
            var aptr = a.GetUnsafePtr();
            var bptr = b.GetUnsafePtr();
#else
            var aptr = (TValueType*)a.GetUnsafePtr();
            var bptr = (TValueType*)b.GetUnsafePtr();
#endif
            for (var i = 0; i < a.Length; ++i)
            {
                if (!EqualityEquals(ref aptr[i], ref bptr[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool EqualityEqualsNativeHashSet<TValueType>(ref NativeHashSet<TValueType> a, ref NativeHashSet<TValueType> b) where TValueType : unmanaged, IEquatable<TValueType>
        {
            if (a.IsCreated != b.IsCreated)
            {
                return false;
            }

            if (!a.IsCreated)
            {
                return true;
            }

#if UTP_TRANSPORT_2_0_ABOVE
            if (a.Count != b.Count)
#else
            if (a.Count() != b.Count())
#endif
            {
                return false;
            }

            foreach (var item in a)
            {
                if (!b.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}

/// <summary>
///     Support methods for equality of NetworkVariable collection types.
///     Because there are multiple overloads of WriteValue/ReadValue based on different generic constraints,
///     but there's no way to achieve the same thing with a class, this sets up various read/write schemes
///     based on which constraints are met by `T` using reflection, which is done at module load time.
/// </summary>
/// <typeparam name="TKey">The type the associated NetworkVariable dictionary collection key templated on</typeparam>
/// <typeparam name="TVal">The type the associated NetworkVariable dictionary collection value templated on</typeparam>
internal class NetworkVariableDictionarySerialization<TKey, TVal>
    where TKey : IEquatable<TKey>
{
    internal static bool GenericEqualsDictionary(ref Dictionary<TKey, TVal> a, ref Dictionary<TKey, TVal> b)
    {
        if (a == null != (b == null))
        {
            return false;
        }

        if (a == null)
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var item in a)
        {
            var hasKey = b.TryGetValue(item.Key, out var val);
            if (!hasKey)
            {
                return false;
            }

            var bVal = item.Value;
            if (!NetworkVariableSerialization<TVal>.AreEqual(ref bVal, ref val))
            {
                return false;
            }
        }

        return true;
    }
}

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
internal class NetworkVariableMapSerialization<TKey, TVal>
    where TKey : unmanaged, IEquatable<TKey>
    where TVal : unmanaged
{
    internal static bool GenericEqualsNativeHashMap(ref NativeHashMap<TKey, TVal> a, ref NativeHashMap<TKey, TVal> b)
    {
        if (a.IsCreated != b.IsCreated)
        {
            return false;
        }

        if (!a.IsCreated)
        {
            return true;
        }

#if UTP_TRANSPORT_2_0_ABOVE
        if (a.Count != b.Count)
#else
            if (a.Count() != b.Count())
#endif
        {
            return false;
        }

        foreach (var item in a)
        {
            var hasKey = b.TryGetValue(item.Key, out var val);
            if (!hasKey || !NetworkVariableSerialization<TVal>.AreEqual(ref item.Value, ref val))
            {
                return false;
            }
        }

        return true;
    }
}
#endif
