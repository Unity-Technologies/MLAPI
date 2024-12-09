using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class AddNetworkPrefabTest : NetcodeIntegrationTest
    {
        internal class EmptyComponent : NetworkBehaviour
        {

        }
        protected override int NumberOfClients => 1;

        private GameObject m_Prefab;

        protected override IEnumerator OnSetup()
        {
            // Host is irrelevant, messages don't get sent to the host "client"
            m_UseHost = false;

            yield return null;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_Prefab = new GameObject("Object");
            var networkObject = m_Prefab.AddComponent<NetworkObject>();
            m_Prefab.AddComponent<EmptyComponent>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            m_ServerNetworkManager.NetworkConfig.SpawnTimeout = 0;
            m_ServerNetworkManager.NetworkConfig.ForceSamePrefabs = false;
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = 0;
                client.NetworkConfig.ForceSamePrefabs = false;
            }
        }

        private EmptyComponent GetObjectForClient(ulong clientId)
        {
#if UNITY_2023_1_OR_NEWER
            var emptyComponents = Object.FindObjectsByType<EmptyComponent>(FindObjectsSortMode.InstanceID);
#else
            var emptyComponents = Object.FindObjectsOfType<EmptyComponent>();
#endif
            foreach (var component in emptyComponents)
            {
                if (component.IsSpawned && component.NetworkManager.LocalClientId == clientId)
                {
                    return component;
                }
            }
            return null;
        }

        private void RegisterPrefab()
        {
            m_ServerNetworkManager.AddNetworkPrefab(m_Prefab);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.AddNetworkPrefab(m_Prefab);
            }
        }

        private void DeregisterPrefab()
        {
            m_ServerNetworkManager.RemoveNetworkPrefab(m_Prefab);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.RemoveNetworkPrefab(m_Prefab);
            }
        }

        private static CoroutineRunner s_CoroutineRunner;

        [UnityTest]
        public IEnumerator WhenSpawningBeforeAddingPrefab_SpawnFails()
        {
            var serverObject = Object.Instantiate(m_Prefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<CreateObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));
        }

        [UnityTest]
        public IEnumerator WhenSpawningAfterAddingServerPrefabButBeforeAddingClientPrefab_SpawnFails()
        {
            m_ServerNetworkManager.AddNetworkPrefab(m_Prefab);

            var serverObject = Object.Instantiate(m_Prefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<CreateObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));
        }

        [UnityTest]
        public IEnumerator WhenSpawningAfterAddingPrefabOnServerAndClient_SpawnSucceeds()
        {
            RegisterPrefab();

            var serverObject = Object.Instantiate(m_Prefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeHandled<CreateObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNotNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));
        }

        [UnityTest]
        public IEnumerator WhenSpawningAfterRemovingPrefabOnClient_SpawnFails()
        {
            RegisterPrefab();

            var serverObject = Object.Instantiate(m_Prefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<CreateObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNotNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));

            serverObject.GetComponent<NetworkObject>().Despawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<DestroyObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));

            DeregisterPrefab();

            serverObject = Object.Instantiate(m_Prefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<CreateObjectMessage>(m_ClientNetworkManagers[0]);
            Assert.IsNull(GetObjectForClient(m_ClientNetworkManagers[0].LocalClientId));
        }
    }
}
