using NUnit.Framework;

namespace Unity.Netcode.EditorTests
{
    internal class XXHashTests
    {
        [Test]
        public void TestXXHash32Short()
        {
            Assert.That("TestStuff".Hash32(), Is.EqualTo(0x64e10c4c));
        }

        [Test]
        public void TestXXHash32Long()
        {
            Assert.That("TestingHashingWithLongStringValues".Hash32(), Is.EqualTo(0xba3d1783));
        }

        [Test]
        public void TestXXHas64Short()
        {
            Assert.That("TestStuff".Hash64(), Is.EqualTo(0x4c3be8d82d14a5a9));
        }

        [Test]
        public void TestXXHash64Long()
        {
            Assert.That("TestingHashingWithLongStringValues".Hash64(), Is.EqualTo(0x5b374f98b10bf246));
        }
    }
}
