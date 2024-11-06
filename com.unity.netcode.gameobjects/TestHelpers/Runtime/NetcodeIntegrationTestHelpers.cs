using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.Netcode.TestHelpers.Runtime
{
    /// <summary>
    /// Provides helpers for running multi instance tests.
    /// </summary>
    public static class NetcodeIntegrationTestHelpers
    {
        public const int DefaultMinFrames = 1;
        public const float DefaultTimeout = 4f;
        private static List<NetworkManager> s_NetworkManagerInstances = new List<NetworkManager>();
        private static Dictionary<NetworkManager, MultiInstanceHooks> s_Hooks = new Dictionary<NetworkManager, MultiInstanceHooks>();
        private static bool s_IsStarted;
        private static int s_ClientCount;
        private static int s_OriginalTargetFrameRate = -1;

        public delegate bool MessageHandleCheck(object receivedMessage);

        internal class MessageHandleCheckWithResult
        {
            public MessageHandleCheck Check;
            public bool Result;
        }
        internal class MessageReceiveCheckWithResult
        {
            public Type CheckType;
            public bool Result;
        }

        private class MultiInstanceHooks : INetworkHooks
        {
            public Dictionary<Type, List<MessageHandleCheckWithResult>> HandleChecks = new Dictionary<Type, List<MessageHandleCheckWithResult>>();
            public List<MessageReceiveCheckWithResult> ReceiveChecks = new List<MessageReceiveCheckWithResult>();

            public static bool CheckForMessageOfType<T>(object receivedMessage) where T : INetworkMessage
            {
                return receivedMessage.GetType() == typeof(T);
            }

            public void OnBeforeSendMessage<T>(ulong clientId, ref T message, NetworkDelivery delivery) where T : INetworkMessage
            {
            }

            public void OnAfterSendMessage<T>(ulong clientId, ref T message, NetworkDelivery delivery, int messageSizeBytes) where T : INetworkMessage
            {
            }

            public void OnBeforeReceiveMessage(ulong senderId, Type messageType, int messageSizeBytes)
            {
                foreach (var check in ReceiveChecks)
                {
                    if (check.CheckType == messageType)
                    {
                        check.Result = true;
                        ReceiveChecks.Remove(check);
                        break;
                    }
                }
            }

            public void OnAfterReceiveMessage(ulong senderId, Type messageType, int messageSizeBytes)
            {
            }

            public void OnBeforeSendBatch(ulong clientId, int messageCount, int batchSizeInBytes, NetworkDelivery delivery)
            {
            }

            public void OnAfterSendBatch(ulong clientId, int messageCount, int batchSizeInBytes, NetworkDelivery delivery)
            {
            }

            public void OnBeforeReceiveBatch(ulong senderId, int messageCount, int batchSizeInBytes)
            {
            }

            public void OnAfterReceiveBatch(ulong senderId, int messageCount, int batchSizeInBytes)
            {
            }

            public bool OnVerifyCanSend(ulong destinationId, Type messageType, NetworkDelivery delivery)
            {
                return true;
            }

            public bool OnVerifyCanReceive(ulong senderId, Type messageType, FastBufferReader messageContent, ref NetworkContext context)
            {
                return true;
            }

            public void OnBeforeHandleMessage<T>(ref T message, ref NetworkContext context) where T : INetworkMessage
            {
            }

            public void OnAfterHandleMessage<T>(ref T message, ref NetworkContext context) where T : INetworkMessage
            {
                if (HandleChecks.ContainsKey(typeof(T)))
                {
                    foreach (var check in HandleChecks[typeof(T)])
                    {
                        if (check.Check(message))
                        {
                            check.Result = true;
                            HandleChecks[typeof(T)].Remove(check);
                            break;
                        }
                    }
                }
            }
        }

        internal const string FirstPartOfTestRunnerSceneName = "InitTestScene";

        public static List<NetworkManager> NetworkManagerInstances => s_NetworkManagerInstances;

        internal static List<IntegrationTestSceneHandler> ClientSceneHandlers = new List<IntegrationTestSceneHandler>();

        /// <summary>
        /// Registers the IntegrationTestSceneHandler for integration tests.
        /// The default client behavior is to not load scenes on the client side.
        /// </summary>
        internal static void RegisterSceneManagerHandler(NetworkManager networkManager, bool allowServer = false)
        {
            if (!networkManager.IsServer || networkManager.IsServer && allowServer)
            {
                var handler = new IntegrationTestSceneHandler(networkManager);
                ClientSceneHandlers.Add(handler);
                networkManager.SceneManager.SceneManagerHandler = handler;
            }
        }

        /// <summary>
        /// Call this to clean up the IntegrationTestSceneHandler and destroy the s_CoroutineRunner.
        /// Note:
        /// If deriving from <see cref="NetcodeIntegrationTest"/> or using <see cref="Destroy"/> then you
        /// typically won't need to do this.
        /// </summary>
        public static void CleanUpHandlers()
        {
            foreach (var handler in ClientSceneHandlers)
            {
                handler.Dispose();
            }
            ClientSceneHandlers.Clear();
        }

        /// <summary>
        /// Call this to register scene validation and the IntegrationTestSceneHandler
        /// Note:
        /// If deriving from <see cref="NetcodeIntegrationTest"/> or using <see cref="Destroy"/> then you
        /// typically won't need to call this.
        /// </summary>
        public static void RegisterHandlers(NetworkManager networkManager, bool serverSideSceneManager = false)
        {
            SceneManagerValidationAndTestRunnerInitialization(networkManager);

            if (!networkManager.IsServer || networkManager.IsServer && serverSideSceneManager)
            {
                // Pass along the serverSideSceneManager property (otherwise the server won't register properly)
                RegisterSceneManagerHandler(networkManager, serverSideSceneManager);
            }
        }

        private static void AddUnityTransport(NetworkManager networkManager)
        {
            // Create transport
            var unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            // We need to increase this buffer size for tests that spawn a bunch of things
            unityTransport.MaxPayloadSize = 256000;
            unityTransport.MaxSendQueueSize = 1024 * 1024;

            // Allow 4 connection attempts that each will time out after 500ms
            unityTransport.MaxConnectAttempts = 4;
            unityTransport.ConnectTimeoutMS = 500;

            // Set the NetworkConfig
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
        }

        private static void AddMockTransport(NetworkManager networkManager)
        {
            // Create transport
            var mockTransport = networkManager.gameObject.AddComponent<MockTransport>();
            // Set the NetworkConfig
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = mockTransport;
        }

        public static NetworkManager CreateServer(bool mockTransport = false)
        {
            // Create gameObject
            var go = new GameObject("NetworkManager - Server");

            // Create networkManager component
            var server = go.AddComponent<NetworkManager>();
            NetworkManagerInstances.Insert(0, server);
            if (mockTransport)
            {
                AddMockTransport(server);
            }
            else
            {
                AddUnityTransport(server);
            }
            return server;
        }

        /// <summary>
        /// Creates NetworkingManagers and configures them for use in a multi instance setting.
        /// </summary>
        /// <param name="clientCount">The amount of clients</param>
        /// <param name="server">The server NetworkManager</param>
        /// <param name="clients">The clients NetworkManagers</param>
        /// <param name="targetFrameRate">The targetFrameRate of the Unity engine to use while the multi instance helper is running. Will be reset on shutdown.</param>
        /// <param name="serverFirst">This determines if the server or clients will be instantiated first (defaults to server first)</param>
        public static bool Create(int clientCount, out NetworkManager server, out NetworkManager[] clients, int targetFrameRate = 60, bool serverFirst = true, bool useMockTransport = false)
        {
            s_NetworkManagerInstances = new List<NetworkManager>();
            server = null;
            if (serverFirst)
            {
                server = CreateServer(useMockTransport);
            }

            CreateNewClients(clientCount, out clients, useMockTransport);

            if (!serverFirst)
            {
                server = CreateServer(useMockTransport);
            }

            s_OriginalTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = targetFrameRate;

            return true;
        }

        internal static NetworkManager CreateNewClient(int identifier, bool mockTransport = false)
        {
            // Create gameObject
            var go = new GameObject("NetworkManager - Client - " + identifier);
            // Create networkManager component
            var networkManager = go.AddComponent<NetworkManager>();
            if (mockTransport)
            {
                AddMockTransport(networkManager);
            }
            else
            {
                AddUnityTransport(networkManager);
            }
            return networkManager;
        }


        /// <summary>
        /// Used to add a client to the already existing list of clients
        /// </summary>
        /// <param name="clientCount">The amount of clients</param>
        /// <param name="clients"></param>
        public static bool CreateNewClients(int clientCount, out NetworkManager[] clients, bool useMockTransport = false)
        {
            clients = new NetworkManager[clientCount];
            for (int i = 0; i < clientCount; i++)
            {
                // Create networkManager component
                clients[i] = CreateNewClient(i, useMockTransport);
            }

            NetworkManagerInstances.AddRange(clients);
            return true;
        }

        /// <summary>
        /// Stops one single client and makes sure to cleanup any static variables in this helper
        /// </summary>
        /// <param name="clientToStop"></param>
        public static void StopOneClient(NetworkManager clientToStop, bool destroy = true)
        {
            clientToStop.Shutdown();
            s_Hooks.Remove(clientToStop);
            if (destroy)
            {
                Object.Destroy(clientToStop.gameObject);
                NetworkManagerInstances.Remove(clientToStop);
            }
        }

        /// <summary>
        /// Starts one single client and makes sure to register the required hooks and handlers
        /// </summary>
        /// <param name="clientToStart"></param>
        public static void StartOneClient(NetworkManager clientToStart)
        {
            clientToStart.StartClient();
            s_Hooks[clientToStart] = new MultiInstanceHooks();
            clientToStart.ConnectionManager.MessageManager.Hook(s_Hooks[clientToStart]);
            if (!NetworkManagerInstances.Contains(clientToStart))
            {
                NetworkManagerInstances.Add(clientToStart);
            }
            // if set, then invoke this for the client
            RegisterHandlers(clientToStart);
        }

        /// <summary>
        /// Should always be invoked when finished with a single unit test
        /// (i.e. during TearDown)
        /// </summary>
        public static void Destroy()
        {
            if (s_IsStarted == false)
            {
                return;
            }

            s_IsStarted = false;

            try
            {
                // Shutdown the server which forces clients to disconnect
                foreach (var networkManager in NetworkManagerInstances)
                {
                    if (networkManager != null && networkManager.IsListening)
                    {
                        networkManager?.Shutdown();
                        s_Hooks.Remove(networkManager);
                    }
                }

                // Destroy the network manager instances
                foreach (var networkManager in NetworkManagerInstances)
                {
                    if (networkManager != null && networkManager.gameObject)
                    {
                        Object.DestroyImmediate(networkManager.gameObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            NetworkManagerInstances.Clear();

            CleanUpHandlers();

            Application.targetFrameRate = s_OriginalTargetFrameRate;
        }

        /// <summary>
        /// We want to exclude the TestRunner scene on the host-server side so it won't try to tell clients to
        /// synchronize to this scene when they connect
        /// </summary>
        private static bool VerifySceneIsValidForClientsToLoad(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            // exclude test runner scene
            if (sceneName.StartsWith(FirstPartOfTestRunnerSceneName))
            {
                return false;
            }
            return true;
        }

        private static bool VerifySceneIsValidForClientsToUnload(Scene scene)
        {
            // Unless specifically set, we always return false
            return false;
        }

        /// <summary>
        /// This registers scene validation callback for the server to prevent it from telling connecting
        /// clients to synchronize (i.e. load) the test runner scene.  This will also register the test runner
        /// scene and its handle for both client(s) and server-host.
        /// </summary>
        private static void SceneManagerValidationAndTestRunnerInitialization(NetworkManager networkManager)
        {
            // If VerifySceneBeforeLoading is not already set, then go ahead and set it so the host/server
            // will not try to synchronize clients to the TestRunner scene.  We only need to do this for the server.
            // All clients in distributed authority mode, should have this registered (since any one client can become the session owner).
            if ((networkManager.IsServer && networkManager.SceneManager.VerifySceneBeforeLoading == null) || networkManager.DistributedAuthorityMode)
            {
                networkManager.SceneManager.VerifySceneBeforeLoading = VerifySceneIsValidForClientsToLoad;

                // If a unit/integration test does not handle this on their own, then Ignore the validation warning
                networkManager.SceneManager.DisableValidationWarnings(true);
            }

            // For testing purposes, all clients always set the VerifySceneBeforeUnloading callback and enabled
            // PostSynchronizationSceneUnloading. Where tests that expect clients to unload scenes should override
            // the callback and return true for the scenes the client(s) is/are allowed to unload.
            if (!networkManager.IsServer && networkManager.SceneManager.VerifySceneBeforeUnloading == null)
            {
                networkManager.SceneManager.VerifySceneBeforeUnloading = VerifySceneIsValidForClientsToUnload;
                networkManager.SceneManager.PostSynchronizationSceneUnloading = true;
            }


            // Register the test runner scene so it will be able to synchronize NetworkObjects without logging a
            // warning about using the currently active scene
            var scene = SceneManager.GetActiveScene();
            // As long as this is a test runner scene (or most likely a test runner scene)
            if (scene.name.StartsWith(FirstPartOfTestRunnerSceneName))
            {
                // Register the test runner scene just so we avoid another warning about not being able to find the
                // scene to synchronize NetworkObjects.  Next, add the currently active test runner scene to the scenes
                // loaded and register the server to client scene handle since host-server shares the test runner scene
                // with the clients.
                if (!networkManager.SceneManager.ScenesLoaded.ContainsKey(scene.handle))
                {
                    networkManager.SceneManager.ScenesLoaded.Add(scene.handle, new NetworkSceneManager.SceneData(null, scene));
                }
                // In distributed authority we need to check if this scene is already added
                if (networkManager.DistributedAuthorityMode)
                {
                    if (!networkManager.SceneManager.ServerSceneHandleToClientSceneHandle.ContainsKey(scene.handle))
                    {
                        networkManager.SceneManager.ServerSceneHandleToClientSceneHandle.Add(scene.handle, scene.handle);
                    }

                    if (!networkManager.SceneManager.ClientSceneHandleToServerSceneHandle.ContainsKey(scene.handle))
                    {
                        networkManager.SceneManager.ClientSceneHandleToServerSceneHandle.Add(scene.handle, scene.handle);
                    }
                    return;
                }
                networkManager.SceneManager.ServerSceneHandleToClientSceneHandle.Add(scene.handle, scene.handle);
            }
        }

        public delegate void BeforeClientStartCallback();

        internal static bool Start(bool host, bool startServer, NetworkManager server, NetworkManager[] clients)
        {
            return Start(host, server, clients, null, startServer);
        }

        /// <summary>
        /// Starts NetworkManager instances created by the Create method.
        /// </summary>
        /// <param name="host">Whether or not to create a Host instead of Server</param>
        /// <param name="server">The Server NetworkManager</param>
        /// <param name="clients">The Clients NetworkManager</param>
        /// <param name="callback">called immediately after server is started and before client(s) are started</param>
        /// <returns></returns>
        public static bool Start(bool host, NetworkManager server, NetworkManager[] clients, BeforeClientStartCallback callback = null, bool startServer = true)
        {
            if (s_IsStarted)
            {
                throw new InvalidOperationException($"{nameof(NetcodeIntegrationTestHelpers)} already thinks it is started. Did you forget to Destroy?");
            }

            s_IsStarted = true;
            s_ClientCount = clients.Length;
            var hooks = (MultiInstanceHooks)null;
            if (startServer)
            {
                if (host)
                {
                    server.StartHost();
                }
                else
                {
                    server.StartServer();
                }

                hooks = new MultiInstanceHooks();
                server.ConnectionManager.MessageManager.Hook(hooks);
                s_Hooks[server] = hooks;

                // Register the server side handler (always pass true for server)
                RegisterHandlers(server, true);
                callback?.Invoke();
            }

            for (int i = 0; i < clients.Length; i++)
            {
                clients[i].StartClient();
                hooks = new MultiInstanceHooks();
                clients[i].ConnectionManager.MessageManager.Hook(hooks);
                s_Hooks[clients[i]] = hooks;

                // if set, then invoke this for the client
                RegisterHandlers(clients[i]);
            }

            return true;
        }

        /// <summary>
        /// Used to return a value of type T from a wait condition
        /// </summary>
        public class ResultWrapper<T>
        {
            public T Result;
        }

        private static uint s_AutoIncrementGlobalObjectIdHashCounter = 111111;

        public static uint GetNextGlobalIdHashValue()
        {
            return ++s_AutoIncrementGlobalObjectIdHashCounter;
        }


        public static bool IsNetcodeIntegrationTestRunning { get; internal set; }
        public static void RegisterNetcodeIntegrationTest(bool registered)
        {
            IsNetcodeIntegrationTestRunning = registered;
        }


        /// <summary>
        /// Normally we would only allow player prefabs to be set to a prefab. Not runtime created objects.
        /// In order to prevent having a Resource folder full of a TON of prefabs that we have to maintain,
        /// MultiInstanceHelper has a helper function that lets you mark a runtime created object to be
        /// treated as a prefab by the Netcode. That's how we can get away with creating the player prefab
        /// at runtime without it being treated as a SceneObject or causing other conflicts with the Netcode.
        /// </summary>
        /// <param name="networkObject">The networkObject to be treated as Prefab</param>
        /// <param name="globalObjectIdHash">The GlobalObjectId to force</param>
        public static void MakeNetworkObjectTestPrefab(NetworkObject networkObject, uint globalObjectIdHash = default)
        {
            // Override `GlobalObjectIdHash` if `globalObjectIdHash` param is set
            if (globalObjectIdHash != default)
            {
                networkObject.GlobalObjectIdHash = globalObjectIdHash;
            }

            // Fallback to auto-increment if `GlobalObjectIdHash` was never set
            if (networkObject.GlobalObjectIdHash == default)
            {
                networkObject.GlobalObjectIdHash = ++s_AutoIncrementGlobalObjectIdHashCounter;
            }

            // Prevent object from being snapped up as a scene object
            networkObject.IsSceneObject = false;

            // To avoid issues with integration tests that forget to clean up,
            // this feature only works with NetcodeIntegrationTest derived classes
            if (IsNetcodeIntegrationTestRunning)
            {
                if (networkObject.GetComponent<ObjectNameIdentifier>() == null && networkObject.GetComponentInChildren<ObjectNameIdentifier>() == null)
                {
                    // Add the object identifier component
                    networkObject.gameObject.AddComponent<ObjectNameIdentifier>();
                }
            }
        }

        public static GameObject CreateNetworkObjectPrefab(string baseName, NetworkManager server, params NetworkManager[] clients)
        {
            void AddNetworkPrefab(NetworkConfig config, NetworkPrefab prefab)
            {
                config.Prefabs.Add(prefab);
            }

            var prefabCreateAssertError = $"You can only invoke this method before starting the network manager(s)!";
            Assert.IsNotNull(server, prefabCreateAssertError);
            Assert.IsFalse(server.IsListening, prefabCreateAssertError);

            var gameObject = new GameObject
            {
                name = baseName
            };
            var networkObject = gameObject.AddComponent<NetworkObject>();
            networkObject.NetworkManagerOwner = server;
            MakeNetworkObjectTestPrefab(networkObject);
            var networkPrefab = new NetworkPrefab() { Prefab = gameObject };

            // We could refactor this test framework to share a NetworkPrefabList instance, but at this point it's
            // probably more trouble than it's worth to verify these lists stay in sync across all tests...
            AddNetworkPrefab(server.NetworkConfig, networkPrefab);
            foreach (var clientNetworkManager in clients)
            {
                AddNetworkPrefab(clientNetworkManager.NetworkConfig, networkPrefab);
            }
            return gameObject;
        }

        // We use GameObject instead of SceneObject to be able to keep hierarchy
        public static void MarkAsSceneObjectRoot(GameObject networkObjectRoot, NetworkManager server, NetworkManager[] clients)
        {
            networkObjectRoot.name += " - Server";

            NetworkObject[] serverNetworkObjects = networkObjectRoot.GetComponentsInChildren<NetworkObject>();

            for (int i = 0; i < serverNetworkObjects.Length; i++)
            {
                serverNetworkObjects[i].NetworkManagerOwner = server;
            }

            for (int i = 0; i < clients.Length; i++)
            {
                GameObject root = Object.Instantiate(networkObjectRoot);
                root.name += " - Client - " + i;

                NetworkObject[] clientNetworkObjects = root.GetComponentsInChildren<NetworkObject>();

                for (int j = 0; j < clientNetworkObjects.Length; j++)
                {
                    clientNetworkObjects[j].NetworkManagerOwner = clients[i];
                }
            }
        }

        /// <summary>
        /// Waits on the client side to be connected.
        /// </summary>
        /// <param name="client">The client</param>
        /// <param name="result">The result. If null, it will automatically assert</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator WaitForClientConnected(NetworkManager client, ResultWrapper<bool> result = null, float timeout = DefaultTimeout)
        {
            yield return WaitForClientsConnected(new NetworkManager[] { client }, result, timeout);
        }

        /// <summary>
        /// Similar to WaitForClientConnected, this waits for multiple clients to be connected.
        /// </summary>
        /// <param name="clients">The clients to be connected</param>
        /// <param name="result">The result. If null, it will automatically assert</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        /// <returns></returns>
        public static IEnumerator WaitForClientsConnected(NetworkManager[] clients, ResultWrapper<bool> result = null, float timeout = DefaultTimeout)
        {
            // Make sure none are the host client
            foreach (var client in clients)
            {
                if (client.IsServer)
                {
                    throw new InvalidOperationException("Cannot wait for connected as server");
                }
            }

            var allConnected = true;
            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeout)
            {
                allConnected = true;
                foreach (var client in clients)
                {
                    if (!client.IsConnectedClient)
                    {
                        allConnected = false;
                        break;
                    }
                }
                if (allConnected)
                {
                    break;
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            if (result != null)
            {
                result.Result = allConnected;
            }
            else
            {
                for (var i = 0; i < clients.Length; ++i)
                {
                    var client = clients[i];
                    // Logging i+1 because that's the local client ID they'll get (0 is server)
                    // Can't use client.LocalClientId because that doesn't get assigned until IsConnectedClient == true,
                    Assert.True(client.IsConnectedClient, $"Client {i + 1} never connected");
                }
            }
        }

        /// <summary>
        /// Waits on the server side for 1 client to be connected
        /// </summary>
        /// <param name="server">The server</param>
        /// <param name="result">The result. If null, it will automatically assert</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator WaitForClientConnectedToServer(NetworkManager server, ResultWrapper<bool> result = null, float timeout = DefaultTimeout)
        {
            yield return WaitForClientsConnectedToServer(server, server.IsHost ? s_ClientCount + 1 : s_ClientCount, result, timeout);
        }

        /// <summary>
        /// Waits on the server side for 1 client to be connected
        /// </summary>
        /// <param name="server">The server</param>
        /// <param name="result">The result. If null, it will automatically assert</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator WaitForClientsConnectedToServer(NetworkManager server, int clientCount = 1, ResultWrapper<bool> result = null, float timeout = DefaultTimeout)
        {
            if (!server.IsServer)
            {
                throw new InvalidOperationException("Cannot wait for connected as client");
            }

            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeout && server.ConnectedClients.Count != clientCount)
            {
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            var res = server.ConnectedClients.Count == clientCount;

            if (result != null)
            {
                result.Result = res;
            }
            else
            {
                Assert.True(res, "A client never connected to server");
            }
        }

        /// <summary>
        /// Gets a NetworkObject instance as it's represented by a certain peer.
        /// </summary>
        /// <param name="networkObjectId">The networkObjectId to get</param>
        /// <param name="representation">The representation to get the object from</param>
        /// <param name="result">The result</param>
        /// <param name="failIfNull">Whether or not to fail if no object is found and result is null</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator GetNetworkObjectByRepresentation(ulong networkObjectId, NetworkManager representation, ResultWrapper<NetworkObject> result, bool failIfNull = true, float timeout = DefaultTimeout)
        {
            if (result == null)
            {
                throw new ArgumentNullException("Result cannot be null");
            }

            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeout && representation.SpawnManager.SpawnedObjects.All(x => x.Value.NetworkObjectId != networkObjectId))
            {
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            result.Result = representation.SpawnManager.SpawnedObjects.First(x => x.Value.NetworkObjectId == networkObjectId).Value;

            if (failIfNull && result.Result == null)
            {
                Assert.Fail("NetworkObject could not be found");
            }
        }

        /// <summary>
        /// Gets a NetworkObject instance as it's represented by a certain peer.
        /// </summary>
        /// <param name="predicate">The predicate used to filter for your target NetworkObject</param>
        /// <param name="representation">The representation to get the object from</param>
        /// <param name="result">The result</param>
        /// <param name="failIfNull">Whether or not to fail if no object is found and result is null</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator GetNetworkObjectByRepresentation(Func<NetworkObject, bool> predicate, NetworkManager representation, ResultWrapper<NetworkObject> result, bool failIfNull = true, float timeout = DefaultTimeout)
        {
            if (result == null)
            {
                throw new ArgumentNullException("Result cannot be null");
            }

            if (predicate == null)
            {
                throw new ArgumentNullException("Predicate cannot be null");
            }

            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeout && !representation.SpawnManager.SpawnedObjects.Any(x => predicate(x.Value)))
            {
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            result.Result = representation.SpawnManager.SpawnedObjects.FirstOrDefault(x => predicate(x.Value)).Value;

            if (failIfNull && result.Result == null)
            {
                Assert.Fail("NetworkObject could not be found");
            }
        }

        /// <summary>
        /// Gets a NetworkObject instance as it's represented by a certain peer.
        /// </summary>
        /// <param name="predicate">The predicate used to filter for your target NetworkObject</param>
        /// <param name="representation">The representation to get the object from</param>
        /// <param name="result">The result</param>
        /// <param name="failIfNull">Whether or not to fail if no object is found and result is null</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static void GetNetworkObjectByRepresentationWithTimeTravel(Func<NetworkObject, bool> predicate, NetworkManager representation, ResultWrapper<NetworkObject> result, bool failIfNull = true, int maxTries = 60)
        {
            if (result == null)
            {
                throw new ArgumentNullException("Result cannot be null");
            }

            if (predicate == null)
            {
                throw new ArgumentNullException("Predicate cannot be null");
            }

            var tries = 0;
            while (++tries < maxTries && !representation.SpawnManager.SpawnedObjects.Any(x => predicate(x.Value)))
            {
                NetcodeIntegrationTest.SimulateOneFrame();
            }

            result.Result = representation.SpawnManager.SpawnedObjects.FirstOrDefault(x => predicate(x.Value)).Value;

            if (failIfNull && result.Result == null)
            {
                Assert.Fail("NetworkObject could not be found");
            }
        }

        /// <summary>
        /// Waits for a predicate condition to be met
        /// </summary>
        /// <param name="predicate">The predicate to wait for</param>
        /// <param name="result">The result. If null, it will fail if the predicate is not met</param>
        /// <param name="minFrames">The min frames to wait for</param>
        /// <param name="maxFrames">The max frames to wait for</param>
        public static IEnumerator WaitForCondition(Func<bool> predicate, ResultWrapper<bool> result = null, float timeout = DefaultTimeout, int minFrames = DefaultMinFrames)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("Predicate cannot be null");
            }

            var startTime = Time.realtimeSinceStartup;

            if (minFrames > 0)
            {
                var waitForFrameCount = Time.frameCount + minFrames;
                yield return new WaitUntil(() => Time.frameCount >= waitForFrameCount);
            }

            while (Time.realtimeSinceStartup - startTime < timeout && !predicate())
            {
                // Changed to 2 frames to avoid the scenario where it would take 1+ frames to
                // see a value change (i.e. discovered in the NetworkTransformTests)
                var nextFrameNumber = Time.frameCount + 2;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            var res = predicate();

            if (result != null)
            {
                result.Result = res;
            }
            else
            {
                Assert.True(res, "PREDICATE CONDITION");
            }
        }

        /// <summary>
        /// Waits for a message of the given type to be received
        /// </summary>
        /// <param name="result">The result. If null, it will fail if the predicate is not met</param>
        /// <param name="timeout">The max time in seconds to wait for</param>
        internal static IEnumerator WaitForMessageOfTypeReceived<T>(NetworkManager toBeReceivedBy, ResultWrapper<bool> result = null, float timeout = DefaultTimeout) where T : INetworkMessage
        {
            var hooks = s_Hooks[toBeReceivedBy];
            var check = new MessageReceiveCheckWithResult { CheckType = typeof(T) };
            hooks.ReceiveChecks.Add(check);
            if (result == null)
            {
                result = new ResultWrapper<bool>();
            }

            var startTime = Time.realtimeSinceStartup;

            while (!check.Result && Time.realtimeSinceStartup - startTime < timeout)
            {
                yield return null;
            }

            var res = check.Result;
            result.Result = res;

            Assert.True(result.Result, $"Expected message {typeof(T).Name} was not received within {timeout}s.");
        }

        /// <summary>
        /// Waits for a message of the given type to be received
        /// </summary>
        /// <param name="result">The result. If null, it will fail if the predicate is not met</param>
        /// <param name="timeout">The max time in seconds to wait for</param>
        internal static IEnumerator WaitForMessageOfTypeHandled<T>(NetworkManager toBeReceivedBy, ResultWrapper<bool> result = null, float timeout = DefaultTimeout) where T : INetworkMessage
        {
            var hooks = s_Hooks[toBeReceivedBy];
            if (!hooks.HandleChecks.ContainsKey(typeof(T)))
            {
                hooks.HandleChecks.Add(typeof(T), new List<MessageHandleCheckWithResult>());
            }
            var check = new MessageHandleCheckWithResult { Check = MultiInstanceHooks.CheckForMessageOfType<T> };
            hooks.HandleChecks[typeof(T)].Add(check);
            if (result == null)
            {
                result = new ResultWrapper<bool>();
            }
            yield return ExecuteWaitForHook(check, result, timeout);

            Assert.True(result.Result, $"Expected message {typeof(T).Name} was not handled within {timeout}s.");
        }

        /// <summary>
        /// Waits for a specific message, defined by a user callback, to be received
        /// </summary>
        /// <param name="requirement">Called for each received message to check if it's the right one</param>
        /// <param name="result">The result. If null, it will fail if the predicate is not met</param>
        /// <param name="timeout">The max time in seconds to wait for</param>
        internal static IEnumerator WaitForMessageMeetingRequirementHandled<T>(NetworkManager toBeReceivedBy, MessageHandleCheck requirement, ResultWrapper<bool> result = null, float timeout = DefaultTimeout)
        {
            var hooks = s_Hooks[toBeReceivedBy];
            if (!hooks.HandleChecks.ContainsKey(typeof(T)))
            {
                hooks.HandleChecks.Add(typeof(T), new List<MessageHandleCheckWithResult>());
            }
            var check = new MessageHandleCheckWithResult { Check = requirement };
            hooks.HandleChecks[typeof(T)].Add(check);
            if (result == null)
            {
                result = new ResultWrapper<bool>();
            }
            yield return ExecuteWaitForHook(check, result, timeout);

            Assert.True(result.Result, $"Expected message meeting user requirements was not handled within {timeout}s.");
        }

        private static IEnumerator ExecuteWaitForHook(MessageHandleCheckWithResult check, ResultWrapper<bool> result, float timeout)
        {
            var startTime = Time.realtimeSinceStartup;

            while (!check.Result && Time.realtimeSinceStartup - startTime < timeout)
            {
                yield return null;
            }

            var res = check.Result;
            result.Result = res;
        }

        public static uint GetGlobalObjectIdHash(NetworkObject networkObject)
        {
            return networkObject.GlobalObjectIdHash;
        }

#if UNITY_EDITOR
        public static void SetRefreshAllPrefabsCallback(Action scenesProcessed)
        {
            NetworkObjectRefreshTool.AllScenesProcessed = scenesProcessed;
        }

        public static void RefreshAllPrefabInstances(NetworkObject networkObject, Action scenesProcessed)
        {
            NetworkObjectRefreshTool.AllScenesProcessed = scenesProcessed;
            networkObject.RefreshAllPrefabInstances();
        }
#endif
    }

    // Empty MonoBehaviour that is a holder of coroutine
    internal class CoroutineRunner : MonoBehaviour
    {
    }
}
