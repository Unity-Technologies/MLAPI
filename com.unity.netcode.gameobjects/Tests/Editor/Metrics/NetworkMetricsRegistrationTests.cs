#if MULTIPLAYER_TOOLS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Multiplayer.Tools.NetStats;

namespace Unity.Netcode.EditorTests.Metrics
{
    internal class NetworkMetricsRegistrationTests
    {
        private static Type[] s_MetricTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x.GetInterfaces().Contains(typeof(INetworkMetricEvent)))
            .ToArray();

        [TestCaseSource(nameof(s_MetricTypes))]
        [Ignore("Disable test while we reevaluate the assumption that INetworkMetricEvent interfaces must be reported from MLAPI.")]
        public void ValidateThatAllMetricTypesAreRegistered(Type metricType)
        {
            var dispatcher = new NetworkMetrics().Dispatcher as MetricDispatcher;
            Assert.NotNull(dispatcher);

            var collection = typeof(MetricDispatcher)
                .GetField("m_Collection", BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(dispatcher) as MetricCollection;
            Assert.NotNull(collection);

            Assert.That(
                collection.Metrics.OfType<IEventMetric>(),
                Has.Exactly(2).Matches<IEventMetric>(
                    eventMetric =>
                    {
                        var eventType = eventMetric.GetType().GetGenericArguments()?.FirstOrDefault();
                        return eventType == metricType;
                    }));
        }
    }
}

#endif
