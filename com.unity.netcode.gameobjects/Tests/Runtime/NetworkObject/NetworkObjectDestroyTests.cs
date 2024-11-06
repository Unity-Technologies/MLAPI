using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Tests calling destroy on spawned / unspawned <see cref="NetworkObject"/>s. Expected behavior:
    /// - Server or client destroy on unspawned => Object gets destroyed, no exceptions
    /// - Server destroy spawned => Object gets destroyed and despawned/destroyed on all clients. Server does not run <see cref="NetworkPrefaInstanceHandler.HandleNetworkPrefabDestroy"/>. Client runs it.
    /// - Client destroy spawned => throw exception.
    /// </summary>


    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    internal class NetworkObjectDestroyTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        public NetworkObjectDestroyTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        /// <summary>
        /// Tests that a server can destroy a NetworkObject and that it gets despawned correctly.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestNetworkObjectAuthorityDestroy()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation(x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId, m_ServerNetworkManager, serverClientPlayerResult);

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation(x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId, m_ClientNetworkManagers[0], clientClientPlayerResult);

            Assert.IsNotNull(serverClientPlayerResult.Result.gameObject);
            Assert.IsNotNull(clientClientPlayerResult.Result.gameObject);

            var targetNetworkManager = m_ClientNetworkManagers[0];
            if (m_DistributedAuthority)
            {
                targetNetworkManager = m_ClientNetworkManagers[1];
                // destroy the authoritative player (distributed authority)
                Object.Destroy(clientClientPlayerResult.Result.gameObject);
            }
            else
            {
                // destroy the authoritative player (client-server)
                Object.Destroy(serverClientPlayerResult.Result.gameObject);
            }

            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeHandled<DestroyObjectMessage>(targetNetworkManager);

            Assert.IsTrue(serverClientPlayerResult.Result == null); // Assert.IsNull doesn't work here
            Assert.IsTrue(clientClientPlayerResult.Result == null);

            // validate that any unspawned networkobject can be destroyed
            var go = new GameObject();
            go.AddComponent<NetworkObject>();
            Object.Destroy(go);

            yield return null;
            Assert.IsTrue(go == null);
        }


        public enum ClientDestroyObject
        {
            ShuttingDown,
            ActiveSession
        }

        private string m_ClientPlayerName;
        private ulong m_ClientNetworkObjectId;
        /// <summary>
        /// Validates the expected behavior when the client-side destroys a <see cref="NetworkObject"/>
        /// </summary>
        [UnityTest]
        public IEnumerator TestNetworkObjectClientDestroy([Values] ClientDestroyObject clientDestroyObject)
        {
            var isShuttingDown = clientDestroyObject == ClientDestroyObject.ShuttingDown;
            var clientPlayer = m_ClientNetworkManagers[0].LocalClient.PlayerObject;
            var clientId = clientPlayer.OwnerClientId;

            //destroying a NetworkObject while shutting down is allowed
            if (isShuttingDown)
            {
                if (m_DistributedAuthority)
                {
                    // Shutdown the 2nd client
                    m_ClientNetworkManagers[1].Shutdown();
                }
                else
                {
                    // Shutdown the
                    m_ClientNetworkManagers[0].Shutdown();
                }
            }
            else
            {
                LogAssert.ignoreFailingMessages = true;
                NetworkLog.NetworkManagerOverride = m_ClientNetworkManagers[0];
            }

            m_ClientPlayerName = clientPlayer.gameObject.name;
            m_ClientNetworkObjectId = clientPlayer.NetworkObjectId;
            if (m_DistributedAuthority)
            {
                m_ClientPlayerName = m_PlayerNetworkObjects[m_ClientNetworkManagers[1].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].gameObject.name;
                m_ClientNetworkObjectId = m_PlayerNetworkObjects[m_ClientNetworkManagers[1].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].NetworkObjectId;

                if (!isShuttingDown)
                {
                    NetworkLog.NetworkManagerOverride = m_ClientNetworkManagers[1];
                }
                // the 2nd client attempts to destroy the 1st client's player object (if shutting down then "ok" if not then not "ok")
                Object.DestroyImmediate(m_PlayerNetworkObjects[m_ClientNetworkManagers[1].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].gameObject);
            }
            else
            {
                // the 1st client attempts to destroy its own player object (if shutting down then "ok" if not then not "ok")
                Object.DestroyImmediate(m_ClientNetworkManagers[0].LocalClient.PlayerObject.gameObject);
            }

            // destroying a NetworkObject while a session is active is not allowed
            if (!isShuttingDown)
            {
                yield return WaitForConditionOrTimeOut(HaveLogsBeenReceived);
                AssertOnTimeout($"Not all expected logs were received when destroying a {nameof(NetworkObject)} on the client side during an active session!");
            }
        }

        private bool HaveLogsBeenReceived()
        {
            if (m_DistributedAuthority)
            {
                if (!NetcodeLogAssert.HasLogBeenReceived(LogType.Error, $"[Netcode] [Invalid Destroy][{m_ClientPlayerName}][NetworkObjectId:{m_ClientNetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-owner client is not valid during a distributed authority session. Call Destroy or Despawn on the client-owner instead."))
                {
                    return false;
                }
            }
            else
            {
                if (!NetcodeLogAssert.HasLogBeenReceived(LogType.Error, $"[Netcode] [Invalid Destroy][{m_ClientPlayerName}][NetworkObjectId:{m_ClientNetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-host client is not valid. Call Destroy or Despawn on the server/host instead."))
                {
                    return false;
                }

                if (!NetcodeLogAssert.HasLogBeenReceived(LogType.Error, $"[Netcode-Server Sender={m_ClientNetworkManagers[0].LocalClientId}] [Invalid Destroy][{m_ClientPlayerName}][NetworkObjectId:{m_ClientNetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-host client is not valid. Call Destroy or Despawn on the server/host instead."))
                {
                    return false;
                }
            }
            return true;
        }

        protected override IEnumerator OnTearDown()
        {
            NetworkLog.NetworkManagerOverride = null;
            LogAssert.ignoreFailingMessages = false;
            return base.OnTearDown();
        }
    }
}
