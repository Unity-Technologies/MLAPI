using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{

    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class SenderIdTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private NetworkManager FirstClient => m_ClientNetworkManagers[0];
        private NetworkManager SecondClient => m_ClientNetworkManagers[1];

        public SenderIdTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        [UnityTest]
        public IEnumerator WhenSendingMessageFromServerToClient_SenderIdIsCorrect()
        {
            var messageContent = new ForceNetworkSerializeByMemcpy<Guid> { Value = Guid.NewGuid() };
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(messageContent);
                m_ServerNetworkManager.CustomMessagingManager.SendUnnamedMessageToAll(writer);
            }

            bool firstReceived = false;
            FirstClient.CustomMessagingManager.OnUnnamedMessage +=
                (sender, reader) =>
                {
                    firstReceived = true;
                    Assert.AreEqual(sender, NetworkManager.ServerClientId);
                    Assert.AreNotEqual(sender, FirstClient.LocalClientId);
                };

            bool secondReceived = false;
            SecondClient.CustomMessagingManager.OnUnnamedMessage +=
                (sender, reader) =>
                {
                    secondReceived = true;
                    Assert.AreEqual(sender, NetworkManager.ServerClientId);
                    Assert.AreNotEqual(sender, FirstClient.LocalClientId);
                };

            yield return new WaitForSeconds(0.2f);

            Assert.IsTrue(firstReceived);
            Assert.IsTrue(secondReceived);
        }
        [UnityTest]
        public IEnumerator WhenSendingMessageFromClientToServer_SenderIdIsCorrect()
        {
            var writer = new FastBufferWriter(1300, Allocator.Temp);
            using (writer)
            {
                FirstClient.CustomMessagingManager.SendNamedMessage("FirstClient", NetworkManager.ServerClientId, writer);
                SecondClient.CustomMessagingManager.SendNamedMessage("SecondClient", NetworkManager.ServerClientId, writer);

            }

            bool firstReceived = false;
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                "FirstClient",
                (sender, reader) =>
                {
                    firstReceived = true;
                    Assert.AreEqual(sender, FirstClient.LocalClientId);
                    Assert.AreNotEqual(sender, SecondClient.LocalClientId);
                    Assert.AreNotEqual(sender, m_ServerNetworkManager.LocalClientId);
                });

            bool secondReceived = false;
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                "SecondClient",
                (sender, reader) =>
                {
                    secondReceived = true;
                    Assert.AreNotEqual(sender, FirstClient.LocalClientId);
                    Assert.AreEqual(sender, SecondClient.LocalClientId);
                    Assert.AreNotEqual(sender, m_ServerNetworkManager.LocalClientId);
                });

            yield return new WaitForSeconds(0.2f);

            Assert.IsTrue(firstReceived);
            Assert.IsTrue(secondReceived);
        }


        private List<ulong> m_ClientsDisconnected = new List<ulong>();
        private ulong m_ClientToValidateDisconnected;

        private bool ValidateClientId()
        {
            return m_ClientsDisconnected.Contains(m_ClientToValidateDisconnected);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            m_ClientsDisconnected.Add(clientId);
        }

        [UnityTest]
        public IEnumerator WhenClientDisconnectsFromServer_ClientIdIsCorrect()
        {
            m_ClientsDisconnected.Clear();
            m_ServerNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            m_ClientToValidateDisconnected = m_ClientNetworkManagers[0].LocalClientId;
            FirstClient.Shutdown();
            yield return WaitForConditionOrTimeOut(ValidateClientId);
            AssertOnTimeout($"Timed out waiting for the server to receive Client-{m_ClientToValidateDisconnected} disconnect event!");

            m_ClientToValidateDisconnected = m_ClientNetworkManagers[1].LocalClientId;
            SecondClient.Shutdown();
            yield return WaitForConditionOrTimeOut(ValidateClientId);
            AssertOnTimeout($"Timed out waiting for the server to receive Client-{m_ClientToValidateDisconnected} disconnect event!");
        }
    }
}
