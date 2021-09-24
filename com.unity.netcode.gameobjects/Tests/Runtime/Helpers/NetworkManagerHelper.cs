using System;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.Netcode.Transports.UNET;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Helper class to instantiate a NetworkManager
    /// This also provides the ability to:
    /// --- instantiate GameObjects with NetworkObject components that returns a Guid for accessing it later.
    /// --- add NetworkBehaviour components to the instantiated GameObjects
    /// --- spawn a NetworkObject using its parent GameObject's Guid
    /// Call StartNetworkManager in the constructor of your runtime unit test class.
    /// Call ShutdownNetworkManager in the destructor  of your runtime unit test class.
    ///
    /// Includes a useful "BuffersMatch" method that allows you to compare two buffers (returns true if they match false if not)
    /// </summary>
    public static class NetworkManagerHelper
    {
        public static SocketTasks StartHostSocketTasks { get; internal set; }
        public static NetworkManager NetworkManagerObject { get; internal set; }
        public static GameObject NetworkManagerGameObject { get; internal set; }

        internal static Dictionary<Guid, GameObject> InstantiatedGameObjects = new Dictionary<Guid, GameObject>();
        internal static Dictionary<Guid, NetworkObject> InstantiatedNetworkObjects = new Dictionary<Guid, NetworkObject>();
        internal static NetworkManagerOperatingMode CurrentNetworkManagerMode;

        /// <summary>
        /// This provides the ability to start NetworkManager in various modes
        /// </summary>
        public enum NetworkManagerOperatingMode
        {
            None,
            Host,
            Server,
            Client,
        }

        /// <summary>
        /// Called upon the RpcQueueTests being instantiated.
        /// This creates an instance of the NetworkManager to be used during unit tests.
        /// Currently, the best method to run unit tests is by starting in host mode as you can
        /// send messages to yourself (i.e. Host-Client to Host-Server and vice versa).
        /// As such, the default setting is to start in Host mode.
        /// </summary>
        /// <param name="managerMode">parameter to specify which mode you want to start the NetworkManager</param>
        /// <param name="networkConfig">parameter to specify custom NetworkConfig settings</param>
        /// <returns>true if it was instantiated or is already instantiate otherwise false means it failed to instantiate</returns>
        public static bool StartNetworkManager(out NetworkManager networkManager, NetworkManagerOperatingMode managerMode = NetworkManagerOperatingMode.Host, NetworkConfig networkConfig = null)
        {
            // If we are changing the current manager mode and the current manager mode is not "None", then stop the NetworkManager mode
            if (CurrentNetworkManagerMode != managerMode && CurrentNetworkManagerMode != NetworkManagerOperatingMode.None)
            {
                StopNetworkManagerMode();
            }

            if (NetworkManagerGameObject == null)
            {
                NetworkManagerGameObject = new GameObject(nameof(NetworkManager));
                NetworkManagerObject = NetworkManagerGameObject.AddComponent<NetworkManager>();

                if (NetworkManagerObject == null)
                {
                    networkManager = null;
                    return false;
                }

                Debug.Log($"{nameof(NetworkManager)} Instantiated.");

                var unetTransport = NetworkManagerGameObject.AddComponent<UNetTransport>();
                if (networkConfig == null)
                {
                    networkConfig = new NetworkConfig
                    {
                        EnableSceneManagement = false,
                    };
                }

                NetworkManagerObject.NetworkConfig = networkConfig;

                unetTransport.ConnectAddress = "127.0.0.1";
                unetTransport.ConnectPort = 7777;
                unetTransport.ServerListenPort = 7777;
                unetTransport.MessageBufferSize = 65535;
                unetTransport.MaxConnections = 100;
                unetTransport.MessageSendMode = UNetTransport.SendMode.Immediately;
                NetworkManagerObject.NetworkConfig.NetworkTransport = unetTransport;

                // Starts the network manager in the mode specified
                StartNetworkManagerMode(managerMode);
            }

            networkManager = NetworkManagerObject;

            return true;
        }

        /// <summary>
        /// Add a GameObject with a NetworkObject component
        /// </summary>
        /// <param name="nameOfGameObject">the name of the object</param>
        /// <returns></returns>
        public static Guid AddGameNetworkObject(string nameOfGameObject)
        {
            var gameObjectId = Guid.NewGuid();

            // Create the player object that we will spawn as a host
            var gameObject = new GameObject(nameOfGameObject);

            Assert.IsNotNull(gameObject);

            var networkObject = gameObject.AddComponent<NetworkObject>();

            Assert.IsNotNull(networkObject);

            Assert.IsFalse(InstantiatedGameObjects.ContainsKey(gameObjectId));
            Assert.IsFalse(InstantiatedNetworkObjects.ContainsKey(gameObjectId));

            InstantiatedGameObjects.Add(gameObjectId, gameObject);
            InstantiatedNetworkObjects.Add(gameObjectId, networkObject);

            return gameObjectId;
        }

        /// <summary>
        /// Helper class to add a component to the GameObject with a NetoworkObject component
        /// </summary>
        /// <typeparam name="T">NetworkBehaviour component being added to the GameObject</typeparam>
        /// <param name="gameObjectIdentifier">ID returned to reference the game object</param>
        /// <returns></returns>
        public static T AddComponentToObject<T>(Guid gameObjectIdentifier) where T : NetworkBehaviour
        {
            Assert.IsTrue(InstantiatedGameObjects.ContainsKey(gameObjectIdentifier));
            return InstantiatedGameObjects[gameObjectIdentifier].AddComponent<T>();
        }

        /// <summary>
        /// Spawn the NetworkObject, so Rpcs can flow
        /// </summary>
        /// <param name="gameObjectIdentifier">ID returned to reference the game object</param>
        public static void SpawnNetworkObject(Guid gameObjectIdentifier)
        {
            Assert.IsTrue(InstantiatedNetworkObjects.ContainsKey(gameObjectIdentifier));
            if (!InstantiatedNetworkObjects[gameObjectIdentifier].IsSpawned)
            {
                InstantiatedNetworkObjects[gameObjectIdentifier].Spawn();
            }
        }

        /// <summary>
        /// Starts the NetworkManager in the current mode specified by managerMode
        /// </summary>
        /// <param name="managerMode">the mode to start the NetworkManager as</param>
        private static void StartNetworkManagerMode(NetworkManagerOperatingMode managerMode)
        {
            CurrentNetworkManagerMode = managerMode;
            switch (CurrentNetworkManagerMode)
            {
                case NetworkManagerOperatingMode.Host:
                    {
                        // Starts the host
                        NetworkManagerObject.StartHost();
                        break;
                    }
                case NetworkManagerOperatingMode.Server:
                    {
                        // Starts the server
                        NetworkManagerObject.StartServer();
                        break;
                    }
                case NetworkManagerOperatingMode.Client:
                    {
                        // Starts the client
                        NetworkManagerObject.StartClient();
                        break;
                    }
            }

            // If we started an netcode session
            if (CurrentNetworkManagerMode != NetworkManagerOperatingMode.None)
            {
                // With some unit tests the Singleton can still be from a previous unit test
                // depending upon the order of operations that occurred.
                if (NetworkManager.Singleton != NetworkManagerObject)
                {
                    NetworkManagerObject.SetSingleton();
                }

                // Only log this if we started an netcode session
                Debug.Log($"{CurrentNetworkManagerMode} started.");
            }
        }

        /// <summary>
        /// Stops the current mode of the NetworkManager
        /// </summary>
        private static void StopNetworkManagerMode()
        {
            NetworkManagerObject.Shutdown();

            Debug.Log($"{CurrentNetworkManagerMode} stopped.");
            CurrentNetworkManagerMode = NetworkManagerOperatingMode.None;
        }

        // This is called, even if we assert and exit early from a test
        public static void ShutdownNetworkManager()
        {
            // clean up any game objects created with custom unit testing components
            foreach (var entry in InstantiatedGameObjects)
            {
                UnityEngine.Object.DestroyImmediate(entry.Value);
            }

            InstantiatedGameObjects.Clear();

            if (NetworkManagerGameObject != null)
            {
                Debug.Log($"{nameof(NetworkManager)} shutdown.");

                StopNetworkManagerMode();
                UnityEngine.Object.DestroyImmediate(NetworkManagerGameObject);
                Debug.Log($"{nameof(NetworkManager)} destroyed.");
            }
            NetworkManagerGameObject = null;
            NetworkManagerObject = null;
        }

        public static bool BuffersMatch(int indexOffset, long targetSize, byte[] sourceArray, byte[] originalArray)
        {
            long largeInt64Blocks = targetSize >> 3; // Divide by 8
            int originalArrayOffset = 0;
            // process by 8 byte blocks if we can
            for (long i = 0; i < largeInt64Blocks; i++)
            {
                if (BitConverter.ToInt64(sourceArray, indexOffset) != BitConverter.ToInt64(originalArray, originalArrayOffset))
                {
                    return false;
                }
                indexOffset += 8;
                originalArrayOffset += 8;
            }

            long offset = largeInt64Blocks * 8;
            long remainder = targetSize - offset;

            // 4 byte block
            if (remainder >= 4)
            {
                if (BitConverter.ToInt32(sourceArray, indexOffset) != BitConverter.ToInt32(originalArray, originalArrayOffset))
                {
                    return false;
                }
                indexOffset += 4;
                originalArrayOffset += 4;
                offset += 4;
            }

            // Remainder of bytes < 4
            if (targetSize - offset > 0)
            {
                for (long i = 0; i < (targetSize - offset); i++)
                {
                    if (sourceArray[indexOffset + i] != originalArray[originalArrayOffset + i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
