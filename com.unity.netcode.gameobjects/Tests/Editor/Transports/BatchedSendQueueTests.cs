using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.Transports.UTP;
#if !UTP_TRANSPORT_2_0_ABOVE
using Unity.Networking.Transport;
#endif

namespace Unity.Netcode.EditorTests
{
    internal class BatchedSendQueueTests
    {
        private const int k_TestQueueCapacity = 16 * 1024;
        private const int k_TestMessageSize = 1020;
        private const int k_NumMessagesToFillQueue = k_TestQueueCapacity / (k_TestMessageSize + BatchedSendQueue.PerMessageOverhead);

        private ArraySegment<byte> m_TestMessage;

        private void AssertIsTestMessage(NativeArray<byte> data)
        {
            var reader = new DataStreamReader(data);
            Assert.AreEqual(k_TestMessageSize, reader.ReadInt());
            for (int i = 0; i < k_TestMessageSize; i++)
            {
                Assert.AreEqual(m_TestMessage.Array[i], reader.ReadByte());
            }
        }

        [OneTimeSetUp]
        public void InitializeTestMessage()
        {
            var data = new byte[k_TestMessageSize];
            for (int i = 0; i < k_TestMessageSize; i++)
            {
                data[i] = (byte)i;
            }
            m_TestMessage = new ArraySegment<byte>(data);
        }

        [Test]
        public void BatchedSendQueue_EmptyOnCreation()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            Assert.AreEqual(0, q.Length);
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedSendQueue_NotCreatedAfterDispose()
        {
            var q = new BatchedSendQueue(k_TestQueueCapacity);
            q.Dispose();
            Assert.False(q.IsCreated);
        }

