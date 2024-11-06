using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// This class contains initialization functions for various different types used in NetworkVariables.
    /// Generally speaking, these methods are called by a module initializer created by codegen (NetworkBehaviourILPP)
    /// and do not need to be called manually.
    ///
    /// There are two types of initializers: Serializers and EqualityCheckers. Every type must have an EqualityChecker
    /// registered to it in order to be used in NetworkVariable; however, not all types need a Serializer. Types without
    /// a serializer registered will fall back to using the delegates in <see cref="UserNetworkVariableSerialization{T}"/>.
    /// If no such delegate has been registered, a type without a serializer will throw an exception on the first attempt
    /// to serialize or deserialize it. (Again, however, codegen handles this automatically and this registration doesn't
    /// typically need to be performed manually.)
    /// </summary>
    public static class NetworkVariableSerializationTypedInitializers
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        internal static void InitializeIntegerSerialization()
        {
            NetworkVariableSerialization<short>.Serializer = new ShortSerializer();
            NetworkVariableSerialization<short>.AreEqual = NetworkVariableEquality<short>.ValueEquals;
            NetworkVariableSerialization<ushort>.Serializer = new UshortSerializer();
            NetworkVariableSerialization<ushort>.AreEqual = NetworkVariableEquality<ushort>.ValueEquals;
            NetworkVariableSerialization<int>.Serializer = new IntSerializer();
            NetworkVariableSerialization<int>.AreEqual = NetworkVariableEquality<int>.ValueEquals;
            NetworkVariableSerialization<uint>.Serializer = new UintSerializer();
            NetworkVariableSerialization<uint>.AreEqual = NetworkVariableEquality<uint>.ValueEquals;
            NetworkVariableSerialization<long>.Serializer = new LongSerializer();
            NetworkVariableSerialization<long>.AreEqual = NetworkVariableEquality<long>.ValueEquals;
            NetworkVariableSerialization<ulong>.Serializer = new UlongSerializer();
            NetworkVariableSerialization<ulong>.AreEqual = NetworkVariableEquality<ulong>.ValueEquals;
        }

        /// <summary>
        /// Registeres an unmanaged type that will be serialized by a direct memcpy into a buffer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedByMemcpy<T>() where T : unmanaged
        {
            NetworkVariableSerialization<T>.Serializer = new UnmanagedTypeSerializer<T>();
        }

        /// <summary>
        /// Registeres an unmanaged type that will be serialized by a direct memcpy into a buffer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedByMemcpyArray<T>() where T : unmanaged
        {
            NetworkVariableSerialization<NativeArray<T>>.Serializer = new UnmanagedArraySerializer<T>();
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Registeres an unmanaged type that will be serialized by a direct memcpy into a buffer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedByMemcpyList<T>() where T : unmanaged
        {
            NetworkVariableSerialization<NativeList<T>>.Serializer = new UnmanagedListSerializer<T>();
        }

        /// <summary>
        /// Registeres a native hash set (this generic implementation works with all types)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_NativeHashSet<T>() where T : unmanaged, IEquatable<T>
        {
            NetworkVariableSerialization<NativeHashSet<T>>.Serializer = new NativeHashSetSerializer<T>();
        }

        /// <summary>
        /// Registeres a native hash set (this generic implementation works with all types)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_NativeHashMap<TKey, TVal>()
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            NetworkVariableSerialization<NativeHashMap<TKey, TVal>>.Serializer = new NativeHashMapSerializer<TKey, TVal>();
        }
