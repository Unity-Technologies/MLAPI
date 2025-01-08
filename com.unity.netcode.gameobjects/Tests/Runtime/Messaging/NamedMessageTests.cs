using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    public class NamedMessageTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private NetworkManager FirstClient => m_ClientNetworkManagers[0];
        private NetworkManager SecondClient => m_ClientNetworkManagers[1];

        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            // Don't spin up and shutdown NetworkManager instances for each test
            // within this set of integration tests.
            return NetworkManagerInstatiationMode.AllTests;
        }

        [UnityTest]
        public IEnumerator NamedMessageIsReceivedOnClientWithContent()
        {
            var messageName = Guid.NewGuid().ToString();

            ulong receivedMessageSender = 0;
            var receivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    receivedMessageSender = sender;

                    reader.ReadValueSafe(out receivedMessageContent);
                });

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(
                    messageName,
                    FirstClient.LocalClientId,
                    writer);
            }

            yield return WaitForMessageReceived<NamedMessage>(new List<NetworkManager> { FirstClient });

            Assert.AreEqual(messageContent.Value, receivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, receivedMessageSender);
        }

        [Test]
        public void NamedMessageIsReceivedOnHostWithContent()
        {
            var messageName = Guid.NewGuid().ToString();

            ulong receivedMessageSender = 0;
            var receivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    receivedMessageSender = sender;

                    reader.ReadValueSafe(out receivedMessageContent);
                });

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(
                    messageName,
                    m_ServerNetworkManager.LocalClientId,
                    writer);
            }

            Assert.AreEqual(messageContent.Value, receivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, receivedMessageSender);
        }

        private void MockNamedMessageCallback(ulong sender, FastBufferReader reader)
        {

        }

        [Test]
        public void NullOrEmptyNamedMessageDoesNotThrowException()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, $"[{nameof(CustomMessagingManager.RegisterNamedMessageHandler)}] Cannot register a named message of type null or empty!");
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(string.Empty, MockNamedMessageCallback);
            LogAssert.Expect(UnityEngine.LogType.Error, $"[{nameof(CustomMessagingManager.RegisterNamedMessageHandler)}] Cannot register a named message of type null or empty!");
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(null, MockNamedMessageCallback);
            LogAssert.Expect(UnityEngine.LogType.Error, $"[{nameof(CustomMessagingManager.UnregisterNamedMessageHandler)}] Cannot unregister a named message of type null or empty!");
            m_ServerNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(string.Empty);
            LogAssert.Expect(UnityEngine.LogType.Error, $"[{nameof(CustomMessagingManager.UnregisterNamedMessageHandler)}] Cannot unregister a named message of type null or empty!");
            m_ServerNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(null);
        }

        [UnityTest]
        public IEnumerator NamedMessageIsReceivedOnMultipleClientsWithContent()
        {
            var messageName = Guid.NewGuid().ToString();

            ulong firstReceivedMessageSender = 0;
            var firstReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    firstReceivedMessageSender = sender;

                    reader.ReadValueSafe(out firstReceivedMessageContent);
                });

            ulong secondReceivedMessageSender = 0;
            var secondReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            SecondClient.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    secondReceivedMessageSender = sender;

                    reader.ReadValueSafe(out secondReceivedMessageContent);
                });

            ulong thirdReceivedMessageSender = 0;
            var thirdReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    thirdReceivedMessageSender = sender;

                    reader.ReadValueSafe(out thirdReceivedMessageContent);
                });

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(
                    messageName,
                    new List<ulong> { m_ServerNetworkManager.LocalClientId, FirstClient.LocalClientId, SecondClient.LocalClientId },
                    writer);
            }

            yield return WaitForMessageReceived<NamedMessage>(new List<NetworkManager> { FirstClient, SecondClient });

            Assert.AreEqual(messageContent.Value, firstReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, firstReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, secondReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, secondReceivedMessageSender);

            Assert.AreEqual(messageContent.Value, thirdReceivedMessageContent.Value);
            Assert.AreEqual(m_ServerNetworkManager.LocalClientId, thirdReceivedMessageSender);
        }

        [UnityTest]
        public IEnumerator WhenSendingNamedMessageToAll_AllClientsReceiveIt()
        {
            var messageName = Guid.NewGuid().ToString();

            ulong firstReceivedMessageSender = 0;
            var firstReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            FirstClient.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    firstReceivedMessageSender = sender;

                    reader.ReadValueSafe(out firstReceivedMessageContent);
                });

            ulong secondReceivedMessageSender = 0;
            var secondReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            SecondClient.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    secondReceivedMessageSender = sender;

                    reader.ReadValueSafe(out secondReceivedMessageContent);
                });

            ulong thirdReceivedMessageSender = 0;
            var thirdReceivedMessageContent = new ForceNetworkSerializeByMemcpy<Guid>(new Guid());
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                messageName,
                (ulong sender, FastBufferReader reader) =>
                {
                    thirdReceivedMessageSender = sender;

                    reader.ReadValueSafe(out thirdReceivedMessageContent);
                });

            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessageToAll(messageName, writer);
            }

            yield return WaitForMessageReceived<NamedMessage>(new List<NetworkManager> { FirstClient, SecondClient });

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
            var messageName = Guid.NewGuid().ToString();
            var messageContent = new ForceNetworkSerializeByMemcpy<Guid>(Guid.NewGuid());
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(messageName, null, writer);
                    });
            }
        }

        [Test]
        public unsafe void ErrorMessageIsPrintedWhenAttemptingToSendNamedMessageWithTooBigBuffer()
        {
            // First try a valid send with the maximum allowed size (this is atm 1264)
            var msgSize = m_ServerNetworkManager.MessageManager.NonFragmentedMessageMaxSize - FastBufferWriter.GetWriteSize<NetworkMessageHeader>() - sizeof(ulong)/*MessageName hash*/ - sizeof(NetworkBatchHeader);
            var bufferSize = m_ServerNetworkManager.MessageManager.NonFragmentedMessageMaxSize;
            var messageName = Guid.NewGuid().ToString();
            var messageContent = new byte[msgSize];
            var writer = new FastBufferWriter(bufferSize, Allocator.Temp, bufferSize * 2);
            using (writer)
            {
                writer.TryBeginWrite(msgSize);
                writer.WriteBytes(messageContent, msgSize, 0);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(messageName, new List<ulong> { FirstClient.LocalClientId }, writer);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(messageName, FirstClient.LocalClientId, writer);
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
                        m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(messageName, new List<ulong> { FirstClient.LocalClientId }, writer);
                    }).Message;
                Assert.IsTrue(message.Contains($"Given message size ({msgSize} bytes) is greater than the maximum"), $"Unexpected exception: {message}");

                message = Assert.Throws<OverflowException>(
                    () =>
                    {
                        m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage(messageName, FirstClient.LocalClientId, writer);
                    }).Message;
                Assert.IsTrue(message.Contains($"Given message size ({msgSize} bytes) is greater than the maximum"), $"Unexpected exception: {message}");
            }
        }
    }
}
