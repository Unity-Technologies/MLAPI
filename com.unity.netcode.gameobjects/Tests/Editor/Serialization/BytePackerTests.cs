using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Random = System.Random;

namespace Unity.Netcode.EditorTests
{
    internal class BytePackerTests
    {
        private enum ByteEnum : byte
        {
            A,
            B,
            C
        }

        private enum SByteEnum : sbyte
        {
            A,
            B,
            C
        }

        private enum ShortEnum : short
        {
            A,
            B,
            C
        }

        private enum UShortEnum : ushort
        {
            A,
            B,
            C
        }

        private enum IntEnum
        {
            A,
            B,
            C
        }

        private enum UIntEnum : uint
        {
            A,
            B,
            C
        }

        private enum LongEnum : long
        {
            A,
            B,
            C
        }

        private enum ULongEnum : ulong
        {
            A,
            B,
            C
        }

        public enum WriteType
        {
            WriteDirect,
            WriteAsObject
        }

        private unsafe void VerifyBytewiseEquality<T>(T value, T otherValue) where T : unmanaged
        {
            byte* asBytePointer = (byte*)&value;
            byte* otherBytePointer = (byte*)&otherValue;
            for (var i = 0; i < sizeof(T); ++i)
            {
                Assert.AreEqual(asBytePointer[i], otherBytePointer[i]);
            }
        }

        private unsafe void RunTypeTest<T>(T value) where T : unmanaged
        {
            var writer = new FastBufferWriter(sizeof(T) * 2, Allocator.Temp);
            using (writer)
            {
                BytePacker.WriteValuePacked(writer, (dynamic)value);
                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {

                    var outVal = new T();
                    MethodInfo method;
                    if (value is Enum)
                    {
                        method = typeof(ByteUnpacker).GetMethods().Single(x =>
                                x.Name == "ReadValuePacked" && x.IsGenericMethodDefinition)
                            .MakeGenericMethod(typeof(T));
                    }
                    else
                    {
                        method = typeof(ByteUnpacker).GetMethod("ReadValuePacked",
                            new[] { typeof(FastBufferReader), typeof(T).MakeByRefType() });
                    }

                    object[] args = { reader, outVal };
                    method.Invoke(null, args);
                    outVal = (T)args[1];
                    Assert.AreEqual(value, outVal);
                    VerifyBytewiseEquality(value, outVal);
                }
            }
        }

        private int GetByteCount64Bits(ulong value)
        {

            if (value <= 0b0000_1111)
            {
                return 1;
            }

            if (value <= 0b0000_1111_1111_1111)
            {
                return 2;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111)
            {
                return 3;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111_1111_1111)
            {
                return 4;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111_1111_1111_1111_1111)
            {
                return 5;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111)
            {
                return 6;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111)
            {
                return 7;
            }

            if (value <= 0b0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111)
            {
                return 8;
            }

            return 9;
        }

        private int GetByteCount32Bits(uint value)
        {

            if (value <= 0b0001_1111)
            {
                return 1;
            }

            if (value <= 0b0001_1111_1111_1111)
            {
                return 2;
            }

            if (value <= 0b0001_1111_1111_1111_1111_1111)
            {
                return 3;
            }

            if (value <= 0b0001_1111_1111_1111_1111_1111_1111_1111)
            {
                return 4;
            }

            return 5;
        }

        private int GetByteCount16Bits(ushort value)
        {

            if (value <= 0b0011_1111)
            {
                return 1;
            }
            if (value <= 0b0011_1111_1111_1111)
            {
                return 2;
            }

            return 3;
        }