#endif

        /// <summary>
        /// Registeres a native hash set (this generic implementation works with all types)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_List<T>()
        {
            NetworkVariableSerialization<List<T>>.Serializer = new ListSerializer<T>();
        }

        /// <summary>
        /// Registeres a native hash set (this generic implementation works with all types)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_HashSet<T>() where T : IEquatable<T>
        {
            NetworkVariableSerialization<HashSet<T>>.Serializer = new HashSetSerializer<T>();
        }

        /// <summary>
        /// Registeres a native hash set (this generic implementation works with all types)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_Dictionary<TKey, TVal>() where TKey : IEquatable<TKey>
        {
            NetworkVariableSerialization<Dictionary<TKey, TVal>>.Serializer = new DictionarySerializer<TKey, TVal>();
        }

        /// <summary>
        /// Registers an unmanaged type that implements INetworkSerializable and will be serialized through a call to
        /// NetworkSerialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedINetworkSerializable<T>() where T : unmanaged, INetworkSerializable
        {
            NetworkVariableSerialization<T>.Serializer = new UnmanagedNetworkSerializableSerializer<T>();
        }

        /// <summary>
        /// Registers an unmanaged type that implements INetworkSerializable and will be serialized through a call to
        /// NetworkSerialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedINetworkSerializableArray<T>() where T : unmanaged, INetworkSerializable
        {
            NetworkVariableSerialization<NativeArray<T>>.Serializer = new UnmanagedNetworkSerializableArraySerializer<T>();
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Registers an unmanaged type that implements INetworkSerializable and will be serialized through a call to
        /// NetworkSerialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_UnmanagedINetworkSerializableList<T>() where T : unmanaged, INetworkSerializable
        {
            NetworkVariableSerialization<NativeList<T>>.Serializer = new UnmanagedNetworkSerializableListSerializer<T>();
        }
#endif

        /// <summary>
        /// Registers a managed type that implements INetworkSerializable and will be serialized through a call to
        /// NetworkSerialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_ManagedINetworkSerializable<T>() where T : class, INetworkSerializable, new()
        {
            NetworkVariableSerialization<T>.Serializer = new ManagedNetworkSerializableSerializer<T>();
        }

        /// <summary>
        /// Registers a FixedString type that will be serialized through FastBufferReader/FastBufferWriter's FixedString
        /// serializers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_FixedString<T>() where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            NetworkVariableSerialization<T>.Serializer = new FixedStringSerializer<T>();
        }

        /// <summary>
        /// Registers a FixedString type that will be serialized through FastBufferReader/FastBufferWriter's FixedString
        /// serializers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_FixedStringArray<T>() where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            NetworkVariableSerialization<NativeArray<T>>.Serializer = new FixedStringArraySerializer<T>();
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Registers a FixedString type that will be serialized through FastBufferReader/FastBufferWriter's FixedString
        /// serializers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeSerializer_FixedStringList<T>() where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            NetworkVariableSerialization<NativeList<T>>.Serializer = new FixedStringListSerializer<T>();
        }
#endif

        /// <summary>
        /// Registers a managed type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_ManagedIEquatable<T>() where T : class, IEquatable<T>
        {
            NetworkVariableSerialization<T>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsObject;
        }

        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedIEquatable<T>() where T : unmanaged, IEquatable<T>
        {
            NetworkVariableSerialization<T>.AreEqual = NetworkVariableEquality<T>.EqualityEquals;
        }

        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedIEquatableArray<T>() where T : unmanaged, IEquatable<T>
        {
            NetworkVariableSerialization<NativeArray<T>>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsArray;
        }
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_List<T>()
        {
            NetworkVariableSerialization<List<T>>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsList;
        }
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_HashSet<T>() where T : IEquatable<T>
        {
            NetworkVariableSerialization<HashSet<T>>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsHashSet;
        }
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_Dictionary<TKey, TVal>()
            where TKey : IEquatable<TKey>
        {
            NetworkVariableSerialization<Dictionary<TKey, TVal>>.AreEqual = NetworkVariableDictionarySerialization<TKey, TVal>.GenericEqualsDictionary;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedIEquatableList<T>() where T : unmanaged, IEquatable<T>
        {
            NetworkVariableSerialization<NativeList<T>>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsNativeList;
        }
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_NativeHashSet<T>() where T : unmanaged, IEquatable<T>
        {
            NetworkVariableSerialization<NativeHashSet<T>>.AreEqual = NetworkVariableEquality<T>.EqualityEqualsNativeHashSet;
        }
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using T.Equals()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_NativeHashMap<TKey, TVal>()
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            NetworkVariableSerialization<NativeHashMap<TKey, TVal>>.AreEqual = NetworkVariableMapSerialization<TKey, TVal>.GenericEqualsNativeHashMap;
        }
#endif

        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using memcmp and only considered
        /// equal if they are bitwise equivalent in memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedValueEquals<T>() where T : unmanaged
        {
            NetworkVariableSerialization<T>.AreEqual = NetworkVariableEquality<T>.ValueEquals;
        }

        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using memcmp and only considered
        /// equal if they are bitwise equivalent in memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedValueEqualsArray<T>() where T : unmanaged
        {
            NetworkVariableSerialization<NativeArray<T>>.AreEqual = NetworkVariableEquality<T>.ValueEqualsArray;
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        /// <summary>
        /// Registers an unmanaged type that will be checked for equality using memcmp and only considered
        /// equal if they are bitwise equivalent in memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_UnmanagedValueEqualsList<T>() where T : unmanaged
        {
            NetworkVariableSerialization<NativeList<T>>.AreEqual = NetworkVariableEquality<T>.ValueEqualsList;
        }
#endif

        /// <summary>
        /// Registers a managed type that will be checked for equality using the == operator
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void InitializeEqualityChecker_ManagedClassEquals<T>() where T : class
        {
            NetworkVariableSerialization<T>.AreEqual = NetworkVariableEquality<T>.ClassEquals;
        }
    }
}
