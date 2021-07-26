using System.Collections;
using MLAPI.Configuration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MLAPI.RuntimeTests
{
    public class NetworkObjectDestroyTests
    {
        private const int k_ClientInstanceCount = 2;

        private NetworkManager m_ServerNetworkManager;
        private NetworkManager[] m_ClientNetworkManagers;
        private GameObject m_PlayerPrefab;
        private GameObject m_DummyPrefab;
        private GameObject m_DummyGameObject;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // we need at least 1 client for tests
            Assert.That(k_ClientInstanceCount, Is.GreaterThan(0));

            Assert.That(MultiInstanceHelpers.Create(k_ClientInstanceCount, out m_ServerNetworkManager, out m_ClientNetworkManagers));
            Assert.That(m_ServerNetworkManager, Is.Not.Null);
            Assert.That(m_ClientNetworkManagers, Is.Not.Null);
            Assert.That(m_ClientNetworkManagers.Length, Is.EqualTo(k_ClientInstanceCount));

            m_PlayerPrefab = new GameObject("PlayerPrefabPrototype");
            MultiInstanceHelpers.MakeNetworkedObjectTestPrefab(m_PlayerPrefab.AddComponent<NetworkObject>());

            m_DummyPrefab = new GameObject("DummyPrefabPrototype");
            MultiInstanceHelpers.MakeNetworkedObjectTestPrefab(m_DummyPrefab.AddComponent<NetworkObject>());

            m_ServerNetworkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            m_ServerNetworkManager.NetworkConfig.NetworkPrefabs.Add(new NetworkPrefab { Prefab = m_DummyPrefab });
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
                clientNetworkManager.NetworkConfig.NetworkPrefabs.Add(new NetworkPrefab { Prefab = m_DummyPrefab });
            }

            Assert.That(MultiInstanceHelpers.Start(/* isHost = */ true, m_ServerNetworkManager, m_ClientNetworkManagers));
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientsConnected(m_ClientNetworkManagers));
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientConnectedToServer(m_ServerNetworkManager));
        }

        [TearDown]
        public void Teardown()
        {
            MultiInstanceHelpers.Destroy();

            if (m_PlayerPrefab != null)
            {
                Object.DestroyImmediate(m_PlayerPrefab);
            }

            if (m_DummyGameObject != null)
            {
                Object.DestroyImmediate(m_DummyGameObject);
            }

            if (m_DummyPrefab != null)
            {
                Object.DestroyImmediate(m_DummyPrefab);
            }
        }

        [UnityTest]
        public IEnumerator ShouldDestroyPlayerObjectOnDisconnect()
        {
            var c0PlayerNetworkObject = m_ServerNetworkManager.ConnectedClientsList[0].PlayerObject;
            var c0PlayerGameObject = c0PlayerNetworkObject.gameObject;
            var c0PlayerObjectId = c0PlayerNetworkObject.NetworkObjectId;
            var c0PlayerClientId = m_ServerNetworkManager.ConnectedClientsList[0].ClientId;

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(c0PlayerObjectId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(c0PlayerObjectId));
            }


            m_ServerNetworkManager.DisconnectClient(c0PlayerClientId);

            int nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.ConnectedClients.ContainsKey(c0PlayerClientId), Is.False);
            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(c0PlayerObjectId), Is.False);
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(c0PlayerObjectId), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ShouldDestroyOwnedObjectsOnDisconnect()
        {
            m_DummyGameObject = Object.Instantiate(m_DummyPrefab);
            var sDummyNetworkObject = m_DummyGameObject.GetComponent<NetworkObject>();
            sDummyNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;
            sDummyNetworkObject.DontDestroyWithOwner = false;
            sDummyNetworkObject.Spawn();
            var sDummyObjectId = sDummyNetworkObject.NetworkObjectId;

            int nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            }


            var c0PlayerClientId = m_ServerNetworkManager.ConnectedClientsList[0].ClientId;
            sDummyNetworkObject.ChangeOwnership(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            }


            m_ServerNetworkManager.DisconnectClient(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.False);
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ShouldNotDestroyOwnedObjectsOnDisconnect()
        {
            m_DummyGameObject = Object.Instantiate(m_DummyPrefab);
            var sDummyNetworkObject = m_DummyGameObject.GetComponent<NetworkObject>();
            sDummyNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;
            sDummyNetworkObject.DontDestroyWithOwner = true;
            sDummyNetworkObject.Spawn();
            var sDummyObjectId = sDummyNetworkObject.NetworkObjectId;

            int nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            }


            var c0PlayerClientId = m_ServerNetworkManager.ConnectedClientsList[0].ClientId;
            sDummyNetworkObject.ChangeOwnership(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            }


            m_ServerNetworkManager.DisconnectClient(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.True);
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ShouldNotDestroyDisownedObjectsOnDisconnect()
        {
            m_DummyGameObject = Object.Instantiate(m_DummyPrefab);
            var sDummyNetworkObject = m_DummyGameObject.GetComponent<NetworkObject>();
            sDummyNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;
            sDummyNetworkObject.DontDestroyWithOwner = false;
            sDummyNetworkObject.Spawn();
            var sDummyObjectId = sDummyNetworkObject.NetworkObjectId;

            int nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId));
            }


            var c0PlayerClientId = m_ServerNetworkManager.ConnectedClientsList[0].ClientId;
            sDummyNetworkObject.ChangeOwnership(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(c0PlayerClientId));
            }


            sDummyNetworkObject.RemoveOwnership();

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(m_ServerNetworkManager.ServerClientId));
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects[sDummyObjectId].OwnerClientId, Is.EqualTo(clientNetworkManager.ServerClientId));
            }


            m_ServerNetworkManager.DisconnectClient(c0PlayerClientId);

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.True);
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                Assert.That(clientNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(sDummyObjectId), Is.True);
            }
        }
    }
}
