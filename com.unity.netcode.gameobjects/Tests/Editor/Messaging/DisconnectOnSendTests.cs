using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Netcode.EditorTests
{
    internal class DisconnectOnSendTests
    {
        private struct TestMessage : INetworkMessage, INetworkSerializeByMemcpy
        {
            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
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

        private class DisconnectOnSendMessageSender : INetworkMessageSender
        {
            public NetworkMessageManager MessageManager;

            public void Send(ulong clientId, NetworkDelivery delivery, FastBufferWriter batchData)
            {
                MessageManager.ClientDisconnected(clientId);
            }
        }

        private class TestMessageProvider : INetworkMessageProvider
        {
            // Keep track of what we sent
            private List<NetworkMessageManager.MessageWithHandler> m_MessageList = new List<NetworkMessageManager.MessageWithHandler>
            {
                new NetworkMessageManager.MessageWithHandler
                {
                    MessageType = typeof(TestMessage),
                    Handler = NetworkMessageManager.ReceiveMessage<TestMessage>,
                    GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessage>
                }
            };

            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return m_MessageList;
            }
        }

        private TestMessageProvider m_TestMessageProvider;
        private DisconnectOnSendMessageSender m_MessageSender;
        private NetworkMessageManager m_MessageManager;
        private ulong[] m_Clients = { 0 };

        [SetUp]
        public void SetUp()
        {
            m_MessageSender = new DisconnectOnSendMessageSender();
            m_TestMessageProvider = new TestMessageProvider();
            m_MessageManager = new NetworkMessageManager(m_MessageSender, this, m_TestMessageProvider);
            m_MessageSender.MessageManager = m_MessageManager;
            m_MessageManager.ClientConnected(0);
            m_MessageManager.SetVersion(0, XXHash.Hash32(typeof(TestMessage).FullName), 0);
        }

        [TearDown]
        public void TearDown()
        {
            m_MessageManager.Dispose();
        }

        private TestMessage GetMessage()
        {
            return new TestMessage();
        }

        [Test]
        public void WhenDisconnectIsCalledDuringSend_NoErrorsOccur()
        {
            var message = GetMessage();
            m_MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, m_Clients);

            // This is where an exception would be thrown and logged.
            m_MessageManager.ProcessSendQueues();
        }
    }
}
