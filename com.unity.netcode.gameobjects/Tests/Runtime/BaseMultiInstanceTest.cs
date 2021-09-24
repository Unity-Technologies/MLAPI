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
            finally
            {
                if (m_PlayerPrefab != null)
                {
                    Object.Destroy(m_PlayerPrefab);
                    m_PlayerPrefab = null;
                }
            }

            // Make sure we clean up after ourselves and destroy any remaining NetworkObjects
            // before we exit our test
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
        /// synchronize to this scene when they connect
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
        /// This registers scene validation callback for the server to prevent it from telling connecting
        /// clients to synchronize (i.e. load) the test runner scene.  This will also register the test runner
        /// scene and its handle for both client(s) and server-host.
        /// </summary>
        private void SceneManagerValidationAndTestRunnerInitialization(NetworkManager networkManager)
        {
            // If VerifySceneBeforeLoading is not already set, then go ahead and set it so the host/server
            // will not try to synchronize clients to the TestRunner scene.  We only need to do this for the server.
            if (networkManager.IsServer && networkManager.SceneManager.VerifySceneBeforeLoading == null)
            {
                networkManager.SceneManager.VerifySceneBeforeLoading = VerifySceneIsValidForClientsToLoad;
                // If a unit/integration test does not handle this on their own, then Ignore the validation warning
                networkManager.SceneManager.IgnoreSceneValidationWarning = true;
            }

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
                networkManager.SceneManager.GetAndAddNewlyLoadedSceneByName(scene.name);
                networkManager.SceneManager.ServerSceneHandleToClientSceneHandle.Add(scene.handle, scene.handle);
            }
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
            // Make sure there are no remaining NetworkObjects from a previous test
            // before we start our new test
            var networkObjects = Object.FindObjectsOfType<NetworkObject>().ToList();
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

            // Start the instances and pass in our SceneManagerInitialization action that is invoked immediately after host-server
            // is started and after each client is started.
            if (!MultiInstanceHelpers.Start(useHost, server, clients, SceneManagerValidationAndTestRunnerInitialization))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // Wait for connection on client side
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientsConnected(clients));

            // Wait for connection on server side
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForClientsConnectedToServer(server, useHost ? nbClients + 1 : nbClients));
        }
    }
}
