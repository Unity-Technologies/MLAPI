using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class RespawnInSceneObjectsAfterShutdown : NetcodeIntegrationTest
    {
        public const string SceneToLoad = "InSceneNetworkObject";
        private const string k_InSceneObjectName = "InSceneObject";
        protected override int NumberOfClients => 0;
        protected Scene m_SceneLoaded;

        public RespawnInSceneObjectsAfterShutdown(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override void OnOneTimeSetup()
        {
            base.OnOneTimeSetup();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            Assert.IsTrue(m_ServerNetworkManager.SceneManager.LoadScene(SceneToLoad, LoadSceneMode.Additive) == SceneEventProgressStatus.Started);
            return base.OnServerAndClientsConnected();
        }

        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != m_ServerNetworkManager.LocalClientId)
            {
                return;
            }
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        if (sceneEvent.Scene.IsValid() && sceneEvent.Scene.isLoaded && sceneEvent.SceneName.Contains(SceneToLoad))
                        {
                            m_SceneLoaded = sceneEvent.Scene;
                        }
                        break;
                    }
            }
        }

        [UnityTest]
        public IEnumerator RespawnInSceneObjectAfterShutdown()
        {
            yield return WaitForConditionOrTimeOut(() => m_SceneLoaded.IsValid() && m_SceneLoaded.isLoaded);
            var networkObject = s_GlobalNetworkObjects[0].Values.Where((c) => c.name.Contains(k_InSceneObjectName)).FirstOrDefault();
            Assert.IsNotNull(networkObject, $"Could not find any {nameof(NetworkObject)}s that contained {k_InSceneObjectName} as part of its name!");
            Assert.IsTrue(networkObject.IsSpawned, $"{networkObject.name} is not spawned on initial load!");

            m_ServerNetworkManager.Shutdown();
            yield return s_DefaultWaitForTick;
            Assert.IsTrue(!networkObject.IsSpawned, $"{networkObject.name} is still spawned after shutting down!");

            m_ServerNetworkManager.StartHost();
            yield return s_DefaultWaitForTick;
            Assert.IsTrue(networkObject.IsSpawned, $"{networkObject.name} is not spawned on restarting host!");

            SceneManager.UnloadSceneAsync(m_SceneLoaded);
            yield return WaitForConditionOrTimeOut(() => !m_SceneLoaded.isLoaded);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for scene {SceneToLoad} to unload!");
        }
    }
}
