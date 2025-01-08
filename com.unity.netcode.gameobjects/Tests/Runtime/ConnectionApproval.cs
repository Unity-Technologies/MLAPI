using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(PlayerCreation.Prefab)]
    [TestFixture(PlayerCreation.PrefabHash)]
    [TestFixture(PlayerCreation.NoPlayer)]
    [TestFixture(PlayerCreation.FailValidation)]
    internal class ConnectionApprovalTests : NetcodeIntegrationTest
    {
        private const string k_InvalidToken = "Invalid validation token!";
        public enum PlayerCreation
        {
            Prefab,
            PrefabHash,
            NoPlayer,
            FailValidation
        }
        private PlayerCreation m_PlayerCreation;
        private bool m_ClientDisconnectReasonValidated;

        private Dictionary<ulong, bool> m_Validated = new Dictionary<ulong, bool>();

        public ConnectionApprovalTests(PlayerCreation playerCreation)
        {
            m_PlayerCreation = playerCreation;
        }

        protected override int NumberOfClients => 1;

        private Guid m_ValidationToken;

        protected override bool ShouldCheckForSpawnedPlayers()
        {
            return m_PlayerCreation != PlayerCreation.NoPlayer;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ClientDisconnectReasonValidated = false;
            m_BypassConnectionTimeout = m_PlayerCreation == PlayerCreation.FailValidation;
            m_Validated.Clear();
            m_ValidationToken = Guid.NewGuid();
            var validationToken = Encoding.UTF8.GetBytes(m_ValidationToken.ToString());
            m_ServerNetworkManager.ConnectionApprovalCallback = NetworkManagerObject_ConnectionApprovalCallback;
            m_ServerNetworkManager.NetworkConfig.PlayerPrefab = m_PlayerCreation == PlayerCreation.Prefab ? m_PlayerPrefab : null;
            if (m_PlayerCreation == PlayerCreation.PrefabHash)
            {
                m_ServerNetworkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab() { Prefab = m_PlayerPrefab });
            }
            m_ServerNetworkManager.NetworkConfig.ConnectionApproval = true;
            m_ServerNetworkManager.NetworkConfig.ConnectionData = validationToken;

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.PlayerPrefab = m_PlayerCreation == PlayerCreation.Prefab ? m_PlayerPrefab : null;
                if (m_PlayerCreation == PlayerCreation.PrefabHash)
                {
                    client.NetworkConfig.Prefabs.Add(new NetworkPrefab() { Prefab = m_PlayerPrefab });
                }
                client.NetworkConfig.ConnectionApproval = true;
                client.NetworkConfig.ConnectionData = m_PlayerCreation == PlayerCreation.FailValidation ? Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) : validationToken;
                if (m_PlayerCreation == PlayerCreation.FailValidation)
                {
                    client.OnClientDisconnectCallback += Client_OnClientDisconnectCallback;
                }
            }

            base.OnServerAndClientsCreated();
        }

        private void Client_OnClientDisconnectCallback(ulong clientId)
        {
            m_ClientNetworkManagers[0].OnClientDisconnectCallback -= Client_OnClientDisconnectCallback;
            m_ClientDisconnectReasonValidated = m_ClientNetworkManagers[0].LocalClientId == clientId && m_ClientNetworkManagers[0].DisconnectReason == k_InvalidToken;
        }

        private bool ClientAndHostValidated()
        {
            if (!m_Validated.ContainsKey(m_ServerNetworkManager.LocalClientId) || !m_Validated[m_ServerNetworkManager.LocalClientId])
            {
                return false;
            }
            if (m_PlayerCreation == PlayerCreation.FailValidation)
            {
                return m_ClientDisconnectReasonValidated;
            }
            else
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!m_Validated.ContainsKey(client.LocalClientId) || !m_Validated[client.LocalClientId])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator ConnectionApproval()
        {
            yield return WaitForConditionOrTimeOut(ClientAndHostValidated);
            AssertOnTimeout("Timed out waiting for all clients to be approved!");
        }

        private void NetworkManagerObject_ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var stringGuid = Encoding.UTF8.GetString(request.Payload);

            if (m_ValidationToken.ToString() == stringGuid)
            {
                m_Validated.Add(request.ClientNetworkId, true);
                response.Approved = true;
            }
            else
            {
                response.Approved = false;
                response.Reason = "Invalid validation token!";
            }

            response.CreatePlayerObject = ShouldCheckForSpawnedPlayers();
            response.Position = null;
            response.Rotation = null;
            response.PlayerPrefabHash = m_PlayerCreation == PlayerCreation.PrefabHash ? m_PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash : null;
        }


        [Test]
        public void VerifyUniqueNetworkConfigPerRequest()
        {
            var networkConfig = new NetworkConfig
            {
                EnableSceneManagement = true,
                TickRate = 30
            };
            var currentHash = networkConfig.GetConfig();
            networkConfig.EnableSceneManagement = false;
            networkConfig.TickRate = 60;
            var newHash = networkConfig.GetConfig(false);

            Assert.True(currentHash != newHash, $"Hashed {nameof(NetworkConfig)} values {currentHash} and {newHash} should not be the same!");
        }
    }
}
