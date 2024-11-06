using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.Netcode.RuntimeTests.UnityTransportTestHelpers;

namespace Unity.Netcode.RuntimeTests
{
    internal class UnityTransportTests
    {
        // No need to test all reliable delivery methods since they all map to the same pipeline.
        private static readonly NetworkDelivery[] k_DeliveryParameters =
        {
            NetworkDelivery.Unreliable,
            NetworkDelivery.UnreliableSequenced,
            NetworkDelivery.Reliable
        };

        private static readonly NetworkFamily[] k_NetworkFamiltyParameters =
        {
            NetworkFamily.Ipv4,
#if !(UNITY_SWITCH || UNITY_PS4 || UNITY_PS5)
            // IPv6 is not supported on Switch, PS4, and PS5.
            NetworkFamily.Ipv6
#endif
        };

        private UnityTransport m_Server, m_Client1, m_Client2;
        private List<TransportEvent> m_ServerEvents, m_Client1Events, m_Client2Events;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (m_Server)
            {
                m_Server.Shutdown();

                // Need to destroy the GameObject (all assigned components will get destroyed too)
                UnityEngine.Object.DestroyImmediate(m_Server.gameObject);
            }

            if (m_Client1)
            {
                m_Client1.Shutdown();

                // Need to destroy the GameObject (all assigned components will get destroyed too)
                UnityEngine.Object.DestroyImmediate(m_Client1.gameObject);
            }

            if (m_Client2)
            {
                m_Client2.Shutdown();

                // Need to destroy the GameObject (all assigned components will get destroyed too)
                UnityEngine.Object.DestroyImmediate(m_Client2.gameObject);
            }

            m_ServerEvents?.Clear();
            m_Client1Events?.Clear();
            m_Client2Events?.Clear();

            yield return null;
        }

        // Check if can make a simple data exchange.
        [UnityTest]
        public IEnumerator PingPong(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            InitializeTransport(out m_Server, out m_ServerEvents, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var ping = new ArraySegment<byte>(Encoding.ASCII.GetBytes("ping"));
            m_Client1.Send(m_Client1.ServerClientId, ping, delivery);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            Assert.That(m_ServerEvents[1].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("ping")));

            var pong = new ArraySegment<byte>(Encoding.ASCII.GetBytes("pong"));
            m_Server.Send(m_ServerEvents[0].ClientID, pong, delivery);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_Client1Events);

