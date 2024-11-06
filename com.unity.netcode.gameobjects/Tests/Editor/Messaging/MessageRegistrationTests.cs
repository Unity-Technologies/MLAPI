using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Netcode.EditorTests
{
    internal class MessageRegistrationTests
    {
        private struct TestMessageOne : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                writer.WriteValue(this);
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

        private struct TestMessageTwo : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                writer.WriteValue(this);
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
        private class TestMessageProviderOne : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessageOne),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessageOne>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessageOne>
                    },
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessageTwo),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessageTwo>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessageTwo>
                    }
                };
            }
        }

        private struct TestMessageThree : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                writer.WriteValue(this);
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
        private class TestMessageProviderTwo : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessageThree),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessageThree>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessageThree>
                    }
                };
            }
        }
        private struct TestMessageFour : INetworkMessage, INetworkSerializeByMemcpy
        {
            public int A;
            public int B;
            public int C;
            public void Serialize(FastBufferWriter writer, int targetVersion)
            {
                writer.WriteValue(this);
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
        private class TestMessageProviderThree : INetworkMessageProvider
        {
            public List<NetworkMessageManager.MessageWithHandler> GetMessages()
            {
                return new List<NetworkMessageManager.MessageWithHandler>
                {
                    new NetworkMessageManager.MessageWithHandler
                    {
                        MessageType = typeof(TestMessageFour),
                        Handler = NetworkMessageManager.ReceiveMessage<TestMessageFour>,
                        GetVersion = NetworkMessageManager.CreateMessageAndGetVersion<TestMessageFour>
                    }
                };
            }
        }

        [Test]
        public void WhenCreatingMessageSystem_OnlyProvidedTypesAreRegistered()
        {
            var sender = new NopMessageSender();

            using var systemOne = new NetworkMessageManager(sender, null, new TestMessageProviderOne());
            using var systemTwo = new NetworkMessageManager(sender, null, new TestMessageProviderTwo());
            using var systemThree = new NetworkMessageManager(sender, null, new TestMessageProviderThree());

            using (systemOne)
            using (systemTwo)
            using (systemThree)
            {
                Assert.AreEqual(2, systemOne.MessageHandlerCount);
                Assert.AreEqual(1, systemTwo.MessageHandlerCount);
                Assert.AreEqual(1, systemThree.MessageHandlerCount);

                Assert.Contains(typeof(TestMessageOne), systemOne.MessageTypes);
                Assert.Contains(typeof(TestMessageTwo), systemOne.MessageTypes);
                Assert.Contains(typeof(TestMessageThree), systemTwo.MessageTypes);
                Assert.Contains(typeof(TestMessageFour), systemThree.MessageTypes);
            }
        }

        [Test]
        public void WhenCreatingMessageSystem_BoundTypeMessageHandlersAreRegistered()
        {
            var sender = new NopMessageSender();

            using var systemOne = new NetworkMessageManager(sender, null, new TestMessageProviderOne());
            using var systemTwo = new NetworkMessageManager(sender, null, new TestMessageProviderTwo());
            using var systemThree = new NetworkMessageManager(sender, null, new TestMessageProviderThree());

            using (systemOne)
            using (systemTwo)
            using (systemThree)
            {
                NetworkMessageManager.MessageHandler handlerOne = NetworkMessageManager.ReceiveMessage<TestMessageOne>;
                NetworkMessageManager.MessageHandler handlerTwo = NetworkMessageManager.ReceiveMessage<TestMessageTwo>;
                NetworkMessageManager.MessageHandler handlerThree = NetworkMessageManager.ReceiveMessage<TestMessageThree>;
                NetworkMessageManager.MessageHandler handlerFour = NetworkMessageManager.ReceiveMessage<TestMessageFour>;

                Assert.AreEqual(handlerOne, systemOne.MessageHandlers[systemOne.GetMessageType(typeof(TestMessageOne))]);
                Assert.AreEqual(handlerTwo, systemOne.MessageHandlers[systemOne.GetMessageType(typeof(TestMessageTwo))]);
                Assert.AreEqual(handlerThree, systemTwo.MessageHandlers[systemTwo.GetMessageType(typeof(TestMessageThree))]);
                Assert.AreEqual(handlerFour, systemThree.MessageHandlers[systemThree.GetMessageType(typeof(TestMessageFour))]);
            }
        }
    }
}
