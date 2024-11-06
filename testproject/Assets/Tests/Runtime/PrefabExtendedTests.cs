using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TestProject.ManualTests;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    // DAMODE-TODO: When scene management is working in distributed authority mode we need to update this test
    [TestFixture(SceneManagementTypes.SceneManagementEnabled)]
    [TestFixture(SceneManagementTypes.SceneManagementDisabled)]
    public class PrefabExtendedTests : NetcodeIntegrationTest
    {
        private const string k_PrefabTestScene = "PrefabTestScene";
        public enum SceneManagementTypes
        {
            SceneManagementEnabled,
            SceneManagementDisabled,
        }

        protected override int NumberOfClients => 0;

        private Scene m_ServerSideTestScene;

        private bool m_SceneManagementEnabled;

        private List<NetworkObject> m_ObjectsToSpawn = new List<NetworkObject>();

        private List<NetworkObject> m_ServerSpawnedObjects = new List<NetworkObject>();

        private StringBuilder m_ErrorLog = new StringBuilder();

        public PrefabExtendedTests(SceneManagementTypes sceneManagementType)
        {
            m_SceneManagementEnabled = sceneManagementType == SceneManagementTypes.SceneManagementEnabled;
        }

        /// <summary>
        /// We are also testing that a scene loaded prior to the server starting will be included in the
        /// servers loaded scenes and will synchronize a client with the scene.
        /// </summary>
        protected override IEnumerator OnSetup()
        {
            m_ObjectsToSpawn.Clear();
            m_ServerSpawnedObjects.Clear();
            InScenePlacedHelper.ServerSpawnedInScenePlaced.Clear();
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Load a scene but don't make it the active scene.
            SceneManager.LoadSceneAsync(k_PrefabTestScene, LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(() => m_ServerSideTestScene.IsValid() && m_ServerSideTestScene.isLoaded);
            AssertOnTimeout($"Timed out waiting for {k_PrefabTestScene} scene to load during {nameof(OnSetup)}!");
            yield return base.OnSetup();
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (scene.isLoaded && scene.name == k_PrefabTestScene)
            {
                m_ServerSideTestScene = scene;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// Clean up after ourselves
        /// </summary>
        protected override IEnumerator OnTearDown()
        {
            InScenePlacedHelper.ServerSpawnedInScenePlaced.Clear();
            m_ObjectsToSpawn.Clear();
            if (m_ServerSideTestScene.IsValid() && m_ServerSideTestScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(m_ServerSideTestScene);
            }

            foreach (var spawnedObject in m_ObjectsToSpawn)
            {
                if (spawnedObject.IsSpawned)
                {
                    spawnedObject.Despawn();
                }
            }
            m_ObjectsToSpawn.Clear();

            return base.OnTearDown();
        }

        /// <summary>
        /// Configure the server and starting client(s)
        /// </summary>
        protected override void OnServerAndClientsCreated()
        {
            foreach (NetworkPrefab obj in PrefabTestConfig.Instance.TestPrefabs.PrefabList)
            {
                m_ObjectsToSpawn.Add(obj.Prefab.GetComponent<NetworkObject>());
            }

            m_ServerNetworkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(PrefabTestConfig.Instance.TestPrefabs);
            m_ServerNetworkManager.NetworkConfig.EnableSceneManagement = m_SceneManagementEnabled;

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.EnableSceneManagement = m_SceneManagementEnabled;
                client.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(PrefabTestConfig.Instance.TestPrefabs);
            }

            base.OnServerAndClientsCreated();
        }

        /// <summary>
        /// Configure the server to run in additive client synchronization mode
        /// </summary>
        protected override IEnumerator OnStartedServerAndClients()
        {
            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            return base.OnStartedServerAndClients();
        }

        /// <summary>
        /// Configure late joining clients NetworkManager
        /// </summary>
        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.EnableSceneManagement = m_SceneManagementEnabled;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(PrefabTestConfig.Instance.TestPrefabs);
            base.OnNewClientCreated(networkManager);
        }

        /// <summary>
        /// Validates that all spawned NetworkObjects are present and their corresponding 
        /// GlobalObjectIdHash values match
        /// </summary>
        private bool ValidateAllClientsSpawnedObjects()
        {
            m_ErrorLog.Clear();
            foreach (var spawnedObject in m_ServerSpawnedObjects)
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!s_GlobalNetworkObjects.ContainsKey(client.LocalClientId))
                    {
                        m_ErrorLog.AppendLine($"{nameof(s_GlobalNetworkObjects)} does not contain client id: {client.LocalClientId}!");
                        return false;
                    }

                    if (!s_GlobalNetworkObjects[client.LocalClientId].ContainsKey(spawnedObject.NetworkObjectId))
                    {
                        m_ErrorLog.AppendLine($"{nameof(s_GlobalNetworkObjects)} for Client-{client.LocalClientId} does not contain " +
                            $"{nameof(NetworkObject.NetworkObjectId)} ({spawnedObject.NetworkObjectId}) for {nameof(NetworkObject)} {spawnedObject.name}!");
                        return false;
                    }

                    var clientSpawnedObject = s_GlobalNetworkObjects[client.LocalClientId][spawnedObject.NetworkObjectId];
                    // When scene management is disabled, we match against the InScenePlacedSourceGlobalObjectIdHash for in-scene placed NetworkObjects
                    var spawnedObjectGlobalObjectIdHash = !m_SceneManagementEnabled && spawnedObject.IsSceneObject.Value ? spawnedObject.InScenePlacedSourceGlobalObjectIdHash : spawnedObject.GlobalObjectIdHash;
                    // Validate the GlobalObjectIdHash values match
                    if (clientSpawnedObject.GlobalObjectIdHash != spawnedObjectGlobalObjectIdHash)
                    {
                        m_ErrorLog.AppendLine($"Client-{client.LocalClientId} spawned object {clientSpawnedObject.name} with a " +
                            $"{nameof(NetworkObject.NetworkObjectId)} ({spawnedObject.NetworkObjectId}) has a {nameof(NetworkObject.GlobalObjectIdHash)} value of " +
                            $"{clientSpawnedObject.GlobalObjectIdHash} and was expecting it to be {spawnedObject.GlobalObjectIdHash}!");
                        return false;
                    }

                    // Validate the PrefabGlobalObjectIdHash values match (i.e. they both used the same source asset)
                    if (clientSpawnedObject.PrefabGlobalObjectIdHash != spawnedObject.PrefabGlobalObjectIdHash)
                    {
                        // This is "ok" for the legacy manual instantiate and spawn scenario
                        if (m_InstantiateAndSpawnType == InstantiateAndSpawnMethods.Manual && spawnedObject.PrefabGlobalObjectIdHash == 0)
                        {
                            continue;
                        }
                        m_ErrorLog.AppendLine($"Client-{client.LocalClientId} spawned object {clientSpawnedObject.name} with a " +
                            $"{nameof(NetworkObject.NetworkObjectId)} ({spawnedObject.NetworkObjectId}) has a {nameof(NetworkObject.PrefabGlobalObjectIdHash)} value of " +
                            $"{clientSpawnedObject.PrefabGlobalObjectIdHash} and was expecting it to be {spawnedObject.PrefabGlobalObjectIdHash}!");
                        return false;
                    }
                }
            }
            return true;
        }

        public enum InstantiateAndSpawnMethods
        {
            Manual,
            SpawnManager,
            NetworkObject,
        }

        private InstantiateAndSpawnMethods m_InstantiateAndSpawnType;

        /// <summary>
        /// This test validates that the network prefab instance used to spawn works when scene management is enabled
        /// and disabled. It also validates tha:
        /// - Overrides are used for dynamically spawned NetworkObjects
        /// - That an in-scene placed NetworkObject prefab that has a registered override still spawns the source prefab
        /// on the client-side when scene management is disabled, but will also still spawn the prefab override when
        /// dynamically spawend.
        /// This test also validates that when scene management is disabled, there is no need to register in-scene placed
        /// NetworkObjects (this is now automatically configured for the user).
        /// </summary>
        [UnityTest]
        public IEnumerator TestPrefabsSpawning([Values] InstantiateAndSpawnMethods instantiateAndSpawnType)
        {
            var gloabalObjectId = m_SceneManagementEnabled ? 0 : InScenePlacedHelper.ServerInSceneDefined.First().GlobalObjectIdHash;
            var firstError = $"[Netcode] Failed to create object locally. [globalObjectIdHash={gloabalObjectId}]. NetworkPrefab could not be found. Is the prefab registered with NetworkManager?";
            var secondError = $"[Netcode] Failed to spawn NetworkObject for Hash {gloabalObjectId}.";
            m_InstantiateAndSpawnType = instantiateAndSpawnType;

            // We have to spawn the first client manually in order to account for the errors when scene management is disabled.
            // Spawn the client prior to dynamically spawning our prefab instances. Two of the in-scene placed NetworkObjects
            // have overrides defined so we can assure that when scene management is disabled the in-scene placed instances
            // spawn the original prefab and when spawning dynamically the override is used.
            yield return CreateAndStartNewClient();

            var spawnManager = m_ServerNetworkManager.SpawnManager;
            // If scene management is enabled, then we want to verify against the editor 
            // assigned in-scene placed NetworkObjects
            if (m_SceneManagementEnabled)
            {
                foreach (var gameObject in PrefabTestConfig.Instance.InScenePlacedObjects)
                {
                    m_ServerSpawnedObjects.Add(gameObject.GetComponent<NetworkObject>());
                }
            }
            else // Otherwise, we use the server-side registered InScenePlacedHelper to check against
            {
                foreach (var networkObject in InScenePlacedHelper.ServerSpawnedInScenePlaced)
                {
                    m_ServerSpawnedObjects.Add(networkObject);
                }
            }
            var manualSpawnCount = 0;
            // Now, dynamically spawn several NetworkObject instances
            foreach (var prefabNetworkObject in m_ObjectsToSpawn)
            {
                var spawnedNetworkObject = (NetworkObject)null;
                // Assure the legacy way of instantiating and spawning an override still works
                if (instantiateAndSpawnType == InstantiateAndSpawnMethods.Manual)
                {
                    var prefabOverride = m_ServerNetworkManager.GetNetworkPrefabOverride(prefabNetworkObject.gameObject);
                    var prefabOverrideNetworkObject = prefabOverride.GetComponent<NetworkObject>();
                    // Just don't spawn anything that does not have an OverrideToNetworkPrefab entry
                    if (!m_ServerNetworkManager.NetworkConfig.Prefabs.OverrideToNetworkPrefab.ContainsKey(prefabOverrideNetworkObject.GlobalObjectIdHash))
                    {
                        continue;
                    }
                    var gameObjectInstance = Object.Instantiate(prefabOverride.gameObject);
                    spawnedNetworkObject = gameObjectInstance.GetComponent<NetworkObject>();
                    spawnedNetworkObject.Spawn();
                    manualSpawnCount++;
                }
                else
                {
                    spawnedNetworkObject = InstantiateAndSpawn(prefabNetworkObject, instantiateAndSpawnType);
                }
                m_ServerSpawnedObjects.Add(spawnedNetworkObject);
                yield return s_DefaultWaitForTick;
            }

            if (instantiateAndSpawnType == InstantiateAndSpawnMethods.Manual)
            {
                Assert.True(manualSpawnCount > 0, $"Did not manually instantiate and spawn any objects!");
            }

            // Validate that the client synchronized with the in-scene placed and dynamically spawned NetworkObjects
            yield return WaitForConditionOrTimeOut(ValidateAllClientsSpawnedObjects);
            AssertOnTimeout($"[First Stage][{instantiateAndSpawnType}] Validating spawned objects faild with the following error: {m_ErrorLog}");

            yield return CreateAndStartNewClient();
            yield return WaitForConditionOrTimeOut(ValidateAllClientsSpawnedObjects);
            AssertOnTimeout($"[Second Stage][{instantiateAndSpawnType}] Validating spawned objects faild with the following error: {m_ErrorLog}");

            if (instantiateAndSpawnType != InstantiateAndSpawnMethods.Manual)
            {
                LogAssert.Expect(LogType.Error, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotAuthority]);
                InstantiateAndSpawn(m_ObjectsToSpawn[0], instantiateAndSpawnType, true);
            }
        }

        [UnityTest]
        public IEnumerator TestsInstantiateAndSpawnErrors([Values] InstantiateAndSpawnMethods instantiateAndSpawnType)
        {
            // If scene management is enabled, then we want to verify against the editor 
            // assigned in-scene placed NetworkObjects
            if (m_SceneManagementEnabled)
            {
                foreach (var gameObject in PrefabTestConfig.Instance.InScenePlacedObjects)
                {
                    m_ServerSpawnedObjects.Add(gameObject.GetComponent<NetworkObject>());
                }
            }
            else // Otherwise, we use the server-side registered InScenePlacedHelper to check against
            {
                foreach (var networkObject in InScenePlacedHelper.ServerSpawnedInScenePlaced)
                {
                    m_ServerSpawnedObjects.Add(networkObject);
                }
            }

            m_ServerSpawnedObjects.Add(InstantiateAndSpawn(m_ObjectsToSpawn[0], instantiateAndSpawnType));
            // Validate that the client synchronized with the in-scene placed and dynamically spawned NetworkObjects
            yield return WaitForConditionOrTimeOut(ValidateAllClientsSpawnedObjects);
            AssertOnTimeout($"[First Stage] Validating spawned objects faild with the following error: {m_ErrorLog}");

            LogAssert.Expect(LogType.Error, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotRegisteredNetworkPrefab]);
            InstantiateAndSpawn(m_ServerSpawnedObjects[0], instantiateAndSpawnType);

            // The Network Prefab is null error can only happen when invoking from NetworkSpawnManager
            if (instantiateAndSpawnType == InstantiateAndSpawnMethods.SpawnManager)
            {
                LogAssert.Expect(LogType.Error, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NetworkPrefabNull]);
                InstantiateAndSpawn(null, instantiateAndSpawnType);
            }
            else
            {
                // The NetworkManager is null error can only happen when invoking from Network Prefab
                LogAssert.Expect(LogType.Error, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NetworkManagerNull]);
                InstantiateAndSpawn(m_ObjectsToSpawn[0], instantiateAndSpawnType, false, true);
            }

            m_ServerNetworkManager.Shutdown();
            LogAssert.Expect(LogType.Warning, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.InvokedWhenShuttingDown]);
            InstantiateAndSpawn(m_ObjectsToSpawn[0], instantiateAndSpawnType);
            // The not listening error can only happen when trying to instantiate and spawn on a Network Prefab 
            if (instantiateAndSpawnType == InstantiateAndSpawnMethods.NetworkObject)
            {
                LogAssert.Expect(LogType.Error, NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NoActiveSession]);
                yield return WaitForConditionOrTimeOut(() => !m_ServerNetworkManager.IsListening);
                InstantiateAndSpawn(m_ObjectsToSpawn[0], instantiateAndSpawnType);
            }
        }

        private NetworkObject InstantiateAndSpawn(NetworkObject prefabNetworkObject, InstantiateAndSpawnMethods instantiateAndSpawnType, bool clientNetworkManager = false, bool useNullNetworkManager = false)
        {
            var spawnedObject = (NetworkObject)null;
            var networkManager = clientNetworkManager ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;
            if (instantiateAndSpawnType == InstantiateAndSpawnMethods.SpawnManager)
            {
                spawnedObject = networkManager.SpawnManager.InstantiateAndSpawn(prefabNetworkObject);
            }
            else
            {
                if (useNullNetworkManager)
                {
                    spawnedObject = prefabNetworkObject.InstantiateAndSpawn(null);
                }
                else
                {
                    spawnedObject = prefabNetworkObject.InstantiateAndSpawn(networkManager);
                }
            }
            return spawnedObject;
        }

    }
}
