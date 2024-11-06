using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.Transports.UTP;
#if !UTP_TRANSPORT_2_0_ABOVE
using Unity.Networking.Transport;
#endif

namespace Unity.Netcode.EditorTests
{
    internal class BatchedReceiveQueueTests
    {
        [Test]
        public void BatchedReceiveQueue_EmptyReader()
        {
            var data = new NativeArray<byte>(0, Allocator.Temp);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);
            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedReceiveQueue_SingleMessage()
        {
            var dataLength = sizeof(int) + 1;

            var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            writer.WriteInt(1);
            writer.WriteByte(42);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);

            Assert.False(q.IsEmpty);

            var message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedReceiveQueue_MultipleMessages()
        {
            var dataLength = (sizeof(int) + 1) * 2;

            var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            writer.WriteInt(1);
            writer.WriteByte(42);
            writer.WriteInt(1);
            writer.WriteByte(142);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);

            Assert.False(q.IsEmpty);

            var message1 = q.PopMessage();
            Assert.AreEqual(1, message1.Count);
            Assert.AreEqual((byte)42, message1.Array[message1.Offset]);

            var message2 = q.PopMessage();
            Assert.AreEqual(1, message2.Count);
            Assert.AreEqual((byte)142, message2.Array[message2.Offset]);

            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedReceiveQueue_PartialMessage()
        {
            var dataLength = sizeof(int);

            var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            writer.WriteInt(42);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);

            Assert.False(q.IsEmpty);
            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
        }

        [Test]
        public void BatchedReceiveQueue_PushReader_ToFilledQueue()
        {
            var data1Length = sizeof(int);
            var data2Length = sizeof(byte);

            var data1 = new NativeArray<byte>(data1Length, Allocator.Temp);
            var data2 = new NativeArray<byte>(data2Length, Allocator.Temp);

            var writer1 = new DataStreamWriter(data1);
            writer1.WriteInt(1);
            var writer2 = new DataStreamWriter(data2);
            writer2.WriteByte(42);

            var reader1 = new DataStreamReader(data1);
            var reader2 = new DataStreamReader(data2);

            var q = new BatchedReceiveQueue(reader1);

            Assert.False(q.IsEmpty);

            q.PushReader(reader2);

            Assert.False(q.IsEmpty);

            var message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedReceiveQueue_PushReader_ToPartiallyFilledQueue()
        {
            var dataLength = sizeof(int) + 1;

            var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            writer.WriteInt(1);
            writer.WriteByte(42);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);

            reader = new DataStreamReader(data);
            q.PushReader(reader);

            var message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            reader = new DataStreamReader(data);
            q.PushReader(reader);

            message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedReceiveQueue_PushReader_ToEmptyQueue()
        {
            var dataLength = sizeof(int) + 1;

            var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            writer.WriteInt(1);
            writer.WriteByte(42);

            var reader = new DataStreamReader(data);
            var q = new BatchedReceiveQueue(reader);

            Assert.False(q.IsEmpty);

            q.PopMessage();

            Assert.True(q.IsEmpty);

            reader = new DataStreamReader(data);
            q.PushReader(reader);

            var message = q.PopMessage();
            Assert.AreEqual(1, message.Count);
            Assert.AreEqual((byte)42, message.Array[message.Offset]);

            Assert.AreEqual(default(ArraySegment<byte>), q.PopMessage());
            Assert.True(q.IsEmpty);
        }
    }
}
