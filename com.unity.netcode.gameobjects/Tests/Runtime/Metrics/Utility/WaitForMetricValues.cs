#if MULTIPLAYER_TOOLS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Multiplayer.Tools.NetStats;

namespace Unity.Netcode.RuntimeTests.Metrics.Utility
{
    internal class WaitForMetricValues<TMetric> : IMetricObserver
    {
        readonly string m_MetricName;
        bool m_Found;
        bool m_HasError;
        string m_Error;
        uint m_NbFrames = 0;
        IReadOnlyCollection<TMetric> m_Values;

        public delegate bool Filter(TMetric metric);

        Filter m_FilterDelegate;


        public WaitForMetricValues(IMetricDispatcher dispatcher, DirectionalMetricInfo directionalMetricName)
        {
            m_MetricName = directionalMetricName.Id;

            dispatcher.RegisterObserver(this);
        }

        public WaitForMetricValues(IMetricDispatcher dispatcher, DirectionalMetricInfo directionalMetricName, Filter filter)
            : this(dispatcher, directionalMetricName)
        {
            m_FilterDelegate = filter;
        }

        public IEnumerator WaitForMetricsReceived()
        {
            yield return WaitForFrames(60);
        }

        public IReadOnlyCollection<TMetric> AssertMetricValuesHaveBeenFound()
        {
            if (m_HasError)
            {
                Assert.Fail(m_Error);
            }

            if (!m_Found)
            {
                Assert.Fail($"Found no matching values for metric of type '{typeof(TMetric).Name}', with name '{m_MetricName}' during '{m_NbFrames}' frames.");
            }

            return m_Values;
        }

        public void Observe(MetricCollection collection)
        {
            if (m_Found || m_HasError)
            {
                return;
            }

            var metric = collection.Metrics.SingleOrDefault(x => x.Name == m_MetricName);
            if (metric == default)
            {
                m_HasError = true;
                m_Error = $"Metric collection does not contain metric named '{m_MetricName}'.";

                return;
            }

            var typedMetric = metric as IEventMetric<TMetric>;
            if (typedMetric == default)
            {
                m_HasError = true;
                m_Error = $"Metric collection contains a metric of type '{metric.GetType().Name}' for name '{m_MetricName}', but was expecting '{typeof(TMetric).Name}'.";

                return;
            }

            if (typedMetric.Values.Any())
            {
                // Apply filter if one was provided
                m_Values = m_FilterDelegate != null ? typedMetric.Values.Where(x => m_FilterDelegate(x)).ToList() : typedMetric.Values.ToList();
                m_Found = m_Values.Count > 0;
            }
        }

        private IEnumerator WaitForFrames(uint maxNbFrames)
        {
            while (!m_Found && m_NbFrames < maxNbFrames)
            {
                m_NbFrames++;
                yield return null;
            }
        }
    }
}
#endif
