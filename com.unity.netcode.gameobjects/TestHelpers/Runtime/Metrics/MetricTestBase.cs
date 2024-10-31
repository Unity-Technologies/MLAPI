#if MULTIPLAYER_TOOLS
using System.Collections;
using System.Collections.Generic;
using Unity.Multiplayer.Tools.NetStats;

namespace Unity.Netcode.TestHelpers.Runtime.Metrics
{
    internal abstract class SingleClientMetricTestBase : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        internal NetworkManager Server { get; private set; }

        internal NetworkMetrics ServerMetrics { get; private set; }
        internal IMetricDispatcher ServerMetricsDispatcher { get; private set; }

        internal NetworkManager Client { get; private set; }

        internal NetworkMetrics ClientMetrics { get; private set; }

        internal IMetricDispatcher ClientMetricsDispatcher { get; private set; }

        protected override void OnServerAndClientsCreated()
        {
            Server = m_ServerNetworkManager;
            Client = m_ClientNetworkManagers[0];
            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            ServerMetrics = Server.NetworkMetrics as NetworkMetrics;
            ClientMetrics = Client.NetworkMetrics as NetworkMetrics;
            ServerMetricsDispatcher = new TestDispatcher(ServerMetrics);
            ClientMetricsDispatcher = new TestDispatcher(ClientMetrics);
            yield return base.OnStartedServerAndClients();
        }
    }

    public abstract class DualClientMetricTestBase : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        internal NetworkManager Server { get; private set; }

        internal NetworkMetrics ServerMetrics { get; private set; }

        internal IMetricDispatcher ServerMetricsDispatcher { get; private set; }

        internal NetworkManager FirstClient { get; private set; }

        internal NetworkMetrics FirstClientMetrics { get; private set; }

        internal IMetricDispatcher FirstClientMetricsDispatcher { get; private set; }

        internal NetworkManager SecondClient { get; private set; }

        internal NetworkMetrics SecondClientMetrics { get; private set; }

        internal IMetricDispatcher SecondClientMetricsDispatcher { get; private set; }

        protected override void OnServerAndClientsCreated()
        {
            Server = m_ServerNetworkManager;
            FirstClient = m_ClientNetworkManagers[0];
            SecondClient = m_ClientNetworkManagers[1];
            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            ServerMetrics = Server.NetworkMetrics as NetworkMetrics;
            FirstClientMetrics = FirstClient.NetworkMetrics as NetworkMetrics;
            SecondClientMetrics = SecondClient.NetworkMetrics as NetworkMetrics;
            ServerMetricsDispatcher = new TestDispatcher(ServerMetrics);
            FirstClientMetricsDispatcher = new TestDispatcher(FirstClientMetrics);
            SecondClientMetricsDispatcher = new TestDispatcher(SecondClientMetrics);
            yield return base.OnStartedServerAndClients();

        }
    }

    // Since the NetworkMetrics were directly dispatching the metrics, the dispatcher was not needed
    // This is an adapter to stay compatible with the old dispatching mechanism
    internal class TestDispatcher : IMetricDispatcher
    {
        private readonly IList<IMetricObserver> m_Observers = new List<IMetricObserver>();

        public TestDispatcher(NetworkMetrics metrics)
        {
            metrics.OnMetricsDispatched += Dispatch;
        }

        public void RegisterObserver(IMetricObserver observer)
        {
            m_Observers.Add(observer);
        }

        public void Dispatch(IMetricCollection collection)
        {
            for (var i = 0; i < m_Observers.Count; i++)
            {
                var snapshotObserver = m_Observers[i];
                snapshotObserver.Observe(collection);
            }
        }
    }

}
#endif
