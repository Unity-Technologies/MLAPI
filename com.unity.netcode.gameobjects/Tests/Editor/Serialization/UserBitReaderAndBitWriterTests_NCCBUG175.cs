using NUnit.Framework;
using Unity.Collections;

namespace Unity.Netcode.EditorTests
{
    internal class UserBitReaderAndBitWriterTests_NCCBUG175
    {

        [Test]
        public void WhenBitwiseWritingMoreThan8Bits_ValuesAreCorrect()
        {
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            ulong inVal = 123456789;

            for (int i = 0; i < 100; ++i)
            {
                writer.WriteValueSafe(i);
            }

            using (var bitWriter = writer.EnterBitwiseContext())
            {
                for (int i = 0; i < 16; ++i)
                {
                    Assert.IsTrue((bitWriter.TryBeginWriteBits(32)));
                    bitWriter.WriteBits(inVal, 31);
                    bitWriter.WriteBit(true);
                }
            }

            using var reader = new FastBufferReader(writer, Allocator.Temp);

            for (int i = 0; i < 100; ++i)
            {
                reader.ReadValueSafe(out int outVal);
                Assert.AreEqual(i, outVal);
            }

            using var bitReader = reader.EnterBitwiseContext();
            for (int i = 0; i < 16; ++i)
            {
                Assert.IsTrue(bitReader.TryBeginReadBits(32));
                bitReader.ReadBits(out ulong outVal, 31);
                bitReader.ReadBit(out bool bit);
                Assert.AreEqual(inVal, outVal);
                Assert.AreEqual(true, bit);
            }
        }

        [Test]
        public void WhenBitwiseReadingMoreThan8Bits_ValuesAreCorrect()
        {
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            ulong inVal = 123456789;

            for (int i = 0; i < 100; ++i)
            {
                writer.WriteValueSafe(i);
            }

            uint combined = (uint)inVal | (1u << 31);
            writer.WriteValueSafe(combined);
            writer.WriteValueSafe(combined);
            writer.WriteValueSafe(combined);

            using var reader = new FastBufferReader(writer, Allocator.Temp);

            for (int i = 0; i < 100; ++i)
            {
                reader.ReadValueSafe(out int outVal);
                Assert.AreEqual(i, outVal);
            }

            using (var bitReader = reader.EnterBitwiseContext())
            {
                Assert.IsTrue(bitReader.TryBeginReadBits(32));
                bitReader.ReadBits(out ulong outVal, 31);
                bitReader.ReadBit(out bool bit);
                Assert.AreEqual(inVal, outVal);
                Assert.AreEqual(true, bit);

                Assert.IsTrue(bitReader.TryBeginReadBits(32));
                bitReader.ReadBits(out outVal, 31);
                bitReader.ReadBit(out bit);
                Assert.AreEqual(inVal, outVal);
                Assert.AreEqual(true, bit);

                Assert.IsTrue(bitReader.TryBeginReadBits(32));
                bitReader.ReadBits(out outVal, 31);
                bitReader.ReadBit(out bit);
                Assert.AreEqual(inVal, outVal);
                Assert.AreEqual(true, bit);
            }
        }
    }
}
