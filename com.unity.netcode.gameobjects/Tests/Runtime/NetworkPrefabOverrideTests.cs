using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Integration test that validates spawning instances of <see cref="NetworkPrefab"/>s with overrides and
    /// <see cref="NetworkPrefabHandler"/> registered overrides.
    /// </summary>
    [TestFixture(HostOrServer.Server)]
    [TestFixture(HostOrServer.Host)]
    internal class NetworkPrefabOverrideTests : NetcodeIntegrationTest
    {
        private const string k_PrefabRootName = "PrefabObj";
        protected override int NumberOfClients => 2;

        private NetworkPrefab m_ClientSidePlayerPrefab;
        private NetworkPrefab m_PrefabOverride;

        public NetworkPrefabOverrideTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        /// <summary>
        /// Prefab override handler that will instantiate the ServerSideInstance (m_PlayerPrefab) only on server instances
        /// and will spawn the ClientSideInstance (m_ClientSidePlayerPrefab.Prefab) only on clients and/or a host.
        /// </summary>
        public class TestPrefabOverrideHandler : MonoBehaviour, INetworkPrefabInstanceHandler
        {
            public GameObject ServerSideInstance;
            public GameObject ClientSideInstance;
            private NetworkManager m_NetworkManager;

            private void Start()
            {
                m_NetworkManager = GetComponent<NetworkManager>();
                m_NetworkManager.PrefabHandler.AddHandler(ServerSideInstance, this);
            }

            private void OnDestroy()
            {
                if (m_NetworkManager != null && m_NetworkManager.PrefabHandler != null)
                {
                    m_NetworkManager.PrefabHandler.RemoveHandler(ServerSideInstance);
                }
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                var instance = m_NetworkManager.IsClient ? Instantiate(ClientSideInstance) : Instantiate(ServerSideInstance);
                return instance.GetComponent<NetworkObject>();
            }

            public void Destroy(NetworkObject networkObject)
            {
                Object.Destroy(networkObject);
            }
        }


        internal class SpawnDespawnDestroyNotifications : NetworkBehaviour
        {

            public int Despawned { get; private set; }
            public int Destroyed { get; private set; }

            private bool m_WasSpawned;

            private ulong m_LocalClientId;

            public override void OnNetworkSpawn()
            {
                m_WasSpawned = true;
                m_LocalClientId = NetworkManager.LocalClientId;
                base.OnNetworkSpawn();
            }

            public override void OnNetworkDespawn()
            {
                Assert.True(Destroyed == 0, $"{name} on client-{m_LocalClientId} should have a destroy invocation count of 0 but it is {Destroyed}!");
                Assert.True(Despawned == 0, $"{name} on client-{m_LocalClientId} should have a despawn invocation count of 0 but it is {Despawned}!");
                Despawned++;
                base.OnNetworkDespawn();
            }

            public override void OnDestroy()
            {
                // When the original prefabs are destroyed, we want to ignore this check (those instances are never spawned)
                if (m_WasSpawned)
                {
                    Assert.True(Despawned == 1, $"{name} on client-{m_LocalClientId} should have a despawn invocation count of 1 but it is {Despawned}!");
                    Assert.True(Destroyed == 0, $"{name} on client-{m_LocalClientId} should have a destroy invocation count of 0 but it is {Destroyed}!");
                }
                Destroyed++;

                base.OnDestroy();
            }
        }

        /// <summary>
        /// Mock component for testing that the client-side player is using the right
        /// network prefab.
        /// </summary>
        public class ClientSideOnlyComponent : MonoBehaviour
        {

        }

        /// <summary>
        /// When we create the player prefab, make a modified instance that will be used
        /// with the <see cref="TestPrefabOverrideHandler"/>.
        /// </summary>
        protected override void OnCreatePlayerPrefab()
        {
            var clientPlayer = Object.Instantiate(m_PlayerPrefab);
            clientPlayer.AddComponent<ClientSideOnlyComponent>();
            Object.DontDestroyOnLoad(clientPlayer);
            m_ClientSidePlayerPrefab = new NetworkPrefab()
            {
                Prefab = clientPlayer,
            };

            base.OnCreatePlayerPrefab();
        }

        /// <summary>
        /// Add the additional <see cref="NetworkPrefab"/>s and <see cref="TestPrefabOverrideHandler"/>s to
        /// all <see cref="NetworkManager"/> instances.
        /// </summary>
        protected override void OnServerAndClientsCreated()
        {
            // Create a NetworkPrefab with an override

            var basePrefab = NetcodeIntegrationTestHelpers.CreateNetworkObject($"{k_PrefabRootName}-base", m_ServerNetworkManager, true);
            basePrefab.AddComponent<SpawnDespawnDestroyNotifications>();
            var targetPrefab = NetcodeIntegrationTestHelpers.CreateNetworkObject($"{k_PrefabRootName}-over", m_ServerNetworkManager, true);
            targetPrefab.AddComponent<SpawnDespawnDestroyNotifications>();
            m_PrefabOverride = new NetworkPrefab()
            {
                Prefab = basePrefab,
                Override = NetworkPrefabOverride.Prefab,
                SourcePrefabToOverride = basePrefab,
                OverridingTargetPrefab = targetPrefab,
            };

            // Add the prefab override handler for instance specific player prefabs to the server side
            var playerPrefabOverrideHandler = m_ServerNetworkManager.gameObject.AddComponent<TestPrefabOverrideHandler>();
            playerPrefabOverrideHandler.ServerSideInstance = m_PlayerPrefab;
            playerPrefabOverrideHandler.ClientSideInstance = m_ClientSidePlayerPrefab.Prefab;

            // Add the NetworkPrefab with override
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(m_PrefabOverride);
            // Add the client player prefab that will be used on clients (and the host)
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(m_ClientSidePlayerPrefab);

            foreach (var networkManager in m_ClientNetworkManagers)
            {
                // Add the prefab override handler for instance specific player prefabs to the client side
                playerPrefabOverrideHandler = networkManager.gameObject.AddComponent<TestPrefabOverrideHandler>();
                playerPrefabOverrideHandler.ServerSideInstance = m_PlayerPrefab;
                playerPrefabOverrideHandler.ClientSideInstance = m_ClientSidePlayerPrefab.Prefab;

                // Add the NetworkPrefab with override
                networkManager.NetworkConfig.Prefabs.Add(m_PrefabOverride);
                // Add the client player prefab that will be used on clients (and the host)
                networkManager.NetworkConfig.Prefabs.Add(m_ClientSidePlayerPrefab);
            }

            m_PrefabOverride.Prefab.GetComponent<NetworkObject>().IsSceneObject = false;
            m_PrefabOverride.SourcePrefabToOverride.GetComponent<NetworkObject>().IsSceneObject = false;
            m_PrefabOverride.OverridingTargetPrefab.GetComponent<NetworkObject>().IsSceneObject = false;
            m_ClientSidePlayerPrefab.Prefab.GetComponent<NetworkObject>().IsSceneObject = false;

            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnTearDown()
        {
            if (m_PrefabOverride != null)
            {
                if (m_PrefabOverride.SourcePrefabToOverride)
                {
                    Object.Destroy(m_PrefabOverride.SourcePrefabToOverride);
                }

                if (m_PrefabOverride.OverridingTargetPrefab)
                {
                    Object.Destroy(m_PrefabOverride.OverridingTargetPrefab);
                }
            }

            if (m_ClientSidePlayerPrefab != null)
            {
                if (m_ClientSidePlayerPrefab.Prefab)
                {
                    Object.Destroy(m_ClientSidePlayerPrefab.Prefab);
                }
            }
            m_ClientSidePlayerPrefab = null;
            m_PrefabOverride = null;

            yield return base.OnTearDown();
        }


        private GameObject GetPlayerNetworkPrefabObject(NetworkManager networkManager)
        {
            return networkManager.IsClient ? m_ClientSidePlayerPrefab.Prefab : m_PlayerPrefab;
        }

        [UnityTest]
        public IEnumerator PrefabOverrideTests()
        {
            var prefabNetworkObject = (NetworkObject)null;
            var spawnedGlobalObjectId = (uint)0;

            var networkManagers = m_ClientNetworkManagers.ToList();
            if (m_UseHost)
            {
                networkManagers.Insert(0, m_ServerNetworkManager);
            }
            else
            {
                // If running as just a server, validate that all player prefab clone instances are the server side version
                prefabNetworkObject = GetPlayerNetworkPrefabObject(m_ServerNetworkManager).GetComponent<NetworkObject>();
                foreach (var playerEntry in m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId])
                {
                    spawnedGlobalObjectId = playerEntry.Value.GlobalObjectIdHash;
                    Assert.IsTrue(prefabNetworkObject.GlobalObjectIdHash == spawnedGlobalObjectId, $"Server-Side {playerEntry.Value.name} was spawned as prefab ({spawnedGlobalObjectId}) but we expected ({prefabNetworkObject.GlobalObjectIdHash})!");
                }
            }

            // Validates prefab overrides via the NetworkPrefabHandler.
            // Validate the player prefab instance clones relative to all NetworkManagers.
            foreach (var networkManager in networkManagers)
            {
                // Get the expected player prefab to be spawned based on the NetworkManager
                prefabNetworkObject = GetPlayerNetworkPrefabObject(networkManager).GetComponent<NetworkObject>();
                if (networkManager.IsClient)
                {
                    spawnedGlobalObjectId = networkManager.LocalClient.PlayerObject.GlobalObjectIdHash;
                    Assert.IsTrue(prefabNetworkObject.GlobalObjectIdHash == spawnedGlobalObjectId, $"{networkManager.name} spawned player prefab ({spawnedGlobalObjectId}) did not match the expected one ({prefabNetworkObject.GlobalObjectIdHash})!");
                }

                foreach (var playerEntry in m_PlayerNetworkObjects[networkManager.LocalClientId])
                {
                    // We already checked our locally spawned player prefab above
                    if (playerEntry.Key == networkManager.LocalClientId)
                    {
                        continue;
                    }
                    spawnedGlobalObjectId = playerEntry.Value.GlobalObjectIdHash;
                    Assert.IsTrue(prefabNetworkObject.GlobalObjectIdHash == spawnedGlobalObjectId, $"Client-{networkManager.LocalClientId} clone of {playerEntry.Value.name} was spawned as prefab ({spawnedGlobalObjectId}) but we expected ({prefabNetworkObject.GlobalObjectIdHash})!");
                }
            }

            // Validates prefab overrides via NetworkPrefab configuration.
            var spawnedInstance = (NetworkObject)null;
            var networkManagerOwner = m_ServerNetworkManager;

            // Clients and Host will spawn the OverridingTargetPrefab while a dedicated server will spawn the SourcePrefabToOverride
            var expectedServerGlobalObjectIdHash = networkManagerOwner.IsClient ? m_PrefabOverride.OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash : m_PrefabOverride.SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash;
            var expectedClientGlobalObjectIdHash = m_PrefabOverride.OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;

            spawnedInstance = NetworkObject.InstantiateAndSpawn(m_PrefabOverride.SourcePrefabToOverride, networkManagerOwner, networkManagerOwner.LocalClientId);
            var builder = new StringBuilder();
            bool ObjectSpawnedOnAllNetworkMangers()
            {
                builder.Clear();
                if (!m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(spawnedInstance.NetworkObjectId))
                {
                    builder.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} failed to spawn {spawnedInstance.name}-{spawnedInstance.NetworkObjectId}!");
                    return false;
                }
                var instanceGID = m_ServerNetworkManager.SpawnManager.SpawnedObjects[spawnedInstance.NetworkObjectId].GlobalObjectIdHash;
                if (instanceGID != expectedServerGlobalObjectIdHash)
                {
                    builder.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} instance {spawnedInstance.name}-{spawnedInstance.NetworkObjectId} GID is {instanceGID} but was expected to be {expectedServerGlobalObjectIdHash}!");
                    return false;
                }

                foreach (var networkManger in m_ClientNetworkManagers)
                {
                    if (!networkManger.SpawnManager.SpawnedObjects.ContainsKey(spawnedInstance.NetworkObjectId))
                    {
                        builder.AppendLine($"Client-{networkManger.LocalClientId} failed to spawn {spawnedInstance.name}-{spawnedInstance.NetworkObjectId}!");
                        return false;
                    }
                    instanceGID = networkManger.SpawnManager.SpawnedObjects[spawnedInstance.NetworkObjectId].GlobalObjectIdHash;
                    if (instanceGID != expectedClientGlobalObjectIdHash)
                    {
                        builder.AppendLine($"Client-{networkManger.LocalClientId} instance {spawnedInstance.name}-{spawnedInstance.NetworkObjectId} GID is {instanceGID} but was expected to be {expectedClientGlobalObjectIdHash}!");
                        return false;
                    }
                }
                return true;
            }

            yield return WaitForConditionOrTimeOut(ObjectSpawnedOnAllNetworkMangers);
            AssertOnTimeout($"The spawned prefab override validation failed!\n {builder}");

            // Verify that the despawn and destroy order of operations is correct for client owned NetworkObjects and the nunmber of times each is invoked is correct
            expectedServerGlobalObjectIdHash = networkManagerOwner.IsClient ? m_PrefabOverride.OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash : m_PrefabOverride.SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash;
            expectedClientGlobalObjectIdHash = m_PrefabOverride.OverridingTargetPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash;

            spawnedInstance = NetworkObject.InstantiateAndSpawn(m_PrefabOverride.SourcePrefabToOverride, networkManagerOwner, m_ClientNetworkManagers[0].LocalClientId);


            yield return WaitForConditionOrTimeOut(ObjectSpawnedOnAllNetworkMangers);
            AssertOnTimeout($"The spawned prefab override validation failed!\n {builder}");
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            m_ClientNetworkManagers[0].Shutdown();

            // Wait until all of the client's owned objects are destroyed
            // If no asserts occur, then the despawn & destroy order of operations and invocation count is correct
            /// For more information look at: <see cref="SpawnDespawnDestroyNotifications"/>
            bool ClientDisconnected(ulong clientId)
            {
                var clientOwnedObjects = m_ServerNetworkManager.SpawnManager.SpawnedObjects.Where((c) => c.Value.OwnerClientId == clientId).ToList();
                if (clientOwnedObjects.Count > 0)
                {
                    return false;
                }

                clientOwnedObjects = m_ClientNetworkManagers[1].SpawnManager.SpawnedObjects.Where((c) => c.Value.OwnerClientId == clientId).ToList();
                if (clientOwnedObjects.Count > 0)
                {
                    return false;
                }
                return true;
            }

            yield return WaitForConditionOrTimeOut(() => ClientDisconnected(clientId));
            AssertOnTimeout($"Timed out waiting for client to disconnect!");
        }
    }
}

