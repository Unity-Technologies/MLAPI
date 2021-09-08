using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    public abstract class BaseMultiInstanceTest
    {
        private const string k_FirstPartOfTestRunnerSceneName = "InitTestScene";

        protected GameObject m_PlayerPrefab;
        protected NetworkManager m_ServerNetworkManager;
        protected NetworkManager[] m_ClientNetworkManagers;

        protected abstract int NbClients { get; }

        [UnitySetUp]
        public virtual IEnumerator Setup()
        {
            yield return StartSomeClientsAndServerWithPlayers(true, NbClients, _ => { });
        }

        [UnityTearDown]
        public virtual IEnumerator Teardown()
        {
            // Shutdown and clean up both of our NetworkManager instances
            try
            {
                MultiInstanceHelpers.Destroy();
            }
            catch (Exception e) { throw e; }
            finally
            {
                if (m_PlayerPrefab != null)
                {
                    Object.Destroy(m_PlayerPrefab);
                    m_PlayerPrefab = null;
                }
            }

            // Make sure any NetworkObject with a GlobalObjectIdHash value of 0 is destroyed
            // If we are tearing down, we don't want to leave NetworkObjects hanging around
            var networkObjects = Object.FindObjectsOfType<NetworkObject>().ToList();
            foreach (var networkObject in networkObjects)
            {
                Object.DestroyImmediate(networkObject);
            }

            // wait for next frame so everything is destroyed, so following tests can execute from clean environment
            int nextFrameNumber = Time.frameCount + 1;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
        }

        /// <summary>
        /// We want to exclude the TestRunner scene on the host-server side so it won't try to tell clients to
        /// synchronize to this scene when they connect (host-server side only for multiprocess)
        /// </summary>
        private bool VerifySceneIsValidForClientsToLoad(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            // exclude test runner scene
            if (sceneName.StartsWith(k_FirstPartOfTestRunnerSceneName))
            {

                return false;
            }
            return true;
        }

        /// <summary>
        /// Utility to spawn some clients and a server and set them up
        /// </summary>
        /// <param name="nbClients"></param>
        /// <param name="updatePlayerPrefab">Update the prefab with whatever is needed before players spawn</param>
        /// <param name="targetFrameRate">The targetFrameRate of the Unity engine to use while this multi instance test is running. Will be reset on teardown.</param>
        /// <returns></returns>
        public IEnumerator StartSomeClientsAndServerWithPlayers(bool useHost, int nbClients, Action<GameObject> updatePlayerPrefab = null, int targetFrameRate = 60)
        {
            // Make sure any NetworkObject with a GlobalObjectIdHash value of 0 is destroyed
            // If we are tearing down, we don't want to leave NetworkObjects hanging around
            var networkObjects = Object.FindObjectsOfType<NetworkObject>().ToList();
            var networkObjectsList = networkObjects.Where(c => c.GlobalObjectIdHash == 0);
            foreach (var netObject in networkObjects)
            {
                Object.DestroyImmediate(netObject);
            }


            // Create multiple NetworkManager instances
            if (!MultiInstanceHelpers.Create(nbClients, out NetworkManager server, out NetworkManager[] clients, targetFrameRate))
            {
                Debug.LogError("Failed to create instances");
                Assert.Fail("Failed to create instances");
            }

            m_ClientNetworkManagers = clients;
            m_ServerNetworkManager = server;

            // Create playerPrefab
            m_PlayerPrefab = new GameObject("Player");
            NetworkObject networkObject = m_PlayerPrefab.AddComponent<NetworkObject>();
            /*
             * Normally we would only allow player prefabs to be set to a prefab. Not runtime created objects.
             * In order to prevent having a Resource folder full of a TON of prefabs that we have to maintain,
             * MultiInstanceHelper has a helper function that lets you mark a runtime created object to be
             * treated as a prefab by the Netcode. That's how we can get away with creating the player prefab
             * at runtime without it being treated as a SceneObject or causing other conflicts with the Netcode.
             */
            // Make it a prefab
            MultiInstanceHelpers.MakeNetworkObjectTestPrefab(networkObject);

            if (updatePlayerPrefab != null)
            {
                updatePlayerPrefab(m_PlayerPrefab); // update player prefab with whatever is needed before players are spawned
            }

            // Set the player prefab
            server.NetworkConfig.PlayerPrefab = m_PlayerPrefab;

            for (int i = 0; i < clients.Length; i++)
            {
                clients[i].NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            }

            // Start the instances
            if (!MultiInstanceHelpers.Start(useHost, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // If VerifySceneBeforeLoading is not already set, then go ahead and set it so the host/server
            // will not try to synchronize clients to the TestRunner scene.
            if (m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading == null)
            {
                m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading = VerifySceneIsValidForClientsToLoad;
                // If a unit/integration test does not handle this on their own, then Ignore the validation warning
                m_ServerNetworkManager.SceneManager.IgnoreSceneValidationWarning = true;

                // Register the test runner scene so it will be able to synchronize NetworkObjects without logging a
                // warning about using the currently active scene
                var scene = SceneManager.GetActiveScene();
                // As long as this is a test runner scene (or most likely a test runner scene)
                if (scene.name.StartsWith(k_FirstPartOfTestRunnerSceneName))
                {
                    // Register the test runner scene just so we avoid another warning about not being able to find the
                    // scene to synchronize NetworkObjects.  Next, add the currently active test runner scene to the scenes
                    // loaded and register the server to client scene handle since host-server shares the test runner scene
                    // with the clients.
                    server.SceneManager.GetAndAddNewlyLoadedSceneByName(scene.name);
                    server.SceneManager.ServerSceneHandleToClientSceneHandle.Add(scene.handle, scene.handle);
                    for (int i = 0; i < clients.Length; i++)
                    {
                        clients[i].SceneManager.GetAndAddNewlyLoadedSceneByName(scene.name);
                        clients[i].SceneManager.ServerSceneHandleToClientSceneHandle.Add(scene.handle, scene.handle);
                    }
                }
            }

            // Wait for connection on client side
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientsConnected(clients));

            // Wait for connection on server side
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientsConnectedToServer(server, useHost ? nbClients + 1 : nbClients));
        }
    }
}
