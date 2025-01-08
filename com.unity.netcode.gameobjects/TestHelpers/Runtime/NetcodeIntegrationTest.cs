using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Netcode.RuntimeTests;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.TestHelpers.Runtime
{
    /// <summary>
    /// The default Netcode for GameObjects integration test helper class
    /// </summary>
    public abstract class NetcodeIntegrationTest
    {
        /// <summary>
        /// Used to determine if a NetcodeIntegrationTest is currently running to
        /// determine how clients will load scenes
        /// </summary>
        internal static bool IsRunning { get; private set; }

        protected static TimeoutHelper s_GlobalTimeoutHelper = new TimeoutHelper(8.0f);
        protected static WaitForSecondsRealtime s_DefaultWaitForTick = new WaitForSecondsRealtime(1.0f / k_DefaultTickRate);

        public NetcodeLogAssert NetcodeLogAssert;

        /// <summary>
        /// Registered list of all NetworkObjects spawned.
        /// Format is as follows:
        /// [ClientId-side where this NetworkObject instance resides][NetworkObjectId][NetworkObject]
        /// Where finding the NetworkObject with a NetworkObjectId of 10 on ClientId of 2 would be:
        /// s_GlobalNetworkObjects[2][10]
        /// To find the client or server player objects please see:
        /// <see cref="m_PlayerNetworkObjects"/>
        /// </summary>
        protected static Dictionary<ulong, Dictionary<ulong, NetworkObject>> s_GlobalNetworkObjects = new Dictionary<ulong, Dictionary<ulong, NetworkObject>>();

        public static void RegisterNetworkObject(NetworkObject networkObject)
        {
            if (!s_GlobalNetworkObjects.ContainsKey(networkObject.NetworkManager.LocalClientId))
            {
                s_GlobalNetworkObjects.Add(networkObject.NetworkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
            }

            if (s_GlobalNetworkObjects[networkObject.NetworkManager.LocalClientId].ContainsKey(networkObject.NetworkObjectId))
            {
                if (s_GlobalNetworkObjects[networkObject.NetworkManager.LocalClientId] == null)
                {
                    Assert.False(s_GlobalNetworkObjects[networkObject.NetworkManager.LocalClientId][networkObject.NetworkObjectId] != null,
                        $"Duplicate NetworkObjectId {networkObject.NetworkObjectId} found in {nameof(s_GlobalNetworkObjects)} for client id {networkObject.NetworkManager.LocalClientId}!");
                }
                else
                {
                    s_GlobalNetworkObjects[networkObject.NetworkManager.LocalClientId][networkObject.NetworkObjectId] = networkObject;
                }
            }
            else
            {
                s_GlobalNetworkObjects[networkObject.NetworkManager.LocalClientId].Add(networkObject.NetworkObjectId, networkObject);
            }
        }

        public static void DeregisterNetworkObject(NetworkObject networkObject)
        {
            if (networkObject.IsSpawned && networkObject.NetworkManager != null)
            {
                DeregisterNetworkObject(networkObject.NetworkManager.LocalClientId, networkObject.NetworkObjectId);
            }
        }

        public static void DeregisterNetworkObject(ulong localClientId, ulong networkObjectId)
        {
            if (s_GlobalNetworkObjects.ContainsKey(localClientId) && s_GlobalNetworkObjects[localClientId].ContainsKey(networkObjectId))
            {
                s_GlobalNetworkObjects[localClientId].Remove(networkObjectId);
                if (s_GlobalNetworkObjects[localClientId].Count == 0)
                {
                    s_GlobalNetworkObjects.Remove(localClientId);
                }
            }
        }

        protected int TotalClients => m_UseHost ? m_NumberOfClients + 1 : m_NumberOfClients;

        protected const uint k_DefaultTickRate = 30;

        private int m_NumberOfClients;
        protected abstract int NumberOfClients { get; }

        /// <summary>
        /// Set this to false to create the clients first.
        /// Note: If you are using scene placed NetworkObjects or doing any form of scene testing and
        /// get prefab hash id "soft synchronization" errors, then set this to false and run your test
        /// again.  This is a work-around until we can resolve some issues with NetworkManagerOwner and
        /// NetworkManager.Singleton.
        /// </summary>
        protected bool m_CreateServerFirst = true;

        public enum NetworkManagerInstatiationMode
        {
            PerTest, // This will create and destroy new NetworkManagers for each test within a child derived class
            AllTests, // This will create one set of NetworkManagers used for all tests within a child derived class (destroyed once all tests are finished)
            DoNotCreate // This will not create any NetworkManagers, it is up to the derived class to manage.
        }

        public enum HostOrServer
        {
            Host,
            Server
        }

        protected GameObject m_PlayerPrefab;
        protected NetworkManager m_ServerNetworkManager;
        protected NetworkManager[] m_ClientNetworkManagers;

        /// <summary>
        /// Contains each client relative set of player NetworkObject instances
        /// [Client Relative set of player instances][The player instance ClientId][The player instance's NetworkObject]
        /// Example:
        /// To get the player instance with a ClientId of 3 that was instantiated (relative) on the player instance with a ClientId of 2
        /// m_PlayerNetworkObjects[2][3]
        /// </summary>
        protected Dictionary<ulong, Dictionary<ulong, NetworkObject>> m_PlayerNetworkObjects = new Dictionary<ulong, Dictionary<ulong, NetworkObject>>();

        protected bool m_UseHost = true;
        protected int m_TargetFrameRate = 60;

        private NetworkManagerInstatiationMode m_NetworkManagerInstatiationMode;

        protected bool m_EnableVerboseDebug { get; set; }

        /// <summary>
        /// When set to true, this will bypass the entire
        /// wait for clients to connect process.
        /// </summary>
        /// <remarks>
        /// CAUTION:
        /// Setting this to true will bypass other helper
        /// identification related code, so this should only
        /// be used for connection failure oriented testing
        /// </remarks>
        protected bool m_BypassConnectionTimeout { get; set; }

        /// <summary>
        /// Enables "Time Travel" within the test, which swaps the time provider for the SDK from Unity's
        /// <see cref="Time"/> class to <see cref="MockTimeProvider"/>, and also swaps the transport implementation
        /// from <see cref="UnityTransport"/> to <see cref="MockTransport"/>.
        ///
        /// This enables five important things that help with both performance and determinism of tests that involve a
        /// lot of time and waiting:
        /// 1) It allows time to move in a completely deterministic way (testing that something happens after n seconds,
        /// the test will always move exactly n seconds with no chance of any variability in the timing),
        /// 2) It allows skipping periods of time without actually waiting that amount of time, while still simulating
        /// SDK frames as if that time were passing,
        /// 3) It dissociates the SDK's update loop from Unity's update loop, allowing us to simulate SDK frame updates
        /// without waiting for Unity to process things like physics, animation, and rendering that aren't relevant to
        /// the test,
        /// 4) It dissociates the SDK's messaging system from the networking hardware, meaning there's no delay between
        /// a message being sent and it being received, allowing us to deterministically rely on the message being
        /// received within specific time frames for the test, and
        /// 5) It allows tests to be written without the use of coroutines, which not only improves the test's runtime,
        /// but also results in easier-to-read callstacks and removes the possibility for an assertion to result in the
        /// test hanging.
        ///
        /// When time travel is enabled, the following methods become available:
        ///
        /// <see cref="TimeTravel"/>: Simulates a specific number of frames passing over a specific time period
        /// <see cref="TimeTravelToNextTick"/>: Skips forward to the next tick, siumlating at the current application frame rate
        /// <see cref="WaitForConditionOrTimeOutWithTimeTravel(Func{bool},int)"/>: Simulates frames at the application frame rate until the given condition is true
        /// <see cref="WaitForMessageReceivedWithTimeTravel{T}"/>: Simulates frames at the application frame rate until the required message is received
        /// <see cref="WaitForMessagesReceivedWithTimeTravel"/>: Simulates frames at the application frame rate until the required messages are received
        /// <see cref="StartServerAndClientsWithTimeTravel"/>: Starts a server and client and allows them to connect via simulated frames
        /// <see cref="CreateAndStartNewClientWithTimeTravel"/>: Creates a client and waits for it to connect via simulated frames
        /// <see cref="WaitForClientsConnectedOrTimeOutWithTimeTravel(Unity.Netcode.NetworkManager[])"/> Simulates frames at the application frame rate until the given clients are connected
        /// <see cref="StopOneClientWithTimeTravel"/>: Stops a client and simulates frames until it's fully disconnected.
        ///
        /// When time travel is enabled, <see cref="NetcodeIntegrationTest"/> will automatically use these in its methods
        /// when doing things like automatically connecting clients during SetUp.
        ///
        /// Additionally, the following methods replace their non-time-travel equivalents with variants that are not coroutines:
        /// <see cref="OnTimeTravelStartedServerAndClients"/> - called when server and clients are started
        /// <see cref="OnTimeTravelServerAndClientsConnected"/> - called when server and clients are connected
        ///
        /// Note that all of the non-time travel functions can still be used even when time travel is enabled - this is
        /// sometimes needed for, e.g., testing NetworkAnimator, where the unity update loop needs to run to process animations.
        /// However, it's VERY important to note here that, because the SDK will not be operating based on real-world time
        /// but based on the frozen time that's locked in from MockTimeProvider, actions that pass 10 seconds apart by
        /// real-world clock time will be perceived by the SDK as having happened simultaneously if you don't call
        /// <see cref="MockTimeProvider.TimeTravel"/> to cover the equivalent time span in the mock time provider.
        /// (Calling <see cref="MockTimeProvider.TimeTravel"/> instead of <see cref="TimeTravel"/>
        /// will move time forward without simulating any frames, which, in the case where real-world time has passed,
        /// is likely more desirable). In most cases, this desynch won't affect anything, but it is worth noting that
        /// it happens just in case a tested system depends on both the unity update loop happening *and* time moving forward.
        /// </summary>
        protected virtual bool m_EnableTimeTravel => false;

        /// <summary>
        /// If this is false, SetUp will call OnInlineSetUp instead of OnSetUp.
        /// This is a performance advantage when not using the coroutine functionality, as a coroutine that
        /// has no yield instructions in it will nonetheless still result in delaying the continuation of the
        /// method that called it for a full frame after it returns.
        /// </summary>
        protected virtual bool m_SetupIsACoroutine => true;

        /// <summary>
        /// If this is false, TearDown will call OnInlineTearDown instead of OnTearDown.
        /// This is a performance advantage when not using the coroutine functionality, as a coroutine that
        /// has no yield instructions in it will nonetheless still result in delaying the continuation of the
        /// method that called it for a full frame after it returns.
        /// </summary>
        protected virtual bool m_TearDownIsACoroutine => true;

        /// <summary>
        /// Used to display the various integration test
        /// stages and can be used to log verbose information
        /// for troubleshooting an integration test.
        /// </summary>
        /// <param name="msg"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void VerboseDebug(string msg)
        {
            if (m_EnableVerboseDebug)
            {
                Debug.Log(msg);
            }
        }

        /// <summary>
        /// Override this and return true if you need
        /// to troubleshoot a hard to track bug within an
        /// integration test.
        /// </summary>
        protected virtual bool OnSetVerboseDebug()
        {
            return false;
        }

        /// <summary>
        /// The very first thing invoked during the <see cref="OneTimeSetup"/> that
        /// determines how this integration test handles NetworkManager instantiation
        /// and destruction.  <see cref="NetworkManagerInstatiationMode"/>
        /// Override this method to change the default mode:
        /// <see cref="NetworkManagerInstatiationMode.AllTests"/>
        /// </summary>
        protected virtual NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            return NetworkManagerInstatiationMode.PerTest;
        }

        protected virtual void OnOneTimeSetup()
        {
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Application.runInBackground = true;
            m_NumberOfClients = NumberOfClients;
            IsRunning = true;
            m_EnableVerboseDebug = OnSetVerboseDebug();
            IntegrationTestSceneHandler.VerboseDebugMode = m_EnableVerboseDebug;
            VerboseDebug($"Entering {nameof(OneTimeSetup)}");

            m_NetworkManagerInstatiationMode = OnSetIntegrationTestMode();

            // Enable NetcodeIntegrationTest auto-label feature
            NetcodeIntegrationTestHelpers.RegisterNetcodeIntegrationTest(true);
            OnOneTimeSetup();

            VerboseDebug($"Exiting {nameof(OneTimeSetup)}");
        }

        /// <summary>
        /// Called before creating and starting the server and clients
        /// Note: For <see cref="NetworkManagerInstatiationMode.AllTests"/> and
        /// <see cref="NetworkManagerInstatiationMode.PerTest"/> mode integration tests.
        /// For those two modes, if you want to have access to the server or client
        /// <see cref="NetworkManager"/>s then override <see cref="OnServerAndClientsCreated"/>.
        /// <see cref="m_ServerNetworkManager"/> and <see cref="m_ClientNetworkManagers"/>
        /// </summary>
        protected virtual IEnumerator OnSetup()
        {
            yield return null;
        }

        /// <summary>
        /// Called before creating and starting the server and clients
        /// Note: For <see cref="NetworkManagerInstatiationMode.AllTests"/> and
        /// <see cref="NetworkManagerInstatiationMode.PerTest"/> mode integration tests.
        /// For those two modes, if you want to have access to the server or client
        /// <see cref="NetworkManager"/>s then override <see cref="OnServerAndClientsCreated"/>.
        /// <see cref="m_ServerNetworkManager"/> and <see cref="m_ClientNetworkManagers"/>
        /// </summary>
        protected virtual void OnInlineSetup()
        {
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            VerboseDebug($"Entering {nameof(SetUp)}");
            NetcodeLogAssert = new NetcodeLogAssert();
            if (m_EnableTimeTravel)
            {
                if (m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.AllTests)
                {
                    MockTransport.ClearQueues();
                }
                else
                {
                    MockTransport.Reset();
                }
                // Setup the frames per tick for time travel advance to next tick
                ConfigureFramesPerTick();
            }
            if (m_SetupIsACoroutine)
            {
                yield return OnSetup();
            }
            else
            {
                OnInlineSetup();
            }

            if (m_EnableTimeTravel)
            {
                MockTimeProvider.Reset();
                ComponentFactory.Register<IRealTimeProvider>(manager => new MockTimeProvider());
            }

            if (m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.AllTests && m_ServerNetworkManager == null ||
                m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.PerTest)
            {
                CreateServerAndClients();

                if (m_EnableTimeTravel)
                {
                    StartServerAndClientsWithTimeTravel();
                }
                else
                {
                    yield return StartServerAndClients();
                }
            }

            VerboseDebug($"Exiting {nameof(SetUp)}");
        }

        /// <summary>
        /// Override this to add components or adjustments to the default player prefab
        /// <see cref="m_PlayerPrefab"/>
        /// </summary>
        protected virtual void OnCreatePlayerPrefab()
        {
        }

        /// <summary>
        /// Invoked immediately after the player prefab GameObject is created
        /// prior to adding a NetworkObject component
        /// </summary>
        protected virtual void OnPlayerPrefabGameObjectCreated()
        {
        }

        private void CreatePlayerPrefab()
        {
            VerboseDebug($"Entering {nameof(CreatePlayerPrefab)}");
            // Create playerPrefab
            m_PlayerPrefab = new GameObject("Player");
            OnPlayerPrefabGameObjectCreated();
            NetworkObject networkObject = m_PlayerPrefab.AddComponent<NetworkObject>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            OnCreatePlayerPrefab();

            VerboseDebug($"Exiting {nameof(CreatePlayerPrefab)}");
        }

        /// <summary>
        /// This is invoked before the server and client(s) are started.
        /// Override this method if you want to make any adjustments to their
        /// NetworkManager instances.
        /// </summary>
        protected virtual void OnServerAndClientsCreated()
        {
        }

        /// <summary>
        /// Will create <see cref="NumberOfClients"/> number of clients.
        /// To create a specific number of clients <see cref="CreateServerAndClients(int)"/>
        /// </summary>
        protected void CreateServerAndClients()
        {
            CreateServerAndClients(NumberOfClients);
        }

        private void AddRemoveNetworkManager(NetworkManager networkManager, bool addNetworkManager)
        {
            var clientNetworkManagersList = new List<NetworkManager>(m_ClientNetworkManagers);
            if (addNetworkManager)
            {
                clientNetworkManagersList.Add(networkManager);
            }
            else
            {
                clientNetworkManagersList.Remove(networkManager);
            }

            m_ClientNetworkManagers = clientNetworkManagersList.ToArray();
            m_NumberOfClients = clientNetworkManagersList.Count;
        }

        /// <summary>
        /// CreateAndStartNewClient Only
        /// Invoked when the newly created client has been created
        /// </summary>
        protected virtual void OnNewClientCreated(NetworkManager networkManager)
        {
        }

        /// <summary>
        /// CreateAndStartNewClient Only
        /// Invoked when the newly created client has been created and started
        /// </summary>
        protected virtual void OnNewClientStarted(NetworkManager networkManager)
        {
        }

        /// <summary>
        /// CreateAndStartNewClient Only
        /// Invoked when the newly created client has been created, started, and connected
        /// to the server-host.
        /// </summary>
        protected virtual void OnNewClientStartedAndConnected(NetworkManager networkManager)
        {
        }

        /// <summary>
        /// CreateAndStartNewClient Only
        /// Override this method to bypass the waiting for a client to connect.
        /// </summary>
        /// <remarks>
        /// Use this for testing connection and disconnection scenarios
        /// </remarks>
        protected virtual bool ShouldWaitForNewClientToConnect(NetworkManager networkManager)
        {
            return true;
        }

        /// <summary>
        /// This will create, start, and connect a new client while in the middle of an
        /// integration test.
        /// </summary>
        protected IEnumerator CreateAndStartNewClient()
        {
            var networkManager = NetcodeIntegrationTestHelpers.CreateNewClient(m_ClientNetworkManagers.Length, m_EnableTimeTravel);
            networkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;

            // Notification that the new client (NetworkManager) has been created
            // in the event any modifications need to be made before starting the client
            OnNewClientCreated(networkManager);

            NetcodeIntegrationTestHelpers.StartOneClient(networkManager);

            if (LogAllMessages)
            {
                networkManager.ConnectionManager.MessageManager.Hook(new DebugNetworkHooks());
            }

            AddRemoveNetworkManager(networkManager, true);

            OnNewClientStarted(networkManager);

            if (ShouldWaitForNewClientToConnect(networkManager))
            {
                // Wait for the new client to connect
                yield return WaitForClientsConnectedOrTimeOut();

                OnNewClientStartedAndConnected(networkManager);
                if (s_GlobalTimeoutHelper.TimedOut)
                {
                    AddRemoveNetworkManager(networkManager, false);
                    Object.DestroyImmediate(networkManager.gameObject);
                }

                AssertOnTimeout($"{nameof(CreateAndStartNewClient)} timed out waiting for the new client to be connected!");
                ClientNetworkManagerPostStart(networkManager);
                VerboseDebug($"[{networkManager.name}] Created and connected!");
            }
        }

        /// <summary>
        /// This will create, start, and connect a new client while in the middle of an
        /// integration test.
        /// </summary>
        protected void CreateAndStartNewClientWithTimeTravel()
        {
            var networkManager = NetcodeIntegrationTestHelpers.CreateNewClient(m_ClientNetworkManagers.Length, m_EnableTimeTravel);
            networkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;

            // Notification that the new client (NetworkManager) has been created
            // in the event any modifications need to be made before starting the client
            OnNewClientCreated(networkManager);

            NetcodeIntegrationTestHelpers.StartOneClient(networkManager);

            if (LogAllMessages)
            {
                networkManager.ConnectionManager.MessageManager.Hook(new DebugNetworkHooks());
            }

            AddRemoveNetworkManager(networkManager, true);

            OnNewClientStarted(networkManager);

            // Wait for the new client to connect
            var connected = WaitForClientsConnectedOrTimeOutWithTimeTravel();

            OnNewClientStartedAndConnected(networkManager);
            if (!connected)
            {
                AddRemoveNetworkManager(networkManager, false);
                Object.DestroyImmediate(networkManager.gameObject);
            }

            Assert.IsTrue(connected, $"{nameof(CreateAndStartNewClient)} timed out waiting for the new client to be connected!");
            ClientNetworkManagerPostStart(networkManager);
            VerboseDebug($"[{networkManager.name}] Created and connected!");
        }

        /// <summary>
        /// This will stop a client while in the middle of an integration test
        /// </summary>
        protected IEnumerator StopOneClient(NetworkManager networkManager, bool destroy = false)
        {
            NetcodeIntegrationTestHelpers.StopOneClient(networkManager, destroy);
            AddRemoveNetworkManager(networkManager, false);
            yield return WaitForConditionOrTimeOut(() => !networkManager.IsConnectedClient);
        }

        /// <summary>
        /// This will stop a client while in the middle of an integration test
        /// </summary>
        protected void StopOneClientWithTimeTravel(NetworkManager networkManager, bool destroy = false)
        {
            NetcodeIntegrationTestHelpers.StopOneClient(networkManager, destroy);
            AddRemoveNetworkManager(networkManager, false);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(() => !networkManager.IsConnectedClient));
        }

        protected void SetTimeTravelSimulatedLatency(float latencySeconds)
        {
            ((MockTransport)m_ServerNetworkManager.NetworkConfig.NetworkTransport).SimulatedLatencySeconds = latencySeconds;
            foreach (var client in m_ClientNetworkManagers)
            {
                ((MockTransport)client.NetworkConfig.NetworkTransport).SimulatedLatencySeconds = latencySeconds;
            }
        }

        protected void SetTimeTravelSimulatedDropRate(float dropRatePercent)
        {
            ((MockTransport)m_ServerNetworkManager.NetworkConfig.NetworkTransport).PacketDropRate = dropRatePercent;
            foreach (var client in m_ClientNetworkManagers)
            {
                ((MockTransport)client.NetworkConfig.NetworkTransport).PacketDropRate = dropRatePercent;
            }
        }

        protected void SetTimeTravelSimulatedLatencyJitter(float jitterSeconds)
        {
            ((MockTransport)m_ServerNetworkManager.NetworkConfig.NetworkTransport).LatencyJitter = jitterSeconds;
            foreach (var client in m_ClientNetworkManagers)
            {
                ((MockTransport)client.NetworkConfig.NetworkTransport).LatencyJitter = jitterSeconds;
            }
        }

        /// <summary>
        /// Creates the server and clients
        /// </summary>
        /// <param name="numberOfClients"></param>
        protected void CreateServerAndClients(int numberOfClients)
        {
            VerboseDebug($"Entering {nameof(CreateServerAndClients)}");

            CreatePlayerPrefab();

            if (m_EnableTimeTravel)
            {
                m_TargetFrameRate = -1;
            }

            // Create multiple NetworkManager instances
            if (!NetcodeIntegrationTestHelpers.Create(numberOfClients, out NetworkManager server, out NetworkManager[] clients, m_TargetFrameRate, m_CreateServerFirst, m_EnableTimeTravel))
            {
                Debug.LogError("Failed to create instances");
                Assert.Fail("Failed to create instances");
            }

            m_ClientNetworkManagers = clients;
            m_ServerNetworkManager = server;

            if (m_ServerNetworkManager != null)
            {
                s_DefaultWaitForTick = new WaitForSecondsRealtime(1.0f / m_ServerNetworkManager.NetworkConfig.TickRate);
            }

            // Set the player prefab for the server and clients
            m_ServerNetworkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            }

            // Provides opportunity to allow child derived classes to
            // modify the NetworkManager's configuration before starting.
            OnServerAndClientsCreated();

            VerboseDebug($"Exiting {nameof(CreateServerAndClients)}");
        }

        /// <summary>
        /// Override this method and return false in order to be able
        /// to manually control when the server and clients are started.
        /// </summary>
        protected virtual bool CanStartServerAndClients()
        {
            return true;
        }

        /// <summary>
        /// Invoked after the server and clients have started.
        /// Note: No connection verification has been done at this point
        /// </summary>
        protected virtual IEnumerator OnStartedServerAndClients()
        {
            yield return null;
        }

        /// <summary>
        /// Invoked after the server and clients have started.
        /// Note: No connection verification has been done at this point
        /// </summary>
        protected virtual void OnTimeTravelStartedServerAndClients()
        {
        }

        /// <summary>
        /// Invoked after the server and clients have started and verified
        /// their connections with each other.
        /// </summary>
        protected virtual IEnumerator OnServerAndClientsConnected()
        {
            yield return null;
        }

        /// <summary>
        /// Invoked after the server and clients have started and verified
        /// their connections with each other.
        /// </summary>
        protected virtual void OnTimeTravelServerAndClientsConnected()
        {
        }

        private void ClientNetworkManagerPostStart(NetworkManager networkManager)
        {
            networkManager.name = $"NetworkManager - Client - {networkManager.LocalClientId}";
            Assert.NotNull(networkManager.LocalClient.PlayerObject, $"{nameof(StartServerAndClients)} detected that client {networkManager.LocalClientId} does not have an assigned player NetworkObject!");

            // Go ahead and create an entry for this new client
            if (!m_PlayerNetworkObjects.ContainsKey(networkManager.LocalClientId))
            {
                m_PlayerNetworkObjects.Add(networkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
            }

#if UNITY_2023_1_OR_NEWER
            // Get all player instances for the current client NetworkManager instance
            var clientPlayerClones = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where((c) => c.IsPlayerObject && c.OwnerClientId == networkManager.LocalClientId).ToList();
#else
            // Get all player instances for the current client NetworkManager instance
            var clientPlayerClones = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsPlayerObject && c.OwnerClientId == networkManager.LocalClientId).ToList();
#endif
            // Add this player instance to each client player entry
            foreach (var playerNetworkObject in clientPlayerClones)
            {
                // When the server is not the host this needs to be done
                if (!m_PlayerNetworkObjects.ContainsKey(playerNetworkObject.NetworkManager.LocalClientId))
                {
                    m_PlayerNetworkObjects.Add(playerNetworkObject.NetworkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
                }

                if (!m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].ContainsKey(networkManager.LocalClientId))
                {
                    m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].Add(networkManager.LocalClientId, playerNetworkObject);
                }
            }
