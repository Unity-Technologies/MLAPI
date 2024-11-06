using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Validates the client disconnection process.
    /// This assures that:
    /// - When a client disconnects from the server that the server:
    /// -- Detects the client disconnected.
    /// -- Cleans up the transport to NGO client (and vice versa) mappings.
    /// - When a server disconnects a client that:
    /// -- The client detects this disconnection.
    /// -- The server cleans up the transport to NGO client (and vice versa) mappings.
    /// - When <see cref="OwnerPersistence.DestroyWithOwner"/> the server-side player object is destroyed
    /// - When <see cref="OwnerPersistence.DontDestroyWithOwner"/> the server-side player object ownership is transferred back to the server
    /// </summary>
    [TestFixture(OwnerPersistence.DestroyWithOwner)]
    [TestFixture(OwnerPersistence.DontDestroyWithOwner)]
    internal class DisconnectTests : NetcodeIntegrationTest
    {
        public enum OwnerPersistence
        {
            DestroyWithOwner,
            DontDestroyWithOwner
        }

        public enum ClientDisconnectType
        {
            ServerDisconnectsClient,
            ClientDisconnectsFromServer
        }

        protected override int NumberOfClients => 1;

        private OwnerPersistence m_OwnerPersistence;
        private ClientDisconnectType m_ClientDisconnectType;
        private bool m_ClientDisconnected;
        private Dictionary<NetworkManager, ConnectionEventData> m_DisconnectedEvent = new Dictionary<NetworkManager, ConnectionEventData>();
        private ulong m_DisconnectEventClientId;
        private ulong m_TransportClientId;
        private ulong m_ClientId;


        public DisconnectTests(OwnerPersistence ownerPersistence)
        {
            m_OwnerPersistence = ownerPersistence;
        }

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.GetComponent<NetworkObject>().DontDestroyWithOwner = m_OwnerPersistence == OwnerPersistence.DontDestroyWithOwner;
            base.OnCreatePlayerPrefab();
        }

        protected override void OnServerAndClientsCreated()
        {
            // Adjusting client and server timeout periods to reduce test time
            // Get the tick frequency in milliseconds and triple it for the heartbeat timeout
            var heartBeatTimeout = (int)(300 * (1.0f / m_ServerNetworkManager.NetworkConfig.TickRate));
            var unityTransport = m_ServerNetworkManager.NetworkConfig.NetworkTransport as Transports.UTP.UnityTransport;
            if (unityTransport != null)
            {
                unityTransport.HeartbeatTimeoutMS = heartBeatTimeout;
            }

            unityTransport = m_ClientNetworkManagers[0].NetworkConfig.NetworkTransport as Transports.UTP.UnityTransport;
            if (unityTransport != null)
            {
                unityTransport.HeartbeatTimeoutMS = heartBeatTimeout;
            }

            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnSetup()
        {
            m_ClientDisconnected = false;
            m_ClientId = 0;
            m_TransportClientId = 0;
            return base.OnSetup();
        }

        /// <summary>
        /// Used to detect the client disconnected on the server side
        /// </summary>
        private void OnClientDisconnectCallback(ulong obj)
        {
            m_ClientDisconnected = true;
        }

        private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData connectionEventData)
        {
            if (connectionEventData.EventType != ConnectionEvent.ClientDisconnected)
            {
                return;
            }

            m_DisconnectedEvent.Add(networkManager, connectionEventData);
        }

        /// <summary>
        /// Conditional check to assure the transport to client (and vice versa) mappings are cleaned up
        /// </summary>
        private bool TransportIdCleanedUp()
        {
            if (m_ServerNetworkManager.ConnectionManager.TransportIdToClientId(m_TransportClientId) == m_ClientId)
            {
                return false;
            }

            if (m_ServerNetworkManager.ConnectionManager.ClientIdToTransportId(m_ClientId) == m_TransportClientId)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Conditional check to make sure the client player object no longer exists on the server side
        /// </summary>
        private bool DoesServerStillHaveSpawnedPlayerObject()
        {
            if (m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId].ContainsKey(m_ClientId))
            {
                var playerObject = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientId];
                if (playerObject != null && playerObject.IsSpawned)
                {
                    return false;
                }
            }
            return !m_ServerNetworkManager.SpawnManager.SpawnedObjects.Any(x => x.Value.IsPlayerObject && x.Value.OwnerClientId == m_ClientId);
        }

        [UnityTest]
        public IEnumerator ClientPlayerDisconnected([Values] ClientDisconnectType clientDisconnectType)
        {
            m_ClientId = m_ClientNetworkManagers[0].LocalClientId;
            m_ClientDisconnectType = clientDisconnectType;

            var serverSideClientPlayer = m_ServerNetworkManager.ConnectionManager.ConnectedClients[m_ClientId].PlayerObject;

            m_TransportClientId = m_ServerNetworkManager.ConnectionManager.ClientIdToTransportId(m_ClientId);

            if (clientDisconnectType == ClientDisconnectType.ServerDisconnectsClient)
            {
                m_ClientNetworkManagers[0].OnClientDisconnectCallback += OnClientDisconnectCallback;
                m_ClientNetworkManagers[0].OnConnectionEvent += OnConnectionEvent;
                m_ServerNetworkManager.OnConnectionEvent += OnConnectionEvent;
                m_ServerNetworkManager.DisconnectClient(m_ClientId);
            }
            else
            {
                m_ServerNetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
                m_ServerNetworkManager.OnConnectionEvent += OnConnectionEvent;
                m_ClientNetworkManagers[0].OnConnectionEvent += OnConnectionEvent;

                yield return StopOneClient(m_ClientNetworkManagers[0]);
            }

            yield return WaitForConditionOrTimeOut(() => m_ClientDisconnected);
            AssertOnTimeout("Timed out waiting for client to disconnect!");

            if (clientDisconnectType == ClientDisconnectType.ServerDisconnectsClient)
            {
                Assert.IsTrue(m_DisconnectedEvent.ContainsKey(m_ServerNetworkManager), $"Could not find the server {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent[m_ServerNetworkManager].ClientId == m_ClientId, $"Expected ClientID {m_ClientId} but found ClientID {m_DisconnectedEvent[m_ServerNetworkManager].ClientId} for the server {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent.ContainsKey(m_ClientNetworkManagers[0]), $"Could not find the client {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent[m_ClientNetworkManagers[0]].ClientId == m_ClientId, $"Expected ClientID {m_ClientId} but found ClientID {m_DisconnectedEvent[m_ServerNetworkManager].ClientId} for the client {nameof(NetworkManager)} disconnect event entry!");
                // Unregister for this event otherwise it will be invoked during teardown
                m_ServerNetworkManager.OnConnectionEvent -= OnConnectionEvent;
            }
            else
            {
                Assert.IsTrue(m_DisconnectedEvent.ContainsKey(m_ServerNetworkManager), $"Could not find the server {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent[m_ServerNetworkManager].ClientId == m_ClientId, $"Expected ClientID {m_ClientId} but found ClientID {m_DisconnectedEvent[m_ServerNetworkManager].ClientId} for the server {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent.ContainsKey(m_ClientNetworkManagers[0]), $"Could not find the client {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent[m_ClientNetworkManagers[0]].ClientId == m_ClientId, $"Expected ClientID {m_ClientId} but found ClientID {m_DisconnectedEvent[m_ServerNetworkManager].ClientId} for the client {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Count == 1, $"Expected connected client identifiers count to be 1 but it was {m_ServerNetworkManager.ConnectedClientsIds.Count}!");
                Assert.IsTrue(m_ServerNetworkManager.ConnectedClients.Count == 1, $"Expected connected client identifiers count to be 1 but it was {m_ServerNetworkManager.ConnectedClients.Count}!");
                Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsList.Count == 1, $"Expected connected client identifiers count to be 1 but it was {m_ServerNetworkManager.ConnectedClientsList.Count}!");
            }

            if (m_OwnerPersistence == OwnerPersistence.DestroyWithOwner)
            {
                // When we are destroying with the owner, validate the player object is destroyed on the server side
                yield return WaitForConditionOrTimeOut(DoesServerStillHaveSpawnedPlayerObject);
                AssertOnTimeout("Timed out waiting for client's player object to be destroyed!");
            }
            else
            {
                // When we are not destroying with the owner, ensure the player object's ownership was transferred back to the server
                yield return WaitForConditionOrTimeOut(() => serverSideClientPlayer.IsOwnedByServer);
                AssertOnTimeout("The client's player object's ownership was not transferred back to the server!");
            }

            yield return WaitForConditionOrTimeOut(TransportIdCleanedUp);
            AssertOnTimeout("Timed out waiting for transport and client id mappings to be cleaned up!");

            // Validate the host-client generates a OnClientDisconnected event when it shutsdown.
            // Only test when the test run is the client disconnecting from the server (otherwise the server will be shutdown already)
            if (clientDisconnectType == ClientDisconnectType.ClientDisconnectsFromServer)
            {
                m_DisconnectedEvent.Clear();
                m_ClientDisconnected = false;
                m_ServerNetworkManager.Shutdown();

                yield return WaitForConditionOrTimeOut(() => m_ClientDisconnected);
                AssertOnTimeout("Timed out waiting for host-client to generate disconnect message!");

                Assert.IsTrue(m_DisconnectedEvent.ContainsKey(m_ServerNetworkManager), $"Could not find the server {nameof(NetworkManager)} disconnect event entry!");
                Assert.IsTrue(m_DisconnectedEvent[m_ServerNetworkManager].ClientId == NetworkManager.ServerClientId, $"Expected ClientID {m_ClientId} but found ClientID {m_DisconnectedEvent[m_ServerNetworkManager].ClientId} for the server {nameof(NetworkManager)} disconnect event entry!");
            }
        }
    }
}
