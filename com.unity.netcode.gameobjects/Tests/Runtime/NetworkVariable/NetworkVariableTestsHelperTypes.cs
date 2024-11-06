using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetVarPermTestComp : NetworkBehaviour
    {
        public NetworkVariable<Vector3> OwnerWritable_Position = new NetworkVariable<Vector3>(Vector3.one, NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Vector3> ServerWritable_Position = new NetworkVariable<Vector3>(Vector3.one, NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Server);
        public NetworkVariable<Vector3> OwnerReadWrite_Position = new NetworkVariable<Vector3>(Vector3.one, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Owner);
    }

    internal class NetworkVariableMiddleclass<TMiddleclassName> : NetworkVariable<TMiddleclassName>
    {

    }

    internal class NetworkVariableSubclass<TSubclassName> : NetworkVariableMiddleclass<TSubclassName>
    {

    }

    internal class NetworkBehaviourWithNetVarArray : NetworkBehaviour
    {
        public NetworkVariable<int> Int0 = new NetworkVariable<int>();
        public NetworkVariable<int> Int1 = new NetworkVariable<int>();
        public NetworkVariable<int> Int2 = new NetworkVariable<int>();
        public NetworkVariable<int> Int3 = new NetworkVariable<int>();
        public NetworkVariable<int> Int4 = new NetworkVariable<int>();
        public NetworkVariable<int>[] AllInts = new NetworkVariable<int>[5];

        public int InitializedFieldCount => NetworkVariableFields.Count;


        private void Awake()
        {
            AllInts[0] = Int0;
            AllInts[1] = Int1;
            AllInts[2] = Int2;
            AllInts[3] = Int3;
            AllInts[4] = Int4;
        }
    }

    internal struct TypeReferencedOnlyInCustomSerialization1 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    internal struct TypeReferencedOnlyInCustomSerialization2 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    internal struct TypeReferencedOnlyInCustomSerialization3 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    internal struct TypeReferencedOnlyInCustomSerialization4 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    internal struct TypeReferencedOnlyInCustomSerialization5 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    internal struct TypeReferencedOnlyInCustomSerialization6 : INetworkSerializeByMemcpy
    {
        public int I;
    }

    // Both T and U are serializable
    [GenerateSerializationForGenericParameter(0)]
    [GenerateSerializationForGenericParameter(1)]
    internal class CustomSerializableClass<TSerializableType1, TSerializableType2>
    {

    }

    // Only U is serializable
    [GenerateSerializationForGenericParameter(1)]
    internal class CustomSerializableBaseClass<TUnserializableType, TSerializableType>
    {

    }

    // T is serializable, passes TypeReferencedOnlyInCustomSerialization3 as U to the subclass, making it serializable
    [GenerateSerializationForGenericParameter(0)]
    internal class CustomSerializableSubclass<TSerializableType> : CustomSerializableBaseClass<TSerializableType, TypeReferencedOnlyInCustomSerialization3>
    {

    }

    // T is serializable, passes TypeReferencedOnlyInCustomSerialization3 as U to the subclass, making it serializable
    [GenerateSerializationForGenericParameter(0)]
    internal class CustomSerializableSubclassWithNativeArray<TSerializableType> : CustomSerializableBaseClass<TSerializableType, NativeArray<TypeReferencedOnlyInCustomSerialization3>>
    {

    }

    internal class CustomGenericSerializationTestBehaviour : NetworkBehaviour
    {
        public CustomSerializableClass<TypeReferencedOnlyInCustomSerialization1, TypeReferencedOnlyInCustomSerialization2> Value1;
        public CustomSerializableClass<NativeArray<TypeReferencedOnlyInCustomSerialization1>, NativeArray<TypeReferencedOnlyInCustomSerialization2>> Value2;
        public CustomSerializableSubclass<TypeReferencedOnlyInCustomSerialization4> Value3;
        public CustomSerializableSubclassWithNativeArray<NativeArray<TypeReferencedOnlyInCustomSerialization4>> Value4;
    }

    [GenerateSerializationForType(typeof(TypeReferencedOnlyInCustomSerialization5))]
    [GenerateSerializationForType(typeof(NativeArray<TypeReferencedOnlyInCustomSerialization5>))]
    internal struct SomeRandomStruct
    {
        [GenerateSerializationForType(typeof(TypeReferencedOnlyInCustomSerialization6))]
        [GenerateSerializationForType(typeof(NativeArray<TypeReferencedOnlyInCustomSerialization6>))]
        public void Foo()
        {

        }
    }

    internal struct TemplatedValueOnlyReferencedByNetworkVariableSubclass<T> : INetworkSerializeByMemcpy
        where T : unmanaged
    {
        public T Value;
    }

    public enum ByteEnum : byte
    {
        A,
        B,
        C = byte.MaxValue
    }
    public enum SByteEnum : sbyte
    {
        A,
        B,
        C = sbyte.MaxValue
    }
    public enum ShortEnum : short
    {
        A,
        B,
        C = short.MaxValue
    }
    public enum UShortEnum : ushort
    {
        A,
        B,
        C = ushort.MaxValue
    }
    public enum IntEnum : int
    {
        A,
        B,
        C = int.MaxValue
    }
    public enum UIntEnum : uint
    {
        A,
        B,
        C = uint.MaxValue
    }
    public enum LongEnum : long
    {
        A,
        B,
        C = long.MaxValue
    }
    public enum ULongEnum : ulong
    {
        A,
        B,
        C = ulong.MaxValue
    }

    internal struct HashableNetworkVariableTestStruct : INetworkSerializeByMemcpy, IEquatable<HashableNetworkVariableTestStruct>
    {
        public byte A;
        public short B;
        public ushort C;
        public int D;
        public uint E;
        public long F;
        public ulong G;
        public bool H;
        public char I;
        public float J;
        public double K;

        public bool Equals(HashableNetworkVariableTestStruct other)
        {
            return A == other.A && B == other.B && C == other.C && D == other.D && E == other.E && F == other.F && G == other.G && H == other.H && I == other.I && J.Equals(other.J) && K.Equals(other.K);
        }

        public override bool Equals(object obj)
        {
            return obj is HashableNetworkVariableTestStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(A);
            hashCode.Add(B);
            hashCode.Add(C);
            hashCode.Add(D);
            hashCode.Add(E);
            hashCode.Add(F);
            hashCode.Add(G);
            hashCode.Add(H);
            hashCode.Add(I);
            hashCode.Add(J);
            hashCode.Add(K);
            return hashCode.ToHashCode();
        }
    }

    internal struct HashMapKeyStruct : INetworkSerializeByMemcpy, IEquatable<HashMapKeyStruct>
    {
        public byte A;
        public short B;
        public ushort C;
        public int D;
        public uint E;
        public long F;
        public ulong G;
        public bool H;
        public char I;
        public float J;
        public double K;

        public bool Equals(HashMapKeyStruct other)
        {
            return A == other.A && B == other.B && C == other.C && D == other.D && E == other.E && F == other.F && G == other.G && H == other.H && I == other.I && J.Equals(other.J) && K.Equals(other.K);
        }

        public override bool Equals(object obj)
        {
            return obj is HashMapKeyStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(A);
            hashCode.Add(B);
            hashCode.Add(C);
            hashCode.Add(D);
            hashCode.Add(E);
            hashCode.Add(F);
            hashCode.Add(G);
            hashCode.Add(H);
            hashCode.Add(I);
            hashCode.Add(J);
            hashCode.Add(K);
            return hashCode.ToHashCode();
        }
    }

    internal struct HashMapValStruct : INetworkSerializeByMemcpy, IEquatable<HashMapValStruct>
    {
        public byte A;
        public short B;
        public ushort C;
        public int D;
        public uint E;
        public long F;
        public ulong G;
        public bool H;
        public char I;
        public float J;
        public double K;

        public bool Equals(HashMapValStruct other)
        {
            return A == other.A && B == other.B && C == other.C && D == other.D && E == other.E && F == other.F && G == other.G && H == other.H && I == other.I && J.Equals(other.J) && K.Equals(other.K);
        }

        public override bool Equals(object obj)
        {
            return obj is HashMapValStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(A);
            hashCode.Add(B);
            hashCode.Add(C);
            hashCode.Add(D);
            hashCode.Add(E);
            hashCode.Add(F);
            hashCode.Add(G);
            hashCode.Add(H);
            hashCode.Add(I);
            hashCode.Add(J);
            hashCode.Add(K);
            return hashCode.ToHashCode();
        }
    }

    internal struct NetworkVariableTestStruct : INetworkSerializeByMemcpy
    {
        public byte A;
        public short B;
        public ushort C;
        public int D;
        public uint E;
        public long F;
        public ulong G;
        public bool H;
        public char I;
        public float J;
        public double K;

        private static System.Random s_Random = new System.Random();

        public static NetworkVariableTestStruct GetTestStruct()
        {
            var testStruct = new NetworkVariableTestStruct
            {
                A = (byte)s_Random.Next(),
                B = (short)s_Random.Next(),
                C = (ushort)s_Random.Next(),
                D = s_Random.Next(),
                E = (uint)s_Random.Next(),
                F = ((long)s_Random.Next() << 32) + s_Random.Next(),
                G = ((ulong)s_Random.Next() << 32) + (ulong)s_Random.Next(),
                H = true,
                I = '\u263a',
                J = (float)s_Random.NextDouble(),
                K = s_Random.NextDouble(),
            };

            return testStruct;
        }
    }


    internal class HashableNetworkVariableTestClass : INetworkSerializable, IEquatable<HashableNetworkVariableTestClass>
    {
        public HashableNetworkVariableTestStruct Data;

        public bool Equals(HashableNetworkVariableTestClass other)
        {
            return Data.Equals(other.Data);
        }

        public override bool Equals(object obj)
        {
            return obj is HashableNetworkVariableTestClass other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Data);
        }
    }

    internal class HashMapKeyClass : INetworkSerializable, IEquatable<HashMapKeyClass>
    {
        public HashMapKeyStruct Data;

        public bool Equals(HashMapKeyClass other)
        {
            return Data.Equals(other.Data);
        }

        public override bool Equals(object obj)
        {
            return obj is HashMapKeyClass other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Data);
        }
    }

    internal class HashMapValClass : INetworkSerializable, IEquatable<HashMapValClass>
    {
        public HashMapValStruct Data;

        public bool Equals(HashMapValClass other)
        {
            return Data.Equals(other.Data);
        }

        public override bool Equals(object obj)
        {
            return obj is HashMapValClass other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Data);
        }
    }

    internal class NetworkVariableTestClass : INetworkSerializable, IEquatable<NetworkVariableTestClass>
    {
        public NetworkVariableTestStruct Data;

        public bool Equals(NetworkVariableTestClass other)
        {
            return NetworkVariableSerialization<NetworkVariableTestStruct>.AreEqual(ref Data, ref other.Data);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkVariableTestClass other && Equals(other);
        }

        // This type is not used for hashing, we just need to implement IEquatable to verify lists match.
        public override int GetHashCode()
        {
            return 0;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Data);
        }
    }


    // The ILPP code for NetworkVariables to determine how to serialize them relies on them existing as fields of a NetworkBehaviour to find them.
    // Some of the tests below create NetworkVariables on the stack, so this class is here just to make sure the relevant types are all accounted for.
    internal class NetVarILPPClassForTests : NetworkBehaviour
    {
        public NetworkVariable<byte> ByteVar;
        public NetworkVariable<NativeArray<byte>> ByteArrayVar;
        public NetworkVariable<List<byte>> ByteManagedListVar;
        public NetworkVariable<HashSet<byte>> ByteManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<byte>> ByteListVar;
        public NetworkVariable<NativeHashSet<byte>> ByteHashSetVar;
        public NetworkVariable<NativeHashMap<byte, byte>> ByteByteHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, byte>> ULongByteHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, byte>> Vector2ByteHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, byte>> HashMapKeyStructByteHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, byte>> ByteByteDictionaryVar;
        public NetworkVariable<Dictionary<ulong, byte>> ULongByteDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, byte>> Vector2ByteDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, byte>> HashMapKeyClassByteDictionaryVar;

        public NetworkVariable<sbyte> SbyteVar;
        public NetworkVariable<NativeArray<sbyte>> SbyteArrayVar;
        public NetworkVariable<List<sbyte>> SbyteManagedListVar;
        public NetworkVariable<HashSet<sbyte>> SbyteManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<sbyte>> SbyteListVar;
        public NetworkVariable<NativeHashSet<sbyte>> SbyteHashSetVar;
        public NetworkVariable<NativeHashMap<byte, sbyte>> ByteSbyteHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, sbyte>> ULongSbyteHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, sbyte>> Vector2SbyteHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, sbyte>> HashMapKeyStructSbyteHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, sbyte>> ByteSbyteDictionaryVar;
        public NetworkVariable<Dictionary<ulong, sbyte>> ULongSbyteDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, sbyte>> Vector2SbyteDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, sbyte>> HashMapKeyClassSbyteDictionaryVar;

        public NetworkVariable<short> ShortVar;
        public NetworkVariable<NativeArray<short>> ShortArrayVar;
        public NetworkVariable<List<short>> ShortManagedListVar;
        public NetworkVariable<HashSet<short>> ShortManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<short>> ShortListVar;
        public NetworkVariable<NativeHashSet<short>> ShortHashSetVar;
        public NetworkVariable<NativeHashMap<byte, short>> ByteShortHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, short>> ULongShortHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, short>> Vector2ShortHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, short>> HashMapKeyStructShortHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, short>> ByteShortDictionaryVar;
        public NetworkVariable<Dictionary<ulong, short>> ULongShortDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, short>> Vector2ShortDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, short>> HashMapKeyClassShortDictionaryVar;

        public NetworkVariable<ushort> UshortVar;
        public NetworkVariable<NativeArray<ushort>> UshortArrayVar;
        public NetworkVariable<List<ushort>> UshortManagedListVar;
        public NetworkVariable<HashSet<ushort>> UshortManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<ushort>> UshortListVar;
        public NetworkVariable<NativeHashSet<ushort>> UshortHashSetVar;
        public NetworkVariable<NativeHashMap<byte, ushort>> ByteUshortHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, ushort>> ULongUshortHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, ushort>> Vector2UshortHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, ushort>> HashMapKeyStructUshortHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, ushort>> ByteUshortDictionaryVar;
        public NetworkVariable<Dictionary<ulong, ushort>> ULongUshortDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, ushort>> Vector2UshortDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, ushort>> HashMapKeyClassUshortDictionaryVar;

        public NetworkVariable<int> IntVar;
        public NetworkVariable<NativeArray<int>> IntArrayVar;
        public NetworkVariable<List<int>> IntManagedListVar;
        public NetworkVariable<HashSet<int>> IntManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<int>> IntListVar;
        public NetworkVariable<NativeHashSet<int>> IntHashSetVar;
        public NetworkVariable<NativeHashMap<byte, int>> ByteIntHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, int>> ULongIntHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, int>> Vector2IntHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, int>> HashMapKeyStructIntHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, int>> ByteIntDictionaryVar;
        public NetworkVariable<Dictionary<ulong, int>> ULongIntDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, int>> Vector2IntDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, int>> HashMapKeyClassIntDictionaryVar;

        public NetworkVariable<uint> UintVar;
        public NetworkVariable<NativeArray<uint>> UintArrayVar;
        public NetworkVariable<List<uint>> UintManagedListVar;
        public NetworkVariable<HashSet<uint>> UintManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<uint>> UintListVar;
        public NetworkVariable<NativeHashSet<uint>> UintHashSetVar;
        public NetworkVariable<NativeHashMap<byte, uint>> ByteUintHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, uint>> ULongUintHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, uint>> Vector2UintHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, uint>> HashMapKeyStructUintHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, uint>> ByteUintDictionaryVar;
        public NetworkVariable<Dictionary<ulong, uint>> ULongUintDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, uint>> Vector2UintDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, uint>> HashMapKeyClassUintDictionaryVar;

        public NetworkVariable<long> LongVar;
        public NetworkVariable<NativeArray<long>> LongArrayVar;
        public NetworkVariable<List<long>> LongManagedListVar;
        public NetworkVariable<HashSet<long>> LongManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<long>> LongListVar;
        public NetworkVariable<NativeHashSet<long>> LongHashSetVar;
        public NetworkVariable<NativeHashMap<byte, long>> ByteLongHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, long>> ULongLongHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, long>> Vector2LongHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, long>> HashMapKeyStructLongHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, long>> ByteLongDictionaryVar;
        public NetworkVariable<Dictionary<ulong, long>> ULongLongDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, long>> Vector2LongDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, long>> HashMapKeyClassLongDictionaryVar;

        public NetworkVariable<ulong> UlongVar;
        public NetworkVariable<NativeArray<ulong>> UlongArrayVar;
        public NetworkVariable<List<ulong>> UlongManagedListVar;
        public NetworkVariable<HashSet<ulong>> UlongManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<ulong>> UlongListVar;
        public NetworkVariable<NativeHashSet<ulong>> UlongHashSetVar;
        public NetworkVariable<NativeHashMap<byte, ulong>> ByteUlongHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, ulong>> ULongUlongHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, ulong>> Vector2UlongHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, ulong>> HashMapKeyStructUlongHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, ulong>> ByteUlongDictionaryVar;
        public NetworkVariable<Dictionary<ulong, ulong>> ULongUlongDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, ulong>> Vector2UlongDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, ulong>> HashMapKeyClassUlongDictionaryVar;

        public NetworkVariable<bool> BoolVar;
        public NetworkVariable<NativeArray<bool>> BoolArrayVar;
        public NetworkVariable<List<bool>> BoolManagedListVar;
        public NetworkVariable<HashSet<bool>> BoolManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<bool>> BoolListVar;
        public NetworkVariable<NativeHashSet<bool>> BoolHashSetVar;
        public NetworkVariable<NativeHashMap<byte, bool>> ByteBoolHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, bool>> ULongBoolHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, bool>> Vector2BoolHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, bool>> HashMapKeyStructBoolHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, bool>> ByteBoolDictionaryVar;
        public NetworkVariable<Dictionary<ulong, bool>> ULongBoolDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, bool>> Vector2BoolDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, bool>> HashMapKeyClassBoolDictionaryVar;

        public NetworkVariable<char> CharVar;
        public NetworkVariable<NativeArray<char>> CharArrayVar;
        public NetworkVariable<List<char>> CharManagedListVar;
        public NetworkVariable<HashSet<char>> CharManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<char>> CharListVar;
        public NetworkVariable<NativeHashSet<char>> CharHashSetVar;
        public NetworkVariable<NativeHashMap<byte, char>> ByteCharHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, char>> ULongCharHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, char>> Vector2CharHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, char>> HashMapKeyStructCharHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, char>> ByteCharDictionaryVar;
        public NetworkVariable<Dictionary<ulong, char>> ULongCharDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, char>> Vector2CharDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, char>> HashMapKeyClassCharDictionaryVar;

        public NetworkVariable<float> FloatVar;
        public NetworkVariable<NativeArray<float>> FloatArrayVar;
        public NetworkVariable<List<float>> FloatManagedListVar;
        public NetworkVariable<HashSet<float>> FloatManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<float>> FloatListVar;
        public NetworkVariable<NativeHashSet<float>> FloatHashSetVar;
        public NetworkVariable<NativeHashMap<byte, float>> ByteFloatHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, float>> ULongFloatHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, float>> Vector2FloatHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, float>> HashMapKeyStructFloatHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, float>> ByteFloatDictionaryVar;
        public NetworkVariable<Dictionary<ulong, float>> ULongFloatDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, float>> Vector2FloatDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, float>> HashMapKeyClassFloatDictionaryVar;

        public NetworkVariable<double> DoubleVar;
        public NetworkVariable<NativeArray<double>> DoubleArrayVar;
        public NetworkVariable<List<double>> DoubleManagedListVar;
        public NetworkVariable<HashSet<double>> DoubleManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<double>> DoubleListVar;
        public NetworkVariable<NativeHashSet<double>> DoubleHashSetVar;
        public NetworkVariable<NativeHashMap<byte, double>> ByteDoubleHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, double>> ULongDoubleHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, double>> Vector2DoubleHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, double>> HashMapKeyStructDoubleHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, double>> ByteDoubleDictionaryVar;
        public NetworkVariable<Dictionary<ulong, double>> ULongDoubleDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, double>> Vector2DoubleDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, double>> HashMapKeyClassDoubleDictionaryVar;

        public NetworkVariable<ByteEnum> ByteEnumVar;
        public NetworkVariable<NativeArray<ByteEnum>> ByteEnumArrayVar;
        public NetworkVariable<List<ByteEnum>> ByteEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<ByteEnum>> ByteEnumListVar;
#endif
        public NetworkVariable<SByteEnum> SByteEnumVar;
        public NetworkVariable<NativeArray<SByteEnum>> SByteEnumArrayVar;
        public NetworkVariable<List<SByteEnum>> SByteEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<SByteEnum>> SByteEnumListVar;
#endif
        public NetworkVariable<ShortEnum> ShortEnumVar;
        public NetworkVariable<NativeArray<ShortEnum>> ShortEnumArrayVar;
        public NetworkVariable<List<ShortEnum>> ShortEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<ShortEnum>> ShortEnumListVar;
#endif
        public NetworkVariable<UShortEnum> UShortEnumVar;
        public NetworkVariable<NativeArray<UShortEnum>> UShortEnumArrayVar;
        public NetworkVariable<List<UShortEnum>> UShortEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<UShortEnum>> UShortEnumListVar;
#endif
        public NetworkVariable<IntEnum> IntEnumVar;
        public NetworkVariable<NativeArray<IntEnum>> IntEnumArrayVar;
        public NetworkVariable<List<IntEnum>> IntEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<IntEnum>> IntEnumListVar;
#endif
        public NetworkVariable<UIntEnum> UIntEnumVar;
        public NetworkVariable<NativeArray<UIntEnum>> UIntEnumArrayVar;
        public NetworkVariable<List<UIntEnum>> UIntEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<UIntEnum>> UIntEnumListVar;
#endif
        public NetworkVariable<LongEnum> LongEnumVar;
        public NetworkVariable<NativeArray<LongEnum>> LongEnumArrayVar;
        public NetworkVariable<List<LongEnum>> LongEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<LongEnum>> LongEnumListVar;
#endif
        public NetworkVariable<ULongEnum> ULongEnumVar;
        public NetworkVariable<NativeArray<ULongEnum>> ULongEnumArrayVar;
        public NetworkVariable<List<ULongEnum>> ULongEnumManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<ULongEnum>> ULongEnumListVar;
#endif
        public NetworkVariable<Vector2> Vector2Var;
        public NetworkVariable<NativeArray<Vector2>> Vector2ArrayVar;
        public NetworkVariable<List<Vector2>> Vector2ManagedListVar;
        public NetworkVariable<HashSet<Vector2>> Vector2ManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Vector2>> Vector2ListVar;
        public NetworkVariable<NativeHashSet<Vector2>> Vector2HashSetVar;
        public NetworkVariable<NativeHashMap<byte, Vector2>> ByteVector2HashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Vector2>> ULongVector2HashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Vector2>> Vector2Vector2HashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Vector2>> HashMapKeyStructVector2HashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Vector2>> ByteVector2DictionaryVar;
        public NetworkVariable<Dictionary<ulong, Vector2>> ULongVector2DictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Vector2>> Vector2Vector2DictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Vector2>> HashMapKeyClassVector2DictionaryVar;

        public NetworkVariable<Vector3> Vector3Var;
        public NetworkVariable<NativeArray<Vector3>> Vector3ArrayVar;
        public NetworkVariable<List<Vector3>> Vector3ManagedListVar;
        public NetworkVariable<HashSet<Vector3>> Vector3ManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Vector3>> Vector3ListVar;
        public NetworkVariable<NativeHashSet<Vector3>> Vector3HashSetVar;
        public NetworkVariable<NativeHashMap<byte, Vector3>> ByteVector3HashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Vector3>> ULongVector3HashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Vector3>> Vector2Vector3HashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Vector3>> HashMapKeyStructVector3HashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Vector3>> ByteVector3DictionaryVar;
        public NetworkVariable<Dictionary<ulong, Vector3>> ULongVector3DictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Vector3>> Vector2Vector3DictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Vector3>> HashMapKeyClassVector3DictionaryVar;

        public NetworkVariable<Vector2Int> Vector2IntVar;
        public NetworkVariable<NativeArray<Vector2Int>> Vector2IntArrayVar;
        public NetworkVariable<List<Vector2Int>> Vector2IntManagedListVar;
        public NetworkVariable<HashSet<Vector2Int>> Vector2IntManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Vector2Int>> Vector2IntListVar;
        public NetworkVariable<NativeHashSet<Vector2Int>> Vector2IntHashSetVar;
        public NetworkVariable<NativeHashMap<byte, Vector2Int>> ByteVector2IntHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Vector2Int>> ULongVector2IntHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Vector2Int>> Vector2Vector2IntHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Vector2Int>> HashMapKeyStructVector2IntHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Vector2Int>> ByteVector2IntDictionaryVar;
        public NetworkVariable<Dictionary<ulong, Vector2Int>> ULongVector2IntDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Vector2Int>> Vector2Vector2IntDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Vector2Int>> HashMapKeyClassVector2IntDictionaryVar;

        public NetworkVariable<Vector3Int> Vector3IntVar;
        public NetworkVariable<NativeArray<Vector3Int>> Vector3IntArrayVar;
        public NetworkVariable<List<Vector3Int>> Vector3IntManagedListVar;
        public NetworkVariable<HashSet<Vector3Int>> Vector3IntManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Vector3Int>> Vector3IntListVar;
        public NetworkVariable<NativeHashSet<Vector3Int>> Vector3IntHashSetVar;
        public NetworkVariable<NativeHashMap<byte, Vector3Int>> ByteVector3IntHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Vector3Int>> ULongVector3IntHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Vector3Int>> Vector2Vector3IntHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Vector3Int>> HashMapKeyStructVector3IntHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Vector3Int>> ByteVector3IntDictionaryVar;
        public NetworkVariable<Dictionary<ulong, Vector3Int>> ULongVector3IntDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Vector3Int>> Vector2Vector3IntDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Vector3Int>> HashMapKeyClassVector3IntDictionaryVar;

        public NetworkVariable<Vector4> Vector4Var;
        public NetworkVariable<NativeArray<Vector4>> Vector4ArrayVar;
        public NetworkVariable<List<Vector4>> Vector4ManagedListVar;
        public NetworkVariable<HashSet<Vector4>> Vector4ManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Vector4>> Vector4ListVar;
        public NetworkVariable<NativeHashSet<Vector4>> Vector4HashSetVar;
        public NetworkVariable<NativeHashMap<byte, Vector4>> ByteVector4HashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Vector4>> ULongVector4HashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Vector4>> Vector2Vector4HashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Vector4>> HashMapKeyStructVector4HashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Vector4>> ByteVector4DictionaryVar;
        public NetworkVariable<Dictionary<ulong, Vector4>> ULongVector4DictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Vector4>> Vector2Vector4DictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Vector4>> HashMapKeyClassVector4DictionaryVar;

        public NetworkVariable<Quaternion> QuaternionVar;
        public NetworkVariable<NativeArray<Quaternion>> QuaternionArrayVar;
        public NetworkVariable<List<Quaternion>> QuaternionManagedListVar;
        public NetworkVariable<HashSet<Quaternion>> QuaternionManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Quaternion>> QuaternionListVar;
        public NetworkVariable<NativeHashSet<Quaternion>> QuaternionHashSetVar;
        public NetworkVariable<NativeHashMap<byte, Quaternion>> ByteQuaternionHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, Quaternion>> ULongQuaternionHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, Quaternion>> Vector2QuaternionHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, Quaternion>> HashMapKeyStructQuaternionHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, Quaternion>> ByteQuaternionDictionaryVar;
        public NetworkVariable<Dictionary<ulong, Quaternion>> ULongQuaternionDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, Quaternion>> Vector2QuaternionDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, Quaternion>> HashMapKeyClassQuaternionDictionaryVar;

        public NetworkVariable<Color> ColorVar;
        public NetworkVariable<NativeArray<Color>> ColorArrayVar;
        public NetworkVariable<List<Color>> ColorManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Color>> ColorListVar;
#endif
        public NetworkVariable<Color32> Color32Var;
        public NetworkVariable<NativeArray<Color32>> Color32ArrayVar;
        public NetworkVariable<List<Color32>> Color32ManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Color32>> Color32ListVar;
#endif
        public NetworkVariable<Ray> RayVar;
        public NetworkVariable<NativeArray<Ray>> RayArrayVar;
        public NetworkVariable<List<Ray>> RayManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Ray>> RayListVar;
#endif
        public NetworkVariable<Ray2D> Ray2DVar;
        public NetworkVariable<NativeArray<Ray2D>> Ray2DArrayVar;
        public NetworkVariable<List<Ray2D>> Ray2DManagedListVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<Ray2D>> Ray2DListVar;
#endif
        public NetworkVariable<NetworkVariableTestStruct> TestStructVar;
        public NetworkVariable<NativeArray<NetworkVariableTestStruct>> TestStructArrayVar;
        public NetworkVariable<List<NetworkVariableTestClass>> TestStructManagedListVar;
        public NetworkVariable<HashSet<HashableNetworkVariableTestClass>> TestStructManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<NetworkVariableTestStruct>> TestStructListVar;
        public NetworkVariable<NativeHashSet<HashableNetworkVariableTestStruct>> TestStructHashSetVar;
        public NetworkVariable<NativeHashMap<byte, HashMapValStruct>> ByteTestStructHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, HashMapValStruct>> ULongTestStructHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, HashMapValStruct>> Vector2TestStructHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, HashMapValStruct>> HashMapKeyStructTestStructHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, HashMapValClass>> ByteTestStructDictionaryVar;
        public NetworkVariable<Dictionary<ulong, HashMapValClass>> ULongTestStructDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, HashMapValClass>> Vector2TestStructDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, HashMapValClass>> HashMapKeyClassTestStructDictionaryVar;


        public NetworkVariable<FixedString32Bytes> FixedStringVar;
        public NetworkVariable<NativeArray<FixedString32Bytes>> FixedStringArrayVar;
        public NetworkVariable<List<FixedString32Bytes>> FixedStringManagedListVar;
        public NetworkVariable<HashSet<FixedString32Bytes>> FixedStringManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<FixedString32Bytes>> FixedStringListVar;
        public NetworkVariable<NativeHashSet<FixedString32Bytes>> FixedStringHashSetVar;
        public NetworkVariable<NativeHashMap<byte, FixedString32Bytes>> ByteFixedStringHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, FixedString32Bytes>> ULongFixedStringHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, FixedString32Bytes>> Vector2FixedStringHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, FixedString32Bytes>> HashMapKeyStructFixedStringHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, FixedString32Bytes>> ByteFixedStringDictionaryVar;
        public NetworkVariable<Dictionary<ulong, FixedString32Bytes>> ULongFixedStringDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, FixedString32Bytes>> Vector2FixedStringDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, FixedString32Bytes>> HashMapKeyClassFixedStringDictionaryVar;


        public NetworkVariable<UnmanagedNetworkSerializableType> UnmanagedNetworkSerializableTypeVar;
        public NetworkVariable<HashSet<UnmanagedNetworkSerializableType>> UnmanagedNetworkSerializableManagedHashSetVar;
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public NetworkVariable<NativeList<UnmanagedNetworkSerializableType>> UnmanagedNetworkSerializableListVar;
        public NetworkVariable<NativeHashSet<UnmanagedNetworkSerializableType>> UnmanagedNetworkSerializableHashSetVar;
        public NetworkVariable<NativeHashMap<byte, UnmanagedNetworkSerializableType>> ByteUnmanagedNetworkSerializableHashMapVar;
        public NetworkVariable<NativeHashMap<ulong, UnmanagedNetworkSerializableType>> ULongUnmanagedNetworkSerializableHashMapVar;
        public NetworkVariable<NativeHashMap<Vector2, UnmanagedNetworkSerializableType>> Vector2UnmanagedNetworkSerializableHashMapVar;
        public NetworkVariable<NativeHashMap<HashMapKeyStruct, UnmanagedNetworkSerializableType>> HashMapKeyStructUnmanagedNetworkSerializableHashMapVar;
#endif
        public NetworkVariable<Dictionary<byte, UnmanagedNetworkSerializableType>> ByteUnmanagedNetworkSerializableDictionaryVar;
        public NetworkVariable<Dictionary<ulong, UnmanagedNetworkSerializableType>> ULongUnmanagedNetworkSerializableDictionaryVar;
        public NetworkVariable<Dictionary<Vector2, UnmanagedNetworkSerializableType>> Vector2UnmanagedNetworkSerializableDictionaryVar;
        public NetworkVariable<Dictionary<HashMapKeyClass, UnmanagedNetworkSerializableType>> HashMapKeyClassUnmanagedNetworkSerializableDictionaryVar;

        public NetworkVariable<NativeArray<UnmanagedNetworkSerializableType>> UnmanagedNetworkSerializableArrayVar;
        public NetworkVariable<List<UnmanagedNetworkSerializableType>> UnmanagedNetworkSerializableManagedListVar;

        public NetworkVariable<ManagedNetworkSerializableType> ManagedNetworkSerializableTypeVar;

        public NetworkVariable<string> StringVar;
        public NetworkVariable<Guid> GuidVar;
        public NetworkVariableSubclass<TemplatedValueOnlyReferencedByNetworkVariableSubclass<int>> SubclassVar;
    }

    internal class TemplateNetworkBehaviourType<T> : NetworkBehaviour
    {
        public NetworkVariable<T> TheVar;
    }

    internal class IntermediateNetworkBehavior<T> : TemplateNetworkBehaviourType<T>
    {
        public NetworkVariable<T> TheVar2;
    }
#if !NGO_MINIMALPROJECT
    internal class ClassHavingNetworkBehaviour : IntermediateNetworkBehavior<TestClass>
    {

    }

    // Please do not reference TestClass_ReferencedOnlyByTemplateNetworkBehavourType anywhere other than here!
    internal class ClassHavingNetworkBehaviour2 : TemplateNetworkBehaviourType<TestClass_ReferencedOnlyByTemplateNetworkBehaviourType>
    {

    }

    internal class StructHavingNetworkBehaviour : TemplateNetworkBehaviourType<TestStruct>
    {

    }
#endif

    internal struct StructUsedOnlyInNetworkList : IEquatable<StructUsedOnlyInNetworkList>, INetworkSerializeByMemcpy
    {
        public int Value;

        public bool Equals(StructUsedOnlyInNetworkList other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is StructUsedOnlyInNetworkList other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }

}
