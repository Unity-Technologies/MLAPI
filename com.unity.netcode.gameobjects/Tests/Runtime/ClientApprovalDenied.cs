using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;


namespace Unity.Netcode.RuntimeTests
{
    internal class ClientApprovalDenied : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;
        private bool m_ApproveConnection = true;
        private ulong m_PendingClientId = 0;
        private ulong m_DisconnectedClientId = 0;

        private List<ulong> m_DisconnectedClientIdentifiers = new List<ulong>();

        private void ConnectionApproval(NetworkManager.ConnectionApprovalRequest connectionApprovalRequest, NetworkManager.ConnectionApprovalResponse connectionApprovalResponse)
        {
            connectionApprovalResponse.Approved = m_ApproveConnection;
            connectionApprovalResponse.CreatePlayerObject = true;
            // When denied, store the client identifier to use for validating the client disconnected notification identifier matches
            if (!m_ApproveConnection)
            {
                m_PendingClientId = connectionApprovalRequest.ClientNetworkId;
            }
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.ConnectionApproval = true;
            base.OnNewClientCreated(networkManager);
        }

        protected override bool ShouldWaitForNewClientToConnect(NetworkManager networkManager)
        {
            return false;
        }

        /// <summary>
        /// Validates that when a pending client is denied approval the server-host
        /// OnClientDisconnected method will return the valid pending client identifier.
        /// </summary>
        [UnityTest]
        public IEnumerator ClientDeniedAndDisconnectionNotificationTest()
        {
            m_ServerNetworkManager.NetworkConfig.ConnectionApproval = true;
            m_ServerNetworkManager.ConnectionApprovalCallback = ConnectionApproval;
            m_ApproveConnection = false;
            m_ServerNetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            yield return CreateAndStartNewClient();
            yield return WaitForConditionOrTimeOut(() => m_PendingClientId == m_DisconnectedClientId);
            AssertOnTimeout($"Timed out waiting for disconnect notification for pending Client-{m_PendingClientId}!");

            // Validate that we don't get multiple disconnect notifications for clients being disconnected
            // Have a client disconnect remotely
            m_ClientNetworkManagers[0].Shutdown();

            // Have the server disconnect a client
            m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[1].LocalClientId);
            m_ServerNetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            Assert.False(m_DisconnectedClientIdentifiers.Contains(clientId), $"Received two disconnect notifications from Client-{clientId}!");
            m_DisconnectedClientIdentifiers.Add(clientId);
            m_DisconnectedClientId = clientId;
        }
    }
}
