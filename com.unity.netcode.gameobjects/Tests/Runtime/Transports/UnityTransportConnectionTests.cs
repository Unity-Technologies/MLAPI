using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.Netcode.RuntimeTests.UnityTransportTestHelpers;

namespace Unity.Netcode.RuntimeTests
{
    internal class UnityTransportConnectionTests
    {
        // For tests using multiple clients.
        private const int k_NumClients = 5;
        private UnityTransport m_Server;
        private UnityTransport[] m_Clients = new UnityTransport[k_NumClients];
        private List<TransportEvent> m_ServerEvents;
        private List<TransportEvent>[] m_ClientsEvents = new List<TransportEvent>[k_NumClients];

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (m_Server)
            {
                m_Server.Shutdown();
                UnityEngine.Object.DestroyImmediate(m_Server);
            }

            foreach (var transport in m_Clients)
            {
                if (transport)
                {
                    transport.Shutdown();
                    UnityEngine.Object.DestroyImmediate(transport);
                }
            }

            foreach (var transportEvents in m_ClientsEvents)
            {
                transportEvents?.Clear();
            }

            yield return null;
        }

        // Check that invalid endpoint addresses are detected and return false if detected
        [Test]
        public void DetectInvalidEndpoint()
        {
            using var netcodeLogAssert = new NetcodeLogAssert(true);
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);
            m_Server.ConnectionData.Address = "Fubar";
            m_Server.ConnectionData.ServerListenAddress = "Fubar";
            m_Clients[0].ConnectionData.Address = "MoreFubar";
            Assert.False(m_Server.StartServer(), "Server failed to detect invalid endpoint!");
            Assert.False(m_Clients[0].StartClient(), "Client failed to detect invalid endpoint!");
            netcodeLogAssert.LogWasReceived(LogType.Error, $"Network listen address ({m_Server.ConnectionData.Address}) is Invalid!");
            netcodeLogAssert.LogWasReceived(LogType.Error, $"Target server network address ({m_Clients[0].ConnectionData.Address}) is Invalid!");
        }

        // Check connection with a single client.
        [UnityTest]
        public IEnumerator ConnectSingleClient()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            // Check we've received Connect event on server too.
            Assert.AreEqual(1, m_ServerEvents.Count);
            Assert.AreEqual(NetworkEvent.Connect, m_ServerEvents[0].Type);

            yield return null;
        }

        // Check connection with multiple clients.
        [UnityTest]
        public IEnumerator ConnectMultipleClients()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            m_Server.StartServer();

            for (int i = 0; i < k_NumClients; i++)
            {
                InitializeTransport(out m_Clients[i], out m_ClientsEvents[i]);
                m_Clients[i].StartClient();
            }

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[k_NumClients - 1]);

            // Check that every client received a Connect event.
            Assert.True(m_ClientsEvents.All(evs => evs.Count == 1));
            Assert.True(m_ClientsEvents.All(evs => evs[0].Type == NetworkEvent.Connect));

            // Check we've received Connect events on server too.
            Assert.AreEqual(k_NumClients, m_ServerEvents.Count);
            Assert.True(m_ServerEvents.All(ev => ev.Type == NetworkEvent.Connect));

            yield return null;
        }

        // Check server disconnection with a single client.
        [UnityTest]
        public IEnumerator ServerDisconnectSingleClient()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ClientsEvents[0]);

            yield return null;
        }

        // Check server disconnection with multiple clients.
        [UnityTest]
        public IEnumerator ServerDisconnectMultipleClients()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            m_Server.StartServer();

            for (int i = 0; i < k_NumClients; i++)
            {
                InitializeTransport(out m_Clients[i], out m_ClientsEvents[i]);
                m_Clients[i].StartClient();
            }

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[k_NumClients - 1]);

            // Disconnect a single client.
            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            // Need to manually wait since we don't know which client will get the Disconnect.
            yield return new WaitForSeconds(MaxNetworkEventWaitTime);

            // Check that we received a Disconnect event on only one client.
            Assert.AreEqual(1, m_ClientsEvents.Count(evs => evs.Count == 2 && evs[1].Type == NetworkEvent.Disconnect));

            // Disconnect all the other clients.
            for (int i = 1; i < k_NumClients; i++)
            {
                m_Server.DisconnectRemoteClient(m_ServerEvents[i].ClientID);
            }

            // Need to manually wait since we don't know which client got the Disconnect.
            yield return new WaitForSeconds(MaxNetworkEventWaitTime);

            // Check that all clients got a Disconnect event.
            Assert.True(m_ClientsEvents.All(evs => evs.Count == 2));
            Assert.True(m_ClientsEvents.All(evs => evs[1].Type == NetworkEvent.Disconnect));

            yield return null;
        }

        // Check client disconnection from a single client.
        [UnityTest]
        public IEnumerator ClientDisconnectSingleClient()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            m_Clients[0].DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents);
        }

        // Check client disconnection with multiple clients.
        [UnityTest]
        public IEnumerator ClientDisconnectMultipleClients()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            m_Server.StartServer();

            for (int i = 0; i < k_NumClients; i++)
            {
                InitializeTransport(out m_Clients[i], out m_ClientsEvents[i]);
                m_Clients[i].StartClient();
            }

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[k_NumClients - 1]);

            // Disconnect a single client.
            m_Clients[0].DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents);

            // Disconnect all the other clients.
            for (int i = 1; i < k_NumClients; i++)
            {
                m_Clients[i].DisconnectLocalClient();
            }

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents, 5);

            // Check that we got the correct number of Disconnect events on the server.
            Assert.AreEqual(k_NumClients * 2, m_ServerEvents.Count);
            Assert.AreEqual(k_NumClients, m_ServerEvents.Count(e => e.Type == NetworkEvent.Disconnect));

            yield return null;
        }

        // Check that server re-disconnects are no-ops.
        [UnityTest]
        public IEnumerator RepeatedServerDisconnectsNoop()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ClientsEvents[0]);

            var previousServerEventsCount = m_ServerEvents.Count;
            var previousClientEventsCount = m_ClientsEvents[0].Count;

            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            // Need to wait manually since no event should be generated.
            yield return new WaitForSeconds(MaxNetworkEventWaitTime);

            // Check we haven't received anything else on the client or server.
            Assert.AreEqual(m_ServerEvents.Count, previousServerEventsCount);
            Assert.AreEqual(m_ClientsEvents[0].Count, previousClientEventsCount);

            yield return null;
        }

        // Check that client re-disconnects are no-ops.
        [UnityTest]
        public IEnumerator RepeatedClientDisconnectsNoop()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            m_Clients[0].DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents);

            var previousServerEventsCount = m_ServerEvents.Count;
            var previousClientEventsCount = m_ClientsEvents[0].Count;

            m_Clients[0].DisconnectLocalClient();

            // Need to wait manually since no event should be generated.
            yield return new WaitForSeconds(MaxNetworkEventWaitTime);

            // Check we haven't received anything else on the client or server.
            Assert.AreEqual(m_ServerEvents.Count, previousServerEventsCount);
            Assert.AreEqual(m_ClientsEvents[0].Count, previousClientEventsCount);

            yield return null;
        }

        // Check connection with different server/listen addresses.
        [UnityTest]
        public IEnumerator DifferentServerAndListenAddresses()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.SetConnectionData("127.0.0.1", 10042, "0.0.0.0");
            m_Clients[0].SetConnectionData("127.0.0.1", 10042);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            // Check we've received Connect event on server too.
            Assert.AreEqual(1, m_ServerEvents.Count);
            Assert.AreEqual(NetworkEvent.Connect, m_ServerEvents[0].Type);

            yield return null;
        }

        // Check server disconnection with data in send queue.
        [UnityTest]
        public IEnumerator ServerDisconnectWithDataInQueue()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            // Wait for the client to connect before we disconnect the client
            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Server.Send(m_ServerEvents[0].ClientID, data, NetworkDelivery.Unreliable);

            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ClientsEvents[0]);

            if (m_ClientsEvents[0].Count >= 3)
            {
                Assert.AreEqual(NetworkEvent.Disconnect, m_ClientsEvents[0][2].Type);
            }
            else
            {
                yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ClientsEvents[0]);
            }
        }

        // Check client disconnection with data in send queue.
        [UnityTest]
        public IEnumerator ClientDisconnectWithDataInQueue()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);

            m_Server.StartServer();
            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ServerEvents);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Clients[0].Send(m_Clients[0].ServerClientId, data, NetworkDelivery.Unreliable);

            m_Clients[0].DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            if (m_ServerEvents.Count >= 3)
            {
                Assert.AreEqual(NetworkEvent.Disconnect, m_ServerEvents[2].Type);
            }
            else
            {
                yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents);
            }
        }

        // Check that a server can disconnect a client after another client has disconnected.
        [UnityTest]
        public IEnumerator ServerDisconnectAfterClientDisconnect()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Clients[0], out m_ClientsEvents[0]);
            InitializeTransport(out m_Clients[1], out m_ClientsEvents[1]);

            m_Server.StartServer();

            m_Clients[0].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[0]);

            m_Clients[1].StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_ClientsEvents[1]);

            m_Clients[0].DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ServerEvents);

            // Pick the client ID of the still connected client.
            var clientId = m_ServerEvents[0].ClientID;
            if (m_ServerEvents[2].ClientID == clientId)
            {
                clientId = m_ServerEvents[1].ClientID;
            }

            m_Server.DisconnectRemoteClient(clientId);

            yield return WaitForNetworkEvent(NetworkEvent.Disconnect, m_ClientsEvents[1]);

            yield return null;
        }
    }
}
