using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Random = System.Random;

namespace Unity.Netcode.EditorTests
{
    internal class FastBufferReaderTests : BaseFastBufferReaderWriterTest
    {
        private void WriteCheckBytes(FastBufferWriter writer, int writeSize, string failMessage = "")
        {
            Assert.IsTrue(writer.TryBeginWrite(2), "Writer denied write permission");
            writer.WriteValue((byte)0x80);
            Assert.AreEqual(writeSize + 1, writer.Position, failMessage);
            Assert.AreEqual(writeSize + 1, writer.Length, failMessage);
            writer.WriteValue((byte)0xFF);
            Assert.AreEqual(writeSize + 2, writer.Position, failMessage);
            Assert.AreEqual(writeSize + 2, writer.Length, failMessage);
        }

        private void VerifyCheckBytes(FastBufferReader reader, int checkPosition, string failMessage = "")
        {
            reader.Seek(checkPosition);
            reader.TryBeginRead(2);

            reader.ReadByte(out byte value);
            Assert.AreEqual(0x80, value, failMessage);
            reader.ReadByte(out value);
            Assert.AreEqual(0xFF, value, failMessage);
        }

        private void VerifyPositionAndLength(FastBufferReader reader, int length, string failMessage = "")
        {
            Assert.AreEqual(0, reader.Position, failMessage);
            Assert.AreEqual(length, reader.Length, failMessage);
        }

        private FastBufferReader CommonChecks<T>(FastBufferWriter writer, T valueToTest, int writeSize, string failMessage = "") where T : unmanaged
        {
            WriteCheckBytes(writer, writeSize, failMessage);

            var reader = new FastBufferReader(writer, Allocator.Temp);

            VerifyPositionAndLength(reader, writer.Length, failMessage);

            VerifyCheckBytes(reader, writeSize, failMessage);

            reader.Seek(0);

            return reader;
        }

        private void RunWriteMethod<T>(string methodName, FastBufferWriter writer, in T value) where T : unmanaged
        {
            MethodInfo method = typeof(FastBufferWriter).GetMethod(methodName, new[] { typeof(T).MakeByRefType() });
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferWriter).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsArray)
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsGenericType)
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(writer, args);
        }

        private void RunWriteMethod<T>(string methodName, FastBufferWriter writer, in T[] value) where T : unmanaged
        {
            MethodInfo method = typeof(FastBufferWriter).GetMethod(methodName, new[] { typeof(T[]) });
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferWriter).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.IsArray)
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsGenericType)
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(writer, args);
        }

        private void RunWriteMethod<T>(string methodName, FastBufferWriter writer, in NativeArray<T> value) where T : unmanaged
        {
            MethodInfo method = typeof(FastBufferWriter).GetMethod(methodName, new[] { typeof(NativeArray<T>) });
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferWriter).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.Name.Contains("NativeArray"))
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(writer, args);
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        private void RunWriteMethod<T>(string methodName, FastBufferWriter writer, in NativeList<T> value) where T : unmanaged
        {
            MethodInfo method = typeof(FastBufferWriter).GetMethod(methodName, new[] { typeof(NativeList<T>) });
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferWriter).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.Name.Contains("NativeList"))
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(writer, args);
        }