#if UNITY_2023_1_OR_NEWER
            // For late joining clients, add the remaining (if any) cloned versions of each client's player
            clientPlayerClones = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where((c) => c.IsPlayerObject && c.NetworkManager == networkManager).ToList();
#else
            // For late joining clients, add the remaining (if any) cloned versions of each client's player
            clientPlayerClones = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsPlayerObject && c.NetworkManager == networkManager).ToList();
#endif
            foreach (var playerNetworkObject in clientPlayerClones)
            {
                if (!m_PlayerNetworkObjects[networkManager.LocalClientId].ContainsKey(playerNetworkObject.OwnerClientId))
                {
                    m_PlayerNetworkObjects[networkManager.LocalClientId].Add(playerNetworkObject.OwnerClientId, playerNetworkObject);
                }
            }
        }

        protected void ClientNetworkManagerPostStartInit()
        {
            // Creates a dictionary for all player instances client and server relative
            // This provides a simpler way to get a specific player instance relative to a client instance
            foreach (var networkManager in m_ClientNetworkManagers)
            {
                ClientNetworkManagerPostStart(networkManager);
            }

            if (m_UseHost)
            {
#if UNITY_2023_1_OR_NEWER
                var clientSideServerPlayerClones = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#else
                var clientSideServerPlayerClones = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#endif
                foreach (var playerNetworkObject in clientSideServerPlayerClones)
                {
                    // When the server is not the host this needs to be done
                    if (!m_PlayerNetworkObjects.ContainsKey(playerNetworkObject.NetworkManager.LocalClientId))
                    {
                        m_PlayerNetworkObjects.Add(playerNetworkObject.NetworkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
                    }

                    if (!m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].ContainsKey(m_ServerNetworkManager.LocalClientId))
                    {
                        m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].Add(m_ServerNetworkManager.LocalClientId, playerNetworkObject);
                    }
                }
            }
        }

        protected virtual bool LogAllMessages => false;

        protected virtual bool ShouldCheckForSpawnedPlayers()
        {
            return true;
        }

        /// <summary>
        /// This starts the server and clients as long as <see cref="CanStartServerAndClients"/>
        /// returns true.
        /// </summary>
        protected IEnumerator StartServerAndClients()
        {
            if (CanStartServerAndClients())
            {
                VerboseDebug($"Entering {nameof(StartServerAndClients)}");

                // Start the instances and pass in our SceneManagerInitialization action that is invoked immediately after host-server
                // is started and after each client is started.
                if (!NetcodeIntegrationTestHelpers.Start(m_UseHost, m_ServerNetworkManager, m_ClientNetworkManagers))
                {
                    Debug.LogError("Failed to start instances");
                    Assert.Fail("Failed to start instances");
                }

                // When scene management is enabled, we need to re-apply the scenes populated list since we have overriden the ISceneManagerHandler
                // imeplementation at this point. This assures any pre-loaded scenes will be automatically assigned to the server and force clients 
                // to load their own scenes.
                if (m_ServerNetworkManager.NetworkConfig.EnableSceneManagement)
                {
                    var scenesLoaded = m_ServerNetworkManager.SceneManager.ScenesLoaded;
                    m_ServerNetworkManager.SceneManager.SceneManagerHandler.PopulateLoadedScenes(ref scenesLoaded, m_ServerNetworkManager);
                }


                if (LogAllMessages)
                {
                    EnableMessageLogging();
                }

                RegisterSceneManagerHandler();

                // Notification that the server and clients have been started
                yield return OnStartedServerAndClients();

                // When true, we skip everything else (most likely a connection oriented test)
                if (!m_BypassConnectionTimeout)
                {
                    // Wait for all clients to connect
                    yield return WaitForClientsConnectedOrTimeOut();

                    AssertOnTimeout($"{nameof(StartServerAndClients)} timed out waiting for all clients to be connected!");

                    if (m_UseHost || m_ServerNetworkManager.IsHost)
                    {
#if UNITY_2023_1_OR_NEWER
                        // Add the server player instance to all m_ClientSidePlayerNetworkObjects entries
                        var serverPlayerClones = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#else
                        // Add the server player instance to all m_ClientSidePlayerNetworkObjects entries
                        var serverPlayerClones = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#endif
                        foreach (var playerNetworkObject in serverPlayerClones)
                        {
                            if (!m_PlayerNetworkObjects.ContainsKey(playerNetworkObject.NetworkManager.LocalClientId))
                            {
                                m_PlayerNetworkObjects.Add(playerNetworkObject.NetworkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
                            }

                            m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].Add(m_ServerNetworkManager.LocalClientId, playerNetworkObject);
                        }
                    }

                    if (ShouldCheckForSpawnedPlayers())
                    {
                        ClientNetworkManagerPostStartInit();
                    }

                    // Notification that at this time the server and client(s) are instantiated,
                    // started, and connected on both sides.
                    yield return OnServerAndClientsConnected();

                    VerboseDebug($"Exiting {nameof(StartServerAndClients)}");
                }
            }
        }

        /// <summary>
        /// This starts the server and clients as long as <see cref="CanStartServerAndClients"/>
        /// returns true.
        /// </summary>
        protected void StartServerAndClientsWithTimeTravel()
        {
            if (CanStartServerAndClients())
            {
                VerboseDebug($"Entering {nameof(StartServerAndClientsWithTimeTravel)}");

                // Start the instances and pass in our SceneManagerInitialization action that is invoked immediately after host-server
                // is started and after each client is started.
                if (!NetcodeIntegrationTestHelpers.Start(m_UseHost, m_ServerNetworkManager, m_ClientNetworkManagers))
                {
                    Debug.LogError("Failed to start instances");
                    Assert.Fail("Failed to start instances");
                }

                // Time travel does not play nice with scene loading, clear out server side pre-loaded scenes.
                if (m_ServerNetworkManager.NetworkConfig.EnableSceneManagement)
                {
                    m_ServerNetworkManager.SceneManager.ScenesLoaded.Clear();
                }

                if (LogAllMessages)
                {
                    EnableMessageLogging();
                }

                RegisterSceneManagerHandler();

                // Notification that the server and clients have been started
                OnTimeTravelStartedServerAndClients();

                // When true, we skip everything else (most likely a connection oriented test)
                if (!m_BypassConnectionTimeout)
                {
                    // Wait for all clients to connect
                    WaitForClientsConnectedOrTimeOutWithTimeTravel();

                    AssertOnTimeout($"{nameof(StartServerAndClients)} timed out waiting for all clients to be connected!");

                    if (m_UseHost || m_ServerNetworkManager.IsHost)
                    {
#if UNITY_2023_1_OR_NEWER
                        // Add the server player instance to all m_ClientSidePlayerNetworkObjects entries
                        var serverPlayerClones = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#else
                        // Add the server player instance to all m_ClientSidePlayerNetworkObjects entries
                        var serverPlayerClones = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsPlayerObject && c.OwnerClientId == m_ServerNetworkManager.LocalClientId);
#endif
                        foreach (var playerNetworkObject in serverPlayerClones)
                        {
                            if (!m_PlayerNetworkObjects.ContainsKey(playerNetworkObject.NetworkManager.LocalClientId))
                            {
                                m_PlayerNetworkObjects.Add(playerNetworkObject.NetworkManager.LocalClientId, new Dictionary<ulong, NetworkObject>());
                            }

                            m_PlayerNetworkObjects[playerNetworkObject.NetworkManager.LocalClientId].Add(m_ServerNetworkManager.LocalClientId, playerNetworkObject);
                        }
                    }

                    if (ShouldCheckForSpawnedPlayers())
                    {
                        ClientNetworkManagerPostStartInit();
                    }

                    // Notification that at this time the server and client(s) are instantiated,
                    // started, and connected on both sides.
                    OnTimeTravelServerAndClientsConnected();

                    VerboseDebug($"Exiting {nameof(StartServerAndClients)}");
                }
            }
        }

        /// <summary>
        /// Override this method to control when clients
        /// can fake-load a scene.
        /// </summary>
        protected virtual bool CanClientsLoad()
        {
            return true;
        }

        /// <summary>
        /// Override this method to control when clients
        /// can fake-unload a scene.
        /// </summary>
        protected virtual bool CanClientsUnload()
        {
            return true;
        }

        /// <summary>
        /// De-Registers from the CanClientsLoad and CanClientsUnload events of the
        /// ClientSceneHandler (default is IntegrationTestSceneHandler).
        /// </summary>
        protected void DeRegisterSceneManagerHandler()
        {
            IntegrationTestSceneHandler.CanClientsLoad -= ClientSceneHandler_CanClientsLoad;
            IntegrationTestSceneHandler.CanClientsUnload -= ClientSceneHandler_CanClientsUnload;
            IntegrationTestSceneHandler.NetworkManagers.Clear();
        }

        /// <summary>
        /// Registers the CanClientsLoad and CanClientsUnload events of the
        /// ClientSceneHandler.
        /// The default is: <see cref="IntegrationTestSceneHandler"/>.
        /// </summary>
        protected void RegisterSceneManagerHandler()
        {
            IntegrationTestSceneHandler.CanClientsLoad += ClientSceneHandler_CanClientsLoad;
            IntegrationTestSceneHandler.CanClientsUnload += ClientSceneHandler_CanClientsUnload;
        }

        private bool ClientSceneHandler_CanClientsUnload()
        {
            return CanClientsUnload();
        }

        private bool ClientSceneHandler_CanClientsLoad()
        {
            return CanClientsLoad();
        }

        protected bool OnCanSceneCleanUpUnload(Scene scene)
        {
            return true;
        }

        /// <summary>
        /// This shuts down all NetworkManager instances registered via the
        /// <see cref="NetcodeIntegrationTestHelpers"/> class and cleans up
        /// the test runner scene of any left over NetworkObjects.
        /// <see cref="DestroySceneNetworkObjects"/>
        /// </summary>
        protected void ShutdownAndCleanUp()
        {
            VerboseDebug($"Entering {nameof(ShutdownAndCleanUp)}");
            // Shutdown and clean up both of our NetworkManager instances
            try
            {
                DeRegisterSceneManagerHandler();

                NetcodeIntegrationTestHelpers.Destroy();

                m_PlayerNetworkObjects.Clear();
                s_GlobalNetworkObjects.Clear();
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (m_PlayerPrefab != null)
                {
                    Object.DestroyImmediate(m_PlayerPrefab);
                    m_PlayerPrefab = null;
                }
            }

            // Cleanup any remaining NetworkObjects
            DestroySceneNetworkObjects();

            UnloadRemainingScenes();

            // reset the m_ServerWaitForTick for the next test to initialize
            s_DefaultWaitForTick = new WaitForSecondsRealtime(1.0f / k_DefaultTickRate);
            VerboseDebug($"Exiting {nameof(ShutdownAndCleanUp)}");
        }

        /// <summary>
        /// Note: For <see cref="NetworkManagerInstatiationMode.PerTest"/> mode
        /// this is called before ShutdownAndCleanUp.
        /// </summary>
        protected virtual IEnumerator OnTearDown()
        {
            yield return null;
        }

        protected virtual void OnInlineTearDown()
        {
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            IntegrationTestSceneHandler.SceneNameToSceneHandles.Clear();
            VerboseDebug($"Entering {nameof(TearDown)}");
            if (m_TearDownIsACoroutine)
            {
                yield return OnTearDown();
            }
            else
            {
                OnInlineTearDown();
            }

            if (m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.PerTest)
            {
                ShutdownAndCleanUp();
            }

            if (m_EnableTimeTravel)
            {
                ComponentFactory.Deregister<IRealTimeProvider>();
            }

            VerboseDebug($"Exiting {nameof(TearDown)}");
            LogWaitForMessages();
            NetcodeLogAssert.Dispose();
            if (m_EnableTimeTravel)
            {
                if (m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.AllTests)
                {
                    MockTransport.ClearQueues();
                }
                else
                {
                    MockTransport.Reset();
                }
            }
        }

        /// <summary>
        /// Override this method to do handle cleaning up once the test(s)
        /// within the child derived class have completed
        /// Note: For <see cref="NetworkManagerInstatiationMode.AllTests"/> mode
        /// this is called before ShutdownAndCleanUp.
        /// </summary>
        protected virtual void OnOneTimeTearDown()
        {
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            IntegrationTestSceneHandler.VerboseDebugMode = false;
            VerboseDebug($"Entering {nameof(OneTimeTearDown)}");
            OnOneTimeTearDown();

            if (m_NetworkManagerInstatiationMode == NetworkManagerInstatiationMode.AllTests)
            {
                ShutdownAndCleanUp();
            }

            // Disable NetcodeIntegrationTest auto-label feature
            NetcodeIntegrationTestHelpers.RegisterNetcodeIntegrationTest(false);

            UnloadRemainingScenes();

            VerboseDebug($"Exiting {nameof(OneTimeTearDown)}");

            IsRunning = false;
        }

        /// <summary>
        /// Override this to filter out the <see cref="NetworkObject"/>s that you
        /// want to allow to persist between integration tests.
        /// <see cref="DestroySceneNetworkObjects"/>
        /// <see cref="ShutdownAndCleanUp"/>
        /// </summary>
        /// <param name="networkObject">the network object in question to be destroyed</param>
        protected virtual bool CanDestroyNetworkObject(NetworkObject networkObject)
        {
            return true;
        }

        /// <summary>
        /// Destroys all NetworkObjects at the end of a test cycle.
        /// </summary>
        protected void DestroySceneNetworkObjects()
        {
#if UNITY_2023_1_OR_NEWER
            var networkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID);
