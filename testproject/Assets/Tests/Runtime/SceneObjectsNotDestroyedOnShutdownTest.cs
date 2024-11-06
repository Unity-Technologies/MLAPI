using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TestProject.RuntimeTests
{
    public class SceneObjectsNotDestroyedOnShutdownTest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 0;

        private const string k_TestScene = "InSceneNetworkObject";
        private const string k_SceneObjectName = "InSceneObject";
        private Scene m_TestScene;
        private WaitForSeconds m_DefaultWaitForTick = new WaitForSeconds(1.0f / 30);

        [UnityTest]
        public IEnumerator SceneObjectsNotDestroyedOnShutdown()
        {
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_ServerNetworkManager.SceneManager.LoadScene(k_TestScene, LoadSceneMode.Additive);

            yield return WaitForConditionOrTimeOut(() => m_TestScene.IsValid() && m_TestScene.isLoaded);
            AssertOnTimeout($"Timed out waiting for scene {k_TestScene} to load!");
#if UNITY_2023_1_OR_NEWER
            var loadedInSceneObject = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName)).FirstOrDefault();
#else
            var loadedInSceneObject = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName)).FirstOrDefault();
#endif
            Assert.IsNotNull(loadedInSceneObject, $"Failed to find {k_SceneObjectName} before starting client!");

            AssertOnTimeout($"Timed out waiting to find {k_SceneObjectName} after scene load and before starting client!\"");

            yield return CreateAndStartNewClient();

#if UNITY_2023_1_OR_NEWER
            var loadedInSceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName));
#else
            var loadedInSceneObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName));
#endif
            Assert.IsTrue(loadedInSceneObjects.Count() > 1, $"Only found one instance of {k_SceneObjectName} after client connected!");
            m_ClientNetworkManagers[0].Shutdown();
            yield return m_DefaultWaitForTick;
#if UNITY_2023_1_OR_NEWER
            loadedInSceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName));
#else
            loadedInSceneObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName));
#endif
            Assert.IsTrue(loadedInSceneObjects.Count() > 1, $"Only found one instance of {k_SceneObjectName} after client shutdown!");
        }

        [UnityTest]
        public IEnumerator ChildSceneObjectsDoNotDestroyOnShutdown()
        {
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_ServerNetworkManager.SceneManager.LoadScene(k_TestScene, LoadSceneMode.Additive);

            yield return WaitForConditionOrTimeOut(() => m_TestScene.IsValid() && m_TestScene.isLoaded);
            AssertOnTimeout($"Timed out waiting for scene {k_TestScene} to load!");

#if UNITY_2023_1_OR_NEWER
            var loadedInSceneObject = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName)).FirstOrDefault();
#else
            var loadedInSceneObject = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName)).FirstOrDefault();
#endif
            Assert.IsNotNull(loadedInSceneObject, $"Failed to find {k_SceneObjectName} before starting client!");
            yield return CreateAndStartNewClient();

            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            Assert.IsTrue(loadedInSceneObject.TrySetParent(m_PlayerNetworkObjects[0][clientId]), $"Failed to parent in-scene object under client player");

            yield return WaitForConditionOrTimeOut(() => PlayerHasChildren(clientId));
            AssertOnTimeout($"Client-{clientId} player never parented {k_SceneObjectName}!");

#if UNITY_2023_1_OR_NEWER
            var loadedInSceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName));
#else
            var loadedInSceneObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName));
#endif
            Assert.IsTrue(loadedInSceneObjects.Count() > 1, $"Only found one instance of {k_SceneObjectName} after client connected!");
            m_ClientNetworkManagers[0].Shutdown();
            yield return m_DefaultWaitForTick;

            // Sanity check to make sure the client's player no longer has any children
            yield return WaitForConditionOrTimeOut(() => PlayerNoLongerExistsWithChildren(clientId));
            AssertOnTimeout($"Client-{clientId} player still exits with children after client shutdown!");
#if UNITY_2023_1_OR_NEWER
            loadedInSceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.name.Contains(k_SceneObjectName));
#else
            loadedInSceneObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.name.Contains(k_SceneObjectName));
#endif
            // Make sure any in-scene placed NetworkObject instantiated has no parent
            foreach (var insceneObject in loadedInSceneObjects)
            {
                Assert.IsTrue(insceneObject.transform.parent == null, $"{insceneObject.name} is still parented!");
            }

            // We should have exactly 2 in-scene placed NetworkObjects remaining:
            // One instance on host side and one on the disconnected client side.
            Assert.IsTrue(loadedInSceneObjects.Count() == 2, $"Only found one instance of {k_SceneObjectName} after client shutdown!");
        }

        private bool PlayerHasChildren(ulong clientId)
        {
            if (m_PlayerNetworkObjects[clientId].ContainsKey(clientId) && m_PlayerNetworkObjects[clientId][clientId] != null)
            {
                return m_PlayerNetworkObjects[clientId][clientId].transform.childCount > 0;
            }
            return false;
        }

        private bool PlayerNoLongerExistsWithChildren(ulong clientId)
        {
            if (m_PlayerNetworkObjects[0].ContainsKey(clientId) && m_PlayerNetworkObjects[0][clientId] != null)
            {
                return m_PlayerNetworkObjects[0][clientId].transform.childCount == 0;
            }
            return true;
        }

        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != m_ServerNetworkManager.LocalClientId)
            {
                return;
            }

            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == k_TestScene)
            {
                m_TestScene = sceneEvent.Scene;
            }
        }
    }
}
