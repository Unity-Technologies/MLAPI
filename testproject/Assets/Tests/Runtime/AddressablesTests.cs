#if TESTPROJECT_USE_ADDRESSABLES
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

namespace TestProject.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class AddressablesTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        private const string k_ValidObject = "AddressableTestObject.prefab";

        public AddressablesTests(HostOrServer hostOrServer)
        {
            m_UseHost = hostOrServer == HostOrServer.Host;
        }
        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            return NetworkManagerInstatiationMode.DoNotCreate;
        }

        protected override void OnInlineTearDown()
        {
            ShutdownAndCleanUp();
        }

        private IEnumerator LoadAsset(AssetReferenceGameObject asset, NetcodeIntegrationTestHelpers.ResultWrapper<GameObject> prefab)
        {
            var handle = asset.LoadAssetAsync();
            while (!handle.IsDone)
            {
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }
            prefab.Result = handle.Result;
        }

        protected void StartWithAddressableAssetAdded()
        {
            StartServerAndClientsWithTimeTravel();
        }

        private void AddPrefab(GameObject prefab)
        {
            m_ServerNetworkManager.AddNetworkPrefab(prefab);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.AddNetworkPrefab(prefab);
            }
        }

        private void SpawnAndValidate(GameObject prefab, bool waitAndAddOnClient = false)
        {
            // Have to spawn it ourselves.
            var serverObj = Object.Instantiate(prefab);
            serverObj.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObj.GetComponent<NetworkObject>().Spawn();

#if UNITY_2023_1_OR_NEWER
            var objs = Object.FindObjectsByType<AddressableTestScript>(FindObjectsSortMode.InstanceID);
#else
            var objs = Object.FindObjectsOfType<AddressableTestScript>();
#endif

            // Prefabs loaded by addressables actually don't show up in this search.
            // Unlike other tests that make prefabs programmatically, those aren't added to the scene until they're instantiated
            Assert.AreEqual(1, objs.Length);

            var startTime = MockTimeProvider.StaticRealTimeSinceStartup;

            WaitForMessageReceivedWithTimeTravel<CreateObjectMessage>(new List<NetworkManager> { m_ClientNetworkManagers[0] }, ReceiptType.Received);

            if (waitAndAddOnClient)
            {
                // Since it's not added, after the CreateObjectMessage is received, it's not spawned yet
                // Verify that to be the case as a precondition.
#if UNITY_2023_1_OR_NEWER
                objs = Object.FindObjectsByType<AddressableTestScript>(FindObjectsSortMode.InstanceID);
#else
                objs = Object.FindObjectsOfType<AddressableTestScript>();
#endif
                Assert.AreEqual(1, objs.Length);
                WaitForConditionOrTimeOutWithTimeTravel(() => MockTimeProvider.StaticRealTimeSinceStartup - startTime >= m_ClientNetworkManagers[0].NetworkConfig.SpawnTimeout - 0.25);
                foreach (var client in m_ClientNetworkManagers)
                {
                    client.AddNetworkPrefab(prefab);
                }
            }

#if UNITY_2023_1_OR_NEWER
            objs = Object.FindObjectsByType<AddressableTestScript>(FindObjectsSortMode.InstanceID);
#else
            objs = Object.FindObjectsOfType<AddressableTestScript>();
#endif
            Assert.AreEqual(NumberOfClients + 1, objs.Length);
            foreach (var obj in objs)
            {
                Assert.AreEqual(1234567, obj.AnIntVal);
                Assert.AreEqual("1234567", obj.AStringVal);
                Assert.AreEqual("12345671234567", obj.GetValue());
            }
        }

        [UnityTest]
        public IEnumerator WhenLoadingAValidObjectBeforeStarting_SpawningItSucceedsOnServerAndClient()
        {
            var asset = new AssetReferenceGameObject(k_ValidObject);

            CreateServerAndClients();
            var prefabResult = new NetcodeIntegrationTestHelpers.ResultWrapper<GameObject>();
            yield return LoadAsset(asset, prefabResult);
            AddPrefab(prefabResult.Result);
            StartServerAndClientsWithTimeTravel();

            SpawnAndValidate(prefabResult.Result);
        }

        [UnityTest]
        public IEnumerator WhenLoadingAValidObjectAfterStarting_SpawningItSucceedsOnServerAndClient()
        {
            var asset = new AssetReferenceGameObject(k_ValidObject);

            CreateServerAndClients();
            m_ServerNetworkManager.NetworkConfig.ForceSamePrefabs = false;
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.ForceSamePrefabs = false;
            }

            var prefabResult = new NetcodeIntegrationTestHelpers.ResultWrapper<GameObject>();
            yield return LoadAsset(asset, prefabResult);
            StartServerAndClientsWithTimeTravel();
            AddPrefab(prefabResult.Result);

            SpawnAndValidate(prefabResult.Result);
        }

        [UnityTest]
        public IEnumerator WhenSpawningServerPrefabBeforeClientPrefabHasLoaded_SpawningItSucceedsOnServerAndClientAfterConfiguredDelay([Values(1, 2, 3)] int timeout)
        {
            var asset = new AssetReferenceGameObject(k_ValidObject);

            CreateServerAndClients();
            m_ServerNetworkManager.NetworkConfig.ForceSamePrefabs = false;
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
                client.NetworkConfig.ForceSamePrefabs = false;
            }

            var prefabResult = new NetcodeIntegrationTestHelpers.ResultWrapper<GameObject>();
            yield return LoadAsset(asset, prefabResult);
            StartServerAndClientsWithTimeTravel();
            m_ServerNetworkManager.AddNetworkPrefab(prefabResult.Result);

            SpawnAndValidate(prefabResult.Result, true);
        }
    }
}
#endif