        private ulong Get64BitEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
                return value;
            }
        }

        private long Get64BitSignedEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out long value);
                return value;
            }
        }

        private uint Get32BitEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out uint value);
                return value;
            }
        }

        private int Get32BitSignedEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int value);
                return value;
            }
        }

        private ushort Get16BitEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out ushort value);
                return value;
            }
        }

        private short Get16BitSignedEncodedValue(FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            using (reader)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out short value);
                return value;
            }
        }

        [Test]
        public void TestBitPacking64BitsUnsigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(8);
                ulong value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b1111);
                Assert.AreEqual(value, Get64BitEncodedValue(writer));

                for (var i = 0; i < 64; ++i)
                {
                    value = 1UL << i;
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount64Bits(value), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount64Bits(value), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get64BitEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (1UL << i) | (1UL << j);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount64Bits(value), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount64Bits(value), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get64BitEncodedValue(writer));
                    }
                }
            }
        }

        [Test]
        public void TestBitPacking64BitsSigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(8);
                long value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b1111);
                Assert.AreEqual(value, Get64BitSignedEncodedValue(writer));

                for (var i = 0; i < 64; ++i)
                {
                    value = 1U << i;
                    ulong zzvalue = Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get64BitSignedEncodedValue(writer));

                    value = -value;
                    zzvalue = Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get64BitSignedEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (1U << i) | (1U << j);
                        zzvalue = Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get64BitSignedEncodedValue(writer));

                        value = -value;
                        zzvalue = Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount64Bits(zzvalue), writer.ToArray()[0] & 0b1111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get64BitSignedEncodedValue(writer));
                    }
                }
            }
        }

        [Test]
        public void TestBitPacking32BitsUnsigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(4);
                uint value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b111);
                Assert.AreEqual(value, Get32BitEncodedValue(writer));

                for (var i = 0; i < 32; ++i)
                {
                    value = 1U << i;
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount32Bits(value), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount32Bits(value), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get32BitEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (1U << i) | (1U << j);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount32Bits(value), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount32Bits(value), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get32BitEncodedValue(writer));
                    }
                }
            }
        }

        [Test]
        public void TestBitPacking32BitsSigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(4);
                int value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b111);
                Assert.AreEqual(value, Get32BitEncodedValue(writer));

                for (var i = 0; i < 32; ++i)
                {
                    value = 1 << i;
                    uint zzvalue = (uint)Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get32BitSignedEncodedValue(writer));

                    value = -value;
                    zzvalue = (uint)Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get32BitSignedEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (1 << i) | (1 << j);
                        zzvalue = (uint)Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get32BitSignedEncodedValue(writer));

                        value = -value;
                        zzvalue = (uint)Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount32Bits(zzvalue), writer.ToArray()[0] & 0b111, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get32BitSignedEncodedValue(writer));
                    }
                }
            }
        }

        [Test]
        public void TestBitPacking16BitsUnsigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(2);
                ushort value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b11);
                Assert.AreEqual(value, Get16BitEncodedValue(writer));

                for (var i = 0; i < 16; ++i)
                {
                    value = (ushort)(1U << i);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount16Bits(value), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount16Bits(value), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get16BitEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (ushort)((1U << i) | (1U << j));
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount16Bits(value), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount16Bits(value), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get16BitEncodedValue(writer));
                    }
                }
            }
        }
        [Test]
        public void TestBitPacking16BitsSigned()
        {
            var writer = new FastBufferWriter(9, Allocator.Temp);

            using (writer)
            {
                writer.TryBeginWrite(2);
                short value = 0;
                BytePacker.WriteValueBitPacked(writer, value);
                Assert.AreEqual(1, writer.Position);
                Assert.AreEqual(1, writer.ToArray()[0] & 0b11);
                Assert.AreEqual(value, Get16BitEncodedValue(writer));

                for (var i = 0; i < 16; ++i)
                {
                    value = (short)(1 << i);
                    ushort zzvalue = (ushort)Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get16BitSignedEncodedValue(writer));

                    value = (short)-value;
                    zzvalue = (ushort)Arithmetic.ZigZagEncode(value);
                    writer.Seek(0);
                    writer.Truncate();
                    BytePacker.WriteValueBitPacked(writer, value);
                    Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.Position, $"Failed on {value} ({i})");
                    Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i})");
                    Assert.AreEqual(value, Get16BitSignedEncodedValue(writer));

                    for (var j = 0; j < 8; ++j)
                    {
                        value = (short)((1 << i) | (1 << j));
                        zzvalue = (ushort)Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get16BitSignedEncodedValue(writer));

                        value = (short)-value;
                        zzvalue = (ushort)Arithmetic.ZigZagEncode(value);
                        writer.Seek(0);
                        writer.Truncate();
                        BytePacker.WriteValueBitPacked(writer, value);
                        Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.Position, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(GetByteCount16Bits(zzvalue), writer.ToArray()[0] & 0b11, $"Failed on {value} ({i}, {j})");
                        Assert.AreEqual(value, Get16BitSignedEncodedValue(writer));
                    }
                }
            }
        }

        [Test]
        public void TestPackingBasicTypes(
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3), typeof(Vector4),
                typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Ray), typeof(Ray2D))]
            Type testType,
            [Values] WriteType writeType)
        {
            var random = new Random();

            if (testType == typeof(byte))
            {
                byte b = (byte)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(b);
                }
            }
            else if (testType == typeof(sbyte))
            {
                sbyte sb = (sbyte)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(sb);
                }
            }
            else if (testType == typeof(short))
            {
                short s = (short)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(s);
                }
            }
            else if (testType == typeof(ushort))
            {
                ushort us = (ushort)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(us);
                }
            }
            else if (testType == typeof(int))
            {
                int i = random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(i);
                }
            }
            else if (testType == typeof(uint))
            {
                uint ui = (uint)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(ui);
                }
            }
            else if (testType == typeof(long))
            {
                long l = ((long)random.Next() << 32) + random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(l);
                }
            }
            else if (testType == typeof(ulong))
            {
                ulong ul = ((ulong)random.Next() << 32) + (ulong)random.Next();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(ul);
                }
            }
            else if (testType == typeof(bool))
            {
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(true);
                }
            }
            else if (testType == typeof(char))
            {
                char c = 'a';
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(c);
                }

                c = '\u263a';
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(c);
                }
            }
            else if (testType == typeof(float))
            {
                float f = (float)random.NextDouble();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(f);
                }
            }
            else if (testType == typeof(double))
            {
                double d = random.NextDouble();
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(d);
                }
            }
            else if (testType == typeof(ByteEnum))
            {
                ByteEnum e = ByteEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(SByteEnum))
            {
                SByteEnum e = SByteEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(ShortEnum))
            {
                ShortEnum e = ShortEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(UShortEnum))
            {
                UShortEnum e = UShortEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(IntEnum))
            {
                IntEnum e = IntEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(UIntEnum))
            {
                UIntEnum e = UIntEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(LongEnum))
            {
                LongEnum e = LongEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(ULongEnum))
            {
                ULongEnum e = ULongEnum.C;
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(e);
                }
            }
            else if (testType == typeof(Vector2))
            {
                var v = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Vector3))
            {
                var v = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Vector4))
            {
                var v = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Quaternion))
            {
                var v = new Quaternion((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Color))
            {
                var v = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Color32))
            {
                var v = new Color32((byte)random.Next(), (byte)random.Next(), (byte)random.Next(), (byte)random.Next());
                if (writeType == WriteType.WriteDirect)
                {
                    RunTypeTest(v);
                }
            }
            else if (testType == typeof(Ray))
            {
                // Rays need special handling on the equality checks because the constructor normalizes direction
                // Which can cause slight variations in the result
                var v = new Ray(
                    new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()),
                    new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));
                if (writeType == WriteType.WriteDirect)
                {
                    unsafe
                    {
                        var writer = new FastBufferWriter(sizeof(Ray) * 2, Allocator.Temp);
                        using (writer)
                        {
                            BytePacker.WriteValuePacked(writer, v);
                            var reader = new FastBufferReader(writer, Allocator.Temp);
                            using (reader)
                            {
                                ByteUnpacker.ReadValuePacked(reader, out Ray outVal);
                                Assert.AreEqual(v.origin, outVal.origin);
                                Assert.AreEqual(v.direction.x, outVal.direction.x, 0.00001);
                                Assert.AreEqual(v.direction.y, outVal.direction.y, 0.00001);
                                Assert.AreEqual(v.direction.z, outVal.direction.z, 0.00001);
                            }
                        }
                    }
                }
            }
            else if (testType == typeof(Ray2D))
            {
                // Rays need special handling on the equality checks because the constructor normalizes direction
                // Which can cause slight variations in the result
                var v = new Ray2D(
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
                if (writeType == WriteType.WriteDirect)
                {
                    unsafe
                    {
                        var writer = new FastBufferWriter(sizeof(Ray2D) * 2, Allocator.Temp);
                        using (writer)
                        {
                            BytePacker.WriteValuePacked(writer, v);
                            var reader = new FastBufferReader(writer, Allocator.Temp);
                            using (reader)
                            {
                                ByteUnpacker.ReadValuePacked(reader, out Ray2D outVal);
                                Assert.AreEqual(v.origin, outVal.origin);
                                Assert.AreEqual(v.direction.x, outVal.direction.x, 0.00001);
                                Assert.AreEqual(v.direction.y, outVal.direction.y, 0.00001);
                            }
                        }
                    }
                }
            }
            else
            {
                Assert.Fail("No type handler was provided for this type in the test!");
            }
        }
    }
}