            Assert.That(m_Client1Events[1].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("pong")));

            yield return null;
        }

        // Check if can make a simple data exchange (both ways at a time).
        [UnityTest]
        public IEnumerator PingPongSimultaneous(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            InitializeTransport(out m_Server, out m_ServerEvents, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var ping = new ArraySegment<byte>(Encoding.ASCII.GetBytes("ping"));
            m_Server.Send(m_ServerEvents[0].ClientID, ping, delivery);
            m_Client1.Send(m_Client1.ServerClientId, ping, delivery);

            // Once one event is in the other should be too.
            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            Assert.That(m_ServerEvents[1].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("ping")));
            Assert.That(m_Client1Events[1].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("ping")));

            var pong = new ArraySegment<byte>(Encoding.ASCII.GetBytes("pong"));
            m_Server.Send(m_ServerEvents[0].ClientID, pong, delivery);
            m_Client1.Send(m_Client1.ServerClientId, pong, delivery);

            // Once one event is in the other should be too.
            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            Assert.That(m_ServerEvents[2].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("pong")));
            Assert.That(m_Client1Events[2].Data, Is.EquivalentTo(Encoding.ASCII.GetBytes("pong")));

            yield return null;
        }

        // Test is ignored on Switch, PS4, and PS5 because on these platforms the OS buffers for
        // loopback traffic are too small for the amount of data sent in a single update here.
        [UnityTest]
        [UnityPlatform(exclude = new[] { RuntimePlatform.Switch, RuntimePlatform.PS4, RuntimePlatform.PS5 })]
        public IEnumerator SendMaximumPayloadSize(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            // We want something that's over the old limit of ~44KB for reliable payloads.
            var payloadSize = 64 * 1024;

            InitializeTransport(out m_Server, out m_ServerEvents, payloadSize, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, payloadSize, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var payloadData = new byte[payloadSize];
            for (int i = 0; i < payloadData.Length; i++)
            {
                payloadData[i] = (byte)i;
            }

            var payload = new ArraySegment<byte>(payloadData);
            m_Client1.Send(m_Client1.ServerClientId, payload, delivery);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents, MaxNetworkEventWaitTime * 4);

            Assert.AreEqual(payloadSize, m_ServerEvents[1].Data.Count);

            var receivedArray = m_ServerEvents[1].Data.Array;
            var receivedArrayOffset = m_ServerEvents[1].Data.Offset;
            for (int i = 0; i < payloadSize; i++)
            {
                Assert.AreEqual(payloadData[i], receivedArray[receivedArrayOffset + i]);
            }

            yield return null;
        }

        // Check making multiple sends to a client in a single frame.
        [UnityTest]
        public IEnumerator MultipleSendsSingleFrame(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            InitializeTransport(out m_Server, out m_ServerEvents, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data1 = new byte[10];
            data1[0] = 11;
            m_Client1.Send(m_Client1.ServerClientId, new ArraySegment<byte>(data1), delivery);

            var data2 = new byte[3000];
            data2[0] = 22;
            m_Client1.Send(m_Client1.ServerClientId, new ArraySegment<byte>(data2), delivery);

            var data3 = new byte[10];
            data3[0] = 33;
            m_Client1.Send(m_Client1.ServerClientId, new ArraySegment<byte>(data3), delivery);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            Assert.AreEqual(4, m_ServerEvents.Count);
            Assert.AreEqual(NetworkEvent.Data, m_ServerEvents[3].Type);

            Assert.AreEqual(11, m_ServerEvents[1].Data.First());
            Assert.AreEqual(10, m_ServerEvents[1].Data.Count);

            Assert.AreEqual(22, m_ServerEvents[2].Data.First());
            Assert.AreEqual(3000, m_ServerEvents[2].Data.Count);

            Assert.AreEqual(33, m_ServerEvents[3].Data.First());
            Assert.AreEqual(10, m_ServerEvents[3].Data.Count);

            yield return null;
        }

        // Check sending data to multiple clients.
        [UnityTest]
        public IEnumerator SendMultipleClients(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            InitializeTransport(out m_Server, out m_ServerEvents, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, family: family);
            InitializeTransport(out m_Client2, out m_Client2Events, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();
            m_Client2.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);
            if (m_Client2Events.Count == 0)
            {
                yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client2Events);
            }

            // Ensure we got both Connect events.
            Assert.AreEqual(2, m_ServerEvents.Count);

            var data1 = new ArraySegment<byte>(new byte[] { 11 });
            m_Server.Send(m_ServerEvents[0].ClientID, data1, delivery);

            var data2 = new ArraySegment<byte>(new byte[] { 22 });
            m_Server.Send(m_ServerEvents[1].ClientID, data2, delivery);

            // Once one has received its data, the other should have too.
            yield return WaitForNetworkEvent(NetworkEvent.Data, m_Client1Events);

            // Do make sure the other client got its Data event.
            Assert.AreEqual(2, m_Client2Events.Count);
            Assert.AreEqual(NetworkEvent.Data, m_Client2Events[1].Type);

            byte c1Data = m_Client1Events[1].Data.First();
            byte c2Data = m_Client2Events[1].Data.First();
            Assert.That((c1Data == 11 && c2Data == 22) || (c1Data == 22 && c2Data == 11));

            yield return null;
        }

        // Check receiving data from multiple clients.
        [UnityTest]
        public IEnumerator ReceiveMultipleClients(
            [ValueSource("k_DeliveryParameters")] NetworkDelivery delivery,
            [ValueSource("k_NetworkFamiltyParameters")] NetworkFamily family)
        {
            InitializeTransport(out m_Server, out m_ServerEvents, family: family);
            InitializeTransport(out m_Client1, out m_Client1Events, family: family);
            InitializeTransport(out m_Client2, out m_Client2Events, family: family);

            m_Server.StartServer();
            m_Client1.StartClient();
            m_Client2.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);
            if (m_Client2Events.Count == 0)
            {
                yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client2Events);
            }

            var data1 = new ArraySegment<byte>(new byte[] { 11 });
            m_Client1.Send(m_Client1.ServerClientId, data1, delivery);

            var data2 = new ArraySegment<byte>(new byte[] { 22 });
            m_Client2.Send(m_Client2.ServerClientId, data2, delivery);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            // Make sure we got both data messages.
            Assert.AreEqual(4, m_ServerEvents.Count);
            Assert.AreEqual(NetworkEvent.Data, m_ServerEvents[3].Type);

            byte sData1 = m_ServerEvents[2].Data.First();
            byte sData2 = m_ServerEvents[3].Data.First();
            Assert.That((sData1 == 11 && sData2 == 22) || (sData1 == 22 && sData2 == 11));

            yield return null;
        }

        // Check that we get disconnected when overflowing the reliable send queue.
        [UnityTest]
        public IEnumerator DisconnectOnReliableSendQueueOverflow()
        {
            const int maxSendQueueSize = 16 * 1024;

            InitializeTransport(out m_Server, out m_ServerEvents, maxSendQueueSize: maxSendQueueSize);
            InitializeTransport(out m_Client1, out m_Client1Events, maxSendQueueSize: maxSendQueueSize);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            m_Server.Shutdown();

            var numSends = (maxSendQueueSize / 1024);

            for (int i = 0; i < numSends; i++)
            {
                var payload = new ArraySegment<byte>(new byte[1024]);
                m_Client1.Send(m_Client1.ServerClientId, payload, NetworkDelivery.Reliable);
            }

            LogAssert.Expect(LogType.Error, "Couldn't add payload of size 1024 to reliable send queue. " +
                $"Closing connection {m_Client1.ServerClientId} as reliability guarantees can't be maintained.");

            Assert.AreEqual(2, m_Client1Events.Count);
            Assert.AreEqual(NetworkEvent.Disconnect, m_Client1Events[1].Type);

            yield return null;
        }

        // Check that it's fine to overflow the unreliable send queue (traffic is flushed on overflow).
        // Test is ignored on Switch, PS4, and PS5 because on these platforms the OS buffers for
        // loopback traffic are too small for the amount of data sent in a single update here.
        [UnityTest]
        [UnityPlatform(exclude = new[] { RuntimePlatform.Switch, RuntimePlatform.PS4, RuntimePlatform.PS5 })]
        public IEnumerator SendCompletesOnUnreliableSendQueueOverflow()
        {
            const int maxSendQueueSize = 16 * 1024;

            InitializeTransport(out m_Server, out m_ServerEvents, maxSendQueueSize: maxSendQueueSize);
            InitializeTransport(out m_Client1, out m_Client1Events, maxSendQueueSize: maxSendQueueSize);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var numSends = (maxSendQueueSize / 1024) + 1;

            for (int i = 0; i < numSends; i++)
            {
                var payload = new ArraySegment<byte>(new byte[1024]);
                m_Client1.Send(m_Client1.ServerClientId, payload, NetworkDelivery.Unreliable);
            }

            // Manually wait. This ends up generating quite a bit of packets and it might take a
            // while for everything to make it to the server.
            yield return new WaitForSeconds(numSends * 0.02f);

            // Extra event is the connect event.
            Assert.AreEqual(numSends + 1, m_ServerEvents.Count);

            for (int i = 1; i <= numSends; i++)
            {
                Assert.AreEqual(NetworkEvent.Data, m_ServerEvents[i].Type);
                Assert.AreEqual(1024, m_ServerEvents[i].Data.Count);
            }

            yield return null;
        }

