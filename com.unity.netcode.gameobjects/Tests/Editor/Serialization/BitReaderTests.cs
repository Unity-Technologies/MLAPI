using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Netcode.EditorTests
{
    internal class BitReaderTests
    {
        [Test]
        public void TestReadingOneBit()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBit(true);

                    bitWriter.WriteBit(true);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                }

                writer.WriteByte(0b11111111);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    Assert.IsTrue(reader.TryBeginRead(3));
                    using (var bitReader = reader.EnterBitwiseContext())
                    {
                        bool b;
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);
                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);

                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);

                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);
                        bitReader.ReadBit(out b);
                        Assert.IsFalse(b);
                        bitReader.ReadBit(out b);
                        Assert.IsTrue(b);
                    }

                    reader.ReadByte(out byte lastByte);
                    Assert.AreEqual(0b11111111, lastByte);
                }
            }
        }
        [Test]
        public unsafe void TestTryBeginReadBits()
        {
            var nativeArray = new NativeArray<byte>(4, Allocator.Temp);
            var reader = new FastBufferReader(nativeArray, Allocator.Temp);
            nativeArray.Dispose();
            using (reader)
            {
                int* asInt = (int*)reader.GetUnsafePtr();
                *asInt = 0b11111111_00001010_10101011;

                using (var bitReader = reader.EnterBitwiseContext())
                {
                    Assert.Throws<InvalidOperationException>(() => reader.TryBeginRead(1));
                    Assert.Throws<InvalidOperationException>(() => reader.TryBeginReadValue(1));
                    Assert.IsTrue(bitReader.TryBeginReadBits(1));
                    bitReader.ReadBit(out bool b);
                    Assert.IsTrue(b);

                    // Can't use Assert.Throws() because ref struct BitWriter can't be captured in a lambda
                    try
                    {
                        bitReader.ReadBit(out b);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }
                    Assert.IsTrue(bitReader.TryBeginReadBits(3));
                    bitReader.ReadBit(out b);
                    Assert.IsTrue(b);
                    bitReader.ReadBit(out b);
                    Assert.IsFalse(b);
                    bitReader.ReadBit(out b);
                    Assert.IsTrue(b);

                    byte byteVal;
                    try
                    {
                        bitReader.ReadBits(out byteVal, 4);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }

                    try
                    {
                        bitReader.ReadBits(out byteVal, 1);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }
                    Assert.IsTrue(bitReader.TryBeginReadBits(3));

                    try
                    {
                        bitReader.ReadBits(out byteVal, 4);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }
                    Assert.IsTrue(bitReader.TryBeginReadBits(4));
                    bitReader.ReadBits(out byteVal, 3);
                    Assert.AreEqual(0b010, byteVal);

                    Assert.IsTrue(bitReader.TryBeginReadBits(5));

                    bitReader.ReadBits(out byteVal, 5);
                    Assert.AreEqual(0b10101, byteVal);
                }

                Assert.AreEqual(2, reader.Position);

                Assert.IsTrue(reader.TryBeginRead(1));
                reader.ReadByte(out byte nextByte);
                Assert.AreEqual(0b11111111, nextByte);

                Assert.IsTrue(reader.TryBeginRead(1));
                reader.ReadByte(out nextByte);
                Assert.AreEqual(0b00000000, nextByte);

                Assert.IsFalse(reader.TryBeginRead(1));
                using (var bitReader = reader.EnterBitwiseContext())
                {
                    Assert.IsFalse(bitReader.TryBeginReadBits(1));
                }
            }
        }

        [Test]
        public void TestReadingMultipleBits()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBits(0b11111111, 1);
                    bitWriter.WriteBits(0b11111111, 1);
                    bitWriter.WriteBits(0b11111110, 2);
                    bitWriter.WriteBits(0b11111000, 4);
                    bitWriter.WriteBits(0b11111010, 4);
                }
                writer.WriteByte(0b11111111);


                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    Assert.IsTrue(reader.TryBeginRead(3));
                    using (var bitReader = reader.EnterBitwiseContext())
                    {
                        byte b;
                        bitReader.ReadBits(out b, 1);
                        Assert.AreEqual(0b1, b);

                        bitReader.ReadBits(out b, 1);
                        Assert.AreEqual(0b1, b);

                        bitReader.ReadBits(out b, 2);
                        Assert.AreEqual(0b10, b);

                        bitReader.ReadBits(out b, 4);
                        Assert.AreEqual(0b1000, b);

                        bitReader.ReadBits(out b, 4);
                        Assert.AreEqual(0b1010, b);
                    }

                    reader.ReadByte(out byte lastByte);
                    Assert.AreEqual(0b11111111, lastByte);
                }
            }
        }

        [Test]
        public void TestReadingMultipleBitsToLongs()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBits(0b11111111UL, 1);
                    bitWriter.WriteBits(0b11111111UL, 1);
                    bitWriter.WriteBits(0b11111110UL, 2);
                    bitWriter.WriteBits(0b11111000UL, 4);
                    bitWriter.WriteBits(0b11111010UL, 4);
                }

                writer.WriteByte(0b11111111);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    Assert.IsTrue(reader.TryBeginRead(3));
                    using (var bitReader = reader.EnterBitwiseContext())
                    {
                        ulong ul;
                        bitReader.ReadBits(out ul, 1);
                        Assert.AreEqual(0b1, ul);

                        bitReader.ReadBits(out ul, 1);
                        Assert.AreEqual(0b1, ul);

                        bitReader.ReadBits(out ul, 2);
                        Assert.AreEqual(0b10, ul);

                        bitReader.ReadBits(out ul, 4);
                        Assert.AreEqual(0b1000, ul);

                        bitReader.ReadBits(out ul, 4);
                        Assert.AreEqual(0b1010, ul);
                    }

                    reader.ReadByte(out byte lastByte);
                    Assert.AreEqual(0b11111111, lastByte);
                }
            }
        }

        [Test]
        public unsafe void TestReadingMultipleBytesToLongs([Range(1U, 64U)] uint numBits)
        {
            ulong value = 0xFFFFFFFFFFFFFFFF;
            var reader = new FastBufferReader((byte*)&value, Allocator.Temp, sizeof(ulong));
            using (reader)
            {
                ulong* asUlong = (ulong*)reader.GetUnsafePtr();

                Assert.AreEqual(value, *asUlong);
                var mask = 0UL;
                for (var i = 0; i < numBits; ++i)
                {
                    mask |= (1UL << i);
                }

                ulong readValue;

                Assert.IsTrue(reader.TryBeginRead(sizeof(ulong)));
                using (var bitReader = reader.EnterBitwiseContext())
                {
                    bitReader.ReadBits(out readValue, numBits);
                }
                Assert.AreEqual(value & mask, readValue);
            }
        }

        [Test]
        public unsafe void TestReadingMultipleBytesToLongsMisaligned([Range(1U, 63U)] uint numBits)
        {
            ulong value = 0b01010101_10101010_01010101_10101010_01010101_10101010_01010101_10101010;
            var reader = new FastBufferReader((byte*)&value, Allocator.Temp, sizeof(ulong));
            using (reader)
            {
                ulong* asUlong = (ulong*)reader.GetUnsafePtr();

                Assert.AreEqual(value, *asUlong);
                var mask = 0UL;
                for (var i = 0; i < numBits; ++i)
                {
                    mask |= (1UL << i);
                }

                ulong readValue;

                Assert.IsTrue(reader.TryBeginRead(sizeof(ulong)));
                using (var bitReader = reader.EnterBitwiseContext())
                {
                    bitReader.ReadBit(out bool unused);
                    bitReader.ReadBits(out readValue, numBits);
                }
                Assert.AreEqual((value >> 1) & mask, readValue);
            }
        }

        [Test]
        public unsafe void TestReadingBitsThrowsIfTryBeginReadNotCalled()
        {
            var nativeArray = new NativeArray<byte>(4, Allocator.Temp);
            var reader = new FastBufferReader(nativeArray, Allocator.Temp);
            nativeArray.Dispose();
            using (reader)
            {
                int* asInt = (int*)reader.GetUnsafePtr();
                *asInt = 0b11111111_00001010_10101011;

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitReader = reader.EnterBitwiseContext();
                    bitReader.ReadBit(out bool b);
                });

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitReader = reader.EnterBitwiseContext();
                    bitReader.ReadBits(out byte b, 1);
                });

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitReader = reader.EnterBitwiseContext();
                    bitReader.ReadBits(out ulong ul, 1);
                });

                Assert.AreEqual(0, reader.Position);

                Assert.Throws<OverflowException>(() =>
                {
                    Assert.IsTrue(reader.TryBeginRead(1));
                    using var bitReader = reader.EnterBitwiseContext();
                    ulong ul;
                    try
                    {
                        bitReader.ReadBits(out ul, 4);
                        bitReader.ReadBits(out ul, 4);
                    }
                    catch (OverflowException)
                    {
                        Assert.Fail("Overflow exception was thrown too early.");
                        throw;
                    }
                    bitReader.ReadBits(out ul, 4);
                });

            }
        }
    }
}
