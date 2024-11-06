#if MULTIPLAYER_TOOLS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Multiplayer.Tools.MetricTypes;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Netcode.TestHelpers.Runtime.Metrics;

namespace Unity.Netcode.RuntimeTests.Metrics
{
    internal class NetworkObjectMetricsTests : SingleClientMetricTestBase
    {
        // Keep less than 23 chars to avoid issues if compared against a 32-byte fixed string
        //     since it will have "(Clone)" appended
        private const string k_NewNetworkObjectName = "MetricObject";
        private GameObject m_NewNetworkPrefab;

        /// <summary>
        /// Use OnServerAndClientsCreated to create any additional prefabs that you might need
        /// </summary>
        protected override void OnServerAndClientsCreated()
        {
            base.OnServerAndClientsCreated();
            m_NewNetworkPrefab = CreateNetworkObjectPrefab(k_NewNetworkObjectName);
        }

        private NetworkObject SpawnNetworkObject()
        {
            return SpawnObject(m_NewNetworkPrefab, m_ServerNetworkManager).GetComponent<NetworkObject>();
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectSpawnSentMetric()
        {
            var waitForMetricEvent = new WaitForEventMetricValues<ObjectSpawnedEvent>(ServerMetrics.Dispatcher, NetworkMetricTypes.ObjectSpawnedSent);

            var spawnedObject = SpawnNetworkObject();

            yield return waitForMetricEvent.WaitForMetricsReceived();

            var objectSpawnedSentMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, objectSpawnedSentMetricValues.Count);

            var objectSpawned = objectSpawnedSentMetricValues.Last();
            Assert.AreEqual(Client.LocalClientId, objectSpawned.Connection.Id);
            Assert.AreEqual(spawnedObject.name, objectSpawned.NetworkId.Name);
            Assert.AreNotEqual(0, objectSpawned.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectSpawnReceivedMetric()
        {
            var waitForMetricEvent = new WaitForEventMetricValues<ObjectSpawnedEvent>(ClientMetrics.Dispatcher, NetworkMetricTypes.ObjectSpawnedReceived);

            var networkObject = SpawnNetworkObject();
            yield return s_DefaultWaitForTick;

            yield return waitForMetricEvent.WaitForMetricsReceived();

            var objectSpawnedReceivedMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, objectSpawnedReceivedMetricValues.Count);
            var clientSideObject = s_GlobalNetworkObjects[1][networkObject.NetworkObjectId];
            var objectSpawned = objectSpawnedReceivedMetricValues.First();
            Assert.AreEqual(Server.LocalClientId, objectSpawned.Connection.Id);
            Assert.AreEqual(networkObject.NetworkObjectId, objectSpawned.NetworkId.NetworkId);
            Assert.AreEqual(clientSideObject.name, objectSpawned.NetworkId.Name);
            Assert.AreNotEqual(0, objectSpawned.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectDestroySentMetric()
        {
            var waitForMetricEvent = new WaitForEventMetricValues<ObjectDestroyedEvent>(ServerMetrics.Dispatcher, NetworkMetricTypes.ObjectDestroyedSent);
            var networkObject = SpawnNetworkObject();
            var objectName = networkObject.name;

            Server.SpawnManager.OnDespawnObject(networkObject, true);

            // Wait for the metric to be received
            yield return waitForMetricEvent.WaitForMetricsReceived();
            var objectDestroyedSentMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();

            // The server should have sent 1 destroy message to the 1 client connected
            Assert.AreEqual(1, objectDestroyedSentMetricValues.Count);

            var objectDestroyed = objectDestroyedSentMetricValues.Last();
            Assert.AreEqual(Client.LocalClientId, objectDestroyed.Connection.Id);
            Assert.AreEqual(objectName, objectDestroyed.NetworkId.Name);
            Assert.AreNotEqual(0, objectDestroyed.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectDestroyReceivedMetric()
        {
            var networkObject = SpawnNetworkObject();

            yield return s_DefaultWaitForTick;

            var waitForMetricEvent = new WaitForEventMetricValues<ObjectDestroyedEvent>(ClientMetrics.Dispatcher, NetworkMetricTypes.ObjectDestroyedReceived);
            var objectId = networkObject.NetworkObjectId;
            var objectName = s_GlobalNetworkObjects[1][objectId].name;

            Server.SpawnManager.OnDespawnObject(networkObject, true);
            yield return s_DefaultWaitForTick;

            yield return waitForMetricEvent.WaitForMetricsReceived();

            var objectDestroyedReceivedMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, objectDestroyedReceivedMetricValues.Count);

            var objectDestroyed = objectDestroyedReceivedMetricValues.First();
            Assert.AreEqual(Server.LocalClientId, objectDestroyed.Connection.Id);
            Assert.AreEqual(objectId, objectDestroyed.NetworkId.NetworkId);
            Assert.AreEqual(objectName, objectDestroyed.NetworkId.Name);
            Assert.AreNotEqual(0, objectDestroyed.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackMultipleNetworkObjectSpawnSentMetric()
        {
            var networkObject1 = SpawnNetworkObject();
            var networkObject2 = SpawnNetworkObject();
            yield return s_DefaultWaitForTick;

            NetworkObject.NetworkHide(new List<NetworkObject> { networkObject1, networkObject2 }, Client.LocalClientId);

            yield return s_DefaultWaitForTick;

            var waitForMetricEvent = new WaitForEventMetricValues<ObjectSpawnedEvent>(ServerMetrics.Dispatcher, NetworkMetricTypes.ObjectSpawnedSent);

            NetworkObject.NetworkShow(new List<NetworkObject> { networkObject1, networkObject2 }, Client.LocalClientId);
            yield return s_DefaultWaitForTick;

            yield return waitForMetricEvent.WaitForMetricsReceived();

            var objectSpawnedSentMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(2, objectSpawnedSentMetricValues.Count); // As there's a client and server, this event is emitted twice.
            Assert.That(
                objectSpawnedSentMetricValues,
                Has.Exactly(1).Matches<ObjectSpawnedEvent>(
                    x => Client.LocalClientId == x.Connection.Id
                         && x.NetworkId.NetworkId == networkObject1.NetworkObjectId
                         && x.NetworkId.Name == networkObject1.name));
            Assert.That(
                objectSpawnedSentMetricValues,
                Has.Exactly(1).Matches<ObjectSpawnedEvent>(
                    x => Client.LocalClientId == x.Connection.Id
                         && x.NetworkId.NetworkId == networkObject2.NetworkObjectId
                         && x.NetworkId.Name == networkObject2.name));

            Assert.AreEqual(1, objectSpawnedSentMetricValues.Select(x => x.BytesCount).Distinct().Count());
            Assert.That(objectSpawnedSentMetricValues.Select(x => x.BytesCount), Has.All.Not.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TrackMultipleNetworkObjectDestroySentMetric()
        {
            var networkObject1 = SpawnNetworkObject();
            var networkObject2 = SpawnNetworkObject();

            yield return s_DefaultWaitForTick;

            var waitForMetricEvent = new WaitForEventMetricValues<ObjectDestroyedEvent>(ServerMetrics.Dispatcher, NetworkMetricTypes.ObjectDestroyedSent);

            NetworkObject.NetworkHide(new List<NetworkObject> { networkObject1, networkObject2 }, Client.LocalClientId);
            yield return s_DefaultWaitForTick;
            yield return waitForMetricEvent.WaitForMetricsReceived();

            var objectDestroyedSentMetricValues = waitForMetricEvent.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(2, objectDestroyedSentMetricValues.Count); // As there's a client and server, this event is emitted twice.
            Assert.That(
                objectDestroyedSentMetricValues,
                Has.Exactly(1).Matches<ObjectDestroyedEvent>(
                    x => Client.LocalClientId == x.Connection.Id
                         && x.NetworkId.NetworkId == networkObject1.NetworkObjectId
                         && x.NetworkId.Name == networkObject1.name));
            Assert.That(
                objectDestroyedSentMetricValues,
                Has.Exactly(1).Matches<ObjectDestroyedEvent>(
                    x => Client.LocalClientId == x.Connection.Id
                         && x.NetworkId.NetworkId == networkObject2.NetworkObjectId
                         && x.NetworkId.Name == networkObject2.name));

            Assert.AreEqual(1, objectDestroyedSentMetricValues.Select(x => x.BytesCount).Distinct().Count());
            Assert.That(objectDestroyedSentMetricValues.Select(x => x.BytesCount), Has.All.Not.EqualTo(0));
        }

#if MULTIPLAYER_TOOLS_1_0_0_PRE_7
        [UnityTest]
        public IEnumerator TrackNetworkObjectCountAfterSpawnOnServer()
        {
            SpawnNetworkObject();

            var waitForGaugeValues = new WaitForGaugeMetricValues(ServerMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects);

            yield return s_DefaultWaitForTick;
            yield return waitForGaugeValues.WaitForMetricsReceived();

            var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(3, value);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectCountAfterSpawnOnClient()
        {
            SpawnNetworkObject();

            //By default, we have 2 network objects
            //There's a slight delay between the spawn on the server and the spawn on the client
            //We want to have metrics when the value is different than the 2 default one to confirm the client has the new value
            var waitForGaugeValues = new WaitForGaugeMetricValues(ClientMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects, metric => (int)metric != 2);

            yield return waitForGaugeValues.WaitForMetricsReceived();

            var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(3, value);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectCountAfterDespawnOnServer()
        {
            var objectList = Server.SpawnManager.SpawnedObjectsList;
            for (int i = objectList.Count - 1; i >= 0; --i)
            {
                objectList.ElementAt(i).Despawn();
            }

            var waitForGaugeValues = new WaitForGaugeMetricValues(ServerMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects);

            yield return s_DefaultWaitForTick;
            yield return waitForGaugeValues.WaitForMetricsReceived();

            var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(0, value);
        }

        [UnityTest]
        public IEnumerator TrackNetworkObjectCountAfterDespawnOnClient()
        {
            var initialSpawnCount = Server.SpawnManager.SpawnedObjectsList.Count;
            var spawnedObjects = new List<GameObject>();
            //By default, we have 2 network objects and will have spawned 4 so we want to wait for metrics to tell us we have 6 spawned objects
            var waitForGaugeValues = new WaitForGaugeMetricValues(ClientMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects, metric => (int)metric == 6);

            for (int i = 0; i < 4; i++)
            {
                spawnedObjects.Add(SpawnObject(m_NewNetworkPrefab, Server));
            }

            yield return waitForGaugeValues.WaitForMetricsReceived();
            var value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            Assert.AreEqual(6, value);

            // Create a new gauge that waits for the initial spawned client count
            waitForGaugeValues = new WaitForGaugeMetricValues(ClientMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects, metric => (int)metric != 6);

            // Now despawn the 4 spawned objects
            foreach (var spawnedObject in spawnedObjects)
            {
                spawnedObject.GetComponent<NetworkObject>().Despawn();
            }
            spawnedObjects.Clear();
            yield return waitForGaugeValues.WaitForMetricsReceived();
            value = waitForGaugeValues.AssertMetricValueHaveBeenFound();

            // Validate the value is equals to the spawned objects prior to spawning the network prefab instances
            Assert.AreEqual(initialSpawnCount, value);

            // Create a new gauge that waits for the initial spawned client count
            waitForGaugeValues = new WaitForGaugeMetricValues(ClientMetrics.Dispatcher, NetworkMetricTypes.NetworkObjects, metric => (int)metric == 0);

            // Now assure despawning players are being tracked too
            var spawnedPlayers = Server.SpawnManager.SpawnedObjectsList.ToList();
            foreach (var spawnedObject in spawnedPlayers)
            {
                spawnedObject.Despawn();
            }
            yield return waitForGaugeValues.WaitForMetricsReceived();
            value = waitForGaugeValues.AssertMetricValueHaveBeenFound();
            // Nothing should be spawned on the client at this point
            Assert.AreEqual(0, value);
        }
#endif
    }
}
#endif
