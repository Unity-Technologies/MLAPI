using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Random = System.Random;

namespace Unity.Netcode.EditorTests
{
    internal abstract class BaseFastBufferReaderWriterTest
    {
        protected enum ByteEnum : byte
        {
            A,
            B,
            C = byte.MaxValue
        }
        protected enum SByteEnum : sbyte
        {
            A,
            B,
            C = sbyte.MaxValue
        }
        protected enum ShortEnum : short
        {
            A,
            B,
            C = short.MaxValue
        }
        protected enum UShortEnum : ushort
        {
            A,
            B,
            C = ushort.MaxValue
        }
        protected enum IntEnum : int
        {
            A,
            B,
            C = int.MaxValue
        }
        protected enum UIntEnum : uint
        {
            A,
            B,
            C = uint.MaxValue
        }
        protected enum LongEnum : long
        {
            A,
            B,
            C = long.MaxValue
        }
        protected enum ULongEnum : ulong
        {
            A,
            B,
            C = ulong.MaxValue
        }

        protected struct TestStruct : INetworkSerializeByMemcpy
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
        }

        public enum WriteType
        {
            WriteDirect,
            WriteSafe
        }

        protected abstract void RunTypeTest<T>(T valueToTest) where T : unmanaged;

        protected abstract void RunTypeTestSafe<T>(T valueToTest) where T : unmanaged;

        protected abstract void RunTypeArrayTest<T>(T[] valueToTest) where T : unmanaged;

        protected abstract void RunTypeArrayTestSafe<T>(T[] valueToTest) where T : unmanaged;

        protected abstract void RunTypeNativeArrayTest<T>(NativeArray<T> valueToTest) where T : unmanaged;

        protected abstract void RunTypeNativeArrayTestSafe<T>(NativeArray<T> valueToTest) where T : unmanaged;

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        protected abstract void RunTypeNativeListTest<T>(NativeList<T> valueToTest) where T : unmanaged;

        protected abstract void RunTypeNativeListTestSafe<T>(NativeList<T> valueToTest) where T : unmanaged;
#endif

        private Random m_Random = new Random();
        protected TestStruct GetTestStruct()
        {
            var testStruct = new TestStruct
            {
                A = (byte)m_Random.Next(),
                B = (short)m_Random.Next(),
                C = (ushort)m_Random.Next(),
                D = m_Random.Next(),
                E = (uint)m_Random.Next(),
                F = ((long)m_Random.Next() << 32) + m_Random.Next(),
                G = ((ulong)m_Random.Next() << 32) + (ulong)m_Random.Next(),
                H = true,
                I = '\u263a',
                J = (float)m_Random.NextDouble(),
                K = m_Random.NextDouble(),
            };

            return testStruct;
        }

        private void RunTestWithWriteType<T>(T val, WriteType wt, FastBufferWriter.ForPrimitives _ = default) where T : unmanaged
        {
            switch (wt)
            {
                case WriteType.WriteDirect:
                    RunTypeTest(val);
                    break;
                case WriteType.WriteSafe:
                    RunTypeTestSafe(val);
                    break;
            }
        }