#if !UTP_TRANSPORT_2_0_ABOVE
        // Check that simulator parameters are effective. We only check with the drop rate, because
        // that's easy to check and we only really want to make sure the simulator parameters are
        // configured properly (the simulator pipeline stage is already well-tested in UTP).
        [UnityTest]
        [UnityPlatform(include = new[] { RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor })]
        public IEnumerator SimulatorParametersAreEffective()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.SetDebugSimulatorParameters(0, 0, 100);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Client1.Send(m_Client1.ServerClientId, data, NetworkDelivery.Reliable);

            yield return new WaitForSeconds(MaxNetworkEventWaitTime);

            Assert.AreEqual(1, m_ServerEvents.Count);

            yield return null;
        }

        // Check that RTT is reported correctly.
        [UnityTest]
        [UnityPlatform(include = new[] { RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor })]
        public IEnumerator CurrentRttReportedCorrectly()
        {
            const int simulatedRtt = 25;

            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.SetDebugSimulatorParameters(simulatedRtt, 0, 0);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Client1.Send(m_Client1.ServerClientId, data, NetworkDelivery.Reliable);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents,
                timeout: MaxNetworkEventWaitTime + (2 * simulatedRtt));

            Assert.GreaterOrEqual(m_Client1.GetCurrentRtt(m_Client1.ServerClientId), simulatedRtt);

            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator SendQueuesFlushedOnShutdown([ValueSource("k_DeliveryParameters")] NetworkDelivery delivery)
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Client1.Send(m_Client1.ServerClientId, data, delivery);

            m_Client1.Shutdown();

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SendQueuesFlushedOnLocalClientDisconnect([ValueSource("k_DeliveryParameters")] NetworkDelivery delivery)
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Client1.Send(m_Client1.ServerClientId, data, delivery);

            m_Client1.DisconnectLocalClient();

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_ServerEvents);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SendQueuesFlushedOnRemoteClientDisconnect([ValueSource("k_DeliveryParameters")] NetworkDelivery delivery)
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var data = new ArraySegment<byte>(new byte[] { 42 });
            m_Server.Send(m_Client1.ServerClientId, data, delivery);

            m_Server.DisconnectRemoteClient(m_ServerEvents[0].ClientID);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_Client1Events);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReliablePayloadsCanBeLargerThanMaximum()
        {
            InitializeTransport(out m_Server, out m_ServerEvents);
            InitializeTransport(out m_Client1, out m_Client1Events);

            m_Server.StartServer();
            m_Client1.StartClient();

            yield return WaitForNetworkEvent(NetworkEvent.Connect, m_Client1Events);

            var payloadSize = UnityTransport.InitialMaxPayloadSize + 1;
            var data = new ArraySegment<byte>(new byte[payloadSize]);

            m_Server.Send(m_Client1.ServerClientId, data, NetworkDelivery.Reliable);

            yield return WaitForNetworkEvent(NetworkEvent.Data, m_Client1Events);

            yield return null;
        }
    }
}
