using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TestProject.RuntimeTests
{

    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class NetworkSceneManagerPopulateInSceneTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 0;


        protected Dictionary<uint, GameObject> m_InSceneObjectList = new Dictionary<uint, GameObject>();

        public NetworkSceneManagerPopulateInSceneTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override IEnumerator OnSetup()
        {
            m_InSceneObjectList.Clear();
            return base.OnSetup();
        }

        protected override void OnServerAndClientsCreated()
        {
            // Create one that simulates when an in-scene placed NetworkObject is first instantiated when
            // the scene is loaded (i.e. IsSceneObject is null)
            var inScenePrefab = CreateNetworkObjectPrefab("NewSceneObject");
            var networkObject = inScenePrefab.GetComponent<NetworkObject>();
            networkObject.IsSceneObject = null;
            networkObject.NetworkManagerOwner = m_ServerNetworkManager;
            m_InSceneObjectList.Add(networkObject.GlobalObjectIdHash, inScenePrefab);

            // Create one that simulates when an in-scene placed NetworkObject has already been instantiated
            // (i.e. IsSceneObject is true) which can happen if a client disconnects and then reconnects without
            // unloading/reloading any scenes.
            inScenePrefab = CreateNetworkObjectPrefab("SetInSceneObject");
            networkObject = inScenePrefab.GetComponent<NetworkObject>();
            networkObject.IsSceneObject = true;
            networkObject.NetworkManagerOwner = m_ServerNetworkManager;
            m_InSceneObjectList.Add(networkObject.GlobalObjectIdHash, inScenePrefab);
        }

        [UnityTest]
        public IEnumerator PopulateScenePlacedObjectsTest()
        {
            var activeScene = SceneManager.GetActiveScene();

            m_ServerNetworkManager.SceneManager.PopulateScenePlacedObjects(activeScene, true);
            var scenePlacedNetworkObjects = m_ServerNetworkManager.SceneManager.ScenePlacedObjects;
            foreach (var entry in m_InSceneObjectList)
            {
                // Verify the GlobalObjectIdHash for this object has an entry
                Assert.IsTrue(scenePlacedNetworkObjects.ContainsKey(entry.Key), $"Failed to find {nameof(NetworkObject.GlobalObjectIdHash)}({entry.Key}) for {entry.Value.name} in the {nameof(NetworkSceneManager.ScenePlacedObjects)}!");

                // Verify the active scene for this object has an entry
                Assert.IsTrue(scenePlacedNetworkObjects[entry.Key].ContainsKey(activeScene.handle), $"Failed to find the scene handle {activeScene.handle} ({activeScene.name}) entry for {entry.Value.name} in the {nameof(NetworkSceneManager.ScenePlacedObjects)}!");

                // Verify the GameObject is the same one
                var inSceneGameObject = scenePlacedNetworkObjects[entry.Key][activeScene.handle].gameObject;
                Assert.IsTrue(inSceneGameObject == entry.Value, $"{nameof(GameObject)} {entry.Value.name} is not the same as {inSceneGameObject.name}!");
            }

            yield break;
        }

        protected override IEnumerator OnTearDown()
        {
            foreach (var spawnedInstance in m_InSceneObjectList)
            {
                Object.Destroy(spawnedInstance.Value);
            }
            return base.OnTearDown();
        }
    }
}
