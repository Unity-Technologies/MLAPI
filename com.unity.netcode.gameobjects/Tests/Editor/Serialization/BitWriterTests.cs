using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Netcode.EditorTests
{
    internal class BitWriterTests
    {
        [Test]
        public unsafe void TestWritingOneBit()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                int* asInt = (int*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asInt);

                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b1, *asInt);

                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b11, *asInt);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b1011, *asInt);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b10001011, *asInt);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b1010_10001011, *asInt);
                }

                Assert.AreEqual(2, writer.Position);
                Assert.AreEqual(0b1010_10001011, *asInt);

                writer.WriteByte(0b11111111);
                Assert.AreEqual(0b11111111_00001010_10001011, *asInt);
            }
        }
        [Test]
        public unsafe void TestTryBeginWriteBits()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                int* asInt = (int*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asInt);

                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    Assert.Throws<InvalidOperationException>(() => writer.TryBeginWrite(1));
                    Assert.Throws<InvalidOperationException>(() => writer.TryBeginWriteValue(1));
                    Assert.IsTrue(bitWriter.TryBeginWriteBits(1));
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b1, *asInt);

                    // Can't use Assert.Throws() because ref struct BitWriter can't be captured in a lambda
                    try
                    {
                        bitWriter.WriteBit(true);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }

                    Assert.IsTrue(bitWriter.TryBeginWriteBits(3));
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b11, *asInt);

                    bitWriter.WriteBit(false);
                    bitWriter.WriteBit(true);
                    Assert.AreEqual(0b1011, *asInt);

                    try
                    {
                        bitWriter.WriteBits(0b11111111, 4);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }

                    try
                    {
                        bitWriter.WriteBits(0b11111111, 1);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }
                    Assert.IsTrue(bitWriter.TryBeginWriteBits(3));

                    try
                    {
                        bitWriter.WriteBits(0b11111111, 4);
                    }
                    catch (OverflowException)
                    {
                        // Should get called here.
                    }
                    Assert.IsTrue(bitWriter.TryBeginWriteBits(4));

                    bitWriter.WriteBits(0b11111010, 3);

                    Assert.AreEqual(0b00101011, *asInt);

                    Assert.IsTrue(bitWriter.TryBeginWriteBits(5));

                    bitWriter.WriteBits(0b11110101, 5);
                    Assert.AreEqual(0b1010_10101011, *asInt);
                }

                Assert.AreEqual(2, writer.Position);
                Assert.AreEqual(0b1010_10101011, *asInt);

                Assert.IsTrue(writer.TryBeginWrite(1));
                writer.WriteByte(0b11111111);
                Assert.AreEqual(0b11111111_00001010_10101011, *asInt);

                Assert.IsTrue(writer.TryBeginWrite(1));
                writer.WriteByte(0b00000000);
                Assert.AreEqual(0b11111111_00001010_10101011, *asInt);

                Assert.IsFalse(writer.TryBeginWrite(1));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    Assert.IsFalse(bitWriter.TryBeginWriteBits(1));
                }
            }
        }

        [Test]
        public unsafe void TestWritingMultipleBits()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                int* asInt = (int*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asInt);

                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBits(0b11111111, 1);
                    Assert.AreEqual(0b1, *asInt);

                    bitWriter.WriteBits(0b11111111, 1);
                    Assert.AreEqual(0b11, *asInt);

                    bitWriter.WriteBits(0b11111110, 2);
                    Assert.AreEqual(0b1011, *asInt);

                    bitWriter.WriteBits(0b11111000, 4);
                    Assert.AreEqual(0b10001011, *asInt);

                    bitWriter.WriteBits(0b11111010, 4);
                    Assert.AreEqual(0b1010_10001011, *asInt);
                }

                Assert.AreEqual(2, writer.Position);
                Assert.AreEqual(0b1010_10001011, *asInt);

                writer.WriteByte(0b11111111);
                Assert.AreEqual(0b11111111_00001010_10001011, *asInt);
            }
        }

        [Test]
        public unsafe void TestWritingMultipleBitsFromLongs()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                int* asInt = (int*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asInt);

                Assert.IsTrue(writer.TryBeginWrite(3));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBits(0b11111111UL, 1);
                    Assert.AreEqual(0b1, *asInt);

                    bitWriter.WriteBits(0b11111111UL, 1);
                    Assert.AreEqual(0b11, *asInt);

                    bitWriter.WriteBits(0b11111110UL, 2);
                    Assert.AreEqual(0b1011, *asInt);

                    bitWriter.WriteBits(0b11111000UL, 4);
                    Assert.AreEqual(0b10001011, *asInt);

                    bitWriter.WriteBits(0b11111010UL, 4);
                    Assert.AreEqual(0b1010_10001011, *asInt);
                }

                Assert.AreEqual(2, writer.Position);
                Assert.AreEqual(0b1010_10001011, *asInt);

                writer.WriteByte(0b11111111);
                Assert.AreEqual(0b11111111_00001010_10001011, *asInt);
            }
        }

        [Test]
        public unsafe void TestWritingMultipleBytesFromLongs([Range(1U, 64U)] uint numBits)
        {
            var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
            using (writer)
            {
                ulong* asUlong = (ulong*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asUlong);
                var mask = 0UL;
                for (var i = 0; i < numBits; ++i)
                {
                    mask |= (1UL << i);
                }

                ulong value = 0xFFFFFFFFFFFFFFFF;

                Assert.IsTrue(writer.TryBeginWrite(sizeof(ulong)));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBits(value, numBits);
                }
                Assert.AreEqual(value & mask, *asUlong);
            }
        }

        [Test]
        public unsafe void TestWritingMultipleBytesFromLongsMisaligned([Range(1U, 63U)] uint numBits)
        {
            var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
            using (writer)
            {
                ulong* asUlong = (ulong*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asUlong);
                var mask = 0UL;
                for (var i = 0; i < numBits; ++i)
                {
                    mask |= (1UL << i);
                }

                ulong value = 0xFFFFFFFFFFFFFFFF;

                Assert.IsTrue(writer.TryBeginWrite(sizeof(ulong)));
                using (var bitWriter = writer.EnterBitwiseContext())
                {
                    bitWriter.WriteBit(false);
                    bitWriter.WriteBits(value, numBits);
                }
                Assert.AreEqual(value & mask, *asUlong >> 1);
            }
        }

        [Test]
        public unsafe void TestWritingBitsThrowsIfTryBeginWriteNotCalled()
        {
            var writer = new FastBufferWriter(4, Allocator.Temp);
            using (writer)
            {
                int* asInt = (int*)writer.GetUnsafePtr();

                Assert.AreEqual(0, *asInt);

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitWriter = writer.EnterBitwiseContext();
                    bitWriter.WriteBit(true);
                });

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitWriter = writer.EnterBitwiseContext();
                    bitWriter.WriteBit(false);
                });

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitWriter = writer.EnterBitwiseContext();
                    bitWriter.WriteBits(0b11111111, 1);
                });

                Assert.Throws<OverflowException>(() =>
                {
                    using var bitWriter = writer.EnterBitwiseContext();
                    bitWriter.WriteBits(0b11111111UL, 1);
                });

                Assert.AreEqual(0, writer.Position);
                Assert.AreEqual(0, *asInt);

                writer.WriteByteSafe(0b11111111);
                Assert.AreEqual(0b11111111, *asInt);


                Assert.Throws<OverflowException>(() =>
                {
                    Assert.IsTrue(writer.TryBeginWrite(1));
                    using var bitWriter = writer.EnterBitwiseContext();
                    try
                    {
                        bitWriter.WriteBits(0b11111111UL, 4);
                        bitWriter.WriteBits(0b11111111UL, 4);
                    }
                    catch (OverflowException)
                    {
                        Assert.Fail("Overflow exception was thrown too early.");
                    }
                    bitWriter.WriteBits(0b11111111UL, 1);
                });

            }
        }
    }
}
