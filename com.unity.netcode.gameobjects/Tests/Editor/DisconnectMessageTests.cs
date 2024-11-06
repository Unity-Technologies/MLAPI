using NUnit.Framework;
using Unity.Collections;

namespace Unity.Netcode.EditorTests
{
    internal class DisconnectMessageTests
    {
        [Test]
        public void EmptyDisconnectReason()
        {
            var networkContext = new NetworkContext();
            var writer = new FastBufferWriter(20, Allocator.Temp, 20);
            var msg = new DisconnectReasonMessage
            {
                Reason = string.Empty
            };
            msg.Serialize(writer, msg.Version);

            var fbr = new FastBufferReader(writer, Allocator.Temp);
            var recvMsg = new DisconnectReasonMessage();
            recvMsg.Deserialize(fbr, ref networkContext, msg.Version);

            Assert.IsEmpty(recvMsg.Reason);
        }

        [Test]
        public void DisconnectReason()
        {
            var networkContext = new NetworkContext();
            var writer = new FastBufferWriter(20, Allocator.Temp, 20);
            var msg = new DisconnectReasonMessage
            {
                Reason = "Foo"
            };
            msg.Serialize(writer, msg.Version);

            var fbr = new FastBufferReader(writer, Allocator.Temp);
            var recvMsg = new DisconnectReasonMessage();
            recvMsg.Deserialize(fbr, ref networkContext, msg.Version);

            Assert.AreEqual("Foo", recvMsg.Reason);
        }

        [Test]
        public void DisconnectReasonTooLong()
        {
            var networkContext = new NetworkContext();
            var writer = new FastBufferWriter(20, Allocator.Temp, 20);
            var msg = new DisconnectReasonMessage
            {
                Reason = "ThisStringIsWayLongerThanTwentyBytes"
            };
            msg.Serialize(writer, msg.Version);

            var fbr = new FastBufferReader(writer, Allocator.Temp);
            var recvMsg = new DisconnectReasonMessage();
            recvMsg.Deserialize(fbr, ref networkContext, msg.Version);

            Assert.IsEmpty(recvMsg.Reason);
        }
    }
}
