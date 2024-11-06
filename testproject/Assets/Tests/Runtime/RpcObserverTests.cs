using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    /// <summary>
    /// Integration test to validate ClientRpcs will only
    /// send to observers of the NetworkObject
    /// </summary>
#if NGO_DAMODE
    [TestFixture(HostOrServer.DAHost)]
#endif
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class RpcObserverTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 9;

        private GameObject m_TestPrefab;

        private GameObject m_ServerPrefabInstance;
        private RpcObserverObject m_ServerRpcObserverObject;

        private NativeArray<ulong> m_NonObserverArrayError;
        private bool m_ArrayAllocated;

        public RpcObserverTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override void OnServerAndClientsCreated()
        {
            m_TestPrefab = CreateNetworkObjectPrefab($"{nameof(RpcObserverObject)}");
            m_TestPrefab.AddComponent<RpcObserverObject>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            m_ServerPrefabInstance = SpawnObject(m_TestPrefab, m_ServerNetworkManager);
            m_ServerRpcObserverObject = m_ServerPrefabInstance.GetComponent<RpcObserverObject>();
            return base.OnServerAndClientsConnected();
        }

        [UnityTest]
        public IEnumerator ClientRpcObserverTest()
        {
            // Wait for all clients to report they have spawned an instance of our test prefab
            yield return WaitForConditionOrTimeOut(m_ServerRpcObserverObject.AllClientsSpawned);

            var nonObservers = new List<ulong>();

            // We start out with all clients being observers of the test prefab
            // Test that all clients receive the RPC
            yield return RunRpcObserverTest(nonObservers);

            // This hides the test prefab from one client and then runs the test
            // and repeats until all clients are no longer observers
            foreach (var clientId in m_ServerNetworkManager.ConnectedClientsIds)
            {
                if (clientId == m_ServerNetworkManager.LocalClientId)
                {
                    continue;
                }
                // Hide it from the client
                m_ServerRpcObserverObject.NetworkObject.NetworkHide(clientId);
                nonObservers.Add(clientId);
                // Provide 1 tick for the client to hide the NetworkObject
                yield return s_DefaultWaitForTick;

                // Run the test
                yield return RunRpcObserverTest(nonObservers);
            }

            // ****** Verify that sending to non-observer(s) generates error ******
            var clientRpcParams = new ClientRpcParams();
            // Verify that we get an error message when we try to send to a non-observer using TargetClientIds
            clientRpcParams.Send.TargetClientIds = new List<ulong>() { nonObservers[0] };
            m_ServerNetworkManager.LogLevel = LogLevel.Error;
            LogAssert.Expect(LogType.Error, "[Netcode] " + m_ServerRpcObserverObject.GenerateObserverErrorMessage(clientRpcParams, nonObservers[0]));
            m_ServerRpcObserverObject.ObserverMessageClientRpc(clientRpcParams);
            yield return s_DefaultWaitForTick;

            m_NonObserverArrayError = new NativeArray<ulong>(clientRpcParams.Send.TargetClientIds.ToArray(), Allocator.Persistent);
            m_ArrayAllocated = true;

            // Now clean the TargetClientIds to prepare for the next TargetClientIdsNativeArray error check
            clientRpcParams.Send.TargetClientIds = null;

            // Now verify that we get an error message when we try to send to a non-observer using TargetClientIdsNativeArray
            clientRpcParams.Send.TargetClientIdsNativeArray = m_NonObserverArrayError;
            LogAssert.Expect(LogType.Error, "[Netcode] " + m_ServerRpcObserverObject.GenerateObserverErrorMessage(clientRpcParams, nonObservers[0]));
            m_ServerRpcObserverObject.ObserverMessageClientRpc(clientRpcParams);
            yield return s_DefaultWaitForTick;

            // Validate we can still just send to the host-client when no clients are connected
            if (m_UseHost)
            {
                m_ServerRpcObserverObject.ResetTest();

                foreach (var clientId in nonObservers)
                {
                    m_ServerNetworkManager.DisconnectClient(clientId);
                }

                yield return s_DefaultWaitForTick;

                m_ServerRpcObserverObject.ObserverMessageClientRpc();

                yield return s_DefaultWaitForTick;

                Assert.True(m_ServerRpcObserverObject.HostReceivedMessage, "Host failed to receive the ClientRpc when no clients were connected!");
                Assert.False(m_ServerRpcObserverObject.NonObserversReceivedRPC(nonObservers), $"Non-observers ({m_ServerRpcObserverObject.GetClientIdsAsString(nonObservers)}) received the RPC message!");
            }

            m_ServerNetworkManager.LogLevel = LogLevel.Normal;
        }

        /// <summary>
        /// This will send an RPC to all observers of the test prefab and check
        /// to see if the observer clients receive the message and also confirms
        /// that the non-observer clients did not receive the message.
        /// </summary>
        private IEnumerator RunRpcObserverTest(List<ulong> nonObservers)
        {
            m_ServerRpcObserverObject.ResetTest();

            m_ServerRpcObserverObject.ObserverMessageClientRpc();

            yield return WaitForConditionOrTimeOut(m_ServerRpcObserverObject.AllObserversReceivedRPC);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all clients to receive message!\n" +
                $"Clients that received the message:{m_ServerRpcObserverObject.GetClientIdsAsString()}");
            Assert.False(m_ServerRpcObserverObject.NonObserversReceivedRPC(nonObservers), $"Non-observers ({m_ServerRpcObserverObject.GetClientIdsAsString(nonObservers)}) received the RPC message!");

            // Always verify the host received the RPC
            if (m_UseHost)
            {
                Assert.True(m_ServerRpcObserverObject.HostReceivedMessage, $"Host failed to receive the ClientRpc with the following observers: {m_ServerRpcObserverObject.GetClientIdsAsString()}!");
            }
        }

        /// <summary>
        /// Validates that when a NetworkObject is despawned but not destroyed
        /// and then re-spawned that the observer list is cleared
        /// </summary>
        [UnityTest]
        public IEnumerator DespawnRespawnObserverTest()
        {
            var nonObservers = new List<ulong>();
            m_ServerRpcObserverObject.ResetTest();
            // Wait for all clients to report they have spawned an instance of our test prefab
            yield return WaitForConditionOrTimeOut(m_ServerRpcObserverObject.AllClientsSpawned);

            m_ServerRpcObserverObject.ObserverMessageClientRpc();

            yield return WaitForConditionOrTimeOut(m_ServerRpcObserverObject.AllObserversReceivedRPC);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all clients to receive message!\n" +
                $"Clients that received the message:{m_ServerRpcObserverObject.GetClientIdsAsString()}");
            Assert.False(m_ServerRpcObserverObject.NonObserversReceivedRPC(nonObservers), $"Non-observers ({m_ServerRpcObserverObject.GetClientIdsAsString(nonObservers)}) received the RPC message!");

            m_ServerRpcObserverObject.NetworkObject.Despawn(false);

            Assert.True(m_ServerRpcObserverObject.NetworkObject.Observers.Count == 0, $"Despawned {m_ServerRpcObserverObject.name} but it still has {m_ServerRpcObserverObject.NetworkObject.Observers.Count} observers!");

            for (int i = 4; i < NumberOfClients; i++)
            {
                nonObservers.Add(m_ClientNetworkManagers[i].LocalClientId);
                m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[i].LocalClientId);
            }

            yield return s_DefaultWaitForTick;
            m_ServerRpcObserverObject.ResetTest();
            m_ServerRpcObserverObject.NetworkObject.Spawn();
            m_ServerRpcObserverObject.ObserverMessageClientRpc();
            yield return WaitForConditionOrTimeOut(m_ServerRpcObserverObject.AllObserversReceivedRPC);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all clients to receive message!\n" +
                $"Clients that received the message:{m_ServerRpcObserverObject.GetClientIdsAsString()}");
            Assert.False(m_ServerRpcObserverObject.NonObserversReceivedRPC(nonObservers), $"Non-observers ({m_ServerRpcObserverObject.GetClientIdsAsString(nonObservers)}) received the RPC message!");

            // Always verify the host received the RPC
            if (m_UseHost)
            {
                Assert.True(m_ServerRpcObserverObject.HostReceivedMessage, $"Host failed to receive the ClientRpc with the following observers: {m_ServerRpcObserverObject.GetClientIdsAsString()}!");
            }
        }

        protected override IEnumerator OnTearDown()
        {
            // Make sure to dispose of the native array
            if (m_ArrayAllocated)
            {
                m_ArrayAllocated = false;
                m_NonObserverArrayError.Dispose();
            }
            return base.OnTearDown();
        }
    }

    /// <summary>
    /// Test prefab component used with RpcObserverTests
    /// </summary>
    public class RpcObserverObject : NetworkBehaviour
    {
        public readonly List<ulong> ObserversThatReceivedRPC = new List<ulong>();

        public static readonly List<ulong> ClientInstancesSpawned = new List<ulong>();

        protected bool m_NotifyClientReceivedMessage;
        public bool HostReceivedMessage { get; internal set; }

        public string GetClientIdsAsString(List<ulong> clientIds = null)
        {
            if (clientIds == null)
            {
                clientIds = ObserversThatReceivedRPC;
            }
            var clientIdsAsString = string.Empty;
            foreach (var clientId in clientIds)
            {
                clientIdsAsString += $"({clientId})";
            }
            return clientIdsAsString;
        }

        /// <summary>
        /// Returns true if all connected clients have spawned the test prefab
        /// </summary>
        public bool AllClientsSpawned()
        {
            if (!IsServer)
            {
                return false;
            }

            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }

                if (!ClientInstancesSpawned.Contains(clientId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns true if all observers have received the RPC
        /// </summary>
        public bool AllObserversReceivedRPC()
        {
            if (!IsServer)
            {
                return false;
            }

            foreach (var clientId in NetworkObject.Observers)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!ObserversThatReceivedRPC.Contains(clientId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns true if any clientId in the nonObservers list received the RPC
        /// </summary>
        /// <param name="nonObservers">list of clientIds that should not have received the RPC message</param>
        public bool NonObserversReceivedRPC(List<ulong> nonObservers)
        {
            foreach (var clientId in nonObservers)
            {
                // return false if a non-observer received the RPC
                if (ObserversThatReceivedRPC.Contains(clientId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Clears the received
        /// </summary>
        public void ResetTest()
        {
            ObserversThatReceivedRPC.Clear();
            HostReceivedMessage = false;
        }

        /// <summary>
        /// Called from server-host once per test run
        /// </summary>
        [ClientRpc]
        public void ObserverMessageClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (IsHost)
            {
                HostReceivedMessage = true;
            }
            else
            {
                m_NotifyClientReceivedMessage = true;
            }
        }

        /// <summary>
        /// Called by each observer client that received the ObserverMessageClientRpc message
        /// The sender id is added to the ObserversThatReceivedRPC list
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ObserverMessageServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ObserversThatReceivedRPC.Add(serverRpcParams.Receive.SenderClientId);
        }

        public override void OnNetworkSpawn()
        {
            if (IsClient)
            {
                ClientInstancesSpawned.Add(NetworkManager.LocalClientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsClient)
            {
                ClientInstancesSpawned.Remove(NetworkManager.LocalClientId);
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                return;
            }

            if (m_NotifyClientReceivedMessage)
            {
                m_NotifyClientReceivedMessage = false;
                ObserverMessageServerRpc();
            }
        }
    }
}