        public void BaseTypeTest(Type testType, WriteType writeType)
        {
            var random = new Random();

            if (testType == typeof(byte))
            {
                RunTestWithWriteType((byte)random.Next(), writeType);
            }
            else if (testType == typeof(sbyte))
            {
                RunTestWithWriteType((sbyte)random.Next(), writeType);
            }
            else if (testType == typeof(short))
            {
                RunTestWithWriteType((short)random.Next(), writeType);
            }
            else if (testType == typeof(ushort))
            {
                RunTestWithWriteType((ushort)random.Next(), writeType);
            }
            else if (testType == typeof(int))
            {
                RunTestWithWriteType(random.Next(), writeType);
            }
            else if (testType == typeof(uint))
            {
                RunTestWithWriteType((uint)random.Next(), writeType);
            }
            else if (testType == typeof(long))
            {
                RunTestWithWriteType(((long)random.Next() << 32) + random.Next(), writeType);
            }
            else if (testType == typeof(ulong))
            {
                RunTestWithWriteType(((ulong)random.Next() << 32) + (ulong)random.Next(), writeType);
            }
            else if (testType == typeof(bool))
            {
                RunTestWithWriteType(true, writeType);
            }
            else if (testType == typeof(char))
            {
                RunTestWithWriteType('a', writeType);
                RunTestWithWriteType('\u263a', writeType);
            }
            else if (testType == typeof(float))
            {
                RunTestWithWriteType((float)random.NextDouble(), writeType);
            }
            else if (testType == typeof(double))
            {
                RunTestWithWriteType(random.NextDouble(), writeType);
            }
            else if (testType == typeof(ByteEnum))
            {
                RunTestWithWriteType(ByteEnum.C, writeType);
            }
            else if (testType == typeof(SByteEnum))
            {
                RunTestWithWriteType(SByteEnum.C, writeType);
            }
            else if (testType == typeof(ShortEnum))
            {
                RunTestWithWriteType(ShortEnum.C, writeType);
            }
            else if (testType == typeof(UShortEnum))
            {
                RunTestWithWriteType(UShortEnum.C, writeType);
            }
            else if (testType == typeof(IntEnum))
            {
                RunTestWithWriteType(IntEnum.C, writeType);
            }
            else if (testType == typeof(UIntEnum))
            {
                RunTestWithWriteType(UIntEnum.C, writeType);
            }
            else if (testType == typeof(LongEnum))
            {
                RunTestWithWriteType(LongEnum.C, writeType);
            }
            else if (testType == typeof(ULongEnum))
            {
                RunTestWithWriteType(ULongEnum.C, writeType);
            }
            else if (testType == typeof(Vector2))
            {
                RunTestWithWriteType(new Vector2((float)random.NextDouble(), (float)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Vector3))
            {
                RunTestWithWriteType(new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Vector2Int))
            {
                RunTestWithWriteType(new Vector2Int((int)random.NextDouble(), (int)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Vector3Int))
            {
                RunTestWithWriteType(new Vector3Int((int)random.NextDouble(), (int)random.NextDouble(), (int)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Vector4))
            {
                RunTestWithWriteType(new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Quaternion))
            {
                RunTestWithWriteType(new Quaternion((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Color))
            {
                RunTestWithWriteType(new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()), writeType);
            }
            else if (testType == typeof(Color32))
            {
                RunTestWithWriteType(new Color32((byte)random.Next(), (byte)random.Next(), (byte)random.Next(), (byte)random.Next()), writeType);
            }
            else if (testType == typeof(Ray))
            {
                RunTestWithWriteType(new Ray(
                    new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()),
                    new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble())), writeType);
            }
            else if (testType == typeof(Ray2D))
            {
                RunTestWithWriteType(new Ray2D(
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble())), writeType);
            }
            else if (testType == typeof(TestStruct))
            {
                RunTestWithWriteType(GetTestStruct(), writeType);
            }
            else
            {
                Assert.Fail("No type handler was provided for this type in the test!");
            }
        }

        public void BaseArrayTypeTest(Type testType, WriteType writeType)
        {
            var random = new Random();
            void RunTypeTestLocal<T>(T[] val, WriteType wt) where T : unmanaged
            {
                switch (wt)
                {
                    case WriteType.WriteDirect:
                        RunTypeArrayTest(val);
                        break;
                    case WriteType.WriteSafe:
                        RunTypeArrayTestSafe(val);
                        break;
                }
            }

            if (testType == typeof(byte))
            {
                RunTypeTestLocal(new[]{
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next()
                }, writeType);
            }
            else if (testType == typeof(sbyte))
            {
                RunTypeTestLocal(new[]{
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next()
                }, writeType);
            }
            else if (testType == typeof(short))
            {
                RunTypeTestLocal(new[]{
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next()
                }, writeType);
            }
            else if (testType == typeof(ushort))
            {
                RunTypeTestLocal(new[]{
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next()
                }, writeType);
            }
            else if (testType == typeof(int))
            {
                RunTypeTestLocal(new[]{
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next()
                }, writeType);
            }
            else if (testType == typeof(uint))
            {
                RunTypeTestLocal(new[]{
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next()
                }, writeType);
            }
            else if (testType == typeof(long))
            {
                RunTypeTestLocal(new[]{
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next()
                }, writeType);
            }
            else if (testType == typeof(ulong))
            {
                RunTypeTestLocal(new[]{
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next()
                }, writeType);
            }
            else if (testType == typeof(bool))
            {
                RunTypeTestLocal(new[]{
                    true,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    false,
                    true
                }, writeType);
            }
            else if (testType == typeof(char))
            {
                RunTypeTestLocal(new[]{
                    'a',
                    '\u263a'
                }, writeType);
            }
            else if (testType == typeof(float))
            {
                RunTypeTestLocal(new[]{
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble()
                }, writeType);
            }
            else if (testType == typeof(double))
            {
                RunTypeTestLocal(new[]{
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble()
                }, writeType);
            }
            else if (testType == typeof(ByteEnum))
            {
                RunTypeTestLocal(new[]{
                    ByteEnum.C,
                    ByteEnum.A,
                    ByteEnum.B
                }, writeType);
            }
            else if (testType == typeof(SByteEnum))
            {
                RunTypeTestLocal(new[]{
                    SByteEnum.C,
                    SByteEnum.A,
                    SByteEnum.B
                }, writeType);
            }
            else if (testType == typeof(ShortEnum))
            {
                RunTypeTestLocal(new[]{
                    ShortEnum.C,
                    ShortEnum.A,
                    ShortEnum.B
                }, writeType);
            }
            else if (testType == typeof(UShortEnum))
            {
                RunTypeTestLocal(new[]{
                    UShortEnum.C,
                    UShortEnum.A,
                    UShortEnum.B
                }, writeType);
            }
            else if (testType == typeof(IntEnum))
            {
                RunTypeTestLocal(new[]{
                    IntEnum.C,
                    IntEnum.A,
                    IntEnum.B
                }, writeType);
            }
            else if (testType == typeof(UIntEnum))
            {
                RunTypeTestLocal(new[]{
                    UIntEnum.C,
                    UIntEnum.A,
                    UIntEnum.B
                }, writeType);
            }
            else if (testType == typeof(LongEnum))
            {
                RunTypeTestLocal(new[]{
                    LongEnum.C,
                    LongEnum.A,
                    LongEnum.B
                }, writeType);
            }
            else if (testType == typeof(ULongEnum))
            {
                RunTypeTestLocal(new[]{
                    ULongEnum.C,
                    ULongEnum.A,
                    ULongEnum.B
                }, writeType);
            }
            else if (testType == typeof(Vector2))
            {
                RunTypeTestLocal(new[]{
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Vector3))
            {
                RunTypeTestLocal(new[]{
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Vector2Int))
            {
                RunTypeTestLocal(new[]{
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Vector3Int))
            {
                RunTypeTestLocal(new[]{
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Vector4))
            {
                RunTypeTestLocal(new[]{
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Quaternion))
            {
                RunTypeTestLocal(new[]{
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Color))
            {
                RunTypeTestLocal(new[]{
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, writeType);
            }
            else if (testType == typeof(Color32))
            {
                RunTypeTestLocal(new[]{
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                }, writeType);
            }
            else if (testType == typeof(Ray))
            {
                RunTypeTestLocal(new[]{
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                }, writeType);
            }
            else if (testType == typeof(Ray2D))
            {
                RunTypeTestLocal(new[]{
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                }, writeType);
            }
            else if (testType == typeof(TestStruct))
            {
                RunTypeTestLocal(new[] {
                    GetTestStruct(),
                    GetTestStruct(),
                    GetTestStruct(),
                }, writeType);
            }
            else
            {
                Assert.Fail("No type handler was provided for this type in the test!");
            }
        }

        public void BaseNativeArrayTypeTest(Type testType, WriteType writeType)
        {
            var random = new Random();
            void RunTypeTestLocal<T>(NativeArray<T> val, WriteType wt) where T : unmanaged
            {
                switch (wt)
                {
                    case WriteType.WriteDirect:
                        RunTypeNativeArrayTest(val);
                        break;
                    case WriteType.WriteSafe:
                        RunTypeNativeArrayTestSafe(val);
                        break;
                }
            }

            if (testType == typeof(byte))
            {
                RunTypeTestLocal(new NativeArray<byte>(new[]{
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(sbyte))
            {
                RunTypeTestLocal(new NativeArray<sbyte>(new[]{
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(short))
            {
                RunTypeTestLocal(new NativeArray<short>(new[]{
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ushort))
            {
                RunTypeTestLocal(new NativeArray<ushort>(new[]{
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(int))
            {
                RunTypeTestLocal(new NativeArray<int>(new[]{
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(uint))
            {
                RunTypeTestLocal(new NativeArray<uint>(new[]{
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(long))
            {
                RunTypeTestLocal(new NativeArray<long>(new[]{
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ulong))
            {
                RunTypeTestLocal(new NativeArray<ulong>(new[]{
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(bool))
            {
                RunTypeTestLocal(new NativeArray<bool>(new[]{
                    true,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    false,
                    true
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(char))
            {
                RunTypeTestLocal(new NativeArray<char>(new[]{
                    'a',
                    '\u263a'
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(float))
            {
                RunTypeTestLocal(new NativeArray<float>(new[]{
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(double))
            {
                RunTypeTestLocal(new NativeArray<double>(new[]{
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ByteEnum))
            {
                RunTypeTestLocal(new NativeArray<ByteEnum>(new[]{
                    ByteEnum.C,
                    ByteEnum.A,
                    ByteEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(SByteEnum))
            {
                RunTypeTestLocal(new NativeArray<SByteEnum>(new[]{
                    SByteEnum.C,
                    SByteEnum.A,
                    SByteEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ShortEnum))
            {
                RunTypeTestLocal(new NativeArray<ShortEnum>(new[]{
                    ShortEnum.C,
                    ShortEnum.A,
                    ShortEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(UShortEnum))
            {
                RunTypeTestLocal(new NativeArray<UShortEnum>(new[]{
                    UShortEnum.C,
                    UShortEnum.A,
                    UShortEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(IntEnum))
            {
                RunTypeTestLocal(new NativeArray<IntEnum>(new[]{
                    IntEnum.C,
                    IntEnum.A,
                    IntEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(UIntEnum))
            {
                RunTypeTestLocal(new NativeArray<UIntEnum>(new[]{
                    UIntEnum.C,
                    UIntEnum.A,
                    UIntEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(LongEnum))
            {
                RunTypeTestLocal(new NativeArray<LongEnum>(new[]{
                    LongEnum.C,
                    LongEnum.A,
                    LongEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ULongEnum))
            {
                RunTypeTestLocal(new NativeArray<ULongEnum>(new[]{
                    ULongEnum.C,
                    ULongEnum.A,
                    ULongEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector2))
            {
                RunTypeTestLocal(new NativeArray<Vector2>(new[]{
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector3))
            {
                RunTypeTestLocal(new NativeArray<Vector3>(new[]{
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector2Int))
            {
                RunTypeTestLocal(new NativeArray<Vector2Int>(new[]{
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector3Int))
            {
                RunTypeTestLocal(new NativeArray<Vector3Int>(new[]{
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector4))
            {
                RunTypeTestLocal(new NativeArray<Vector4>(new[]{
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Quaternion))
            {
                RunTypeTestLocal(new NativeArray<Quaternion>(new[]{
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Color))
            {
                RunTypeTestLocal(new NativeArray<Color>(new[]{
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Color32))
            {
                RunTypeTestLocal(new NativeArray<Color32>(new[]{
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Ray))
            {
                RunTypeTestLocal(new NativeArray<Ray>(new[]{
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Ray2D))
            {
                RunTypeTestLocal(new NativeArray<Ray2D>(new[]{
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(TestStruct))
            {
                RunTypeTestLocal(new NativeArray<TestStruct>(new[] {
                    GetTestStruct(),
                    GetTestStruct(),
                    GetTestStruct(),
                }, Allocator.Temp), writeType);
            }
            else
            {
                Assert.Fail("No type handler was provided for this type in the test!");
            }
        }
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public void BaseNativeListTypeTest(Type testType, WriteType writeType)
        {
            var random = new Random();
            void RunTypeTestLocal<T>(NativeArray<T> val, WriteType wt) where T : unmanaged
            {
                var lst = new NativeList<T>(val.Length, Allocator.Temp);
                foreach (var item in val)
                {
                    lst.Add(item);
                }
                switch (wt)
                {
                    case WriteType.WriteDirect:
                        RunTypeNativeListTest(lst);
                        break;
                    case WriteType.WriteSafe:
                        RunTypeNativeListTestSafe(lst);
                        break;
                }
            }

            if (testType == typeof(byte))
            {
                RunTypeTestLocal(new NativeArray<byte>(new[]{
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next(),
                    (byte) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(sbyte))
            {
                RunTypeTestLocal(new NativeArray<sbyte>(new[]{
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next(),
                    (sbyte) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(short))
            {
                RunTypeTestLocal(new NativeArray<short>(new[]{
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next(),
                    (short) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ushort))
            {
                RunTypeTestLocal(new NativeArray<ushort>(new[]{
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next(),
                    (ushort) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(int))
            {
                RunTypeTestLocal(new NativeArray<int>(new[]{
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next(),
                    random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(uint))
            {
                RunTypeTestLocal(new NativeArray<uint>(new[]{
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next(),
                    (uint) random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(long))
            {
                RunTypeTestLocal(new NativeArray<long>(new[]{
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next(),
                    ((long)random.Next() << 32) + random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ulong))
            {
                RunTypeTestLocal(new NativeArray<ulong>(new[]{
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next(),
                    ((ulong)random.Next() << 32) + (ulong)random.Next()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(bool))
            {
                RunTypeTestLocal(new NativeArray<bool>(new[]{
                    true,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    false,
                    true
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(char))
            {
                RunTypeTestLocal(new NativeArray<char>(new[]{
                    'a',
                    '\u263a'
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(float))
            {
                RunTypeTestLocal(new NativeArray<float>(new[]{
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(double))
            {
                RunTypeTestLocal(new NativeArray<double>(new[]{
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble(),
                    random.NextDouble()
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ByteEnum))
            {
                RunTypeTestLocal(new NativeArray<ByteEnum>(new[]{
                    ByteEnum.C,
                    ByteEnum.A,
                    ByteEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(SByteEnum))
            {
                RunTypeTestLocal(new NativeArray<SByteEnum>(new[]{
                    SByteEnum.C,
                    SByteEnum.A,
                    SByteEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ShortEnum))
            {
                RunTypeTestLocal(new NativeArray<ShortEnum>(new[]{
                    ShortEnum.C,
                    ShortEnum.A,
                    ShortEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(UShortEnum))
            {
                RunTypeTestLocal(new NativeArray<UShortEnum>(new[]{
                    UShortEnum.C,
                    UShortEnum.A,
                    UShortEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(IntEnum))
            {
                RunTypeTestLocal(new NativeArray<IntEnum>(new[]{
                    IntEnum.C,
                    IntEnum.A,
                    IntEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(UIntEnum))
            {
                RunTypeTestLocal(new NativeArray<UIntEnum>(new[]{
                    UIntEnum.C,
                    UIntEnum.A,
                    UIntEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(LongEnum))
            {
                RunTypeTestLocal(new NativeArray<LongEnum>(new[]{
                    LongEnum.C,
                    LongEnum.A,
                    LongEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(ULongEnum))
            {
                RunTypeTestLocal(new NativeArray<ULongEnum>(new[]{
                    ULongEnum.C,
                    ULongEnum.A,
                    ULongEnum.B
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector2))
            {
                RunTypeTestLocal(new NativeArray<Vector2>(new[]{
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector3))
            {
                RunTypeTestLocal(new NativeArray<Vector3>(new[]{
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                    new Vector3((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector2Int))
            {
                RunTypeTestLocal(new NativeArray<Vector2Int>(new[]{
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector2Int((int) random.NextDouble(), (int) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector3Int))
            {
                RunTypeTestLocal(new NativeArray<Vector3Int>(new[]{
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                    new Vector3Int((int) random.NextDouble(), (int) random.NextDouble(), (int) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Vector4))
            {
                RunTypeTestLocal(new NativeArray<Vector4>(new[]{
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Quaternion))
            {
                RunTypeTestLocal(new NativeArray<Quaternion>(new[]{
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                    new Quaternion((float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble(), (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Color))
            {
                RunTypeTestLocal(new NativeArray<Color>(new[]{
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                    new Color((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(),
                        (float) random.NextDouble()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Color32))
            {
                RunTypeTestLocal(new NativeArray<Color32>(new[]{
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                    new Color32((byte) random.Next(), (byte) random.Next(), (byte) random.Next(), (byte) random.Next()),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Ray))
            {
                RunTypeTestLocal(new NativeArray<Ray>(new[]{
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                    new Ray(
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble()),
                        new Vector3((float) random.NextDouble(), (float) random.NextDouble(),
                            (float) random.NextDouble())),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(Ray2D))
            {
                RunTypeTestLocal(new NativeArray<Ray2D>(new[]{
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                    new Ray2D(
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble()),
                        new Vector2((float) random.NextDouble(), (float) random.NextDouble())),
                }, Allocator.Temp), writeType);
            }
            else if (testType == typeof(TestStruct))
            {
                RunTypeTestLocal(new NativeArray<TestStruct>(new[] {
                    GetTestStruct(),
                    GetTestStruct(),
                    GetTestStruct(),
                }, Allocator.Temp), writeType);
            }
            else
            {
                Assert.Fail("No type handler was provided for this type in the test!");
            }
        }
#endif
    }
}