#else
            var networkObjects = Object.FindObjectsOfType<NetworkObject>();
#endif
            foreach (var networkObject in networkObjects)
            {
                // This can sometimes be null depending upon order of operations
                // when dealing with parented NetworkObjects.  If NetworkObjectB
                // is a child of NetworkObjectA and NetworkObjectA comes before
                // NetworkObjectB in the list of NeworkObjects found, then when
                // NetworkObjectA's GameObject is destroyed it will also destroy
                // NetworkObjectB's GameObject which will destroy NetworkObjectB.
                // If there is a null entry in the list, this is the most likely
                // scenario and so we just skip over it.
                if (networkObject == null)
                {
                    continue;
                }

                if (CanDestroyNetworkObject(networkObject))
                {
                    networkObject.NetworkManagerOwner = m_ServerNetworkManager;
                    // Destroy the GameObject that holds the NetworkObject component
                    Object.DestroyImmediate(networkObject.gameObject);
                }
            }
        }

        /// <summary>
        /// For debugging purposes, this will turn on verbose logging of all messages and batches sent and received
        /// </summary>
        protected void EnableMessageLogging()
        {
            m_ServerNetworkManager.ConnectionManager.MessageManager.Hook(new DebugNetworkHooks());
            foreach (var client in m_ClientNetworkManagers)
            {
                client.ConnectionManager.MessageManager.Hook(new DebugNetworkHooks());
            }
        }

        /// <summary>
        /// Waits for the function condition to return true or it will time out.
        /// This will operate at the current m_ServerNetworkManager.NetworkConfig.TickRate
        /// and allow for a unique TimeoutHelper handler (if none then it uses the default)
        /// Notes: This provides more stability when running integration tests that could be
        /// impacted by:
        ///     -how the integration test is being executed (i.e. in editor or in a stand alone build)
        ///     -potential platform performance issues (i.e. VM is throttled or maxed)
        /// Note: For more complex tests, <see cref="ConditionalPredicateBase"/> and the overloaded
        /// version of this method
        /// </summary>
        public static IEnumerator WaitForConditionOrTimeOut(Func<bool> checkForCondition, TimeoutHelper timeOutHelper = null)
        {
            if (checkForCondition == null)
            {
                throw new ArgumentNullException($"checkForCondition cannot be null!");
            }

            // If none is provided we use the default global time out helper
            if (timeOutHelper == null)
            {
                timeOutHelper = s_GlobalTimeoutHelper;
            }

            // Start checking for a timeout
            timeOutHelper.Start();
            while (!timeOutHelper.HasTimedOut())
            {
                // Update and check to see if the condition has been met
                if (checkForCondition.Invoke())
                {
                    break;
                }

                // Otherwise wait for 1 tick interval
                yield return s_DefaultWaitForTick;
            }

            // Stop checking for a timeout
            timeOutHelper.Stop();
        }


        /// <summary>
        /// Waits for the function condition to return true or it will time out. Uses time travel to simulate this
        /// for the given number of frames, simulating delta times at the application frame rate.
        /// </summary>
        public bool WaitForConditionOrTimeOutWithTimeTravel(Func<bool> checkForCondition, int maxTries = 60)
        {
            if (checkForCondition == null)
            {
                throw new ArgumentNullException($"checkForCondition cannot be null!");
            }

            if (!m_EnableTimeTravel)
            {
                throw new ArgumentException($"Time travel must be enabled to use {nameof(WaitForConditionOrTimeOutWithTimeTravel)}!");
            }

            var frameRate = Application.targetFrameRate;
            if (frameRate <= 0)
            {
                frameRate = 60;
            }

            var updateInterval = 1f / frameRate;
            for (var i = 0; i < maxTries; ++i)
            {
                // Simulate a frame passing on all network managers
                TimeTravel(updateInterval, 1);
                // Update and check to see if the condition has been met
                if (checkForCondition.Invoke())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This version accepts an IConditionalPredicate implementation to provide
        /// more flexibility for checking complex conditional cases.
        /// </summary>
        public static IEnumerator WaitForConditionOrTimeOut(IConditionalPredicate conditionalPredicate, TimeoutHelper timeOutHelper = null)
        {
            if (conditionalPredicate == null)
            {
                throw new ArgumentNullException($"checkForCondition cannot be null!");
            }

            // If none is provided we use the default global time out helper
            if (timeOutHelper == null)
            {
                timeOutHelper = s_GlobalTimeoutHelper;
            }

            conditionalPredicate.Started();
            yield return WaitForConditionOrTimeOut(conditionalPredicate.HasConditionBeenReached, timeOutHelper);
            conditionalPredicate.Finished(timeOutHelper.TimedOut);
        }

        /// <summary>
        /// This version accepts an IConditionalPredicate implementation to provide
        /// more flexibility for checking complex conditional cases. Uses time travel to simulate this
        /// for the given number of frames, simulating delta times at the application frame rate.
        /// </summary>
        public bool WaitForConditionOrTimeOutWithTimeTravel(IConditionalPredicate conditionalPredicate, int maxTries = 60)
        {
            if (conditionalPredicate == null)
            {
                throw new ArgumentNullException($"checkForCondition cannot be null!");
            }

            if (!m_EnableTimeTravel)
            {
                throw new ArgumentException($"Time travel must be enabled to use {nameof(WaitForConditionOrTimeOutWithTimeTravel)}!");
            }

            conditionalPredicate.Started();
            var success = WaitForConditionOrTimeOutWithTimeTravel(conditionalPredicate.HasConditionBeenReached, maxTries);
            conditionalPredicate.Finished(!success);
            return success;
        }

        /// <summary>
        /// Validates that all remote clients (i.e. non-server) detect they are connected
        /// to the server and that the server reflects the appropriate number of clients
        /// have connected or it will time out.
        /// </summary>
        /// <param name="clientsToCheck">An array of clients to be checked</param>
        protected IEnumerator WaitForClientsConnectedOrTimeOut(NetworkManager[] clientsToCheck)
        {
            var remoteClientCount = clientsToCheck.Length;
            var serverClientCount = m_ServerNetworkManager.IsHost ? remoteClientCount + 1 : remoteClientCount;

            yield return WaitForConditionOrTimeOut(() => clientsToCheck.Where((c) => c.IsConnectedClient).Count() == remoteClientCount &&
                                                         m_ServerNetworkManager.ConnectedClients.Count == serverClientCount);
        }

        /// <summary>
        /// Validates that all remote clients (i.e. non-server) detect they are connected
        /// to the server and that the server reflects the appropriate number of clients
        /// have connected or it will time out. Uses time travel to simulate this
        /// for the given number of frames, simulating delta times at the application frame rate.
        /// </summary>
        /// <param name="clientsToCheck">An array of clients to be checked</param>
        protected bool WaitForClientsConnectedOrTimeOutWithTimeTravel(NetworkManager[] clientsToCheck)
        {
            var remoteClientCount = clientsToCheck.Length;
            var serverClientCount = m_ServerNetworkManager.IsHost ? remoteClientCount + 1 : remoteClientCount;

            return WaitForConditionOrTimeOutWithTimeTravel(() => clientsToCheck.Where((c) => c.IsConnectedClient).Count() == remoteClientCount &&
                                                                 m_ServerNetworkManager.ConnectedClients.Count == serverClientCount);
        }

        /// <summary>
        /// Overloaded method that just passes in all clients to
        /// <see cref="WaitForClientsConnectedOrTimeOut(NetworkManager[])"/>
        /// </summary>
        protected IEnumerator WaitForClientsConnectedOrTimeOut()
        {
            yield return WaitForClientsConnectedOrTimeOut(m_ClientNetworkManagers);
        }

        /// <summary>
        /// Overloaded method that just passes in all clients to
        /// <see cref="WaitForClientsConnectedOrTimeOut(NetworkManager[])"/> Uses time travel to simulate this
        /// for the given number of frames, simulating delta times at the application frame rate.
        /// </summary>
        protected bool WaitForClientsConnectedOrTimeOutWithTimeTravel()
        {
            return WaitForClientsConnectedOrTimeOutWithTimeTravel(m_ClientNetworkManagers);
        }

        internal IEnumerator WaitForMessageReceived<T>(List<NetworkManager> wiatForReceivedBy, ReceiptType type = ReceiptType.Handled) where T : INetworkMessage
        {
            // Build our message hook entries tables so we can determine if all clients received spawn or ownership messages
            var messageHookEntriesForSpawn = new List<MessageHookEntry>();
            foreach (var clientNetworkManager in wiatForReceivedBy)
            {
                var messageHook = new MessageHookEntry(clientNetworkManager, type);
                messageHook.AssignMessageType<T>();
                messageHookEntriesForSpawn.Add(messageHook);
            }

            // Used to determine if all clients received the CreateObjectMessage
            var hooks = new MessageHooksConditional(messageHookEntriesForSpawn);
            yield return WaitForConditionOrTimeOut(hooks);
            Assert.False(s_GlobalTimeoutHelper.TimedOut);
        }

        internal IEnumerator WaitForMessagesReceived(List<Type> messagesInOrder, List<NetworkManager> waitForReceivedBy, ReceiptType type = ReceiptType.Handled)
        {
            // Build our message hook entries tables so we can determine if all clients received spawn or ownership messages
            var messageHookEntriesForSpawn = new List<MessageHookEntry>();
            foreach (var clientNetworkManager in waitForReceivedBy)
            {
                foreach (var message in messagesInOrder)
                {
                    var messageHook = new MessageHookEntry(clientNetworkManager, type);
                    messageHook.AssignMessageType(message);
                    messageHookEntriesForSpawn.Add(messageHook);
                }
            }

            // Used to determine if all clients received the CreateObjectMessage
            var hooks = new MessageHooksConditional(messageHookEntriesForSpawn);
            yield return WaitForConditionOrTimeOut(hooks);
            Assert.False(s_GlobalTimeoutHelper.TimedOut);
        }


        internal void WaitForMessageReceivedWithTimeTravel<T>(List<NetworkManager> waitForReceivedBy, ReceiptType type = ReceiptType.Handled) where T : INetworkMessage
        {
            // Build our message hook entries tables so we can determine if all clients received spawn or ownership messages
            var messageHookEntriesForSpawn = new List<MessageHookEntry>();
            foreach (var clientNetworkManager in waitForReceivedBy)
            {
                var messageHook = new MessageHookEntry(clientNetworkManager, type);
                messageHook.AssignMessageType<T>();
                messageHookEntriesForSpawn.Add(messageHook);
            }

            // Used to determine if all clients received the CreateObjectMessage
            var hooks = new MessageHooksConditional(messageHookEntriesForSpawn);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(hooks));
        }

        internal void WaitForMessagesReceivedWithTimeTravel(List<Type> messagesInOrder, List<NetworkManager> waitForReceivedBy, ReceiptType type = ReceiptType.Handled)
        {
            // Build our message hook entries tables so we can determine if all clients received spawn or ownership messages
            var messageHookEntriesForSpawn = new List<MessageHookEntry>();
            foreach (var clientNetworkManager in waitForReceivedBy)
            {
                foreach (var message in messagesInOrder)
                {
                    var messageHook = new MessageHookEntry(clientNetworkManager, type);
                    messageHook.AssignMessageType(message);
                    messageHookEntriesForSpawn.Add(messageHook);
                }
            }

            // Used to determine if all clients received the CreateObjectMessage
            var hooks = new MessageHooksConditional(messageHookEntriesForSpawn);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(hooks));
        }

        /// <summary>
        /// Creates a basic NetworkObject test prefab, assigns it to a new
        /// NetworkPrefab entry, and then adds it to the server and client(s)
        /// NetworkManagers' NetworkConfig.NetworkPrefab lists.
        /// </summary>
        /// <param name="baseName">the basic name to be used for each instance</param>
        /// <returns>NetworkObject of the GameObject assigned to the new NetworkPrefab entry</returns>
        protected GameObject CreateNetworkObjectPrefab(string baseName)
        {
            var prefabCreateAssertError = $"You can only invoke this method during {nameof(OnServerAndClientsCreated)} " +
                                          $"but before {nameof(OnStartedServerAndClients)}!";
            Assert.IsNotNull(m_ServerNetworkManager, prefabCreateAssertError);
            Assert.IsFalse(m_ServerNetworkManager.IsListening, prefabCreateAssertError);

            return NetcodeIntegrationTestHelpers.CreateNetworkObjectPrefab(baseName, m_ServerNetworkManager, m_ClientNetworkManagers);
        }

        /// <summary>
        /// Overloaded method <see cref="SpawnObject(NetworkObject, NetworkManager, bool)"/>
        /// </summary>
        protected GameObject SpawnObject(GameObject prefabGameObject, NetworkManager owner, bool destroyWithScene = false)
        {
            var prefabNetworkObject = prefabGameObject.GetComponent<NetworkObject>();
            Assert.IsNotNull(prefabNetworkObject, $"{nameof(GameObject)} {prefabGameObject.name} does not have a {nameof(NetworkObject)} component!");
            return SpawnObject(prefabNetworkObject, owner, destroyWithScene);
        }

        /// <summary>
        /// Spawn a NetworkObject prefab instance
        /// </summary>
        /// <param name="prefabNetworkObject">the prefab NetworkObject to spawn</param>
        /// <param name="owner">the owner of the instance</param>
        /// <param name="destroyWithScene">default is false</param>
        /// <returns>GameObject instance spawned</returns>
        private GameObject SpawnObject(NetworkObject prefabNetworkObject, NetworkManager owner, bool destroyWithScene = false)
        {
            Assert.IsTrue(prefabNetworkObject.GlobalObjectIdHash > 0, $"{nameof(GameObject)} {prefabNetworkObject.name} has a {nameof(NetworkObject.GlobalObjectIdHash)} value of 0! Make sure to make it a valid prefab before trying to spawn!");
            var newInstance = Object.Instantiate(prefabNetworkObject.gameObject);
            var networkObjectToSpawn = newInstance.GetComponent<NetworkObject>();
            networkObjectToSpawn.NetworkManagerOwner = m_ServerNetworkManager; // Required to assure the server does the spawning
            if (owner == m_ServerNetworkManager)
            {
                if (m_UseHost)
                {
                    networkObjectToSpawn.SpawnWithOwnership(owner.LocalClientId, destroyWithScene);
                }
                else
                {
                    networkObjectToSpawn.Spawn(destroyWithScene);
                }
            }
            else
            {
                networkObjectToSpawn.SpawnWithOwnership(owner.LocalClientId, destroyWithScene);
            }

            return newInstance;
        }

        /// <summary>
        /// Overloaded method <see cref="SpawnObjects(NetworkObject, NetworkManager, int, bool)"/>
        /// </summary>
        protected List<GameObject> SpawnObjects(GameObject prefabGameObject, NetworkManager owner, int count, bool destroyWithScene = false)
        {
            var prefabNetworkObject = prefabGameObject.GetComponent<NetworkObject>();
            Assert.IsNotNull(prefabNetworkObject, $"{nameof(GameObject)} {prefabGameObject.name} does not have a {nameof(NetworkObject)} component!");
            return SpawnObjects(prefabNetworkObject, owner, count, destroyWithScene);
        }

        /// <summary>
        /// Will spawn (x) number of prefab NetworkObjects
        /// <see cref="SpawnObject(NetworkObject, NetworkManager, bool)"/>
        /// </summary>
        /// <param name="prefabNetworkObject">the prefab NetworkObject to spawn</param>
        /// <param name="owner">the owner of the instance</param>
        /// <param name="count">number of instances to create and spawn</param>
        /// <param name="destroyWithScene">default is false</param>
        private List<GameObject> SpawnObjects(NetworkObject prefabNetworkObject, NetworkManager owner, int count, bool destroyWithScene = false)
        {
            var gameObjectsSpawned = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                gameObjectsSpawned.Add(SpawnObject(prefabNetworkObject, owner, destroyWithScene));
            }

            return gameObjectsSpawned;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public NetcodeIntegrationTest()
        {
        }

        /// <summary>
        /// Optional Host or Server integration tests
        /// Constructor that allows you To break tests up as a host
        /// and a server.
        /// Example: Decorate your child derived class with TestFixture
        /// and then create a constructor at the child level.
        /// Don't forget to set your constructor public, else Unity will
        /// give you a hard to decipher error
        /// [TestFixture(HostOrServer.Host)]
        /// [TestFixture(HostOrServer.Server)]
        /// public class MyChildClass : NetcodeIntegrationTest
        /// {
        ///     public MyChildClass(HostOrServer hostOrServer) : base(hostOrServer) { }
        /// }
        /// </summary>
        /// <param name="hostOrServer"></param>
        public NetcodeIntegrationTest(HostOrServer hostOrServer)
        {
            m_UseHost = hostOrServer == HostOrServer.Host ? true : false;
        }

        /// <summary>
        /// Just a helper function to avoid having to write the entire assert just to check if you
        /// timed out.
        /// </summary>
        protected void AssertOnTimeout(string timeOutErrorMessage, TimeoutHelper assignedTimeoutHelper = null)
        {
            var timeoutHelper = assignedTimeoutHelper ?? s_GlobalTimeoutHelper;
            Assert.False(timeoutHelper.TimedOut, timeOutErrorMessage);
        }

        private void UnloadRemainingScenes()
        {
            // Unload any remaining scenes loaded but the test runner scene
            // Note: Some tests only unload the server-side instance, and this
            // just assures no currently loaded scenes will impact the next test
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded || scene.name.Contains(NetcodeIntegrationTestHelpers.FirstPartOfTestRunnerSceneName) || !OnCanSceneCleanUpUnload(scene))
                {
                    continue;
                }

                VerboseDebug($"Unloading scene {scene.name}-{scene.handle}");
                var asyncOperation = SceneManager.UnloadSceneAsync(scene);
            }
        }

        private System.Text.StringBuilder m_WaitForLog = new System.Text.StringBuilder();

        private void LogWaitForMessages()
        {
            VerboseDebug(m_WaitForLog.ToString());
            m_WaitForLog.Clear();
        }

        private IEnumerator WaitForTickAndFrames(NetworkManager networkManager, int tickCount, float targetFrames)
        {
            var tickAndFramesConditionMet = false;
            var frameCount = 0;
            var waitForFixedUpdate = new WaitForFixedUpdate();
            m_WaitForLog.Append($"[NetworkManager-{networkManager.LocalClientId}][WaitForTicks-Begin] Waiting for ({tickCount}) network ticks and ({targetFrames}) frames to pass.\n");
            var tickStart = networkManager.NetworkTickSystem.LocalTime.Tick;
            while (!tickAndFramesConditionMet)
            {
                // Wait until both tick and frame counts have reached their targeted values
                if ((networkManager.NetworkTickSystem.LocalTime.Tick - tickStart) >= tickCount && frameCount >= targetFrames)
                {
                    tickAndFramesConditionMet = true;
                }
                else
                {
                    yield return waitForFixedUpdate;
                    frameCount++;
                    // In the event something is broken with time systems (or the like)
                    // Exit if we have exceeded 1000 frames
                    if (frameCount >= 1000.0f)
                    {
                        tickAndFramesConditionMet = true;
                    }
                }
            }

            m_WaitForLog.Append($"[NetworkManager-{networkManager.LocalClientId}][WaitForTicks-End] Waited for ({networkManager.NetworkTickSystem.LocalTime.Tick - tickStart}) network ticks and ({frameCount}) frames to pass.\n");
            yield break;
        }

        /// <summary>
        /// Yields until specified amount of network ticks and the expected number of frames has been passed.
        /// </summary>
        protected IEnumerator WaitForTicks(NetworkManager networkManager, int count)
        {
            var targetTick = networkManager.NetworkTickSystem.LocalTime.Tick + count;

            // Calculate the expected number of frame updates that should occur during the tick count wait period
            var frameFrequency = 1.0f / (Application.targetFrameRate >= 60 && Application.targetFrameRate <= 100 ? Application.targetFrameRate : 60.0f);
            var tickFrequency = 1.0f / networkManager.NetworkConfig.TickRate;
            var framesPerTick = tickFrequency / frameFrequency;

            // Total number of frames to occur over the specified number of ticks
            var totalFrameCount = framesPerTick * count;
            m_WaitForLog.Append($"[NetworkManager-{networkManager.LocalClientId}][WaitForTicks] TickRate ({networkManager.NetworkConfig.TickRate}) | Tick Wait ({count}) | TargetFrameRate ({Application.targetFrameRate}) | Target Frames ({framesPerTick * count})\n");
            yield return WaitForTickAndFrames(networkManager, count, totalFrameCount);
        }

        /// <summary>
        /// Simulate a number of frames passing over a specific amount of time.
        /// The delta time simulated for each frame will be evenly divided as time/numFrames
        /// This will only simulate the netcode update loop, as well as update events on
        /// NetworkBehaviour instances, and will not simulate any Unity update processes (physics, etc)
        /// </summary>
        /// <param name="amountOfTimeInSeconds"></param>
        /// <param name="numFramesToSimulate"></param>
        protected static void TimeTravel(double amountOfTimeInSeconds, int numFramesToSimulate = -1)
        {
            if (numFramesToSimulate < 0)
            {
                var frameRate = Application.targetFrameRate;
                if (frameRate <= 0)
                {
                    frameRate = 60;
                }
                numFramesToSimulate = Math.Max((int)(amountOfTimeInSeconds / frameRate), 1);
            }
            var interval = amountOfTimeInSeconds / numFramesToSimulate;
            for (var i = 0; i < numFramesToSimulate; ++i)
            {
                MockTimeProvider.TimeTravel(interval);
                SimulateOneFrame();
            }
        }

        protected virtual uint GetTickRate()
        {
            return k_DefaultTickRate;
        }

        protected virtual int GetFrameRate()
        {
            return Application.targetFrameRate == 0 ? 60 : Application.targetFrameRate;
        }

        private int m_FramesPerTick = 0;
        private float m_TickFrequency = 0;

        /// <summary>
        /// Recalculates the <see cref="m_TickFrequency"/> and <see cref="m_FramesPerTick"/> that is
        /// used in <see cref="TimeTravelAdvanceTick"/>.
        /// </summary>
        protected void ConfigureFramesPerTick()
        {
            m_TickFrequency = 1.0f / GetTickRate();
            m_FramesPerTick = Math.Max((int)(m_TickFrequency / GetFrameRate()), 1);
        }

        /// <summary>
        /// Helper function to time travel exactly one tick's worth of time at the current frame and tick rates.
        /// This is NetcodeIntegrationTest instance relative and will automatically adjust based on <see cref="GetFrameRate"/>
        /// and <see cref="GetTickRate"/>.
        /// </summary>
        protected void TimeTravelAdvanceTick()
        {
            TimeTravel(m_TickFrequency, m_FramesPerTick);
        }

        /// <summary>
        /// Helper function to time travel exactly one tick's worth of time at the current frame and tick rates.
        /// ** Is based on the global k_DefaultTickRate and is not local to each NetcodeIntegrationTest instance **
        /// </summary>
        public static void TimeTravelToNextTick()
        {
            var timePassed = 1.0f / k_DefaultTickRate;
            var frameRate = Application.targetFrameRate;
            if (frameRate <= 0)
            {
                frameRate = 60;
            }
            var frames = Math.Max((int)(timePassed / frameRate), 1);
            TimeTravel(timePassed, frames);
        }

        private struct UpdateData
        {
            public MethodInfo Update;
            public MethodInfo FixedUpdate;
            public MethodInfo LateUpdate;
        }

        private static object[] s_EmptyObjectArray = { };
        private static Dictionary<Type, UpdateData> s_UpdateFunctionCache = new Dictionary<Type, UpdateData>();

        /// <summary>
        /// Simulates one SDK frame. This can be used even without TimeTravel, though it's of somewhat less use
        /// without TimeTravel, as, without the mock transport, it will likely not provide enough time for any
        /// sent messages to be received even if called dozens of times.
        /// </summary>
        public static void SimulateOneFrame()
        {
            foreach (NetworkUpdateStage updateStage in Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                var stage = updateStage;
                // These two are out of order numerically due to backward compatibility
                // requirements. We have to swap them to maintain correct execution
                // order.
                if (stage == NetworkUpdateStage.PostScriptLateUpdate)
                {
                    stage = NetworkUpdateStage.PostLateUpdate;
                }
                else if (stage == NetworkUpdateStage.PostLateUpdate)
                {
                    stage = NetworkUpdateStage.PostScriptLateUpdate;
                }
                NetworkUpdateLoop.RunNetworkUpdateStage(stage);

                if (stage == NetworkUpdateStage.Update || stage == NetworkUpdateStage.FixedUpdate || stage == NetworkUpdateStage.PreLateUpdate)
                {
                    foreach (var behaviour in Object.FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None))
                    {
                        var type = behaviour.GetType();
                        if (!s_UpdateFunctionCache.TryGetValue(type, out var updateData))
                        {
                            updateData = new UpdateData
                            {
                                Update = type.GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                                FixedUpdate = type.GetMethod("FixedUpdate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                                LateUpdate = type.GetMethod("LateUpdate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                            };
                            s_UpdateFunctionCache[type] = updateData;
                        }

                        switch (stage)
                        {
                            case NetworkUpdateStage.FixedUpdate:
                                updateData.FixedUpdate?.Invoke(behaviour, new object[] { });
                                break;
                            case NetworkUpdateStage.Update:
                                updateData.Update?.Invoke(behaviour, new object[] { });
                                break;
                            case NetworkUpdateStage.PreLateUpdate:
                                updateData.LateUpdate?.Invoke(behaviour, new object[] { });
                                break;
                        }
                    }
                }
            }
        }
    }
}
