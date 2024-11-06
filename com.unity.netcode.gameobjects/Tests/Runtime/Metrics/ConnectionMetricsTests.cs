#if MULTIPLAYER_TOOLS
#if MULTIPLAYER_TOOLS_1_0_0_PRE_7

using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode.TestHelpers.Runtime.Metrics;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests.Metrics
{
    [TestFixture(ClientCount.OneClient, HostOrServer.Host)]
    [TestFixture(ClientCount.TwoClients, HostOrServer.Host)]
    [TestFixture(ClientCount.OneClient, HostOrServer.Server)]
    [TestFixture(ClientCount.TwoClients, HostOrServer.Server)]
    internal class ConnectionMetricsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => m_ClientCount;

        private int m_ClientCount;

        public enum ClientCount
        {
            OneClient = 1,
            TwoClients,
        }

        public ConnectionMetricsTests(ClientCount clientCount, HostOrServer hostOrServer)
            : base(hostOrServer)
        {
            m_ClientCount = (int)clientCount;
        }

        private int GetClientCountForFixture()
        {
            return m_ClientCount + ((m_UseHost) ? 1 : 0);
        }

        [UnityTest]
        public IEnumerator UpdateConnectionCountOnServer()
        {
            var waitForGaugeValues = new WaitForGaugeMetricValues((m_ServerNetworkManager.NetworkMetrics as NetworkMetrics).Dispatcher, NetworkMetricTypes.ConnectedClients);

            yield return waitForGaugeValues.WaitForMetricsReceived();

            var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(GetClientCountForFixture(), value);
        }

        [UnityTest]
        public IEnumerator UpdateConnectionCountOnClient()
        {
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var waitForGaugeValues = new WaitForGaugeMetricValues((clientNetworkManager.NetworkMetrics as NetworkMetrics).Dispatcher, NetworkMetricTypes.ConnectedClients);

                yield return waitForGaugeValues.WaitForMetricsReceived();

                var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
                Assert.AreEqual(1, value);
            }
        }
    }
}

#endif
#endif
