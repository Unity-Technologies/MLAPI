using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using Random = System.Random;

namespace Unity.Netcode.EditorTests
{
    internal class MessageSendingTests
    {
        private struct TestMessage : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public static bool Serialized;

            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                Serialized = true;
                writer.WriteValueSafe(this);
            }

            public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
            {
                return true;
            }

            public void Handle(ref NetworkContext context)
            {
            }

            public int Version => 0;
        }

        private class TestMessageSender : INetworkMessageSender
        {
            public List<byte[]> MessageQueue = new List<byte[]>();

            public void Send(ulong clientId, NetworkDelivery delivery, FastBufferWriter batchData)
            {
                MessageQueue.Add(batchData.ToArray());
            }
        }

        private class TestMessageProvider : INetworkMessageProvider, IDisposable
        {
            // Keep track of what we sent
            private List<List<NetworkMessageManager.MessageWithHandler>> m_CachedMessages = new List<List<NetworkMessageManager.MessageWithHandler>>();

            public void Dispose()
            {
                foreach (var cachedItem in m_CachedMessages)
                {
                    // Clear out any references to NetworkMessageManager.MessageWithHandlers
                    cachedItem.Clear();
                }

                m_CachedMessages.Clear();
            }

            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                var messageList = new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessage),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessage>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessage>
                    }
                };
                // Track messages sent
                m_CachedMessages.Add(messageList);
                return messageList;
            }
        }

        private TestMessageProvider m_TestMessageProvider;
        private TestMessageSender m_MessageSender;
        private NetworkMessageManager m_MessageManager;
        private ulong[] m_Clients = { 0 };

        [SetUp]
        public void SetUp()
        {
            TestMessage.Serialized = false;
            m_MessageSender = new TestMessageSender();
            m_TestMessageProvider = new TestMessageProvider();
            m_MessageManager = new NetworkMessageManager(m_MessageSender, this, m_TestMessageProvider);
            m_MessageManager.ClientConnected(0);
            m_MessageManager.SetVersion(0, XXHash.Hash32(typeof(TestMessage).FullName), 0);
        }

        [TearDown]
        public void TearDown()
        {
            m_TestMessageProvider.Dispose();
            m_MessageManager.Dispose();
        }

        private TestMessage GetMessage()
        {
            var random = new Random();
            return new TestMessage
            {
                A = random.Next(),
                B = random.Next(),
                C = random.Next(),
            };
        }

        [Test]
        public void WhenSendingMessage_SerializeIsCalled()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            Assert.IsTrue(TestMessage.Serialized);
        }

        [Test]
        public void WhenSendingMessage_NothingIsSentBeforeProcessingSendQueue()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            Assert.IsEmpty(m_MessageSender.MessageQueue);
        }

        [Test]
        public void WhenProcessingSendQueue_MessageIsSent()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenSendingMultipleMessages_MessagesAreBatched()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenNotExceedingBatchSize_NewBatchesAreNotCreated()
        {
            var message = GetMessage();
            var size = UnsafeUtility.SizeOf<TestMessage>() + 2; // MessageHeader packed with this message will be 2 bytes
            for (var i = 0; i < (m_MessageManager.NonFragmentedMessageMaxSize - UnsafeUtility.SizeOf<NetworkBatchHeader>()) / size; ++i)
            {
                m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            }

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenExceedingBatchSize_NewBatchesAreCreated([Values(500, 1000, 1300, 2000)] int maxMessageSize)
        {
            var message = GetMessage();
            m_MessageManager.NonFragmentedMessageMaxSize = maxMessageSize;
            var size = UnsafeUtility.SizeOf<TestMessage>() + 2; // MessageHeader packed with this message will be 2 bytes
            for (var i = 0; i < ((m_MessageManager.NonFragmentedMessageMaxSize - UnsafeUtility.SizeOf<NetworkBatchHeader>()) / size) + 1; ++i)
            {
                m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            }

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(2, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenExceedingPerClientBatchSizeLessThanDefault_NewBatchesAreCreated([Values(500, 1000, 1300, 2000)] int maxMessageSize)
        {
            var message = GetMessage();
            m_MessageManager.NonFragmentedMessageMaxSize = maxMessageSize * 5;
            var clients = new ulong[] { 0, 1, 2 };
            m_MessageManager.ClientConnected(1);
            m_MessageManager.ClientConnected(2);
            m_MessageManager.SetVersion(1, XXHash.Hash32(typeof(TestMessage).FullName), 0);
            m_MessageManager.SetVersion(2, XXHash.Hash32(typeof(TestMessage).FullName), 0);

            for (var i = 0; i < clients.Length; ++i)
            {
                m_MessageManager.PeerMTUSizes[clients[i]] = maxMessageSize * (i + 1);
            }

            var size = UnsafeUtility.SizeOf<TestMessage>() + 2; // MessageHeader packed with this message will be 2 bytes
            for (var i = 0; i < clients.Length; ++i)
            {
                for (var j = 0; j < ((m_MessageManager.PeerMTUSizes[clients[i]] - UnsafeUtility.SizeOf<NetworkBatchHeader>()) / size) + 1; ++j)
                {
                    m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, clients[i]);
                }
            }

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(2 * clients.Length, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenExceedingPerClientBatchSizeGreaterThanDefault_OnlyOneNewBatcheIsCreated([Values(500, 1000, 1300, 2000)] int maxMessageSize)
        {
            var message = GetMessage();
            m_MessageManager.NonFragmentedMessageMaxSize = 128;
            var clients = new ulong[] { 0, 1, 2 };
            m_MessageManager.ClientConnected(1);
            m_MessageManager.ClientConnected(2);
            m_MessageManager.SetVersion(1, XXHash.Hash32(typeof(TestMessage).FullName), 0);
            m_MessageManager.SetVersion(2, XXHash.Hash32(typeof(TestMessage).FullName), 0);

            for (var i = 0; i < clients.Length; ++i)
            {
                m_MessageManager.PeerMTUSizes[clients[i]] = maxMessageSize * (i + 1);
            }

            var size = UnsafeUtility.SizeOf<TestMessage>() + 2; // MessageHeader packed with this message will be 2 bytes
            for (var i = 0; i < clients.Length; ++i)
            {
                for (var j = 0; j < ((m_MessageManager.PeerMTUSizes[clients[i]] - UnsafeUtility.SizeOf<NetworkBatchHeader>()) / size) + 1; ++j)
                {
                    m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, clients[i]);
                }
            }

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(2 * clients.Length, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenExceedingMTUSizeWithFragmentedDelivery_NewBatchesAreNotCreated([Values(500, 1000, 1300, 2000)] int maxMessageSize)
        {
            var message = GetMessage();
            m_MessageManager.NonFragmentedMessageMaxSize = maxMessageSize;
            var size = UnsafeUtility.SizeOf<TestMessage>() + 2; // MessageHeader packed with this message will be 2 bytes
            for (var i = 0; i < ((m_MessageManager.NonFragmentedMessageMaxSize - UnsafeUtility.SizeOf<NetworkBatchHeader>()) / size) + 1; ++i)
            {
                m_MessageManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, m_Clients);
            }

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenSwitchingDelivery_NewBatchesAreCreated()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Unreliable, m_Clients);

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(2, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenSwitchingChannel_NewBatchesAreNotCreated()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);

            m_MessageManager.ProcessSendQueues();
            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
        }

        [Test]
        public void WhenSendingMessaged_SentDataIsCorrect()
        {
            var message = GetMessage();
            var message2 = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);
            m_MessageManager.SendMessage(ref message2, NetworkDelivery.Reliable, m_Clients);

            m_MessageManager.ProcessSendQueues();
            var reader = new FastBufferReader(m_MessageSender.MessageQueue[0], Allocator.Temp);
            using (reader)
            {
                reader.ReadValueSafe(out NetworkBatchHeader header);
                Assert.AreEqual(2, header.BatchCount);

                NetworkMessageHeader messageHeader;

                ByteUnpacker.ReadValueBitPacked(reader, out messageHeader.MessageType);
                ByteUnpacker.ReadValueBitPacked(reader, out messageHeader.MessageSize);

                Assert.AreEqual(m_MessageManager.GetMessageType(typeof(TestMessage)), messageHeader.MessageType);
                Assert.AreEqual(UnsafeUtility.SizeOf<TestMessage>(), messageHeader.MessageSize);
                reader.ReadValueSafe(out TestMessage receivedMessage);
                Assert.AreEqual(message, receivedMessage);

                ByteUnpacker.ReadValueBitPacked(reader, out messageHeader.MessageType);
                ByteUnpacker.ReadValueBitPacked(reader, out messageHeader.MessageSize);

                Assert.AreEqual(m_MessageManager.GetMessageType(typeof(TestMessage)), messageHeader.MessageType);
                Assert.AreEqual(UnsafeUtility.SizeOf<TestMessage>(), messageHeader.MessageSize);
                reader.ReadValueSafe(out TestMessage receivedMessage2);
                Assert.AreEqual(message2, receivedMessage2);
            }
        }

        private class TestNoHandlerMessageProvider : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessage),
                        Handler = null,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessage>
                    }
                };
            }
        }

        [Test]
        public void WhenReceivingAMessageWithoutAHandler_ExceptionIsLogged()
        {
            // If a NetworkMessageManager already exists then dispose of it before creating a new NetworkMessageManager (otherwise memory leak)
            if (m_MessageManager != null)
            {
                m_MessageManager.Dispose();
                m_MessageManager = null;
            }

            // Since m_MessageManager is disposed during teardown we don't need to worry about that here.
            m_MessageManager = new NetworkMessageManager(new NopMessageSender(), this, new TestNoHandlerMessageProvider());
            m_MessageManager.ClientConnected(0);

            var messageHeader = new NetworkMessageHeader
            {
                MessageSize = (ushort)UnsafeUtility.SizeOf<TestMessage>(),
                MessageType = m_MessageManager.GetMessageType(typeof(TestMessage)),
            };

            var message = GetMessage();

            var writer = new FastBufferWriter(m_MessageManager.NonFragmentedMessageMaxSize, Allocator.Temp);
            using (writer)
            {
                writer.TryBeginWrite(FastBufferWriter.GetWriteSize(message));
                writer.WriteValue(message);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    m_MessageManager.HandleMessage(messageHeader, reader, 0, 0, 0);
                    LogAssert.Expect(LogType.Exception, new Regex(".*HandlerNotRegisteredException.*"));
                }
            }
        }
    }
}
