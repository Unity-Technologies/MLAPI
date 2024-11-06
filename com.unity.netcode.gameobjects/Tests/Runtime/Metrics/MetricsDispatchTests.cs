#if MULTIPLAYER_TOOLS
using System;
using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer.Tools.NetStats;
using UnityEngine.TestTools;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests.Metrics
{
    internal class MetricsDispatchTests
    {
        private int m_NbDispatches;

        private NetworkManager m_NetworkManager;

        [SetUp]
        public void SetUp()
        {
            var networkManagerStarted = NetworkManagerHelper.StartNetworkManager(
                out m_NetworkManager,
                NetworkManagerHelper.NetworkManagerOperatingMode.Host,
                new NetworkConfig
                {
                    TickRate = 1,
                });
            Assert.IsTrue(networkManagerStarted);

            var networkMetrics = m_NetworkManager.NetworkMetrics as NetworkMetrics;
            networkMetrics.Dispatcher.RegisterObserver(new MockMetricsObserver(() => m_NbDispatches++));
        }

        [TearDown]
        public void TearDown()
        {
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        [UnityTest]
        public IEnumerator VerifyNetworkMetricsDispatchesOncePerFrame()
        {
            var nbDispatchesBeforeFrame = m_NbDispatches;

            yield return null; // Wait one frame so dispatch occurs

            var nbDispatchesAfterFrame = m_NbDispatches;

            Assert.AreEqual(1, nbDispatchesAfterFrame - nbDispatchesBeforeFrame);
        }

        private class MockMetricsObserver : IMetricObserver
        {
            private readonly Action m_OnObserve;

            public MockMetricsObserver(Action onObserve)
            {
                m_OnObserve = onObserve;
            }

            public void Observe(MetricCollection collection)
            {
                m_OnObserve?.Invoke();
            }
        }
    }
}
#endif
