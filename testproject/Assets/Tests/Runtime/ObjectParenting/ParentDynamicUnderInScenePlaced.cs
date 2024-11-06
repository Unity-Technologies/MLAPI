using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    public class ParentDynamicUnderInScenePlacedHelper : NetworkBehaviour
    {
        public static Dictionary<ulong, NetworkObject> Instances = new Dictionary<ulong, NetworkObject>();
        public override void OnNetworkSpawn()
        {
            if (IsServer && NetworkManager.IsServer)
            {
                // Migrate into the same scene as the player
                SceneManager.MoveGameObjectToScene(gameObject, NetworkManager.LocalClient.PlayerObject.gameObject.scene);

                // Now parent it under the same in-scene placed NetworkObject
                var targetObject = NetworkManager.LocalClient.PlayerObject.transform.parent;
                NetworkObject.TrySetParent(targetObject, false);
            }
            Instances.Add(NetworkManager.LocalClientId, NetworkObject);
        }
    }

    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class ParentDynamicUnderInScenePlaced : NetcodeIntegrationTest
    {
        private const string k_SceneToLoad = "GenericInScenePlacedObject";
        protected override int NumberOfClients => 0;
        private GameObject m_DynamicallySpawned;
        private bool m_SceneIsLoaded;

        public ParentDynamicUnderInScenePlaced(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override IEnumerator OnSetup()
        {
            ParentDynamicUnderInScenePlacedHelper.Instances.Clear();
            return base.OnSetup();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_DynamicallySpawned = CreateNetworkObjectPrefab("DynamicObj");
            m_DynamicallySpawned.AddComponent<ParentDynamicUnderInScenePlacedHelper>();
            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            return base.OnStartedServerAndClients();
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            foreach (var networkPrefab in m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            }
            base.OnNewClientCreated(networkManager);
        }

        protected override void OnNewClientStarted(NetworkManager networkManager)
        {
            m_ServerNetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            base.OnNewClientStarted(networkManager);
        }

        private NetworkObject m_FailedValidation;
        private bool TestParentedAndNotInScenePlaced()
        {
            // Always assign m_FailedValidation to avoid possible null reference crashes.
            var serverPlayer = m_FailedValidation = m_ServerNetworkManager.LocalClient.PlayerObject;
            if (serverPlayer.transform.parent == null || serverPlayer.IsSceneObject.Value == true)
            {
                m_FailedValidation = serverPlayer;
                return false;
            }

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var lateJoinPlayer = clientNetworkManager.LocalClient.PlayerObject;
                if (lateJoinPlayer.transform.parent == null || lateJoinPlayer.IsSceneObject.Value == true)
                {
                    m_FailedValidation = lateJoinPlayer;
                    return false;
                }
            }

            foreach (var dynamicallySpawned in ParentDynamicUnderInScenePlacedHelper.Instances)
            {
                var networkObject = dynamicallySpawned.Value;
                if (networkObject.transform.parent == null || networkObject.IsSceneObject.Value == true)
                {
                    m_FailedValidation = networkObject;
                    return false;
                }
            }
            return true;
        }

        private ulong m_TargetScenePlacedId;
        /// <summary>
        /// Integration test that validates parenting dynamically spawned NetworkObjects
        /// under an in-scene placed NetworkObject works properly.
        /// </summary>
        /// <remarks>
        /// This test validates that:
        /// - Players can be parented under an in-scene placed NetworkObject
        /// - Dynamically spawned NetworkObjects can be parented under an in-scene placed NetworkObject
        /// - Late joining clients properly synchronize the parented NetworkObjects
        /// </remarks>
        [UnityTest]
        public IEnumerator ParentUnderInSceneplaced()
        {
            m_ServerNetworkManager.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
            m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive);
            // Wait for the scene with the in-scene placed NetworkObject to be loaded
            yield return WaitForConditionOrTimeOut(() => m_SceneIsLoaded == true);
            AssertOnTimeout($"Timed out waiting for the scene {k_SceneToLoad} to load!");

            // Wait for the host-server's player to be parented under the in-scene placed NetworkObject
            yield return WaitForConditionOrTimeOut(TestParentedAndNotInScenePlaced);
            AssertOnTimeout($"[{m_FailedValidation.name}] Failed validation! InScenePlaced ({m_FailedValidation.IsSceneObject.Value}) | Was Parented ({m_FailedValidation.transform.position != null})");
            m_TargetScenePlacedId = m_ServerNetworkManager.LocalClient.PlayerObject.transform.parent.GetComponent<NetworkObject>().NetworkObjectId;

            // Now dynamically spawn a NetworkObject to also test dynamically spawned NetworkObjects being parented
            // under in-scene placed NetworkObjects
            var dynamicallySpawnedServerSide = Object.Instantiate(m_DynamicallySpawned);
            dynamicallySpawnedServerSide.GetComponent<NetworkObject>().Spawn(true);

            for (int i = 0; i < 5; i++)
            {
                yield return CreateAndStartNewClient();
                if (m_DistributedAuthority)
                {
                    yield return WaitForConditionOrTimeOut(() => ParentPlayerToInSceneNetworkObjectFound(i));
                    AssertOnTimeout($"[Client-{i + 1}] Failed to find in-scene placed NetworkObject or failed to parent under it!");
                }
                yield return WaitForConditionOrTimeOut(TestParentedAndNotInScenePlaced);
                AssertOnTimeout($"[{m_FailedValidation.name}] Failed validation! InScenePlaced ({m_FailedValidation.IsSceneObject.Value}) | Was Parented ({m_FailedValidation.transform.position != null})");
            }
        }

        /// <summary>
        /// For distributed authority mode, we have to find the in-scene placed object to parent the newly spawned player under
        /// </summary>
        private bool ParentPlayerToInSceneNetworkObjectFound(int index)
        {
            if (m_ClientNetworkManagers.Length - 1 != index)
            {
                return false;
            }
            var clientId = m_ClientNetworkManagers[index].LocalClientId;
            if (!s_GlobalNetworkObjects.ContainsKey(clientId))
            {
                return false;
            }

            if (!s_GlobalNetworkObjects[clientId].ContainsKey(m_TargetScenePlacedId))
            {
                return false;
            }

            if (!m_ClientNetworkManagers[index].LocalClient.PlayerObject.TrySetParent(s_GlobalNetworkObjects[clientId][m_TargetScenePlacedId], false))
            {
                return false;
            }

            return true;
        }

        private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (clientId == m_ServerNetworkManager.LocalClientId && sceneName == k_SceneToLoad)
            {
                m_SceneIsLoaded = true;
                m_ServerNetworkManager.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;
            }
        }
    }
}
