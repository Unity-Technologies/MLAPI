using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    public class UnnamedMessageTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private NetworkManager FirstClient => m_ClientNetworkManagers[0];
        private NetworkManager SecondClient => m_ClientNetworkManagers[1];

        [UnityTest]
        public IEnumerator UnnamedMessageIsReceivedOnClientWithContent()
        {
            ulong receivedMessageSender = 0;
            var receivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    receivedMessageSender = sender;

                    reader.ReadValueSafe(out receivedMessageContent);
                };

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(
                    FirstClient.LocalClientId,
                    writer);
            }

            yield return WaitForMessageReceived<UnnamedMessage>(new List<NetworkManager> { FirstClient });

            Assert.AreEqual(messageContent.Value, receivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, receivedMessageSender);
        }

        [Test]
        public void UnnamedMessageIsReceivedOnHostWithContent()
        {
            ulong receivedMessageSender = 0;
            var receivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    receivedMessageSender = sender;

                    reader.ReadValueSafe(out receivedMessageContent);
                };

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(
                    m_ServerNetworkManager.LocalClientId,
                    writer);
            }

            Assert.AreEqual(messageContent.Value, receivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, receivedMessageSender);
        }

        [UnityTest]
        public IEnumerator UnnamedMessageIsReceivedOnMultipleClientsWithContent()
        {
            ulong firstReceivedMessageSender = 0;
            var firstReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    firstReceivedMessageSender = sender;

                    reader.ReadValueSafe(out firstReceivedMessageContent);
                };

            ulong secondReceivedMessageSender = 0;
            var secondReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            SecondClient.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    secondReceivedMessageSender = sender;

                    reader.ReadValueSafe(out secondReceivedMessageContent);
                };

            ulong thirdReceivedMessageSender = 0;
            var thirdReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    thirdReceivedMessageSender = sender;

                    reader.ReadValueSafe(out thirdReceivedMessageContent);
                };

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(
                    new List<ulong> { m_ServerNetworkManager.LocalClientId, FirstClient.LocalClientId, SecondClient.LocalClientId },
                    writer);
            }

            yield return WaitForMessageReceived<UnnamedMessage>(new List<NetworkManager> { FirstClient, SecondClient });

            Assert.AreEqual(messageContent.Value, firstReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, firstReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, secondReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, secondReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, thirdReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, thirdReceivedMessageSender);
        }

        [UnityTest]
        public IEnumerator WhenSendingUnnamedMessageToAll_AllClientsReceiveIt()
        {
            ulong firstReceivedMessageSender = 0;
            var firstReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    firstReceivedMessageSender = sender;

                    reader.ReadValueSafe(out firstReceivedMessageContent);
                };

            ulong secondReceivedMessageSender = 0;
            var secondReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            SecondClient.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    secondReceivedMessageSender = sender;

                    reader.ReadValueSafe(out secondReceivedMessageContent);
                };

            ulong thirdReceivedMessageSender = 0;
            var thirdReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.OnUnnamedMessage +=
                (ulong sender, FastBufferReader reader) =>
                {
                    thirdReceivedMessageSender = sender;

                    reader.ReadValueSafe(out thirdReceivedMessageContent);
                };

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessageToAll(writer);
            }

            yield return WaitForMessageReceived<UnnamedMessage>(new List<NetworkManager> { FirstClient, SecondClient });

            Assert.AreEqual(messageContent.Value, firstReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, firstReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, secondReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, secondReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, thirdReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, thirdReceivedMessageSender);
        }

        [Test]
        public void WhenSendingNamedMessageToNullClientList_ArgumentNullExceptionIsThrown()
        {
            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(null, writer);
                    });
            }
        }

        [Test]
        public unsafe void ErrorMessageIsPrintedWhenAttemptingToSendUnnamedMessageWithTooBigBuffer()
        {
            // First try a valid send with the maximum allowed size (this is atm 1272)
            var msgSize = m_ServerNetworkManager.MessageManager.NonFragmentedMessageMaxSize - FastBufferWriter.GetWriteSize<NetworkMessageHeader>() - sizeof(NetworkBatchHeader);
            var bufferSize = m_ServerNetworkManager.MessageManager.NonFragmentedMessageMaxSize;
            var messageContent = new byte[msgSize];
            var writer = new FastBufferWriter(bufferSize, Allocator.Temp, bufferSize * 2);
            using (writer)
            {
                writer.TryBeginWrite(msgSize);
                writer.WriteBytes(messageContent, msgSize, 0);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(new List<ulong> { FirstClient.LocalClientId }, writer);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(FirstClient.LocalClientId, writer);
            }

            msgSize++;
            messageContent = new byte[msgSize];
            writer = new FastBufferWriter(bufferSize, Allocator.Temp, bufferSize * 2);
            using (writer)
            {
                writer.TryBeginWrite(msgSize);
                writer.WriteBytes(messageContent, msgSize, 0);
                var message = Assert.Throws<OverflowException>(
                    () =>
                    {
                        m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(new List<ulong> { FirstClient.LocalClientId }, writer);
                    }).Message;
                Assert.IsTrue(message.Contains($"Given message size ({msgSize} bytes) is greater than the maximum"), $"Unexpected exception: {message}");

                message = Assert.Throws<OverflowException>(
                    () =>
                    {
                        m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(FirstClient.LocalClientId, writer);
                    }).Message;
                Assert.IsTrue(message.Contains($"Given message size ({msgSize} bytes) is greater than the maximum"), $"Unexpected exception: {message}");
            }
        }
    }
}
