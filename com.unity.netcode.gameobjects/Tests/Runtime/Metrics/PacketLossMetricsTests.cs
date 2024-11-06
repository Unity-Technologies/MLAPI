#if MULTIPLAYER_TOOLS
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7

using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode.TestHelpers.Runtime.Metrics;
using Unity.Netcode.Transports.UTP;
#if UTP_TRANSPORT_2_0_ABOVE
using Unity.Networking.Transport.Utilities;
#endif
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests.Metrics
{
    internal class PacketLossMetricsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;
        private readonly int m_PacketLossRate = 25;
        private readonly int m_PacketLossRangeDelta = 3;
        private readonly int m_MessageSize = 200;

        public PacketLossMetricsTests()
            : base(HostOrServer.Server)
        { }

        protected override void OnServerAndClientsCreated()
        {
            var clientTransport = (UnityTransport)m_ClientNetworkManagers[0].NetworkConfig.NetworkTransport;
#if !UTP_TRANSPORT_2_0_ABOVE
            clientTransport.SetDebugSimulatorParameters(0, 0, m_PacketLossRate);
#endif

            // Determined through trial and error. With both UTP 1.2 and 2.0, this random seed
            // results in an effective packet loss percentage between 22% and 28%. Future UTP
            // updates may change the RNG call patterns and cause this test to fail, in which
            // case the value should be modified again.
            clientTransport.DebugSimulatorRandomSeed = 4;

            base.OnServerAndClientsCreated();
        }

        [UnityTest]
        public IEnumerator TrackPacketLossAsServer()
        {
            var waitForPacketLossMetric = new WaitForGaugeMetricValues((m_ServerNetworkManager.NetworkMetrics as NetworkMetrics).Dispatcher,
                NetworkMetricTypes.PacketLoss,
                metric => metric == 0.0d);

            for (int i = 0; i < 1000; ++i)
            {
                using var writer = new FastBufferWriter(m_MessageSize, Allocator.Persistent);
                writer.WriteBytesSafe(new byte[m_MessageSize]);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage("Test", m_ServerNetworkManager.ConnectedClientsIds, writer);
            }

            yield return waitForPacketLossMetric.WaitForMetricsReceived();

            var packetLossValue = waitForPacketLossMetric.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(0d, packetLossValue);
        }

        [UnityTest]
        public IEnumerator TrackPacketLossAsClient()
        {
            double packetLossRateMinRange = (m_PacketLossRate - m_PacketLossRangeDelta) / 100d;
            double packetLossRateMaxrange = (m_PacketLossRate + m_PacketLossRangeDelta) / 100d;
            var clientNetworkManager = m_ClientNetworkManagers[0];

#if UTP_TRANSPORT_2_0_ABOVE
            var clientTransport = (UnityTransport)clientNetworkManager.NetworkConfig.NetworkTransport;
            clientTransport.NetworkDriver.CurrentSettings.TryGet<SimulatorUtility.Parameters>(out var parameters);
            parameters.PacketDropPercentage = m_PacketLossRate;
            clientTransport.NetworkDriver.ModifySimulatorStageParameters(parameters);
#endif

            var waitForPacketLossMetric = new WaitForGaugeMetricValues((clientNetworkManager.NetworkMetrics as NetworkMetrics).Dispatcher,
                NetworkMetricTypes.PacketLoss,
                metric => packetLossRateMinRange <= metric && metric <= packetLossRateMaxrange);

            for (int i = 0; i < 1000; ++i)
            {
                using var writer = new FastBufferWriter(m_MessageSize, Allocator.Persistent);
                writer.WriteBytesSafe(new byte[m_MessageSize]);
                m_ServerNetworkManager.CustomMessagingManager.SendNamedMessage("Test", m_ServerNetworkManager.ConnectedClientsIds, writer);
            }

            yield return waitForPacketLossMetric.WaitForMetricsReceived();

            var packetLossValue = waitForPacketLossMetric.AssertMetricValueHaveBeenFound();
            Assert.That(packetLossValue, Is.InRange(packetLossRateMinRange, packetLossRateMaxrange));
        }
    }
}

#endif
#endif
