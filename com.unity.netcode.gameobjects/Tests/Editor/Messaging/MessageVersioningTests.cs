using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Unity.Netcode.EditorTests
{
    internal class MessageVersioningTests
    {
        public static int SentVersion;
        public static int ReceivedVersion;

        private const int k_DefaultB = 5;
        private const int k_DefaultC = 10;
        private const int k_DefaultD = 15;
        private const long k_DefaultE = 20;

        private struct VersionedTestMessageV0 : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public static bool Serialized;
            public static bool Deserialized;
            public static bool Handled;
            public static List<VersionedTestMessageV0> DeserializedValues = new List<VersionedTestMessageV0>();

            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                SentVersion = Version;
                Serialized = true;
                writer.WriteValueSafe(A);
                writer.WriteValueSafe(B);
                writer.WriteValueSafe(C);
            }

            public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
            {
                ReceivedVersion = Version;
                Deserialized = true;
                reader.ReadValueSafe(out A);
                reader.ReadValueSafe(out B);
                reader.ReadValueSafe(out C);
                return true;
            }

            public void Handle(ref NetworkContext context)
            {
                Handled = true;
                DeserializedValues.Add(this);
            }

            public int Version => 0;
        }

        private struct VersionedTestMessageV1 : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public int D;
            public static bool Serialized;
            public static bool Deserialized;
            public static bool Downgraded;
            public static bool Upgraded;
            public static bool Handled;
            public static List<VersionedTestMessageV1> DeserializedValues = new List<VersionedTestMessageV1>();

            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                if (targetVersion < Version)
                {
                    Downgraded = true;
                    var v0 = new VersionedTestMessageV0 { A = A, B = B, C = C };
                    v0.Serialize(writer, targetVersion);
                    return;
                }
                SentVersion = Version;
                Serialized = true;
                writer.WriteValueSafe(C);
                writer.WriteValueSafe(D);
                writer.WriteValueSafe(A);
                writer.WriteValueSafe(B);
            }

            public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
            {
                if (receivedMessageVersion < Version)
                {
                    var v0 = new VersionedTestMessageV0();
                    v0.Deserialize(reader, ref context, receivedMessageVersion);
                    A = v0.A;
                    B = v0.B;
                    C = v0.C;
                    D = k_DefaultD;
                    Upgraded = true;
                    return true;
                }
                ReceivedVersion = Version;
                Deserialized = true;
                reader.ReadValueSafe(out C);
                reader.ReadValueSafe(out D);
                reader.ReadValueSafe(out A);
                reader.ReadValueSafe(out B);
                return true;
            }

            public void Handle(ref NetworkContext context)
            {
                Handled = true;
                DeserializedValues.Add(this);
            }

            public int Version => 1;
        }

        private struct VersionedTestMessage : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public float D;
            public long E;
            public static bool Serialized;
            public static bool Deserialized;
            public static bool Downgraded;
            public static bool Upgraded;
            public static bool Handled;
            public static List<VersionedTestMessage> DeserializedValues = new List<VersionedTestMessage>();

            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                if (targetVersion < Version)
                {
                    Downgraded = true;
                    var v1 = new VersionedTestMessageV1 { A = A, B = k_DefaultB, C = k_DefaultC, D = (int)D };
                    v1.Serialize(writer, targetVersion);
                    return;
                }
                SentVersion = Version;
                Serialized = true;
                writer.WriteValueSafe(D);
                writer.WriteValueSafe(A);
                writer.WriteValueSafe(E);
            }

            public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
            {
                if (receivedMessageVersion < Version)
                {
                    var v1 = new VersionedTestMessageV1();
                    v1.Deserialize(reader, ref context, receivedMessageVersion);
                    A = v1.A;
                    D = v1.D;
                    E = k_DefaultE;
                    Upgraded = true;
                    return true;
                }
                ReceivedVersion = Version;
                Deserialized = true;
                reader.ReadValueSafe(out D);
                reader.ReadValueSafe(out A);
                reader.ReadValueSafe(out E);
                return true;
            }

            public void Handle(ref NetworkContext context)
            {
                Handled = true;
                DeserializedValues.Add(this);
            }

            public int Version => 2;
        }

        private class TestMessageProviderV0 : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(VersionedTestMessageV0),
                        Handler = NetworkMessageManager.ReceiveMessage<VersionedTestMessageV0>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<VersionedTestMessageV0>
                    }
                };
            }
        }

        private class TestMessageProviderV1 : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(VersionedTestMessageV1),
                        Handler = NetworkMessageManager.ReceiveMessage<VersionedTestMessageV1>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<VersionedTestMessageV1>
                    }
                };
            }
        }

        private class TestMessageProviderV2 : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(VersionedTestMessage),
                        Handler = NetworkMessageManager.ReceiveMessage<VersionedTestMessage>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<VersionedTestMessage>
                    }
                };
            }
        }

        private class TestMessageSender : INetworkMessageSender
        {
            public List<byte[]> MessageQueue = new List<byte[]>();

            public void Send(ulong clientId, NetworkDelivery delivery, FastBufferWriter batchData)
            {
                MessageQueue.Add(batchData.ToArray());
            }
        }

        private NetworkMessageManager m_MessageManagerV0;
        private NetworkMessageManager m_MessageManagerV1;
        private NetworkMessageManager m_MessageManagerV2;
        private TestMessageSender m_MessageSender;

        private void CreateFakeClients(NetworkMessageManager system, uint hash)
        {
            // Create three fake clients for each NetworkMessageManager
            // client 0 has version 0, client 1 has version 1, and client 2 has version 2
            system.ClientConnected(0);
            system.ClientConnected(1);
            system.ClientConnected(2);
            system.SetVersion(0, hash, 0);
            system.SetVersion(1, hash, 1);
            system.SetVersion(2, hash, 2);
        }

        [SetUp]
        public void SetUp()
        {
            VersionedTestMessageV0.Serialized = false;
            VersionedTestMessageV0.Deserialized = false;
            VersionedTestMessageV0.Handled = false;
            VersionedTestMessageV0.DeserializedValues.Clear();
            VersionedTestMessageV1.Serialized = false;
            VersionedTestMessageV1.Deserialized = false;
            VersionedTestMessageV1.Downgraded = false;
            VersionedTestMessageV1.Upgraded = false;
            VersionedTestMessageV1.Handled = false;
            VersionedTestMessageV1.DeserializedValues.Clear();
            VersionedTestMessage.Serialized = false;
            VersionedTestMessage.Deserialized = false;
            VersionedTestMessage.Downgraded = false;
            VersionedTestMessage.Upgraded = false;
            VersionedTestMessage.Handled = false;
            VersionedTestMessage.DeserializedValues.Clear();
            m_MessageSender = new TestMessageSender();

            m_MessageManagerV0 = new NetworkMessageManager(m_MessageSender, this, new TestMessageProviderV0());
            m_MessageManagerV1 = new NetworkMessageManager(m_MessageSender, this, new TestMessageProviderV1());
            m_MessageManagerV2 = new NetworkMessageManager(m_MessageSender, this, new TestMessageProviderV2());

            CreateFakeClients(m_MessageManagerV0, XXHash.Hash32(typeof(VersionedTestMessageV0).FullName));
            CreateFakeClients(m_MessageManagerV1, XXHash.Hash32(typeof(VersionedTestMessageV1).FullName));
            CreateFakeClients(m_MessageManagerV2, XXHash.Hash32(typeof(VersionedTestMessage).FullName));

            // Make sure that all three messages got the same IDs...
            Assert.AreEqual(
                m_MessageManagerV0.GetMessageType(typeof(VersionedTestMessageV0)),
                m_MessageManagerV1.GetMessageType(typeof(VersionedTestMessageV1)));
            Assert.AreEqual(
                m_MessageManagerV0.GetMessageType(typeof(VersionedTestMessageV0)),
                m_MessageManagerV2.GetMessageType(typeof(VersionedTestMessage)));
        }

        [TearDown]
        public void TearDown()
        {
            m_MessageManagerV0.Dispose();
            m_MessageManagerV1.Dispose();
            m_MessageManagerV2.Dispose();
        }

        private VersionedTestMessageV0 GetMessage_v0()
        {
            var random = new Random();
            return new VersionedTestMessageV0
            {
                A = random.Next(),
                B = random.Next(),
                C = random.Next(),
            };
        }

        private VersionedTestMessageV1 GetMessage_v1()
        {
            var random = new Random();
            return new VersionedTestMessageV1
            {
                A = random.Next(),
                B = random.Next(),
                C = random.Next(),
                D = random.Next(),
            };
        }

        private VersionedTestMessage GetMessage_v2()
        {
            var random = new Random();
            return new VersionedTestMessage
            {
                A = random.Next(),
                D = (float)(random.NextDouble() * 10000),
                E = ((long)random.Next() << 32) + random.Next()
            };
        }

        public void CheckPostSendExpectations(int sourceLocalVersion, int remoteVersion)
        {
            Assert.AreEqual(Math.Min(sourceLocalVersion, remoteVersion) == 0, VersionedTestMessageV0.Serialized);
            Assert.AreEqual(Math.Min(sourceLocalVersion, remoteVersion) == 1, VersionedTestMessageV1.Serialized);
            Assert.AreEqual(Math.Min(sourceLocalVersion, remoteVersion) == 2, VersionedTestMessage.Serialized);
            Assert.AreEqual(sourceLocalVersion >= 1 && remoteVersion < 1, VersionedTestMessageV1.Downgraded);
            Assert.AreEqual(sourceLocalVersion >= 2 && remoteVersion < 2, VersionedTestMessage.Downgraded);

            Assert.AreEqual(1, m_MessageSender.MessageQueue.Count);
            Assert.AreEqual(Math.Min(sourceLocalVersion, remoteVersion), SentVersion);
        }

        public void CheckPostReceiveExpectations(int sourceLocalVersion, int remoteVersion)
        {
            Assert.AreEqual(SentVersion == 0, VersionedTestMessageV0.Deserialized);
            Assert.AreEqual(SentVersion == 1, VersionedTestMessageV1.Deserialized);
            Assert.AreEqual(SentVersion == 2, VersionedTestMessage.Deserialized);
            Assert.AreEqual(remoteVersion >= 1 && sourceLocalVersion < 1, VersionedTestMessageV1.Upgraded);
            Assert.AreEqual(remoteVersion >= 2 && sourceLocalVersion < 2, VersionedTestMessage.Upgraded);

            Assert.AreEqual((remoteVersion == 0 ? 1 : 0), VersionedTestMessageV0.DeserializedValues.Count);
            Assert.AreEqual((remoteVersion == 1 ? 1 : 0), VersionedTestMessageV1.DeserializedValues.Count);
            Assert.AreEqual((remoteVersion == 2 ? 1 : 0), VersionedTestMessage.DeserializedValues.Count);

            Assert.AreEqual(SentVersion, ReceivedVersion);
        }

        private void SendMessageWithVersions<T>(T message, int fromVersion, int toVersion) where T : unmanaged, INetworkMessage
        {
            NetworkMessageManager sendSystem;
            switch (fromVersion)
            {
                case 0: sendSystem = m_MessageManagerV0; break;
                case 1: sendSystem = m_MessageManagerV1; break;
                default: sendSystem = m_MessageManagerV2; break;
            }
            sendSystem.SendMessage(ref message, NetworkDelivery.Reliable, (ulong)toVersion);
            sendSystem.ProcessSendQueues();
            CheckPostSendExpectations(fromVersion, toVersion);

            NetworkMessageManager receiveSystem;
            switch (toVersion)
            {
                case 0: receiveSystem = m_MessageManagerV0; break;
                case 1: receiveSystem = m_MessageManagerV1; break;
                default: receiveSystem = m_MessageManagerV2; break;
            }
            receiveSystem.HandleIncomingData((ulong)fromVersion, new ArraySegment<byte>(m_MessageSender.MessageQueue[0]), 0.0f);
            receiveSystem.ProcessIncomingMessageQueue();
            CheckPostReceiveExpectations(fromVersion, toVersion);

            m_MessageSender.MessageQueue.Clear();
        }

        [Test]
        public void WhenSendingV0ToV0_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v0();

            SendMessageWithVersions(message, 0, 0);

            var receivedMessage = VersionedTestMessageV0.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(message.B, receivedMessage.B);
            Assert.AreEqual(message.C, receivedMessage.C);
        }

        [Test]
        public void WhenSendingV0ToV1_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v0();

            SendMessageWithVersions(message, 0, 1);

            var receivedMessage = VersionedTestMessageV1.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(message.B, receivedMessage.B);
            Assert.AreEqual(message.C, receivedMessage.C);
            Assert.AreEqual(k_DefaultD, receivedMessage.D);
        }

        [Test]
        public void WhenSendingV0ToV2_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v0();

            SendMessageWithVersions(message, 0, 2);

            var receivedMessage = VersionedTestMessage.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual((float)k_DefaultD, receivedMessage.D);
            Assert.AreEqual(k_DefaultE, receivedMessage.E);
        }

        [Test]
        public void WhenSendingV1ToV0_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v1();

            SendMessageWithVersions(message, 1, 0);

            var receivedMessage = VersionedTestMessageV0.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(message.B, receivedMessage.B);
            Assert.AreEqual(message.C, receivedMessage.C);
        }

        [Test]
        public void WhenSendingV1ToV1_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v1();

            SendMessageWithVersions(message, 1, 1);

            var receivedMessage = VersionedTestMessageV1.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(message.B, receivedMessage.B);
            Assert.AreEqual(message.C, receivedMessage.C);
            Assert.AreEqual(message.D, receivedMessage.D);
        }

        [Test]
        public void WhenSendingV1ToV2_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v1();

            SendMessageWithVersions(message, 1, 2);

            var receivedMessage = VersionedTestMessage.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual((float)message.D, receivedMessage.D);
            Assert.AreEqual(k_DefaultE, receivedMessage.E);
        }

        [Test]
        public void WhenSendingV2ToV0_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v2();

            SendMessageWithVersions(message, 2, 0);

            var receivedMessage = VersionedTestMessageV0.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(k_DefaultB, receivedMessage.B);
            Assert.AreEqual(k_DefaultC, receivedMessage.C);
        }

        [Test]
        public void WhenSendingV2ToV1_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v2();

            SendMessageWithVersions(message, 2, 1);

            var receivedMessage = VersionedTestMessageV1.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(k_DefaultB, receivedMessage.B);
            Assert.AreEqual(k_DefaultC, receivedMessage.C);
            Assert.AreEqual((int)message.D, receivedMessage.D);
        }

        [Test]
        public void WhenSendingV2ToV2_DataIsReceivedCorrectly()
        {
            var message = GetMessage_v2();

            SendMessageWithVersions(message, 2, 2);

            var receivedMessage = VersionedTestMessage.DeserializedValues[0];
            Assert.AreEqual(message.A, receivedMessage.A);
            Assert.AreEqual(message.D, receivedMessage.D);
            Assert.AreEqual(message.E, receivedMessage.E);
        }
    }
}