        [Test]
        public void BatchedSendQueue_InitialCapacityLessThanMaximum()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity);
        }

        [Test]
        public void BatchedSendQueue_PushMessage_ReturnValue()
        {
            // Will fit a single test message, but not two (with overhead included).
            var queueCapacity = (k_TestMessageSize * 2) + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(queueCapacity);

            Assert.True(q.PushMessage(m_TestMessage));
            Assert.False(q.PushMessage(m_TestMessage));
        }

        [Test]
        public void BatchedSendQueue_PushMessage_IncreasesLength()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            q.PushMessage(m_TestMessage);
            Assert.AreEqual(k_TestMessageSize + BatchedSendQueue.PerMessageOverhead, q.Length);
        }

        [Test]
        public void BatchedSendQueue_PushMessage_SucceedsAfterConsume()
        {
            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;
            var queueCapacity = messageLength * 2;

            using var q = new BatchedSendQueue(queueCapacity);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            q.Consume(messageLength);
            Assert.IsTrue(q.PushMessage(m_TestMessage));
            Assert.AreEqual(queueCapacity, q.Length);
        }

        [Test]
        public void BatchedSendQueue_PushMessage_GrowsDataIfNeeded()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity);

            var numMessagesToFillMinimum = BatchedSendQueue.MinimumMinimumCapacity / messageLength;
            for (int i = 0; i < numMessagesToFillMinimum; i++)
            {
                q.PushMessage(m_TestMessage);
            }

            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity);

            q.PushMessage(m_TestMessage);

            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity * 2);
        }

        [Test]
        public void BatchedSendQueue_PushMessage_DoesNotGrowDataPastMaximum()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            for (int i = 0; i < k_NumMessagesToFillQueue; i++)
            {
                Assert.IsTrue(q.PushMessage(m_TestMessage));
            }

            Assert.AreEqual(q.Capacity, k_TestQueueCapacity);
            Assert.IsFalse(q.PushMessage(m_TestMessage));
            Assert.AreEqual(q.Capacity, k_TestQueueCapacity);
        }

        [Test]
        public void BatchedSendQueue_PushMessage_TrimsDataAfterGrowing()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            for (int i = 0; i < k_NumMessagesToFillQueue; i++)
            {
                Assert.IsTrue(q.PushMessage(m_TestMessage));
            }

            Assert.AreEqual(q.Capacity, k_TestQueueCapacity);
            q.Consume(messageLength * (k_NumMessagesToFillQueue - 1));
            Assert.IsTrue(q.PushMessage(m_TestMessage));
            Assert.AreEqual(messageLength * 2, q.Length);
            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity * 2);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_ReturnValue()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            var filled = q.FillWriterWithMessages(ref writer);
            Assert.AreEqual(k_TestMessageSize + BatchedSendQueue.PerMessageOverhead, filled);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_NoopIfNoPushedMessages()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(0, q.FillWriterWithMessages(ref writer));
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_NoopIfNotEnoughCapacity()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(2, Allocator.Temp);

            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(0, q.FillWriterWithMessages(ref writer));
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_SinglePushedMessage()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            q.FillWriterWithMessages(ref writer);
            AssertIsTestMessage(data);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_MultiplePushedMessages()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            q.FillWriterWithMessages(ref writer);

            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;
            AssertIsTestMessage(data);
            AssertIsTestMessage(data.GetSubArray(messageLength, messageLength));
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_PartialPushedMessages()
        {
            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(messageLength, Allocator.Temp);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(messageLength, q.FillWriterWithMessages(ref writer));
            AssertIsTestMessage(data);

            q.Consume(messageLength);

            writer = new DataStreamWriter(data);
            Assert.AreEqual(messageLength, q.FillWriterWithMessages(ref writer));
            AssertIsTestMessage(data);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithMessages_StopOnSoftMaxBytes()
        {
            var smallMessage = new ArraySegment<byte>(new byte[10]);
            var largeMessage = new ArraySegment<byte>(new byte[3000]);

            var smallMessageSize = smallMessage.Count + BatchedSendQueue.PerMessageOverhead;
            var largeMessageSize = largeMessage.Count + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(largeMessageSize, Allocator.Temp);

            q.PushMessage(smallMessage);
            q.PushMessage(largeMessage);
            q.PushMessage(smallMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(smallMessageSize, q.FillWriterWithMessages(ref writer, 1000));
            q.Consume(smallMessageSize);

            writer = new DataStreamWriter(data);
            Assert.AreEqual(largeMessageSize, q.FillWriterWithMessages(ref writer, 1000));
            q.Consume(largeMessageSize);

            writer = new DataStreamWriter(data);
            Assert.AreEqual(smallMessageSize, q.FillWriterWithMessages(ref writer, 1000));
            q.Consume(smallMessageSize);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithBytes_NoopIfNoData()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(0, q.FillWriterWithBytes(ref writer));
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithBytes_WriterCapacityMoreThanLength()
        {
            var dataLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(k_TestQueueCapacity, Allocator.Temp);

            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(dataLength, q.FillWriterWithBytes(ref writer));
            AssertIsTestMessage(data);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithBytes_WriterCapacityLessThanLength()
        {
            var dataLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(dataLength, q.FillWriterWithBytes(ref writer));
            AssertIsTestMessage(data);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithBytes_WriterCapacityEqualToLength()
        {
            var dataLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(dataLength, q.FillWriterWithBytes(ref writer));
            AssertIsTestMessage(data);
        }

        [Test]
        public void BatchedSendQueue_FillWriterWithBytes_MaxBytesGreaterThanCapacity()
        {
            var dataLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;

            using var q = new BatchedSendQueue(k_TestQueueCapacity);
            using var data = new NativeArray<byte>(dataLength, Allocator.Temp);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            var writer = new DataStreamWriter(data);
            Assert.AreEqual(dataLength, q.FillWriterWithBytes(ref writer, dataLength * 2));
            AssertIsTestMessage(data);
            Assert.False(writer.HasFailedWrites);
        }

        [Test]
        public void BatchedSendQueue_Consume_LessThanLength()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            q.PushMessage(m_TestMessage);
            q.PushMessage(m_TestMessage);

            var messageLength = k_TestMessageSize + BatchedSendQueue.PerMessageOverhead;
            q.Consume(messageLength);
            Assert.AreEqual(messageLength, q.Length);
        }

        [Test]
        public void BatchedSendQueue_Consume_ExactLength()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            q.PushMessage(m_TestMessage);

            q.Consume(k_TestMessageSize + BatchedSendQueue.PerMessageOverhead);
            Assert.AreEqual(0, q.Length);
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedSendQueue_Consume_MoreThanLength()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            q.PushMessage(m_TestMessage);

            q.Consume(k_TestQueueCapacity);
            Assert.AreEqual(0, q.Length);
            Assert.True(q.IsEmpty);
        }

        [Test]
        public void BatchedSendQueue_Consume_TrimsDataOnEmpty()
        {
            using var q = new BatchedSendQueue(k_TestQueueCapacity);

            for (int i = 0; i < k_NumMessagesToFillQueue; i++)
            {
                q.PushMessage(m_TestMessage);
            }

            Assert.AreEqual(q.Capacity, k_TestQueueCapacity);
            q.Consume(k_TestQueueCapacity);
            Assert.AreEqual(q.Capacity, BatchedSendQueue.MinimumMinimumCapacity);
        }
    }
}
