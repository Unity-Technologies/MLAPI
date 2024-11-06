using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Netcode.EditorTests
{
    internal class MessageReceivingTests
    {
        private struct TestMessage : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public static bool Deserialized;
            public static bool Handled;
            public static List<TestMessage> DeserializedValues = new List<TestMessage>();

            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                writer.WriteValueSafe(this);
            }

            public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
            {
                Deserialized = true;
                reader.ReadValueSafe(out this);
                return true;
            }

            public void Handle(ref NetworkContext context)
            {
                Handled = true;
                DeserializedValues.Add(this);
            }

            public int Version => 0;
        }

        private class TestMessageProvider : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessage),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessage>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessage>
                    }
                };
            }
        }

        private NetworkMessageManager m_MessageManager;

        [SetUp]
        public void SetUp()
        {
            TestMessage.Deserialized = false;
            TestMessage.Handled = false;
            TestMessage.DeserializedValues.Clear();

            m_MessageManager = new NetworkMessageManager(new NopMessageSender(), this, new TestMessageProvider());
            m_MessageManager.SetVersion(0, XXHash.Hash32(typeof(TestMessage).FullName), 0);
        }

        [TearDown]
        public void TearDown()
        {
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
        public void WhenHandlingAMessage_ReceiveMethodIsCalled()
        {
            var messageHeader = new NetworkMessageHeader
            {
                MessageSize = (ushort)UnsafeUtility.SizeOf<TestMessage>(),
                MessageType = m_MessageManager.GetMessageType(typeof(TestMessage)),
            };
            var message = GetMessage();

            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.TryBeginWrite(FastBufferWriter.GetWriteSize(message));
                writer.WriteValue(message);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    m_MessageManager.HandleMessage(messageHeader, reader, 0, 0, 0);
                    Assert.IsTrue(TestMessage.Deserialized);
                    Assert.IsTrue(TestMessage.Handled);
                    Assert.AreEqual(1, TestMessage.DeserializedValues.Count);
                    Assert.AreEqual(message, TestMessage.DeserializedValues[0]);
                }
            }
        }

        [Test]
        public unsafe void WhenHandlingIncomingData_ReceiveIsNotCalledBeforeProcessingIncomingMessageQueue()
        {
            var batchHeader = new NetworkBatchHeader
            {
                BatchCount = 1
            };
            var messageHeader = new NetworkMessageHeader
            {
                MessageSize = (ushort)UnsafeUtility.SizeOf<TestMessage>(),
                MessageType = m_MessageManager.GetMessageType(typeof(TestMessage)),
            };
            var message = GetMessage();

            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.TryBeginWrite(FastBufferWriter.GetWriteSize(batchHeader) +
                                     FastBufferWriter.GetWriteSize(messageHeader) +
                                     FastBufferWriter.GetWriteSize(message));
                writer.WriteValue(batchHeader);
                writer.WriteValue(messageHeader);
                writer.WriteValue(message);

                // Fill out the rest of the batch header
                writer.Seek(0);
                batchHeader = new NetworkBatchHeader
                {
                    Magic = NetworkBatchHeader.MagicValue,
                    BatchSize = writer.Length,
                    BatchHash = XXHash.Hash64(writer.GetUnsafePtr() + sizeof(NetworkBatchHeader), writer.Length - sizeof(NetworkBatchHeader)),
                    BatchCount = 1
                };
                writer.WriteValue(batchHeader);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    m_MessageManager.HandleIncomingData(0, new ArraySegment<byte>(writer.ToArray()), 0);
                    Assert.IsFalse(TestMessage.Deserialized);
                    Assert.IsFalse(TestMessage.Handled);
                    Assert.IsEmpty(TestMessage.DeserializedValues);
                }
            }
        }

        [Test]
        public unsafe void WhenReceivingAMessageAndProcessingMessageQueue_ReceiveMethodIsCalled()
        {
            var batchHeader = new NetworkBatchHeader
            {
                BatchCount = 1
            };
            var messageHeader = new NetworkMessageHeader
            {
                MessageSize = (uint)UnsafeUtility.SizeOf<TestMessage>(),
                MessageType = m_MessageManager.GetMessageType(typeof(TestMessage)),
            };
            var message = GetMessage();

            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(batchHeader);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageType);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageSize);
                writer.WriteValueSafe(message);

                // Fill out the rest of the batch header
                writer.Seek(0);
                batchHeader = new NetworkBatchHeader
                {
                    Magic = NetworkBatchHeader.MagicValue,
                    BatchSize = writer.Length,
                    BatchHash = XXHash.Hash64(writer.GetUnsafePtr() + sizeof(NetworkBatchHeader), writer.Length - sizeof(NetworkBatchHeader)),
                    BatchCount = 1
                };
                writer.WriteValue(batchHeader);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    m_MessageManager.HandleIncomingData(0, new ArraySegment<byte>(writer.ToArray()), 0);
                    m_MessageManager.ProcessIncomingMessageQueue();
                    Assert.IsTrue(TestMessage.Deserialized);
                    Assert.IsTrue(TestMessage.Handled);
                    Assert.AreEqual(1, TestMessage.DeserializedValues.Count);
                    Assert.AreEqual(message, TestMessage.DeserializedValues[0]);
                }
            }
        }

        [Test]
        public unsafe void WhenReceivingMultipleMessagesAndProcessingMessageQueue_ReceiveMethodIsCalledMultipleTimes()
        {
            var batchHeader = new NetworkBatchHeader
            {
                BatchCount = 2
            };
            var messageHeader = new NetworkMessageHeader
            {
                MessageSize = (ushort)UnsafeUtility.SizeOf<TestMessage>(),
                MessageType = m_MessageManager.GetMessageType(typeof(TestMessage)),
            };
            var message = GetMessage();
            var message2 = GetMessage();

            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(batchHeader);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageType);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageSize);
                writer.WriteValueSafe(message);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageType);
                BytePacker.WriteValueBitPacked(writer, messageHeader.MessageSize);
                writer.WriteValueSafe(message2);

                // Fill out the rest of the batch header
                writer.Seek(0);
                batchHeader = new NetworkBatchHeader
                {
                    Magic = NetworkBatchHeader.MagicValue,
                    BatchSize = writer.Length,
                    BatchHash = XXHash.Hash64(writer.GetUnsafePtr() + sizeof(NetworkBatchHeader), writer.Length - sizeof(NetworkBatchHeader)),
                    BatchCount = 2
                };
                writer.WriteValue(batchHeader);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                using (reader)
                {
                    m_MessageManager.HandleIncomingData(0, new ArraySegment<byte>(writer.ToArray()), 0);
                    Assert.IsFalse(TestMessage.Deserialized);
                    Assert.IsFalse(TestMessage.Handled);
                    Assert.IsEmpty(TestMessage.DeserializedValues);

                    m_MessageManager.ProcessIncomingMessageQueue();
                    Assert.IsTrue(TestMessage.Deserialized);
                    Assert.IsTrue(TestMessage.Handled);
                    Assert.AreEqual(2, TestMessage.DeserializedValues.Count);
                    Assert.AreEqual(message, TestMessage.DeserializedValues[0]);
                    Assert.AreEqual(message2, TestMessage.DeserializedValues[1]);
                }
            }
        }
    }
}
