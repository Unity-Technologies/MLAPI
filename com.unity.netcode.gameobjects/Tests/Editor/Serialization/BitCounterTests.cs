using NUnit.Framework;

namespace Unity.Netcode.EditorTests
{
    internal class BitCounterTests
    {
        [Test]
        public void WhenCountingUsedBitsIn64BitValue_ResultMatchesHighBitSetPlusOne([Range(0, 63)] int highBit)
        {
            if (highBit == 0)
            {
                ulong value = 0;
                // 0 is a special case. All values are considered at least 1 bit.
                Assert.AreEqual(1, BitCounter.GetUsedBitCount(value));
            }
            else
            {
                ulong value = 1UL << highBit;
                Assert.AreEqual(highBit + 1, BitCounter.GetUsedBitCount(value));
            }
        }

        [Test]
        public void WhenCountingUsedBitsIn32BitValue_ResultMatchesHighBitSetPlusOne([Range(0, 31)] int highBit)
        {
            if (highBit == 0)
            {
                uint value = 0;
                // 0 is a special case. All values are considered at least 1 bit.
                Assert.AreEqual(1, BitCounter.GetUsedBitCount(value));
            }
            else
            {
                uint value = 1U << highBit;
                Assert.AreEqual(highBit + 1, BitCounter.GetUsedBitCount(value));
            }
        }

        [Test]
        public void WhenCountingUsedBytesIn64BitValue_ResultMatchesHighBitSetOver8PlusOne([Range(0, 63)] int highBit)
        {
            if (highBit == 0)
            {
                ulong value = 0;
                // 0 is a special case. All values are considered at least 1 byte.
                Assert.AreEqual(1, BitCounter.GetUsedByteCount(value));
            }
            else
            {
                ulong value = 1UL << highBit;
                Assert.AreEqual(highBit / 8 + 1, BitCounter.GetUsedByteCount(value));
            }
        }

        [Test]
        public void WhenCountingUsedBytesIn32BitValue_ResultMatchesHighBitSetOver8PlusOne([Range(0, 31)] int highBit)
        {
            if (highBit == 0)
            {
                uint value = 0;
                // 0 is a special case. All values are considered at least 1 byte.
                Assert.AreEqual(1, BitCounter.GetUsedByteCount(value));
            }
            else
            {
                uint value = 1U << highBit;
                Assert.AreEqual(highBit / 8 + 1, BitCounter.GetUsedByteCount(value));
            }
        }
    }
}