#endif

        private void RunReadMethod<T>(string methodName, FastBufferReader reader, out T value) where T : unmanaged
        {
            MethodInfo method = typeof(FastBufferReader).GetMethod(methodName, new[] { typeof(T).MakeByRefType() });
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferReader).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsArray)
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsGenericType)
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }
            value = new T();

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(reader, args);
            value = (T)args[0];
        }

        private void RunReadMethod<T>(string methodName, FastBufferReader reader, out T[] value) where T : unmanaged
        {
            MethodInfo method = null;

            try
            {
                method = typeof(FastBufferReader).GetMethod(methodName, new[] { typeof(T[]).MakeByRefType() });
            }
            catch (AmbiguousMatchException)
            {
                // skip.
            }
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferReader).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.HasElementType || !candidateMethod.GetParameters()[0].ParameterType.GetElementType().IsArray)
                        {
                            continue;
                        }
                        if (candidateMethod.GetParameters()[0].ParameterType.IsGenericType)
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            value = new T[] { };

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(reader, args);
            value = (T[])args[0];
        }

        private void RunReadMethod<T>(string methodName, FastBufferReader reader, out NativeArray<T> value) where T : unmanaged
        {
            MethodInfo method = null;

            try
            {
                method = typeof(FastBufferReader).GetMethod(methodName, new[] { typeof(NativeArray<T>).MakeByRefType(), typeof(Allocator) });
            }
            catch (AmbiguousMatchException)
            {
                // skip.
            }
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferReader).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length < 2 || (candidateMethod.GetParameters().Length > 2 && !candidateMethod.GetParameters()[2].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.Name.Contains("NativeArray"))
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            value = new NativeArray<T>();

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            args[1] = Allocator.Temp;
            for (var i = 2; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(reader, args);
            value = (NativeArray<T>)args[0];
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        private void RunReadMethod<T>(string methodName, FastBufferReader reader, ref NativeList<T> value) where T : unmanaged
        {
            MethodInfo method = null;

            try
            {
                method = typeof(FastBufferReader).GetMethod(methodName, new[] { typeof(NativeList<T>).MakeByRefType() });
            }
            catch (AmbiguousMatchException)
            {
                // skip.
            }
            if (method == null)
            {
                foreach (var candidateMethod in typeof(FastBufferReader).GetMethods())
                {
                    if (candidateMethod.Name == methodName && candidateMethod.IsGenericMethodDefinition)
                    {
                        if (candidateMethod.GetParameters().Length == 0 || (candidateMethod.GetParameters().Length > 1 && !candidateMethod.GetParameters()[1].HasDefaultValue))
                        {
                            continue;
                        }
                        if (!candidateMethod.GetParameters()[0].ParameterType.Name.Contains("NativeList"))
                        {
                            continue;
                        }
                        try
                        {
                            method = candidateMethod.MakeGenericMethod(typeof(T));
                            break;
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            Assert.NotNull(method);

            object[] args = new object[method.GetParameters().Length];
            args[0] = value;
            for (var i = 1; i < args.Length; ++i)
            {
                args[i] = method.GetParameters()[i].DefaultValue;
            }
            method.Invoke(reader, args);
        }
#endif

        protected override unsafe void RunTypeTest<T>(T valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            Assert.AreEqual(sizeof(T), writeSize);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);

            using (writer)
            {
                Assert.IsTrue(writer.TryBeginWrite(writeSize + 2), "Writer denied write permission");

                var failMessage = $"RunTypeTest failed with type {typeof(T)} and value {valueToTest}";

                RunWriteMethod(nameof(FastBufferWriter.WriteValue), writer, valueToTest);

                var reader = CommonChecks(writer, valueToTest, writeSize, failMessage);

                using (reader)
                {
                    Assert.IsTrue(reader.TryBeginRead(FastBufferWriter.GetWriteSize<T>()));
                    RunReadMethod(nameof(FastBufferReader.ReadValue), reader, out T result);
                    Assert.AreEqual(valueToTest, result);
                }
            }
        }
        protected override unsafe void RunTypeTestSafe<T>(T valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);

            using (writer)
            {
                Assert.AreEqual(sizeof(T), writeSize);

                var failMessage = $"RunTypeTest failed with type {typeof(T)} and value {valueToTest}";

                RunWriteMethod(nameof(FastBufferWriter.WriteValueSafe), writer, valueToTest);


                var reader = CommonChecks(writer, valueToTest, writeSize, failMessage);

                using (reader)
                {
                    RunReadMethod(nameof(FastBufferReader.ReadValueSafe), reader, out T result);
                    Assert.AreEqual(valueToTest, result);
                }
            }
        }

        private void VerifyArrayEquality<T>(T[] value, T[] compareValue, int offset) where T : unmanaged
        {
            Assert.AreEqual(value.Length, compareValue.Length);

            for (var i = 0; i < value.Length; ++i)
            {
                Assert.AreEqual(value[i], compareValue[i]);
            }
        }

        private void VerifyArrayEquality<T>(NativeArray<T> value, NativeArray<T> compareValue, int offset) where T : unmanaged
        {
            Assert.AreEqual(value.Length, compareValue.Length);

            for (var i = 0; i < value.Length; ++i)
            {
                Assert.AreEqual(value[i], compareValue[i]);
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        private void VerifyArrayEquality<T>(NativeList<T> value, NativeList<T> compareValue, int offset) where T : unmanaged
        {
            Assert.AreEqual(value.Length, compareValue.Length);

            for (var i = 0; i < value.Length; ++i)
            {
                Assert.AreEqual(value[i], compareValue[i]);
            }
        }
#endif

        protected override unsafe void RunTypeArrayTest<T>(T[] valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);
                Assert.IsTrue(writer.TryBeginWrite(writeSize + 2), "Writer denied write permission");

                RunWriteMethod(nameof(FastBufferWriter.WriteValue), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    Assert.IsTrue(reader.TryBeginRead(writeSize));
                    RunReadMethod(nameof(FastBufferReader.ReadValue), reader, out T[] result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }

        protected override unsafe void RunTypeArrayTestSafe<T>(T[] valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);

                RunWriteMethod(nameof(FastBufferWriter.WriteValueSafe), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    RunReadMethod(nameof(FastBufferReader.ReadValueSafe), reader, out T[] result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }

        protected override unsafe void RunTypeNativeArrayTest<T>(NativeArray<T> valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);
                Assert.IsTrue(writer.TryBeginWrite(writeSize + 2), "Writer denied write permission");

                RunWriteMethod(nameof(FastBufferWriter.WriteValue), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    Assert.IsTrue(reader.TryBeginRead(writeSize));
                    RunReadMethod(nameof(FastBufferReader.ReadValue), reader, out NativeArray<T> result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }

        protected override unsafe void RunTypeNativeArrayTestSafe<T>(NativeArray<T> valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);

                RunWriteMethod(nameof(FastBufferWriter.WriteValueSafe), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    RunReadMethod(nameof(FastBufferReader.ReadValueSafe), reader, out NativeArray<T> result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        protected override unsafe void RunTypeNativeListTest<T>(NativeList<T> valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);
                Assert.IsTrue(writer.TryBeginWrite(writeSize + 2), "Writer denied write permission");

                RunWriteMethod(nameof(FastBufferWriter.WriteValue), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    Assert.IsTrue(reader.TryBeginRead(writeSize));
                    var result = new NativeList<T>(Allocator.Temp);
                    RunReadMethod(nameof(FastBufferReader.ReadValueInPlace), reader, ref result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }

        protected override unsafe void RunTypeNativeListTestSafe<T>(NativeList<T> valueToTest)
        {
            var writeSize = FastBufferWriter.GetWriteSize(valueToTest);
            var writer = new FastBufferWriter(writeSize + 2, Allocator.Temp);
            using (writer)
            {
                Assert.AreEqual(sizeof(int) + sizeof(T) * valueToTest.Length, writeSize);

                RunWriteMethod(nameof(FastBufferWriter.WriteValueSafe), writer, valueToTest);

                WriteCheckBytes(writer, writeSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    var result = new NativeList<T>(Allocator.Temp);
                    RunReadMethod(nameof(FastBufferReader.ReadValueSafeInPlace), reader, ref result);
                    VerifyArrayEquality(valueToTest, result, 0);

                    VerifyCheckBytes(reader, writeSize);
                }
            }
        }
#endif

        [Test]
        public void GivenFastBufferWriterContainingValue_WhenReadingUnmanagedType_ValueMatchesWhatWasWritten(
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(TestStruct))]
            Type testType,
            [Values] WriteType writeType)
        {
            BaseTypeTest(testType, writeType);
        }

        [Test]
        public void GivenFastBufferWriterContainingValue_WhenReadingArrayOfUnmanagedElementType_ValueMatchesWhatWasWritten(
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(TestStruct))]
            Type testType,
            [Values] WriteType writeType)
        {
            BaseArrayTypeTest(testType, writeType);
        }

        [Test]
        public void GivenFastBufferWriterContainingValue_WhenReadingNativeArrayOfUnmanagedElementType_ValueMatchesWhatWasWritten(
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(TestStruct))]
            Type testType,
            [Values] WriteType writeType)
        {
            BaseNativeArrayTypeTest(testType, writeType);
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        [Test]
        public void GivenFastBufferWriterContainingValue_WhenReadingNativeListOfUnmanagedElementType_ValueMatchesWhatWasWritten(
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(TestStruct))]
            Type testType,
            [Values] WriteType writeType)
        {
            BaseNativeListTypeTest(testType, writeType);
        }
#endif

        public unsafe void RunFixedStringTest<T>(T fixedStringValue, int numBytesWritten, WriteType writeType) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            fixedStringValue.Length = numBytesWritten;

            var serializedValueSize = FastBufferWriter.GetWriteSize(fixedStringValue);

            Assert.AreEqual(serializedValueSize, fixedStringValue.Length + sizeof(int));

            var writer = new FastBufferWriter(serializedValueSize + 3, Allocator.Temp);
            using (writer)
            {
                var offset = 0;
                switch (writeType)
                {
                    case WriteType.WriteDirect:
                        Assert.IsTrue(writer.TryBeginWrite(serializedValueSize + 2), "Writer denied write permission");
                        writer.WriteValue(fixedStringValue);
                        break;
                    case WriteType.WriteSafe:
                        writer.WriteValueSafe(fixedStringValue);
                        break;

                }

                WriteCheckBytes(writer, serializedValueSize + offset);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    var result = new T();
                    reader.ReadValueSafe(out result);
                    Assert.AreEqual(fixedStringValue, result);

                    VerifyCheckBytes(reader, serializedValueSize);
                }
            }
        }

        [TestCase(3, WriteType.WriteDirect)]
        [TestCase(5, WriteType.WriteSafe)]
        [TestCase(16, WriteType.WriteDirect)]
        [TestCase(29, WriteType.WriteSafe)]
        public void WhenReadingFixedString32Bytes_ValueIsReadCorrectly(int numBytesWritten, WriteType writeType)
        {
            // Repeats 01234567890123456789...
            string valueToTest = "";
            for (var i = 0; i < 29; ++i)
            {
                valueToTest += (i % 10).ToString();
            }

            var fixedStringValue = new FixedString32Bytes(valueToTest);

            RunFixedStringTest(fixedStringValue, numBytesWritten, writeType);
        }

        [TestCase(3, WriteType.WriteDirect)]
        [TestCase(5, WriteType.WriteSafe)]
        [TestCase(16, WriteType.WriteDirect)]
        [TestCase(29, WriteType.WriteSafe)]
        [TestCase(61, WriteType.WriteSafe)]
        public void WhenReadingFixedString64Bytes_ValueIsReadCorrectly(int numBytesWritten, WriteType writeType)
        {
            // Repeats 01234567890123456789...
            string valueToTest = "";
            for (var i = 0; i < 61; ++i)
            {
                valueToTest += (i % 10).ToString();
            }

            var fixedStringValue = new FixedString64Bytes(valueToTest);

            RunFixedStringTest(fixedStringValue, numBytesWritten, writeType);
        }

        [TestCase(3, WriteType.WriteDirect)]
        [TestCase(5, WriteType.WriteSafe)]
        [TestCase(16, WriteType.WriteDirect)]
        [TestCase(29, WriteType.WriteSafe)]
        [TestCase(61, WriteType.WriteSafe)]
        [TestCase(125, WriteType.WriteSafe)]
        public void WhenReadingFixedString128Bytes_ValueIsReadCorrectly(int numBytesWritten, WriteType writeType)
        {
            // Repeats 01234567890123456789...
            string valueToTest = "";
            for (var i = 0; i < 125; ++i)
            {
                valueToTest += (i % 10).ToString();
            }

            var fixedStringValue = new FixedString128Bytes(valueToTest);

            RunFixedStringTest(fixedStringValue, numBytesWritten, writeType);
        }

        [TestCase(3, WriteType.WriteDirect)]
        [TestCase(5, WriteType.WriteSafe)]
        [TestCase(16, WriteType.WriteDirect)]
        [TestCase(29, WriteType.WriteSafe)]
        [TestCase(61, WriteType.WriteSafe)]
        [TestCase(125, WriteType.WriteSafe)]
        [TestCase(509, WriteType.WriteSafe)]
        public void WhenReadingFixedString512Bytes_ValueIsReadCorrectly(int numBytesWritten, WriteType writeType)
        {
            // Repeats 01234567890123456789...
            string valueToTest = "";
            for (var i = 0; i < 509; ++i)
            {
                valueToTest += (i % 10).ToString();
            }

            var fixedStringValue = new FixedString512Bytes(valueToTest);

            RunFixedStringTest(fixedStringValue, numBytesWritten, writeType);
        }

        [TestCase(3, WriteType.WriteDirect)]
        [TestCase(5, WriteType.WriteSafe)]
        [TestCase(16, WriteType.WriteDirect)]
        [TestCase(29, WriteType.WriteSafe)]
        [TestCase(61, WriteType.WriteSafe)]
        [TestCase(125, WriteType.WriteSafe)]
        [TestCase(509, WriteType.WriteSafe)]
        [TestCase(4093, WriteType.WriteSafe)]
        public void WhenReadingFixedString4096Bytes_ValueIsReadCorrectly(int numBytesWritten, WriteType writeType)
        {
            // Repeats 01234567890123456789...
            string valueToTest = "";
            for (var i = 0; i < 4093; ++i)
            {
                valueToTest += (i % 10).ToString();
            }

            var fixedStringValue = new FixedString4096Bytes(valueToTest);

            RunFixedStringTest(fixedStringValue, numBytesWritten, writeType);
        }

        [TestCase(false, WriteType.WriteDirect)]
        [TestCase(false, WriteType.WriteSafe)]
        [TestCase(true, WriteType.WriteDirect)]
        [TestCase(true, WriteType.WriteSafe)]
        public void GivenFastBufferWriterContainingValue_WhenReadingString_ValueMatchesWhatWasWritten(bool oneByteChars, WriteType writeType)
        {
            string valueToTest = "Hello, I am a test string!";

            var serializedValueSize = FastBufferWriter.GetWriteSize(valueToTest, oneByteChars);

            var writer = new FastBufferWriter(serializedValueSize + 3, Allocator.Temp);
            using (writer)
            {
                switch (writeType)
                {
                    case WriteType.WriteDirect:
                        Assert.IsTrue(writer.TryBeginWrite(serializedValueSize + 2), "Writer denied write permission");
                        writer.WriteValue(valueToTest, oneByteChars);
                        break;
                    case WriteType.WriteSafe:
                        writer.WriteValueSafe(valueToTest, oneByteChars);
                        break;
                }

                WriteCheckBytes(writer, serializedValueSize);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length);

                    string result = null;
                    switch (writeType)
                    {
                        case WriteType.WriteDirect:
                            Assert.IsTrue(reader.TryBeginRead(serializedValueSize + 2), "Reader denied read permission");
                            reader.ReadValue(out result, oneByteChars);
                            break;
                        case WriteType.WriteSafe:
                            reader.ReadValueSafe(out result, oneByteChars);
                            break;
                    }
                    Assert.AreEqual(valueToTest, result);

                    VerifyCheckBytes(reader, serializedValueSize);
                }
            }
        }


        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 0)]

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 1)]
        [TestCase(7, 1)]

        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 2)]
        [TestCase(6, 2)]

        [TestCase(1, 3)]
        [TestCase(2, 3)]
        [TestCase(3, 3)]
        [TestCase(4, 3)]
        [TestCase(5, 3)]

        [TestCase(1, 4)]
        [TestCase(2, 4)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]

        [TestCase(1, 5)]
        [TestCase(2, 5)]
        [TestCase(3, 5)]

        [TestCase(1, 6)]
        [TestCase(2, 6)]

        [TestCase(1, 7)]
        public void GivenFastBufferWriterContainingValue_WhenReadingPartialValue_ValueMatchesWhatWasWritten(int count, int offset)
        {
            var random = new Random();
            var valueToTest = ((ulong)random.Next() << 32) + (ulong)random.Next();
            var writer = new FastBufferWriter(sizeof(ulong) + 2, Allocator.Temp);
            using (writer)
            {
                Assert.IsTrue(writer.TryBeginWrite(count + 2), "Writer denied write permission");
                writer.WritePartialValue(valueToTest, count, offset);

                var failMessage = $"TestReadingPartialValues failed with value {valueToTest}";
                WriteCheckBytes(writer, count, failMessage);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    VerifyPositionAndLength(reader, writer.Length, failMessage);
                    Assert.IsTrue(reader.TryBeginRead(count + 2), "Reader denied read permission");

                    ulong mask = 0;
                    for (var i = 0; i < count; ++i)
                    {
                        mask = (mask << 8) | 0b11111111;
                    }

                    mask <<= (offset * 8);

                    reader.ReadPartialValue(out ulong result, count, offset);
                    Assert.AreEqual(valueToTest & mask, result & mask, failMessage);
                    VerifyCheckBytes(reader, count, failMessage);
                }
            }
        }


        [Test]
        public unsafe void GivenFastBufferReaderInitializedFromFastBufferWriterContainingValue_WhenCallingToArray_ReturnedArrayMatchesContentOfWriter()
        {
            var testStruct = GetTestStruct();
            var requiredSize = FastBufferWriter.GetWriteSize(testStruct);
            var writer = new FastBufferWriter(requiredSize, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(requiredSize);
                writer.WriteValue(testStruct);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    var array = reader.ToArray();
                    var underlyingArray = writer.GetUnsafePtr();
                    for (var i = 0; i < array.Length; ++i)
                    {
                        Assert.AreEqual(array[i], underlyingArray[i]);
                    }
                }
            }
        }

        [Test]
        public void WhenCreatingAReaderFromAnEmptyArraySegment_LengthIsZero()
        {
            var bytes = new byte[] { };
            var input = new ArraySegment<byte>(bytes, 0, 0);
            using var reader = new FastBufferReader(input, Allocator.Temp);
            Assert.AreEqual(0, reader.Length);
        }

        [Test]
        public void WhenCreatingAReaderFromAnEmptyArray_LengthIsZero()
        {
            var input = new byte[] { };
            using var reader = new FastBufferReader(input, Allocator.Temp);
            Assert.AreEqual(0, reader.Length);
        }

        [Test]
        public void WhenCreatingAReaderFromAnEmptyNativeArray_LengthIsZero()
        {
            var input = new NativeArray<byte>(0, Allocator.Temp);
            using var reader = new FastBufferReader(input, Allocator.Temp);
            Assert.AreEqual(0, reader.Length);
        }

        [Test]
        public void WhenCreatingAReaderFromAnEmptyFastBufferWriter_LengthIsZero()
        {
            var input = new FastBufferWriter(0, Allocator.Temp);
            using var reader = new FastBufferReader(input, Allocator.Temp);
            Assert.AreEqual(0, reader.Length);
        }

        [Test]
        public void WhenCreatingAReaderFromAnEmptyBuffer_LengthIsZero()
        {
            var input = new byte[] { };
            unsafe
            {
                fixed (byte* ptr = input)
                {
                    using var reader = new FastBufferReader(ptr, Allocator.Temp, 0);
                    Assert.AreEqual(0, reader.Length);
                }
            }
        }

        [Test]
        public void WhenCreatingNewFastBufferReader_IsInitializedIsTrue()
        {
            var array = new NativeArray<byte>(100, Allocator.Temp);
            var reader = new FastBufferReader(array, Allocator.Temp);
            Assert.AreEqual(true, reader.IsInitialized);
            reader.Dispose();
            array.Dispose();
        }

        [Test]
        public void WhenDisposingFastBufferReader_IsInitializedIsFalse()
        {
            var array = new NativeArray<byte>(100, Allocator.Temp);
            var reader = new FastBufferReader(array, Allocator.Temp);
            reader.Dispose();
            Assert.AreEqual(false, reader.IsInitialized);
            array.Dispose();
            Assert.AreEqual(false, reader.IsInitialized);
        }

        [Test]
        public void WhenUsingDefaultFastBufferReader_IsInitializedIsFalse()
        {
            FastBufferReader writer = default;
            Assert.AreEqual(false, writer.IsInitialized);
        }

        [Test]
        public void WhenCallingReadByteWithoutCallingTryBeingReadFirst_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                Assert.Throws<OverflowException>(() => { emptyReader.ReadByte(out byte b); });
            }
        }

        [Test]
        public void WhenCallingReadBytesWithoutCallingTryBeingReadFirst_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            byte[] b = { 0, 1, 2 };
            using (emptyReader)
            {
                Assert.Throws<OverflowException>(() => { emptyReader.ReadBytes(ref b, 3); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithUnmanagedTypeWithoutCallingTryBeingReadFirst_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                Assert.Throws<OverflowException>(() => { emptyReader.ReadValue(out int i); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithByteArrayWithoutCallingTryBeingReadFirst_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                Assert.Throws<OverflowException>(() => { emptyReader.ReadValue(out byte[] b); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithStringWithoutCallingTryBeingReadFirst_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                Assert.Throws<OverflowException>(() => { emptyReader.ReadValue(out string s); });
            }
        }

        [Test]
        public void WhenCallingReadValueAfterCallingTryBeginWriteWithTooFewBytes_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(sizeof(int) - 1);
                Assert.Throws<OverflowException>(() => { emptyReader.ReadValue(out int i); });
            }
        }

        [Test]
        public void WhenCallingReadBytePastBoundaryMarkedByTryBeginWrite_OverflowExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(sizeof(int) - 1);
                emptyReader.ReadByte(out byte b);
                emptyReader.ReadByte(out b);
                emptyReader.ReadByte(out b);
                Assert.Throws<OverflowException>(() => { emptyReader.ReadByte(out b); });
            }
        }

        [Test]
        public void WhenCallingReadByteDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadByte(out byte b); });
            }
        }

        [Test]
        public void WhenCallingReadBytesDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                byte[] b = { 0, 1, 2 };
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadBytes(ref b, 3); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithUnmanagedTypeDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValue(out int i); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithByteArrayDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValue(out byte[] b); });
            }
        }

        [Test]
        public void WhenCallingReadValueWithStringDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValue(out string s); });
            }
        }

        [Test]
        public void WhenCallingReadByteSafeDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadByteSafe(out byte b); });
            }
        }

        [Test]
        public void WhenCallingReadBytesSafeDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                byte[] b = { 0, 1, 2 };
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadBytesSafe(ref b, 3); });
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithUnmanagedTypeDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValueSafe(out int i); });
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithByteArrayDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValueSafe(out byte[] b); });
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithStringDuringBitwiseContext_InvalidOperationExceptionIsThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                using var context = emptyReader.EnterBitwiseContext();
                Assert.Throws<InvalidOperationException>(() => { emptyReader.ReadValueSafe(out string s); });
            }
        }

        [Test]
        public void WhenCallingReadByteAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadByte(out byte theByte);
            }
        }

        [Test]
        public void WhenCallingReadBytesAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }

                byte[] theBytes = { 0, 1, 2 };
                emptyReader.ReadBytes(ref theBytes, 3);
            }
        }

        [Test]
        public void WhenCallingReadValueWithUnmanagedTypeAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValue(out int i);
            }
        }

        [Test]
        public void WhenCallingReadValueWithByteArrayAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValue(out byte[] theBytes);
            }
        }

        [Test]
        public void WhenCallingReadValueWithStringAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValue(out string s);
            }
        }

        [Test]
        public void WhenCallingReadByteSafeAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadByteSafe(out byte theByte);
            }
        }

        [Test]
        public void WhenCallingReadBytesSafeAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }

                byte[] theBytes = { 0, 1, 2 };
                emptyReader.ReadBytesSafe(ref theBytes, 3);
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithUnmanagedTypeAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValueSafe(out int i);
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithByteArrayAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValueSafe(out byte[] theBytes);
            }
        }

        [Test]
        public void WhenCallingReadValueSafeWithStringAfterExitingBitwiseContext_InvalidOperationExceptionIsNotThrown()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                using (var context = emptyReader.EnterBitwiseContext())
                {
                    context.ReadBit(out bool theBit);
                }
                emptyReader.ReadValueSafe(out string s);
            }
        }

        [Test]
        public void WhenCallingTryBeginRead_TheAllowedReadPositionIsMarkedRelativeToCurrentPosition()
        {
            var nativeArray = new NativeArray<byte>(100, Allocator.Temp);
            var emptyReader = new FastBufferReader(nativeArray, Allocator.Temp);

            using (emptyReader)
            {
                emptyReader.TryBeginRead(100);
                emptyReader.ReadByte(out byte b);
                emptyReader.TryBeginRead(1);
                emptyReader.ReadByte(out b);
                Assert.Throws<OverflowException>(() => { emptyReader.ReadByte(out byte b); });
            }
        }

        [Test]
        public void WhenReadingAfterSeeking_TheNewReadComesFromTheCorrectPosition()
        {
            var writer = new FastBufferWriter(100, Allocator.Temp);
            using (writer)
            {
                writer.WriteByteSafe(1);
                writer.WriteByteSafe(3);
                writer.WriteByteSafe(2);
                writer.WriteByteSafe(5);
                writer.WriteByteSafe(4);
                writer.WriteByteSafe(0);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    reader.Seek(5);
                    reader.ReadByteSafe(out byte b);
                    Assert.AreEqual(reader.Position, 6);
                    Assert.AreEqual(reader.Length, writer.Length);
                    Assert.AreEqual(0, b);

                    reader.Seek(0);
                    reader.ReadByteSafe(out b);
                    Assert.AreEqual(reader.Position, 1);
                    Assert.AreEqual(reader.Length, writer.Length);
                    Assert.AreEqual(1, b);

                    reader.Seek(10);
                    Assert.AreEqual(reader.Position, writer.Length);
                    Assert.AreEqual(reader.Length, writer.Length);

                    reader.Seek(2);
                    reader.ReadByteSafe(out b);
                    Assert.AreEqual(2, b);

                    reader.Seek(1);
                    reader.ReadByteSafe(out b);
                    Assert.AreEqual(3, b);

                    reader.Seek(4);
                    reader.ReadByteSafe(out b);
                    Assert.AreEqual(4, b);

                    reader.Seek(3);
                    reader.ReadByteSafe(out b);
                    Assert.AreEqual(5, b);

                    Assert.AreEqual(reader.Position, 4);
                    Assert.AreEqual(reader.Length, writer.Length);
                }
            }
        }

        [Test]
        public unsafe void WhenCallingTryBeginReadInternal_AllowedReadPositionDoesNotMoveBackward()
        {
            var reader = new FastBufferReader(new NativeArray<byte>(100, Allocator.Temp), Allocator.Temp);
            using (reader)
            {
                reader.TryBeginRead(25);
                reader.TryBeginReadInternal(5);
                Assert.AreEqual(reader.Handle->AllowedReadMark, 25);
            }
        }
    }
}
