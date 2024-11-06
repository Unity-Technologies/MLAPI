using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode.RuntimeTests
{
    internal class InvalidConnectionEventsTest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        public InvalidConnectionEventsTest() : base(HostOrServer.Server) { }

        private class Hooks<TCatchType> : INetworkHooks
        {
            public void OnBeforeSendMessage<T>(ulong clientId, ref T message, NetworkDelivery delivery) where T : INetworkMessage
            {
            }

            public void OnAfterSendMessage<T>(ulong clientId, ref T message, NetworkDelivery delivery, int messageSizeBytes) where T : INetworkMessage
            {
            }

            public void OnBeforeReceiveMessage(ulong senderId, Type messageType, int messageSizeBytes)
            {
            }

            public void OnAfterReceiveMessage(ulong senderId, Type messageType, int messageSizeBytes)
            {
            }

            public void OnBeforeSendBatch(ulong clientId, int messageCount, int batchSizeInBytes, NetworkDelivery delivery)
            {
            }

            public void OnAfterSendBatch(ulong clientId, int messageCount, int batchSizeInBytes, NetworkDelivery delivery)
            {
            }

            public void OnBeforeReceiveBatch(ulong senderId, int messageCount, int batchSizeInBytes)
            {
            }

            public void OnAfterReceiveBatch(ulong senderId, int messageCount, int batchSizeInBytes)
            {
            }

            public bool OnVerifyCanSend(ulong destinationId, Type messageType, NetworkDelivery delivery)
            {
                return true;
            }

            public bool OnVerifyCanReceive(ulong senderId, Type messageType, FastBufferReader messageContent, ref NetworkContext context)
            {
                return true;
            }

            public void OnBeforeHandleMessage<T>(ref T message, ref NetworkContext context) where T : INetworkMessage
            {
                if (typeof(T) == typeof(TCatchType))
                {
                    Debug.Log("Woompa");
                    Assert.Fail($"{typeof(T).Name} was received when it should not have been.");
                }
            }

            public void OnAfterHandleMessage<T>(ref T message, ref NetworkContext context) where T : INetworkMessage
            {
            }
        }

        [UnityTest]
        public IEnumerator WhenSendingConnectionApprovedToAlreadyConnectedClient_ConnectionApprovedMessageIsRejected()
        {
            var message = new ConnectionApprovedMessage
            {
                ConnectedClientIds = new NativeArray<ulong>(0, Allocator.Temp)
            };
            m_ServerNetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, m_ClientNetworkManagers[0].LocalClientId);

            // Unnamed message is something to wait for. When this one is received,
            // we know the above one has also reached its destination.
            var writer = new FastBufferWriter(1, Allocator.Temp);
            using (writer)
            {
                writer.WriteByteSafe(0);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(m_ClientNetworkManagers[0].LocalClientId, writer);
            }

            m_ClientNetworkManagers[0].ConnectionManager.MessageManager.Hook(new Hooks<ConnectionApprovedMessage>());

            LogAssert.Expect(LogType.Error, new Regex($"A {nameof(ConnectionApprovedMessage)} was received from the server when the connection has already been established\\. NetworkTransport: Unity.Netcode.Transports.UTP.UnityTransport UnityTransportProtocol: UnityTransport. This should not happen\\."));

            yield return WaitForMessageReceived<UnnamedMessage>(m_ClientNetworkManagers.ToList());
        }

        [UnityTest]
        public IEnumerator WhenSendingConnectionRequestToAnyClient_ConnectionRequestMessageIsRejected()
        {
            var message = new ConnectionRequestMessage();
            m_ServerNetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, m_ClientNetworkManagers[0].LocalClientId);

            // Unnamed message is something to wait for. When this one is received,
            // we know the above one has also reached its destination.
            var writer = new FastBufferWriter(1, Allocator.Temp);
            using (writer)
            {
                writer.WriteByteSafe(0);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessage(m_ClientNetworkManagers[0].LocalClientId, writer);
            }

            m_ClientNetworkManagers[0].ConnectionManager.MessageManager.Hook(new Hooks<ConnectionRequestMessage>());

            LogAssert.Expect(LogType.Error, new Regex($"A {nameof(ConnectionRequestMessage)} was received from the server on the client side\\. NetworkTransport: Unity.Netcode.Transports.UTP.UnityTransport UnityTransportProtocol: UnityTransport. This should not happen\\."));

            yield return WaitForMessageReceived<UnnamedMessage>(m_ClientNetworkManagers.ToList());
        }

        [UnityTest]
        public IEnumerator WhenSendingConnectionRequestFromAlreadyConnectedClient_ConnectionRequestMessageIsRejected()
        {
            var message = new ConnectionRequestMessage();
            m_ClientNetworkManagers[0].ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, m_ServerNetworkManager.LocalClientId);

            // Unnamed message is something to wait for. When this one is received,
            // we know the above one has also reached its destination.
            var writer = new FastBufferWriter(1, Allocator.Temp);
            using (writer)
            {
                writer.WriteByteSafe(0);
                m_ClientNetworkManagers[0].CustomMessagingManager.SendUnnamedMessage(m_ServerNetworkManager.LocalClientId, writer);
            }

            m_ServerNetworkManager.ConnectionManager.MessageManager.Hook(new Hooks<ConnectionRequestMessage>());

            LogAssert.Expect(LogType.Error, new Regex($"A {nameof(ConnectionRequestMessage)} was received from a client when the connection has already been established\\. NetworkTransport: Unity.Netcode.Transports.UTP.UnityTransport UnityTransportProtocol: UnityTransport. This should not happen\\."));

            yield return WaitForMessageReceived<UnnamedMessage>(new List<NetworkManager> { m_ServerNetworkManager });
        }

        [UnityTest]
        public IEnumerator WhenSendingConnectionApprovedFromAnyClient_ConnectionApprovedMessageIsRejected()
        {
            var message = new ConnectionApprovedMessage
            {
                ConnectedClientIds = new NativeArray<ulong>(0, Allocator.Temp)
            };
            m_ClientNetworkManagers[0].ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, m_ServerNetworkManager.LocalClientId);

            // Unnamed message is something to wait for. When this one is received,
            // we know the above one has also reached its destination.
            var writer = new FastBufferWriter(1, Allocator.Temp);
            using (writer)
            {
                writer.WriteByteSafe(0);
                m_ClientNetworkManagers[0].CustomMessagingManager.SendUnnamedMessage(m_ServerNetworkManager.LocalClientId, writer);
            }

            m_ServerNetworkManager.ConnectionManager.MessageManager.Hook(new Hooks<ConnectionApprovedMessage>());

            LogAssert.Expect(LogType.Error, new Regex($"A {nameof(ConnectionApprovedMessage)} was received from a client on the server side\\. NetworkTransport: Unity.Netcode.Transports.UTP.UnityTransport UnityTransportProtocol: UnityTransport. This should not happen\\."));

            yield return WaitForMessageReceived<UnnamedMessage>(new List<NetworkManager> { m_ServerNetworkManager });
        }
    }
}
