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
    /// - When <see cref="DisconnectTests.OwnerPersistence.DestroyWithOwner"/> the server-side player object is destroyed
    /// - When <see cref="DisconnectTests.OwnerPersistence.DontDestroyWithOwner"/> the server-side player object ownership is transferred back to the server
    /// </summary>
    [TestFixture(HostOrServer.Server)]
    [TestFixture(HostOrServer.Host)]
    internal class PeerDisconnectCallbackTests : NetcodeIntegrationTest
    {

        public enum ClientDisconnectType
        {
            ServerDisconnectsClient,
            ClientDisconnectsFromServer
        }

        protected override int NumberOfClients => 3;

        private int m_ClientDisconnectCount;
        private int m_PeerDisconnectCount;


        public PeerDisconnectCallbackTests(HostOrServer hostOrServer)
            : base(hostOrServer)
        {
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
            m_ClientDisconnectCount = 0;
            m_PeerDisconnectCount = 0;
            return base.OnSetup();
        }

        private void OnConnectionEventCallback(NetworkManager networkManager, ConnectionEventData data)
        {
            switch (data.EventType)
            {
                case ConnectionEvent.ClientDisconnected:
                    Assert.IsFalse(data.PeerClientIds.IsCreated);
                    ++m_ClientDisconnectCount;
                    break;
                case ConnectionEvent.PeerDisconnected:
                    Assert.IsFalse(data.PeerClientIds.IsCreated);
                    ++m_PeerDisconnectCount;
                    break;
            }
        }

        [UnityTest]
        public IEnumerator TestPeerDisconnectCallback([Values] ClientDisconnectType clientDisconnectType, [Values(1ul, 2ul, 3ul)] ulong disconnectedClient)
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                client.OnConnectionEvent += OnConnectionEventCallback;
                if (m_UseHost)
                {
                    Assert.IsTrue(client.ConnectedClientsIds.Contains(0ul));
                }
                Assert.IsTrue(client.ConnectedClientsIds.Contains(1ul));
                Assert.IsTrue(client.ConnectedClientsIds.Contains(2ul));
                Assert.IsTrue(client.ConnectedClientsIds.Contains(3ul));
                Assert.AreEqual(client.ServerIsHost, m_UseHost);
            }
            m_ServerNetworkManager.OnConnectionEvent += OnConnectionEventCallback;
            if (m_UseHost)
            {
                Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(0ul));
            }
            Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(1ul));
            Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(2ul));
            Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(3ul));
            Assert.AreEqual(m_ServerNetworkManager.ServerIsHost, m_UseHost);

            // Set up a WaitForMessageReceived hook.
            // In some cases the message will be received during StopOneClient, but it is not guaranteed
            // So we start the listener before we call Stop so it will be noticed regardless of whether it happens
            // during StopOneClient or whether we have to wait for it
            var messageHookEntriesForSpawn = new List<MessageHookEntry>();
            foreach (var clientNetworkManager in m_ClientNetworkManagers.Where(c => c.LocalClientId != disconnectedClient))
            {
                var messageHook = new MessageHookEntry(clientNetworkManager);
                messageHook.AssignMessageType<ClientDisconnectedMessage>();
                messageHookEntriesForSpawn.Add(messageHook);
            }

            // Used to determine if all clients received the CreateObjectMessage
            var hooks = new MessageHooksConditional(messageHookEntriesForSpawn);

            if (clientDisconnectType == ClientDisconnectType.ServerDisconnectsClient)
            {
                m_ServerNetworkManager.DisconnectClient(disconnectedClient);
            }
            else
            {
                yield return StopOneClient(m_ClientNetworkManagers[disconnectedClient - 1]);
            }

            yield return WaitForConditionOrTimeOut(hooks);

            Assert.False(s_GlobalTimeoutHelper.TimedOut);

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.IsConnectedClient)
                {
                    Assert.IsEmpty(client.ConnectedClientsIds);
                    continue;
                }
                if (m_UseHost)
                {
                    Assert.IsTrue(client.ConnectedClientsIds.Contains(0ul), $"[Client-{client.LocalClientId}][Connected ({client.IsConnectedClient})] Still has client identifier 0!");
                }

                for (var i = 1ul; i < 3ul; ++i)
                {
                    if (i == disconnectedClient)
                    {
                        Assert.IsFalse(client.ConnectedClientsIds.Contains(i), $"[Client-{client.LocalClientId}][Connected ({client.IsConnectedClient})] Still has client identifier {i}!");
                    }
                    else
                    {
                        Assert.IsTrue(client.ConnectedClientsIds.Contains(i), $"[Client-{client.LocalClientId}][Connected ({client.IsConnectedClient})] Still has client identifier {i}!");
                    }
                }
            }
            if (m_UseHost)
            {
                Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(0ul));
            }

            for (var i = 1ul; i < 3ul; ++i)
            {
                if (i == disconnectedClient)
                {
                    Assert.IsFalse(m_ServerNetworkManager.ConnectedClientsIds.Contains(i));
                }
                else
                {
                    Assert.IsTrue(m_ServerNetworkManager.ConnectedClientsIds.Contains(i));
                }
            }

            // If disconnected, the server and the client that disconnected will be notified
            Assert.AreEqual(2, m_ClientDisconnectCount);
            // Host receives peer disconnect, dedicated server does not
            Assert.AreEqual(m_UseHost ? 3 : 2, m_PeerDisconnectCount);
        }
    }
}
