using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;


namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    [TestFixture(HostOrServer.DAHost)]
    internal class NetworkClientAndPlayerObjectTests : NetcodeIntegrationTest
    {
        private const int k_PlayerPrefabCount = 6;
        protected override int NumberOfClients => 2;

        private List<GameObject> m_PlayerPrefabs = new List<GameObject>();
        private Dictionary<ulong, uint> m_ChangedPlayerPrefabs = new Dictionary<ulong, uint>();


        public NetworkClientAndPlayerObjectTests(HostOrServer hostOrServer) : base(hostOrServer)
        {
        }

        protected override IEnumerator OnTearDown()
        {
            m_PlayerPrefabs.Clear();
            return base.OnTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_PlayerPrefabs.Clear();
            for (int i = 0; i < k_PlayerPrefabCount; i++)
            {
                m_PlayerPrefabs.Add(CreateNetworkObjectPrefab($"PlayerPrefab{i}"));
            }
            base.OnServerAndClientsCreated();
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.Prefabs = m_ServerNetworkManager.NetworkConfig.Prefabs;
            if (m_DistributedAuthority)
            {
                networkManager.OnFetchLocalPlayerPrefabToSpawn = FetchPlayerPrefabToSpawn;
            }
            base.OnNewClientCreated(networkManager);
        }

        /// <summary>
        /// Only for distributed authority mode
        /// </summary>
        /// <returns>a unique player prefab for the player</returns>
        private GameObject FetchPlayerPrefabToSpawn()
        {
            var prefabObject = GetRandomPlayerPrefab();
            var clientId = m_ClientNetworkManagers[m_ClientNetworkManagers.Length - 1].LocalClientId;
            m_ChangedPlayerPrefabs.Add(clientId, prefabObject.GlobalObjectIdHash);
            return prefabObject.gameObject;
        }


        private StringBuilder m_ErrorLogLevel3 = new StringBuilder();
        private StringBuilder m_ErrorLogLevel2 = new StringBuilder();
        private StringBuilder m_ErrorLogLevel1 = new StringBuilder();

        private bool ValidateNetworkClient(NetworkClient networkClient)
        {
            m_ErrorLogLevel3.Clear();
            var success = true;
            if (networkClient == null)
            {
                m_ErrorLogLevel3.Append($"[NetworkClient is NULL]");
                // Log error
                success = false;
            }
            if (!networkClient.IsConnected)
            {
                m_ErrorLogLevel3.Append($"[NetworkClient {nameof(NetworkClient.IsConnected)}] is false]");
                // Log error
                success = false;
            }
            if (networkClient.PlayerObject == null)
            {
                m_ErrorLogLevel3.Append($"[NetworkClient {nameof(NetworkClient.PlayerObject)}] is NULL]");
                // Log error
                success = false;
            }
            return success;
        }

        private bool ValidateNetworkManagerNetworkClients(NetworkManager networkManager)
        {
            var success = true;
            m_ErrorLogLevel2.Clear();

            // Number of connected clients plus the DAHost
            var expectedCount = m_ClientNetworkManagers.Length + (m_UseHost ? 1 : 0);

            if (networkManager.ConnectedClients.Count != expectedCount)
            {
                m_ErrorLogLevel2.Append($"[{nameof(NetworkManager.ConnectedClients)} count: {networkManager.ConnectedClients.Count} vs expected count: {expectedCount}]");
                // Log error
                success = false;
            }

            if (m_UseHost && !ValidateNetworkClient(networkManager.LocalClient))
            {
                m_ErrorLogLevel2.Append($"[Local NetworkClient: --({m_ErrorLogLevel3})--]");
                // Log error
                success = false;
            }

            foreach (var networkClient in networkManager.ConnectedClients)
            {
                // When just running a server, ignore the server's local NetworkClient
                if (!m_UseHost && networkManager.IsServer)
                {
                    continue;
                }
                if (!ValidateNetworkClient(networkManager.LocalClient))
                {
                    // Log error
                    success = false;
                    m_ErrorLogLevel2.Append($"[NetworkClient-{networkManager.LocalClientId}: --({m_ErrorLogLevel3})--]");
                }
            }
            return success;
        }

        private bool AllNetworkClientsValidated()
        {
            m_ErrorLogLevel1.Clear();
            var success = true;

            if (!UseCMBService())
            {
                if (!ValidateNetworkManagerNetworkClients(m_ServerNetworkManager))
                {
                    m_ErrorLogLevel1.AppendLine($"[Client-{m_ServerNetworkManager.LocalClientId}]{m_ErrorLogLevel2}");
                    // Log error
                    success = false;
                }
            }

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                if (!ValidateNetworkManagerNetworkClients(clientNetworkManager))
                {
                    m_ErrorLogLevel1.AppendLine($"[Client-{clientNetworkManager.LocalClientId}]{m_ErrorLogLevel2}");
                    // Log error
                    success = false;
                }
            }
            return success;
        }

        /// <summary>
        /// Validates that all NetworkManager instances have valid NetworkClients for all connected clients
        /// Validates the same thing when a client late joins and when a client disconnects.
        /// </summary>
        [UnityTest]
        public IEnumerator ValidateNetworkClients()
        {
            // Validate the initial clients created
            yield return WaitForConditionOrTimeOut(AllNetworkClientsValidated);
            AssertOnTimeout($"[Start] Not all NetworkClients were valid!\n{m_ErrorLogLevel1}");

            // Late join a player and revalidate all instances
            yield return CreateAndStartNewClient();
            yield return WaitForConditionOrTimeOut(AllNetworkClientsValidated);
            AssertOnTimeout($"[Late Join] Not all NetworkClients were valid!\n{m_ErrorLogLevel1}");

            // Disconnect a player and revalidate all instances
            var initialCount = m_ClientNetworkManagers.Length;
            yield return StopOneClient(m_ClientNetworkManagers[m_ClientNetworkManagers.Length - 1], true);
            // Sanity check to assure we removed the NetworkManager from m_ClientNetworkManagers
            Assert.False(initialCount == m_ClientNetworkManagers.Length, $"Disconnected player and expected total number of client {nameof(NetworkManager)}s " +
                $"to be {initialCount - 1} but it was still {initialCount}!");

            yield return WaitForConditionOrTimeOut(AllNetworkClientsValidated);
            AssertOnTimeout($"[Client Disconnect] Not all NetworkClients were valid!\n{m_ErrorLogLevel1}");

        }

        /// <summary>
        /// Verify that all NetworkClients are pointing to the correct player object, even if
        /// the player object is changed.
        /// </summary>
        private bool ValidatePlayerObjectOnClients(NetworkManager clientToValidate)
        {
            m_ErrorLogLevel2.Clear();
            var success = true;
            var expectedGlobalObjectIdHash = m_ChangedPlayerPrefabs[clientToValidate.LocalClientId];
            if (expectedGlobalObjectIdHash != clientToValidate.LocalClient.PlayerObject.GlobalObjectIdHash)
            {
                m_ErrorLogLevel2.Append($"[Local Prefab Mismatch][Expected GlobalObjectIdHash: {expectedGlobalObjectIdHash} but was {clientToValidate.LocalClient.PlayerObject.GlobalObjectIdHash}]");
                success = false;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (client == clientToValidate)
                {
                    continue;
                }
                var remoteNetworkClient = client.ConnectedClients[clientToValidate.LocalClientId];
                if (expectedGlobalObjectIdHash != remoteNetworkClient.PlayerObject.GlobalObjectIdHash)
                {
                    m_ErrorLogLevel2.Append($"[Client-{client.LocalClientId} Prefab Mismatch][Expected GlobalObjectIdHash: {expectedGlobalObjectIdHash} but was {remoteNetworkClient.PlayerObject.GlobalObjectIdHash}]");
                    success = false;
                }
            }
            return success;
        }

        private bool ValidateAllPlayerObjects()
        {
            m_ErrorLogLevel1.Clear();
            var success = true;
            if (m_UseHost && !UseCMBService())
            {
                if (!ValidatePlayerObjectOnClients(m_ServerNetworkManager))
                {
                    m_ErrorLogLevel1.AppendLine($"[Client-{m_ServerNetworkManager.LocalClientId}]{m_ErrorLogLevel2}");
                    success = false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!ValidatePlayerObjectOnClients(client))
                {
                    m_ErrorLogLevel1.AppendLine($"[Client-{client.LocalClientId}]{m_ErrorLogLevel2}");
                    success = false;
                }
            }

            return success;
        }

        private NetworkObject GetRandomPlayerPrefab()
        {
            return m_PlayerPrefabs[Random.Range(0, m_PlayerPrefabs.Count() - 1)].GetComponent<NetworkObject>();
        }

        /// <summary>
        /// Validates that when a client changes their player object that all connected client instances mirror the
        /// client's new player object.
        /// </summary>
        [UnityTest]
        public IEnumerator ValidatePlayerObjects()
        {
            // Just do a quick validation for all connected client's NetworkClients
            yield return WaitForConditionOrTimeOut(AllNetworkClientsValidated);
            AssertOnTimeout($"Not all NetworkClients were valid!\n{m_ErrorLogLevel1}");

            // Now, have each client spawn a new player object
            m_ChangedPlayerPrefabs.Clear();
            var playerInstance = (GameObject)null;
            var playerPrefabToSpawn = (NetworkObject)null;
            if (m_UseHost)
            {
                playerPrefabToSpawn = GetRandomPlayerPrefab();
                playerInstance = SpawnPlayerObject(playerPrefabToSpawn.gameObject, m_ServerNetworkManager);
                m_ChangedPlayerPrefabs.Add(m_ServerNetworkManager.LocalClientId, playerPrefabToSpawn.GlobalObjectIdHash);
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                playerPrefabToSpawn = GetRandomPlayerPrefab();
                playerInstance = SpawnPlayerObject(playerPrefabToSpawn.gameObject, client);
                m_ChangedPlayerPrefabs.Add(client.LocalClientId, playerPrefabToSpawn.GlobalObjectIdHash);
            }

            // Validate that all connected clients' NetworkClient instances have the correct player object for each connected client
            yield return WaitForConditionOrTimeOut(ValidateAllPlayerObjects);
            AssertOnTimeout($"[Existing Clients] Not all NetworkClient player objects were valid!\n{m_ErrorLogLevel1}");

            // Distributed authority only feature validation (NetworkManager.OnFetchLocalPlayerPrefabToSpawn)
            if (m_DistributedAuthority)
            {
                // Now test the fetch prefab callback to assure that this is working correctly.
                // Start a new client and wait for it to connect
                yield return CreateAndStartNewClient();
                // Do another validation pass.
                yield return WaitForConditionOrTimeOut(ValidateAllPlayerObjects);
                AssertOnTimeout($"[Late Joined Client] Not all NetworkClient player objects were valid!\n{m_ErrorLogLevel1}");
            }
        }
    }
}
