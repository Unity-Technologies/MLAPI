using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;



namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class InScenePlacedNetworkObjectTests : IntegrationTestWithApproximation
    {
        protected override int NumberOfClients => 2;

        private const string k_SceneToLoad = "InSceneNetworkObject";
        private const string k_InSceneUnder = "InSceneUnderGameObject";
        private const string k_InSceneUnderWithNT = "InSceneUnderGameObjectWithNT";
        private Scene m_ServerSideSceneLoaded;
        private bool m_CanStartServerAndClients;
        private string m_SceneLoading = k_SceneToLoad;

        public InScenePlacedNetworkObjectTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override IEnumerator OnSetup()
        {
            NetworkObjectTestComponent.Reset();
            NetworkObjectTestComponent.VerboseDebug = m_EnableVerboseDebug;
            m_CanStartServerAndClients = false;
            return base.OnSetup();
        }

        /// <summary>
        /// Very important to always have a backup "unloading" catch
        /// in the event your test fails it could not potentially unload
        /// a scene and the proceeding tests could be impacted by this!
        /// </summary>
        /// <returns></returns>
        protected override IEnumerator OnTearDown()
        {
            yield return CleanUpLoadedScene();
        }

        protected override bool CanStartServerAndClients()
        {
            return m_CanStartServerAndClients;
        }

        /// <summary>
        /// This verifies that in-scene placed NetworkObjects will be properly
        /// synchronized if:
        /// 1.) Despawned prior to a client late-joining
        /// 2.) Re-spawned after having been despawned without registering the in-scene
        /// NetworkObject as a NetworkPrefab
        /// </summary>
        [UnityTest]
        public IEnumerator InSceneNetworkObjectSynchAndSpawn()
        {
            // Because despawning a client will cause it to shutdown and clean everything in the
            // scene hierarchy, we have to prevent one of the clients from spawning initially before
            // we test synchronizing late joining clients with despawned in-scene placed NetworkObjects.
            // So, we prevent the automatic starting of the server and clients, remove the client we
            // will be targeting to join late from the m_ClientNetworkManagers array, start the server
            // and the remaining client, despawn the in-scene NetworkObject, and then start and synchronize
            // the clientToTest.
            var clientToTest = m_ClientNetworkManagers[1];
            var clients = m_ClientNetworkManagers.ToList();
            clients.Remove(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();
            m_CanStartServerAndClients = true;
            yield return StartServerAndClients();
            clients.Add(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();

            NetworkObjectTestComponent.ServerNetworkObjectInstance = null;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += Server_OnSceneEvent;
            var status = m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive);
            Assert.IsTrue(status == SceneEventProgressStatus.Started, $"When attempting to load scene {k_SceneToLoad} was returned the following progress status: {status}");

            // This verifies the scene loaded and the in-scene placed NetworkObjects spawned.
            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == TotalClients - 1);
            AssertOnTimeout($"Timed out waiting for total spawned in-scene placed NetworkObjects to reach a count of {TotalClients - 1} and is currently {NetworkObjectTestComponent.SpawnedInstances.Count}");

            // Get the server-side instance of the in-scene NetworkObject
            Assert.True(s_GlobalNetworkObjects.ContainsKey(m_ServerNetworkManager.LocalClientId), $"Could not find server instance of the test in-scene NetworkObject!");
            var serverObject = NetworkObjectTestComponent.ServerNetworkObjectInstance;
            Assert.IsNotNull(serverObject, $"Could not find server-side in-scene placed NetworkObject!");
            Assert.IsTrue(serverObject.IsSpawned, $"{serverObject.name} is not spawned!");

            // Despawn the in-scene placed NetworkObject
            Debug.Log("Despawning In-Scene placed NetworkObject");
            serverObject.Despawn(false);
            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == 0);
            AssertOnTimeout($"Timed out waiting for all in-scene instances to be despawned!  Current spawned count: {NetworkObjectTestComponent.SpawnedInstances.Count()}");

            // Now late join a client
            NetworkObjectTestComponent.OnInSceneObjectDespawned += OnInSceneObjectDespawned;
            NetcodeIntegrationTestHelpers.StartOneClient(clientToTest);
            yield return WaitForConditionOrTimeOut(() => (clientToTest.IsConnectedClient && clientToTest.IsListening));
            AssertOnTimeout($"Timed out waiting for {clientToTest.name} to reconnect!");

            yield return s_DefaultWaitForTick;

            // Make sure the late-joining client's in-scene placed NetworkObject received the despawn notification during synchronization
            Assert.IsNotNull(m_JoinedClientDespawnedNetworkObject, $"{clientToTest.name} did not despawn the in-scene placed NetworkObject when connecting and synchronizing!");

            // Update the newly joined client information
            ClientNetworkManagerPostStartInit();

            // We should still have no spawned in-scene placed NetworkObjects at this point
            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == 0);
            AssertOnTimeout($"{clientToTest.name} spawned in-scene placed NetworkObject!");

            // Now test that the despawned in-scene placed NetworkObject can be re-spawned (without having been registered as a NetworkPrefab)
            serverObject.Spawn();

            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == TotalClients);
            AssertOnTimeout($"Timed out waiting for all in-scene instances to be spawned!  Current spawned count: {NetworkObjectTestComponent.SpawnedInstances.Count()} | Expected spawn count: {TotalClients}");

            // Test NetworkHide on the first client
            var firstClientId = m_ClientNetworkManagers[0].LocalClientId;

            serverObject.NetworkHide(firstClientId);

            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == TotalClients - 1);
            AssertOnTimeout($"[NetworkHide] Timed out waiting for Client-{firstClientId} to despawn the in-scene placed NetworkObject! Current spawned count: {NetworkObjectTestComponent.SpawnedInstances.Count()} | Expected spawn count: {TotalClients - 1}");

            // Validate that the first client can spawn the "netcode hidden" in-scene placed NetworkObject
            serverObject.NetworkShow(firstClientId);
            yield return WaitForConditionOrTimeOut(() => NetworkObjectTestComponent.SpawnedInstances.Count == TotalClients);
            AssertOnTimeout($"[NetworkShow] Timed out waiting for Client-{firstClientId} to spawn the in-scene placed NetworkObject! Current spawned count: {NetworkObjectTestComponent.SpawnedInstances.Count()} | Expected spawn count: {TotalClients}");

            CleanUpLoadedScene();
        }

        private Scene m_ClientLoadedScene;

        [UnityTest]
        public IEnumerator ParentedInSceneObjectLateJoiningClient()
        {
            // Because despawning a client will cause it to shutdown and clean everything in the
            // scene hierarchy, we have to prevent one of the clients from spawning initially before
            // we test synchronizing late joining clients.
            // So, we prevent the automatic starting of the server and clients, remove the client we
            // will be targeting to join late from the m_ClientNetworkManagers array, start the server
            // and the remaining client, despawn the in-scene NetworkObject, and then start and synchronize
            // the clientToTest.
            var clientToTest = m_ClientNetworkManagers[1];
            var clients = m_ClientNetworkManagers.ToList();
            clients.Remove(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();
            m_CanStartServerAndClients = true;
            yield return StartServerAndClients();
            clients.Add(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();

            NetworkObjectTestComponent.ServerNetworkObjectInstance = null;

            m_ClientNetworkManagers[0].SceneManager.OnSceneEvent += OnSceneEvent;
            m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(() => m_ClientLoadedScene.IsValid() && m_ClientLoadedScene.isLoaded);
            AssertOnTimeout($"Timed out waiting for {k_SceneToLoad} scene to be loaded!");
            m_ClientNetworkManagers[0].SceneManager.OnSceneEvent -= OnSceneEvent;
            var serverInSceneObjectInstance = NetworkObjectTestComponent.ServerNetworkObjectInstance;
            Assert.IsNotNull(serverInSceneObjectInstance, $"Could not get the server-side registration of {nameof(NetworkObjectTestComponent)}!");
            var firstClientInSceneObjectInstance = NetworkObjectTestComponent.SpawnedInstances.Where((c) => c.NetworkManager == m_ClientNetworkManagers[0]).FirstOrDefault();
            Assert.IsNotNull(firstClientInSceneObjectInstance, $"Could not get the client-side registration of {nameof(NetworkObjectTestComponent)}!");
            Assert.IsTrue(firstClientInSceneObjectInstance.NetworkManager == m_ClientNetworkManagers[0]);

            // Parent the object
            serverInSceneObjectInstance.transform.parent = m_ServerNetworkManager.LocalClient.PlayerObject.transform;

            var clientSideServerPlayer = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][NetworkManager.ServerClientId];

            yield return WaitForConditionOrTimeOut(() => firstClientInSceneObjectInstance.transform.parent != null && firstClientInSceneObjectInstance.transform.parent == clientSideServerPlayer.transform);
            AssertOnTimeout($"Timed out waiting for the client-side id ({m_ClientNetworkManagers[0].LocalClientId}) server player transform to be set on the client-side in-scene object!");
            // Now late join a client
            NetcodeIntegrationTestHelpers.StartOneClient(clientToTest);
            yield return WaitForConditionOrTimeOut(() => (clientToTest.IsConnectedClient && clientToTest.IsListening));
            AssertOnTimeout($"Timed out waiting for {clientToTest.name} to reconnect!");

            yield return s_DefaultWaitForTick;

            // Update the newly joined client information
            ClientNetworkManagerPostStartInit();

            var lateJoinClientInSceneObjectInstance = NetworkObjectTestComponent.SpawnedInstances.Where((c) => c.NetworkManager == m_ClientNetworkManagers[1]).FirstOrDefault();
            Assert.IsNotNull(lateJoinClientInSceneObjectInstance, $"Could not get the client-side registration of {nameof(NetworkObjectTestComponent)} for the late joining client!");

            // Now get the late-joining client's instance for the server player
            clientSideServerPlayer = m_PlayerNetworkObjects[clientToTest.LocalClientId][NetworkManager.ServerClientId];

            // Validate the late joined client's in-scene NetworkObject is parented to the server-side player
            yield return WaitForConditionOrTimeOut(() => lateJoinClientInSceneObjectInstance.transform.parent != null && lateJoinClientInSceneObjectInstance.transform.parent == clientSideServerPlayer.transform);
            AssertOnTimeout($"Timed out waiting for the client-side id ({m_ClientNetworkManagers[0].LocalClientId}) server player transform to be set on the client-side in-scene object!");
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == m_SceneLoading && sceneEvent.ClientId == m_ClientNetworkManagers[0].LocalClientId)
            {
                m_ClientLoadedScene = sceneEvent.Scene;
            }
        }

        private NetworkObject m_JoinedClientDespawnedNetworkObject;

        private void OnInSceneObjectDespawned(NetworkObject networkObject)
        {
            m_JoinedClientDespawnedNetworkObject = networkObject;
            NetworkObjectTestComponent.OnInSceneObjectDespawned -= OnInSceneObjectDespawned;
        }

        private void Server_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId && sceneEvent.SceneEventType == SceneEventType.LoadComplete
                && sceneEvent.Scene.IsValid() && sceneEvent.Scene.isLoaded)
            {
                m_ServerSideSceneLoaded = sceneEvent.Scene;
                m_ServerNetworkManager.SceneManager.OnSceneEvent -= Server_OnSceneEvent;
            }
        }

        private IEnumerator CleanUpLoadedScene()
        {
            if (m_ServerSideSceneLoaded.IsValid() && m_ServerSideSceneLoaded.isLoaded)
            {
                Debug.Log("Unloading scene now");
                m_ServerNetworkManager.SceneManager.OnSceneEvent += Unload_OnSceneEvent;
                m_ServerNetworkManager.SceneManager.UnloadScene(m_ServerSideSceneLoaded);
                yield return WaitForConditionOrTimeOut(() => m_ClientNetworkManagers.Where((c) => !c.IsListening).Count() == 0);
                AssertOnTimeout($"[CleanUpLoadedScene] Timed out waiting for all in-scene instances to be despawned!  Current spawned count: {m_ClientNetworkManagers.Where((c) => !c.IsListening).Count()}");
            }
        }

        private void Unload_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId && sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted)
            {
                m_ServerNetworkManager.SceneManager.OnSceneEvent -= Unload_OnSceneEvent;
            }
        }


        private bool m_AllClientsLoadedScene;
        private bool m_AllClientsUnloadedScene;

        private int m_NumberOfInstancesCheck;

        private Scene m_SceneLoaded;

        private bool HaveAllClientsDespawnedInSceneObject()
        {
            // Make sure we despawned all instances
            if (NetworkObjectTestComponent.DespawnedInstances.Count < m_NumberOfInstancesCheck)
            {
                return false;
            }

            foreach (var despawnedInstance in NetworkObjectTestComponent.DespawnedInstances)
            {
                if (despawnedInstance.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HaveAllClientsSpawnedInSceneObject()
        {
            // Make sure we despawned all instances
            if (NetworkObjectTestComponent.SpawnedInstances.Count < m_NumberOfInstancesCheck)
            {
                return false;
            }

            foreach (var despawnedInstance in NetworkObjectTestComponent.SpawnedInstances)
            {
                if (!despawnedInstance.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This validates that users can despawn in-scene placed NetworkObjects and disable the
        /// associated GameObject when OnNetworkDespawn is invoked while still being able to
        /// re-spawn the same in-scene placed NetworkObject.
        /// This test validates this for:
        /// - Currently connected clients
        /// - Late joining client
        /// - Scene switching and having the server despawn the NetworkObject the first time it is spawned.
        /// </summary>
        [UnityTest]
        public IEnumerator EnableDisableInSceneObjectTests()
        {
            NetworkObjectTestComponent.ServerNetworkObjectInstance = null;
            // Enabled disabling the NetworkObject when it is despawned
            NetworkObjectTestComponent.DisableOnDespawn = true;
            // Set the number of instances to expect
            m_NumberOfInstancesCheck = NumberOfClients + (m_UseHost ? 1 : 0);

            // Start the host and clients and load the in-scene object scene additively
            m_CanStartServerAndClients = true;
            yield return StartServerAndClients();
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
            m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(() => m_AllClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for {k_SceneToLoad} scene to be loaded on all clients!");
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted -= SceneManager_OnLoadEventCompleted;

            // Verify all connected clients spawned the in-scene placed NetworkObject
            yield return WaitForConditionOrTimeOut(HaveAllClientsSpawnedInSceneObject);
            AssertOnTimeout($"Timed out waiting for all instances to be spawned and enabled!");

            var serverInSceneObjectInstance = NetworkObjectTestComponent.ServerNetworkObjectInstance;
            Assert.IsNotNull(serverInSceneObjectInstance, $"Could not get the server-side registration of {nameof(NetworkObjectTestComponent)}!");

            // Test #1: Despawn the in-scene placed NetworkObject and verify it is despawned and disabled on the clients
            serverInSceneObjectInstance.Despawn(false);

            yield return WaitForConditionOrTimeOut(HaveAllClientsDespawnedInSceneObject);
            AssertOnTimeout($"[Test #1] Timed out waiting for all instances to be despawned and disabled!");

            // Test #2: Late-join a client and re-verify that all in-scene placed object instances are still disabled
            yield return CreateAndStartNewClient();

            var newlyJoinedClient = m_ClientNetworkManagers[NumberOfClients];

            m_NumberOfInstancesCheck++;
            yield return WaitForConditionOrTimeOut(HaveAllClientsDespawnedInSceneObject);
            AssertOnTimeout($"[Test #2] Timed out waiting for all instances to be despawned and disabled!");

            // Test #3: Now spawn the same in-scene placed NetworkObject
            serverInSceneObjectInstance.gameObject.SetActive(true);
            serverInSceneObjectInstance.Spawn();
            yield return WaitForConditionOrTimeOut(HaveAllClientsSpawnedInSceneObject);
            AssertOnTimeout($"[Test #2] Timed out waiting for all instances to be enabled and spawned!");

            // Test #4: Now unload the in-scene object's scene and scene switch to the same scene while
            // also having the server-side disable the in-scene placed NetworkObject and verify all
            // connected clients completed the scene switch and that all in-scene placed NetworkObjects
            // are despawned and disabled.
            m_AllClientsLoadedScene = false;
            m_AllClientsUnloadedScene = false;

            NetworkObjectTestComponent.ServerNetworkObjectInstance = null;
            NetworkObjectTestComponent.DisableOnSpawn = true;
            m_ServerNetworkManager.SceneManager.OnUnloadEventCompleted += SceneManager_OnUnloadEventCompleted;
            m_ServerNetworkManager.SceneManager.UnloadScene(m_SceneLoaded);
            yield return WaitForConditionOrTimeOut(() => m_AllClientsUnloadedScene);
            AssertOnTimeout($"Timed out waiting for {k_SceneToLoad} scene to be unloaded on all clients!");
            m_ServerNetworkManager.SceneManager.OnUnloadEventCompleted -= SceneManager_OnUnloadEventCompleted;

            // Verify the spawned instances list is empty
            Assert.True(NetworkObjectTestComponent.SpawnedInstances.Count == 0, $"There are {NetworkObjectTestComponent.SpawnedInstances.Count} that did not despawn when the scene was unloaded!");

            // Go ahead and clear out the despawned instances list
            NetworkObjectTestComponent.DespawnedInstances.Clear();

            // Now scene switch (LoadSceneMode.Single) into the scene with the in-scene placed NetworkObject we have been testing
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
            m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Single);
            yield return WaitForConditionOrTimeOut(() => m_AllClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for {k_SceneToLoad} scene to be loaded on all clients!");
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted -= SceneManager_OnLoadEventCompleted;

            // Verify all client instances are disabled and despawned when done scene switching
            yield return WaitForConditionOrTimeOut(HaveAllClientsDespawnedInSceneObject);
            AssertOnTimeout($"[Test #4] Timed out waiting for all instances to be despawned and disabled!");

            serverInSceneObjectInstance = NetworkObjectTestComponent.ServerNetworkObjectInstance;
            Assert.IsNotNull(serverInSceneObjectInstance, $"[Test #4] Could not get the server-side registration of {nameof(NetworkObjectTestComponent)}!");

            // Test #5: Now spawn the in-scene placed NetworkObject
            serverInSceneObjectInstance.gameObject.SetActive(true);
            serverInSceneObjectInstance.Spawn();

            // Verify all clients spawned their in-scene NetworkObject relative instance
            yield return WaitForConditionOrTimeOut(HaveAllClientsSpawnedInSceneObject);
            AssertOnTimeout($"[Test #2] Timed out waiting for all instances to be enabled and spawned!");
            yield return StopOneClient(newlyJoinedClient, true);

            // Tests complete!
        }

        private void SceneManager_OnUnloadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            foreach (var clientId in clientsCompleted)
            {
                Assert.True(m_ServerNetworkManager.ConnectedClientsIds.Contains(clientId));
            }
            m_AllClientsUnloadedScene = true;
        }

        private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            foreach (var clientId in clientsCompleted)
            {
                Assert.True(m_ServerNetworkManager.ConnectedClientsIds.Contains(clientId));
            }
            m_AllClientsLoadedScene = true;
            m_SceneLoaded = SceneManager.GetSceneByName(sceneName);
        }

        public enum ParentSyncSettings
        {
            ParentSync,
            NoParentSync
        }

        public enum TransformSyncSettings
        {
            TransformSync,
            NoTransformSync
        }

        public enum TransformSpace
        {
            World,
            Local
        }

        /// <summary>
        /// This test validates the initial synchronization of an in-scene placed NetworkObject parented
        /// underneath a GameObject. There are two scenes for this tests where the child NetworkObject does
        /// and does not have a NetworkTransform component.
        /// </summary>
        /// <param name="inSceneUnderToLoad">Scene to load</param>
        /// <param name="parentSyncSettings"><see cref="NetworkObject.AutoObjectParentSync"/> settings</param>
        /// <param name="transformSyncSettings"><see cref="NetworkObject.SynchronizeTransform"/> settings</param>
        /// <param name="transformSpace"><see cref="NetworkTransform.InLocalSpace"/> setting (when available)</param>
        [UnityTest]
        public IEnumerator ParentedInSceneObjectUnderGameObject([Values(k_InSceneUnder, k_InSceneUnderWithNT)] string inSceneUnderToLoad,
            [Values] ParentSyncSettings parentSyncSettings, [Values] TransformSyncSettings transformSyncSettings, [Values] TransformSpace transformSpace)
        {
            var useNetworkTransform = m_SceneLoading == k_InSceneUnderWithNT;

            m_SceneLoading = inSceneUnderToLoad;
            // Because despawning a client will cause it to shutdown and clean everything in the
            // scene hierarchy, we have to prevent one of the clients from spawning initially before
            // we test synchronizing late joining clients.
            // So, we prevent the automatic starting of the server and clients, remove the client we
            // will be targeting to join late from the m_ClientNetworkManagers array, start the server
            // and the remaining client, despawn the in-scene NetworkObject, and then start and synchronize
            // the clientToTest.
            var clientToTest = m_ClientNetworkManagers[1];
            var clients = m_ClientNetworkManagers.ToList();

            // Note: This test is a modified copy of ParentedInSceneObjectLateJoiningClient.
            // The 1st client is being ignored in this test and the focus is primarily on the late joining
            // 2nd client after adjustments have been made to the child NetworkBehaviour and if applicable
            // NetworkTransform.

            clients.Remove(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();
            m_CanStartServerAndClients = true;
            yield return StartServerAndClients();
            clients.Add(clientToTest);
            m_ClientNetworkManagers = clients.ToArray();

            NetworkObjectTestComponent.ServerNetworkObjectInstance = null;

            m_ClientNetworkManagers[0].SceneManager.OnSceneEvent += OnSceneEvent;
            m_ServerNetworkManager.SceneManager.LoadScene(m_SceneLoading, LoadSceneMode.Additive);
            yield return WaitForConditionOrTimeOut(() => m_ClientLoadedScene.IsValid() && m_ClientLoadedScene.isLoaded);
            AssertOnTimeout($"Timed out waiting for {k_SceneToLoad} scene to be loaded!");

            m_ClientNetworkManagers[0].SceneManager.OnSceneEvent -= OnSceneEvent;
            var serverInSceneObjectInstance = NetworkObjectTestComponent.ServerNetworkObjectInstance;
            Assert.IsNotNull(serverInSceneObjectInstance, $"Could not get the server-side registration of {nameof(NetworkObjectTestComponent)}!");
            var firstClientInSceneObjectInstance = NetworkObjectTestComponent.SpawnedInstances.Where((c) => c.NetworkManager == m_ClientNetworkManagers[0]).FirstOrDefault();
            Assert.IsNotNull(firstClientInSceneObjectInstance, $"Could not get the client-side registration of {nameof(NetworkObjectTestComponent)}!");
            Assert.IsTrue(firstClientInSceneObjectInstance.NetworkManager == m_ClientNetworkManagers[0]);

            // Parent the object
            var clientSideServerPlayer = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][NetworkManager.ServerClientId];

            serverInSceneObjectInstance.AutoObjectParentSync = parentSyncSettings == ParentSyncSettings.ParentSync;
            serverInSceneObjectInstance.SynchronizeTransform = transformSyncSettings == TransformSyncSettings.TransformSync;

            var serverNetworkTransform = useNetworkTransform ? serverInSceneObjectInstance.GetComponent<NetworkTransform>() : null;
            if (useNetworkTransform)
            {
                serverNetworkTransform.InLocalSpace = transformSpace == TransformSpace.Local;
            }

            // Now late join a client
            NetcodeIntegrationTestHelpers.StartOneClient(clientToTest);
            yield return WaitForConditionOrTimeOut(() => (clientToTest.IsConnectedClient && clientToTest.IsListening));
            AssertOnTimeout($"Timed out waiting for {clientToTest.name} to reconnect!");

            yield return s_DefaultWaitForTick;

            // Update the newly joined client information
            ClientNetworkManagerPostStartInit();

            var lateJoinClientInSceneObjectInstance = NetworkObjectTestComponent.SpawnedInstances.Where((c) => c.NetworkManager == m_ClientNetworkManagers[1]).FirstOrDefault();
            Assert.IsNotNull(lateJoinClientInSceneObjectInstance, $"Could not get the client-side registration of {nameof(NetworkObjectTestComponent)} for the late joining client!");

            // Now make sure the server and newly joined client transform values match.
            RotationsMatch(serverInSceneObjectInstance.transform, lateJoinClientInSceneObjectInstance.transform, transformSpace == TransformSpace.Local);
            PositionsMatch(serverInSceneObjectInstance.transform, lateJoinClientInSceneObjectInstance.transform, transformSpace == TransformSpace.Local);
            // When testing local space we also do a sanity check and validate the world space values too.
            if (transformSpace == TransformSpace.Local)
            {
                RotationsMatch(serverInSceneObjectInstance.transform, lateJoinClientInSceneObjectInstance.transform);
                PositionsMatch(serverInSceneObjectInstance.transform, lateJoinClientInSceneObjectInstance.transform);
            }
            ScaleValuesMatch(serverInSceneObjectInstance.transform, lateJoinClientInSceneObjectInstance.transform);
        }

        protected bool RotationsMatch(Transform transformA, Transform transformB, bool inLocalSpace = false)
        {
            var authorityEulerRotation = inLocalSpace ? transformA.localRotation.eulerAngles : transformA.rotation.eulerAngles;
            var nonAuthorityEulerRotation = inLocalSpace ? transformB.localRotation.eulerAngles : transformB.rotation.eulerAngles;
            var xIsEqual = ApproximatelyEuler(authorityEulerRotation.x, nonAuthorityEulerRotation.x);
            var yIsEqual = ApproximatelyEuler(authorityEulerRotation.y, nonAuthorityEulerRotation.y);
            var zIsEqual = ApproximatelyEuler(authorityEulerRotation.z, nonAuthorityEulerRotation.z);
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{transformA.gameObject.name}][X-{xIsEqual} | Y-{yIsEqual} | Z-{zIsEqual}]" +
                    $"Authority rotation {authorityEulerRotation} != [{transformB.gameObject.name}] NonAuthority rotation {nonAuthorityEulerRotation}");
            }
            else if (m_EnableVerboseDebug)
            {
                VerboseDebug($"[{transformA.gameObject.name}][X-{xIsEqual} | Y-{yIsEqual} | Z-{zIsEqual}] " +
                    $"Authority rotation {authorityEulerRotation} != [{transformB.gameObject.name}] NonAuthority rotation {nonAuthorityEulerRotation}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        protected bool PositionsMatch(Transform transformA, Transform transformB, bool inLocalSpace = false)
        {
            var authorityPosition = inLocalSpace ? transformA.localPosition : transformA.position;
            var nonAuthorityPosition = inLocalSpace ? transformB.localPosition : transformB.position;
            var xIsEqual = Approximately(authorityPosition.x, nonAuthorityPosition.x);
            var yIsEqual = Approximately(authorityPosition.y, nonAuthorityPosition.y);
            var zIsEqual = Approximately(authorityPosition.z, nonAuthorityPosition.z);
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{transformA.gameObject.name}] Authority position {authorityPosition} != [{transformB.gameObject.name}] NonAuthority position {nonAuthorityPosition}");
            }
            else if (m_EnableVerboseDebug)
            {
                VerboseDebug($"[{transformA.gameObject.name}] Authority position {authorityPosition} != [{transformB.gameObject.name}] NonAuthority position {nonAuthorityPosition}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        protected bool ScaleValuesMatch(Transform transformA, Transform transformB)
        {
            var authorityScale = transformA.localScale;
            var nonAuthorityScale = transformB.localScale;
            var xIsEqual = Approximately(authorityScale.x, nonAuthorityScale.x);
            var yIsEqual = Approximately(authorityScale.y, nonAuthorityScale.y);
            var zIsEqual = Approximately(authorityScale.z, nonAuthorityScale.z);
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{transformA.gameObject.name}] Authority scale {authorityScale} != [{transformB.gameObject.name}] NonAuthority scale {nonAuthorityScale}");
            }
            else if (m_EnableVerboseDebug)
            {
                VerboseDebug($"[{transformA.gameObject.name}] Authority scale {authorityScale} == [{transformB.gameObject.name}] NonAuthority scale {nonAuthorityScale}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

    }

    internal class InScenePlacedNetworkObjectClientTests : NetcodeIntegrationTest
    {
        private const string k_SceneToLoad = "InSceneNetworkObject";

        protected override int NumberOfClients => 0;

        private Scene m_Scene;

        protected override IEnumerator OnSetup()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive);
            return base.OnSetup();
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (scene.name == k_SceneToLoad && loadSceneMode == LoadSceneMode.Additive)
            {
                m_Scene = scene;
                SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            }
        }

        protected override IEnumerator OnTearDown()
        {
            if (m_Scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(m_Scene);
            }
            return base.OnTearDown();
        }

        [UnityTest]
        public IEnumerator DespawnAndDestroyNetworkObjects()
        {
            // Simulate a client disconnecting early by just invoking DespawnAndDestroyNetworkObjects to assure
            // this method does not destroy in-scene placed NetworkObjects.
            m_ServerNetworkManager.SpawnManager.DespawnAndDestroyNetworkObjects();

            yield return s_DefaultWaitForTick;

            var insceneObject = GameObject.Find("InSceneObject");
            Assert.IsNotNull(insceneObject, $"Could not find the in-scene placed {nameof(NetworkObject)}: InSceneObject!");
        }
    }
}
