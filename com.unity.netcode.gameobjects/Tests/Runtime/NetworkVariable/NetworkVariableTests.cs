#if !NGO_MINIMALPROJECT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Unity.Netcode.RuntimeTests
{
    internal struct TestStruct : INetworkSerializable, IEquatable<TestStruct>
    {
        public uint SomeInt;
        public bool SomeBool;
        public static bool NetworkSerializeCalledOnWrite;
        public static bool NetworkSerializeCalledOnRead;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                NetworkSerializeCalledOnRead = true;
            }
            else
            {
                NetworkSerializeCalledOnWrite = true;
            }
            serializer.SerializeValue(ref SomeInt);
            serializer.SerializeValue(ref SomeBool);
        }

        public bool Equals(TestStruct other)
        {
            return SomeInt == other.SomeInt && SomeBool == other.SomeBool;
        }

        public override bool Equals(object obj)
        {
            return obj is TestStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)SomeInt * 397) ^ SomeBool.GetHashCode();
            }
        }
    }

    internal class TestClass : INetworkSerializable, IEquatable<TestClass>
    {
        public uint SomeInt;
        public bool SomeBool;
        public static bool NetworkSerializeCalledOnWrite;
        public static bool NetworkSerializeCalledOnRead;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                NetworkSerializeCalledOnRead = true;
            }
            else
            {
                NetworkSerializeCalledOnWrite = true;
            }
            serializer.SerializeValue(ref SomeInt);
            serializer.SerializeValue(ref SomeBool);
        }

        public bool Equals(TestClass other)
        {
            return SomeInt == other.SomeInt && SomeBool == other.SomeBool;
        }

        public override bool Equals(object obj)
        {
            return obj is TestClass other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)SomeInt * 397) ^ SomeBool.GetHashCode();
            }
        }
    }

    // Used just to create a NetworkVariable in the templated NetworkBehaviour type that isn't referenced anywhere else
    // Please do not reference this class anywhere else!
    internal class TestClass_ReferencedOnlyByTemplateNetworkBehaviourType : TestClass, IEquatable<TestClass_ReferencedOnlyByTemplateNetworkBehaviourType>
    {
        public bool Equals(TestClass_ReferencedOnlyByTemplateNetworkBehaviourType other)
        {
            return Equals((TestClass)other);
        }
    }

    internal class NetworkVariableTest : NetworkBehaviour
    {
        public enum SomeEnum
        {
            A,
            B,
            C
        }
        public readonly NetworkVariable<int> TheScalar = new NetworkVariable<int>();
        public readonly NetworkVariable<SomeEnum> TheEnum = new NetworkVariable<SomeEnum>();
        public readonly NetworkList<int> TheList = new NetworkList<int>();
        public readonly NetworkList<StructUsedOnlyInNetworkList> TheStructList = new NetworkList<StructUsedOnlyInNetworkList>();
        public readonly NetworkList<FixedString128Bytes> TheLargeList = new NetworkList<FixedString128Bytes>();

        public readonly NetworkVariable<FixedString32Bytes> FixedString32 = new NetworkVariable<FixedString32Bytes>();

        private void ListChanged(NetworkListEvent<int> e)
        {
            ListDelegateTriggered = true;
        }

        public void Awake()
        {
            TheList.OnListChanged += ListChanged;
        }

        public readonly NetworkVariable<TestStruct> TheStruct = new NetworkVariable<TestStruct>();
        public readonly NetworkVariable<TestClass> TheClass = new NetworkVariable<TestClass>();

        public NetworkVariable<UnmanagedTemplateNetworkSerializableType<TestStruct>> TheTemplateStruct = new NetworkVariable<UnmanagedTemplateNetworkSerializableType<TestStruct>>();
        public NetworkVariable<ManagedTemplateNetworkSerializableType<TestClass>> TheTemplateClass = new NetworkVariable<ManagedTemplateNetworkSerializableType<TestClass>>();

        public bool ListDelegateTriggered;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                NetworkVariableTests.ClientNetworkVariableTestSpawned(this);
            }
            base.OnNetworkSpawn();
        }
    }

    /// <summary>
    /// Handles the more generic conditional logic for NetworkList tests
    /// which can be used with the <see cref="NetcodeIntegrationTest.WaitForConditionOrTimeOut"/>
    /// that accepts anything derived from the <see cref="ConditionalPredicateBase"/> class
    /// as a parameter.
    /// </summary>
    internal class NetworkListTestPredicate : ConditionalPredicateBase
    {
        private const int k_MaxRandomValue = 1000;

        private Dictionary<NetworkListTestStates, Func<bool>> m_StateFunctions;

        // Player1 component on the Server
        private NetworkVariableTest m_Player1OnServer;

        // Player1 component on client1
        private NetworkVariableTest m_Player1OnClient1;

        private string m_TestStageFailedMessage;

        public enum NetworkListTestStates
        {
            Add,
            ContainsLarge,
            Contains,
            VerifyData,
            IndexOf,
        }

        private NetworkListTestStates m_NetworkListTestState;

        public void SetNetworkListTestState(NetworkListTestStates networkListTestState)
        {
            m_NetworkListTestState = networkListTestState;
        }

        /// <summary>
        /// Determines if the condition has been reached for the current NetworkListTestState
        /// </summary>
        protected override bool OnHasConditionBeenReached()
        {
            var isStateRegistered = m_StateFunctions.ContainsKey(m_NetworkListTestState);
            Assert.IsTrue(isStateRegistered);
            return m_StateFunctions[m_NetworkListTestState].Invoke();
        }

        /// <summary>
        /// Provides all information about the players for both sides for simplicity and informative sake.
        /// </summary>
        /// <returns></returns>
        private string ConditionFailedInfo()
        {
            return $"{m_NetworkListTestState} condition test failed:\n Server List Count: {m_Player1OnServer.TheList.Count} vs  Client List Count: {m_Player1OnClient1.TheList.Count}\n" +
                $"Server List Count: {m_Player1OnServer.TheLargeList.Count} vs  Client List Count: {m_Player1OnClient1.TheLargeList.Count}\n" +
                $"Server Delegate Triggered: {m_Player1OnServer.ListDelegateTriggered} | Client Delegate Triggered: {m_Player1OnClient1.ListDelegateTriggered}\n";
        }

        /// <summary>
        /// When finished, check if a time out occurred and if so assert and provide meaningful information to troubleshoot why
        /// </summary>
        protected override void OnFinished()
        {
            Assert.IsFalse(TimedOut, $"{nameof(NetworkListTestPredicate)} timed out waiting for the {m_NetworkListTestState} condition to be reached! \n" + ConditionFailedInfo());
        }

        // Uses the ArrayOperator and validates that on both sides the count and values are the same
        private bool OnVerifyData()
        {
            // Wait until both sides have the same number of elements
            if (m_Player1OnServer.TheList.Count != m_Player1OnClient1.TheList.Count)
            {
                return false;
            }

            // Check the client values against the server values to make sure they match
            for (int i = 0; i < m_Player1OnServer.TheList.Count; i++)
            {
                if (m_Player1OnServer.TheList[i] != m_Player1OnClient1.TheList[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Verifies the data count, values, and that the ListDelegate on both sides was triggered
        /// </summary>
        private bool OnAdd()
        {
            bool wasTriggerred = m_Player1OnServer.ListDelegateTriggered && m_Player1OnClient1.ListDelegateTriggered;
            return wasTriggerred && OnVerifyData();
        }

        /// <summary>
        /// The current version of this test only verified the count of the large list, so that is what this does
        /// </summary>
        private bool OnContainsLarge()
        {
            return m_Player1OnServer.TheLargeList.Count == m_Player1OnClient1.TheLargeList.Count;
        }

        /// <summary>
        /// Tests NetworkList.Contains which also verifies all values are the same on both sides
        /// </summary>
        private bool OnContains()
        {
            // Wait until both sides have the same number of elements
            if (m_Player1OnServer.TheList.Count != m_Player1OnClient1.TheList.Count)
            {
                return false;
            }

            // Parse through all server values and use the NetworkList.Contains method to check if the value is in the list on the client side
            foreach (var serverValue in m_Player1OnServer.TheList)
            {
                if (!m_Player1OnClient1.TheList.Contains(serverValue))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Tests NetworkList.IndexOf and verifies that all values are aligned on both sides
        /// </summary>
        private bool OnIndexOf()
        {
            foreach (var serverSideValue in m_Player1OnServer.TheList)
            {
                var indexToTest = m_Player1OnServer.TheList.IndexOf(serverSideValue);
                if (indexToTest != m_Player1OnServer.TheList.IndexOf(serverSideValue))
                {
                    return false;
                }
            }
            return true;
        }

        public NetworkListTestPredicate(NetworkVariableTest player1OnServer, NetworkVariableTest player1OnClient1, NetworkListTestStates networkListTestState, int elementCount)
        {
            m_NetworkListTestState = networkListTestState;
            m_Player1OnServer = player1OnServer;
            m_Player1OnClient1 = player1OnClient1;
            m_StateFunctions = new Dictionary<NetworkListTestStates, Func<bool>>
            {
                { NetworkListTestStates.Add, OnAdd },
                { NetworkListTestStates.ContainsLarge, OnContainsLarge },
                { NetworkListTestStates.Contains, OnContains },
                { NetworkListTestStates.VerifyData, OnVerifyData },
                { NetworkListTestStates.IndexOf, OnIndexOf }
            };

            if (networkListTestState == NetworkListTestStates.ContainsLarge)
            {
                for (var i = 0; i < elementCount; ++i)
                {
                    m_Player1OnServer.TheLargeList.Add(new FixedString128Bytes());
                }
            }
            else
            {
                for (int i = 0; i < elementCount; i++)
                {
                    m_Player1OnServer.TheList.Add(Random.Range(0, k_MaxRandomValue));
                }
            }
        }
    }

    internal class NetvarDespawnShutdown : NetworkBehaviour
    {
        private NetworkVariable<int> m_IntNetworkVariable = new NetworkVariable<int>();
        private NetworkList<int> m_IntList;

        private void Awake()
        {
            m_IntList = new NetworkList<int>();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                m_IntNetworkVariable.Value = 5;
                for (int i = 0; i < 10; i++)
                {
                    m_IntList.Add(i);
                }
            }
            base.OnNetworkDespawn();
        }
    }

    /// <summary>
    /// Validates that setting values for NetworkVariable or NetworkList during the
    /// OnNetworkDespawn method will not cause an exception to occur.
    /// </summary>
    internal class NetworkVariableModifyOnNetworkDespawn : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        private GameObject m_TestPrefab;

        protected override void OnOneTimeSetup()
        {
            NetworkVariableBase.IgnoreInitializeWarning = true;
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            NetworkVariableBase.IgnoreInitializeWarning = false;
            base.OnOneTimeTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestPrefab = CreateNetworkObjectPrefab("NetVarDespawn");
            m_TestPrefab.AddComponent<NetvarDespawnShutdown>();
            base.OnServerAndClientsCreated();
        }

        private bool OnClientSpawnedTestPrefab(ulong networkObjectId)
        {
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            if (!s_GlobalNetworkObjects.ContainsKey(clientId))
            {
                return false;
            }

            if (!s_GlobalNetworkObjects[clientId].ContainsKey(networkObjectId))
            {
                return false;
            }

            return true;
        }

        [UnityTest]
        public IEnumerator ModifyNetworkVariableOrListOnNetworkDespawn()
        {
            var instance = SpawnObject(m_TestPrefab, m_ServerNetworkManager);
            yield return WaitForConditionOrTimeOut(() => OnClientSpawnedTestPrefab(instance.GetComponent<NetworkObject>().NetworkObjectId));
            m_ServerNetworkManager.Shutdown();
            // As long as no excetptions occur, the test passes.
        }
    }

#if !MULTIPLAYER_TOOLS
    [TestFixture(true)]
#endif
    [TestFixture(false)]
    internal class NetworkVariableTests : NetcodeIntegrationTest
    {
        private const string k_StringTestValue = "abcdefghijklmnopqrstuvwxyz";
        private static readonly FixedString32Bytes k_FixedStringTestValue = k_StringTestValue;
        protected override int NumberOfClients => 2;

        private const uint k_TestUInt = 0x12345678;

        private const int k_TestVal1 = 111;
        private const int k_TestVal2 = 222;
        private const int k_TestVal3 = 333;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        private static List<NetworkVariableTest> s_ClientNetworkVariableTestInstances = new List<NetworkVariableTest>();
        public static void ClientNetworkVariableTestSpawned(NetworkVariableTest networkVariableTest)
        {
            s_ClientNetworkVariableTestInstances.Add(networkVariableTest);
        }

        // Player1 component on the server
        private NetworkVariableTest m_Player1OnServer;

        // Player1 component on client1
        private NetworkVariableTest m_Player1OnClient1;

        private NetworkListTestPredicate m_NetworkListPredicateHandler;

        private readonly bool m_EnsureLengthSafety;

        public NetworkVariableTests(bool ensureLengthSafety)
        {
            m_EnsureLengthSafety = ensureLengthSafety;
        }

        protected override bool CanStartServerAndClients()
        {
            return false;
        }

        protected override void OnOneTimeSetup()
        {
            NetworkVariableBase.IgnoreInitializeWarning = true;
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            NetworkVariableBase.IgnoreInitializeWarning = false;
            base.OnOneTimeTearDown();
        }

        /// <summary>
        /// This is an adjustment to how the server and clients are started in order
        /// to avoid timing issues when running in a stand alone test runner build.
        /// </summary>
        private void InitializeServerAndClients(HostOrServer useHost)
        {
            s_ClientNetworkVariableTestInstances.Clear();
            m_PlayerPrefab.AddComponent<NetworkVariableTest>();

            m_PlayerPrefab.AddComponent<ClassHavingNetworkBehaviour>();
            m_PlayerPrefab.AddComponent<ClassHavingNetworkBehaviour2>();
            m_PlayerPrefab.AddComponent<StructHavingNetworkBehaviour>();

            m_ServerNetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety = m_EnsureLengthSafety;
            m_ServerNetworkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.EnsureNetworkVariableLengthSafety = m_EnsureLengthSafety;
                client.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            }

            Assert.True(NetcodeIntegrationTestHelpers.Start(useHost == HostOrServer.Host, m_ServerNetworkManager, m_ClientNetworkManagers), "Failed to start server and client instances");

            RegisterSceneManagerHandler();

            // Wait for connection on client and server side
            var success = WaitForClientsConnectedOrTimeOutWithTimeTravel();
            Assert.True(success, $"Timed-out waiting for all clients to connect!");

            // These are the *SERVER VERSIONS* of the *CLIENT PLAYER 1 & 2*
            var result = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();

            NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentationWithTimeTravel(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ServerNetworkManager, result);

            // Assign server-side client's player
            m_Player1OnServer = result.Result.GetComponent<NetworkVariableTest>();

            // This is client1's view of itself
            NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentationWithTimeTravel(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ClientNetworkManagers[0], result);

            // Assign client-side local player
            m_Player1OnClient1 = result.Result.GetComponent<NetworkVariableTest>();

            m_Player1OnServer.TheList.Clear();

            if (m_Player1OnServer.TheList.Count > 0)
            {
                throw new Exception("at least one server network container not empty at start");
            }
            if (m_Player1OnClient1.TheList.Count > 0)
            {
                throw new Exception("at least one client network container not empty at start");
            }

            var instanceCount = useHost == HostOrServer.Host ? NumberOfClients * 3 : NumberOfClients * 2;
            // Wait for the client-side to notify it is finished initializing and spawning.
            success = WaitForConditionOrTimeOutWithTimeTravel(() => s_ClientNetworkVariableTestInstances.Count == instanceCount);

            Assert.True(success, "Timed out waiting for all client NetworkVariableTest instances to register they have spawned!");

            TimeTravelToNextTick();
        }

#if !MULTIPLAYER_TOOLS
        /// <summary>
        /// Runs generalized tests on all predefined NetworkVariable types
        /// </summary>
        [Test]
        public void AllNetworkVariableTypes([Values] HostOrServer useHost)
        {
            // Create, instantiate, and host
            // This would normally go in Setup, but since every other test but this one
            //  uses NetworkManagerHelper, and it does its own NetworkManager setup / teardown,
            //  for now we put this within this one test until we migrate it to MIH
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out NetworkManager server, useHost == HostOrServer.Host ? NetworkManagerHelper.NetworkManagerOperatingMode.Host : NetworkManagerHelper.NetworkManagerOperatingMode.Server));

            Assert.IsTrue(server.IsHost == (useHost == HostOrServer.Host), $"{nameof(useHost)} does not match the server.IsHost value!");

            Guid gameObjectId = NetworkManagerHelper.AddGameNetworkObject("NetworkVariableTestComponent");

            var networkVariableTestComponent = NetworkManagerHelper.AddComponentToObject<NetworkVariableTestComponent>(gameObjectId);

            NetworkManagerHelper.SpawnNetworkObject(gameObjectId);

            // Start Testing
            networkVariableTestComponent.EnableTesting = true;

            var success = WaitForConditionOrTimeOutWithTimeTravel(() => true == networkVariableTestComponent.IsTestComplete());
            Assert.True(success, "Timed out waiting for the test to complete!");

            // Stop Testing
            networkVariableTestComponent.EnableTesting = false;

            Assert.IsTrue(networkVariableTestComponent.DidAllValuesChange());
            networkVariableTestComponent.AssertAllValuesAreCorrect();

            // Disable this once we are done.
            networkVariableTestComponent.gameObject.SetActive(false);

            // This would normally go in Teardown, but since every other test but this one
            //  uses NetworkManagerHelper, and it does its own NetworkManager setup / teardown,
            //  for now we put this within this one test until we migrate it to MIH
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        [Test]
        public void ClientWritePermissionTest([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            // client must not be allowed to write to a server auth variable

            LogAssert.Expect(LogType.Error, m_Player1OnClient1.TheScalar.GetWritePermissionError());
            m_Player1OnClient1.TheScalar.Value = k_TestVal1;
        }

        /// <summary>
        /// Runs tests that network variables sync on client whatever the local value of <see cref="Time.timeScale"/>.
        /// </summary>
        [Test]
        public void NetworkVariableSync_WithDifferentTimeScale([Values] HostOrServer useHost, [Values(0.0f, 1.0f, 2.0f)] float timeScale)
        {
            Time.timeScale = timeScale;

            InitializeServerAndClients(useHost);

            m_Player1OnServer.TheScalar.Value = k_TestVal1;

            // Now wait for the client side version to be updated to k_TestVal1
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => m_Player1OnClient1.TheScalar.Value == k_TestVal1);
            Assert.True(success, "Timed out waiting for client-side NetworkVariable to update!");
        }

        [Test]
        public void FixedString32Test([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);
            m_Player1OnServer.FixedString32.Value = k_FixedStringTestValue;

            // Now wait for the client side version to be updated to k_FixedStringTestValue
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => m_Player1OnClient1.FixedString32.Value == k_FixedStringTestValue);
            Assert.True(success, "Timed out waiting for client-side NetworkVariable to update!");
        }

        [Test]
        public void NetworkListAdd([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);
            m_NetworkListPredicateHandler = new NetworkListTestPredicate(m_Player1OnServer, m_Player1OnClient1, NetworkListTestPredicate.NetworkListTestStates.Add, 10);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }

        [Test]
        public void WhenListContainsManyLargeValues_OverflowExceptionIsNotThrown([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);
            m_NetworkListPredicateHandler = new NetworkListTestPredicate(m_Player1OnServer, m_Player1OnClient1, NetworkListTestPredicate.NetworkListTestStates.ContainsLarge, 20);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }

        [Test]
        public void NetworkListContains([Values] HostOrServer useHost)
        {
            // Re-use the NetworkListAdd to initialize the server and client as well as make sure the list is populated
            NetworkListAdd(useHost);

            // Now test the NetworkList.Contains method
            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.Contains);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }


        [Test]
        public void NetworkListInsert([Values] HostOrServer useHost)
        {
            // Re-use the NetworkListAdd to initialize the server and client as well as make sure the list is populated
            NetworkListAdd(useHost);

            // Now randomly insert a random value entry
            m_Player1OnServer.TheList.Insert(Random.Range(0, 9), Random.Range(1, 99));

            // Verify the element count and values on the client matches the server
            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.VerifyData);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }

        [Test]
        public void NetworkListIndexOf([Values] HostOrServer useHost)
        {
            // Re-use the NetworkListAdd to initialize the server and client as well as make sure the list is populated
            NetworkListAdd(useHost);

            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.IndexOf);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }

        [Test]
        public void NetworkListValueUpdate([Values] HostOrServer useHost)
        {
            var testSucceeded = false;
            InitializeServerAndClients(useHost);
            // Add 1 element value and verify it is the same on the client
            m_NetworkListPredicateHandler = new NetworkListTestPredicate(m_Player1OnServer, m_Player1OnClient1, NetworkListTestPredicate.NetworkListTestStates.Add, 1);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));

            // Setup our original and
            var previousValue = m_Player1OnServer.TheList[0];
            var updatedValue = previousValue + 10;

            // Callback that verifies the changed event occurred and that the original and new values are correct
            void TestValueUpdatedCallback(NetworkListEvent<int> changedEvent)
            {
                testSucceeded = changedEvent.PreviousValue == previousValue &&
                                changedEvent.Value == updatedValue;
            }

            // Subscribe to the OnListChanged event on the client side and
            m_Player1OnClient1.TheList.OnListChanged += TestValueUpdatedCallback;
            m_Player1OnServer.TheList[0] = updatedValue;

            // Wait until we know the client side matches the server side before checking if the callback was a success
            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.VerifyData);
            WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler);

            Assert.That(testSucceeded);
            m_Player1OnClient1.TheList.OnListChanged -= TestValueUpdatedCallback;
        }

        private List<int> m_ExpectedValuesServer = new List<int>();
        private List<int> m_ExpectedValuesClient = new List<int>();

        public enum ListRemoveTypes
        {
            Remove,
            RemoveAt
        }


        [Test]
        public void NetworkListRemoveTests([Values] HostOrServer useHost, [Values] ListRemoveTypes listRemoveType)
        {
            m_ExpectedValuesServer.Clear();
            m_ExpectedValuesClient.Clear();
            // Re-use the NetworkListAdd to initialize the server and client as well as make sure the list is populated
            NetworkListAdd(useHost);

            // Randomly remove a few entries
            m_Player1OnServer.TheList.OnListChanged += Server_OnListChanged;
            m_Player1OnClient1.TheList.OnListChanged += Client_OnListChanged;

            // Remove half of the elements
            for (int i = 0; i < (int)(m_Player1OnServer.TheList.Count * 0.5f); i++)
            {
                var index = Random.Range(0, m_Player1OnServer.TheList.Count - 1);
                var value = m_Player1OnServer.TheList[index];
                m_ExpectedValuesServer.Add(value);
                m_ExpectedValuesClient.Add(value);

                if (listRemoveType == ListRemoveTypes.RemoveAt)
                {
                    m_Player1OnServer.TheList.RemoveAt(index);
                }
                else
                {
                    m_Player1OnServer.TheList.Remove(value);
                }
            }

            // Verify the element count and values on the client matches the server
            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.VerifyData);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));

            Assert.True(m_ExpectedValuesServer.Count == 0, $"Server was not notified of all elements removed and still has {m_ExpectedValuesServer.Count} elements left!");
            Assert.True(m_ExpectedValuesClient.Count == 0, $"Client was not notified of all elements removed and still has {m_ExpectedValuesClient.Count} elements left!");
        }

        private void Server_OnListChanged(NetworkListEvent<int> changeEvent)
        {
            Assert.True(m_ExpectedValuesServer.Contains(changeEvent.Value));
            m_ExpectedValuesServer.Remove(changeEvent.Value);
        }

        private void Client_OnListChanged(NetworkListEvent<int> changeEvent)
        {
            Assert.True(m_ExpectedValuesClient.Contains(changeEvent.Value));
            m_ExpectedValuesClient.Remove(changeEvent.Value);
        }

        [Test]
        public void NetworkListClear([Values] HostOrServer useHost)
        {
            // Re-use the NetworkListAdd to initialize the server and client as well as make sure the list is populated
            NetworkListAdd(useHost);
            m_Player1OnServer.TheList.Clear();
            // Verify the element count and values on the client matches the server
            m_NetworkListPredicateHandler.SetNetworkListTestState(NetworkListTestPredicate.NetworkListTestStates.VerifyData);
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(m_NetworkListPredicateHandler));
        }

        [Test]
        public void TestNetworkVariableClass([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyClass()
            {
                return m_Player1OnClient1.TheClass.Value != null &&
                       m_Player1OnClient1.TheClass.Value.SomeBool == m_Player1OnServer.TheClass.Value.SomeBool &&
                       m_Player1OnClient1.TheClass.Value.SomeInt == m_Player1OnServer.TheClass.Value.SomeInt;
            }

            m_Player1OnServer.TheClass.Value = new TestClass { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.TheClass.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyClass));
        }

        [Test]
        public void TestNetworkVariableTemplateClass([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyClass()
            {
                return m_Player1OnClient1.TheTemplateClass.Value.Value != null && m_Player1OnClient1.TheTemplateClass.Value.Value.SomeBool == m_Player1OnServer.TheTemplateClass.Value.Value.SomeBool &&
                       m_Player1OnClient1.TheTemplateClass.Value.Value.SomeInt == m_Player1OnServer.TheTemplateClass.Value.Value.SomeInt;
            }

            m_Player1OnServer.TheTemplateClass.Value = new ManagedTemplateNetworkSerializableType<TestClass> { Value = new TestClass { SomeInt = k_TestUInt, SomeBool = false } };
            m_Player1OnServer.TheTemplateClass.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyClass));
        }

        [Test]
        public void TestNetworkListStruct([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyList()
            {
                return m_Player1OnClient1.TheStructList.Count == m_Player1OnServer.TheStructList.Count &&
                       m_Player1OnClient1.TheStructList[0].Value == m_Player1OnServer.TheStructList[0].Value &&
                       m_Player1OnClient1.TheStructList[1].Value == m_Player1OnServer.TheStructList[1].Value;
            }

            m_Player1OnServer.TheStructList.Add(new StructUsedOnlyInNetworkList { Value = 1 });
            m_Player1OnServer.TheStructList.Add(new StructUsedOnlyInNetworkList { Value = 2 });
            m_Player1OnServer.TheStructList.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyList));
        }

        [Test]
        public void TestNetworkVariableStruct([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyStructure()
            {
                return m_Player1OnClient1.TheStruct.Value.SomeBool == m_Player1OnServer.TheStruct.Value.SomeBool &&
                       m_Player1OnClient1.TheStruct.Value.SomeInt == m_Player1OnServer.TheStruct.Value.SomeInt;
            }

            m_Player1OnServer.TheStruct.Value = new TestStruct { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.TheStruct.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyStructure));
        }

        [Test]
        public void TestNetworkVariableTemplateStruct([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyStructure()
            {
                return m_Player1OnClient1.TheTemplateStruct.Value.Value.SomeBool == m_Player1OnServer.TheTemplateStruct.Value.Value.SomeBool &&
                       m_Player1OnClient1.TheTemplateStruct.Value.Value.SomeInt == m_Player1OnServer.TheTemplateStruct.Value.Value.SomeInt;
            }

            m_Player1OnServer.TheTemplateStruct.Value = new UnmanagedTemplateNetworkSerializableType<TestStruct> { Value = new TestStruct { SomeInt = k_TestUInt, SomeBool = false } };
            m_Player1OnServer.TheTemplateStruct.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyStructure));
        }

        [Test]
        public void TestNetworkVariableTemplateBehaviourClass([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyClass()
            {
                return (m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value != null && m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value.SomeBool == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value.SomeBool &&
                       m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value.SomeInt == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value.SomeInt)
                       && (m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value != null && m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value.SomeBool == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value.SomeBool &&
                       m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value.SomeInt == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value.SomeInt);
            }

            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar.Value = new TestClass { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.Value = new TestClass { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar.SetDirty(true);
            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour>().TheVar2.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyClass));
        }

        [Test]
        public void TestNetworkVariableTemplateBehaviourClassNotReferencedElsewhere([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyClass()
            {
                return m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value != null && m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value.SomeBool == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value.SomeBool &&
                       m_Player1OnClient1.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value.SomeInt == m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value.SomeInt;
            }

            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.Value = new TestClass_ReferencedOnlyByTemplateNetworkBehaviourType { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.GetComponent<ClassHavingNetworkBehaviour2>().TheVar.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyClass));
        }

        [Test]
        public void TestNetworkVariableTemplateBehaviourStruct([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyClass()
            {
                return m_Player1OnClient1.GetComponent<StructHavingNetworkBehaviour>().TheVar.Value.SomeBool == m_Player1OnServer.GetComponent<StructHavingNetworkBehaviour>().TheVar.Value.SomeBool &&
                       m_Player1OnClient1.GetComponent<StructHavingNetworkBehaviour>().TheVar.Value.SomeInt == m_Player1OnServer.GetComponent<StructHavingNetworkBehaviour>().TheVar.Value.SomeInt;
            }

            m_Player1OnServer.GetComponent<StructHavingNetworkBehaviour>().TheVar.Value = new TestStruct { SomeInt = k_TestUInt, SomeBool = false };
            m_Player1OnServer.GetComponent<StructHavingNetworkBehaviour>().TheVar.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyClass));
        }

        [Test]
        public void TestNetworkVariableEnum([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);

            bool VerifyStructure()
            {
                return m_Player1OnClient1.TheEnum.Value == NetworkVariableTest.SomeEnum.C;
            }

            m_Player1OnServer.TheEnum.Value = NetworkVariableTest.SomeEnum.C;
            m_Player1OnServer.TheEnum.SetDirty(true);

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyStructure));
        }

        [Test]
        public void TestINetworkSerializableClassCallsNetworkSerialize([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);
            TestClass.NetworkSerializeCalledOnWrite = false;
            TestClass.NetworkSerializeCalledOnRead = false;
            m_Player1OnServer.TheClass.Value = new TestClass
            {
                SomeBool = true,
                SomeInt = 32
            };

            static bool VerifyCallback() => TestClass.NetworkSerializeCalledOnWrite && TestClass.NetworkSerializeCalledOnRead;

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyCallback));
        }
#endif

        [Test]
        public void TestINetworkSerializableStructCallsNetworkSerialize([Values] HostOrServer useHost)
        {
            InitializeServerAndClients(useHost);
            TestStruct.NetworkSerializeCalledOnWrite = false;
            TestStruct.NetworkSerializeCalledOnRead = false;
            m_Player1OnServer.TheStruct.Value = new TestStruct() { SomeInt = k_TestUInt, SomeBool = false };

            static bool VerifyCallback() => TestStruct.NetworkSerializeCalledOnWrite && TestStruct.NetworkSerializeCalledOnRead;

            // Wait for the client-side to notify it is finished initializing and spawning.
            Assert.True(WaitForConditionOrTimeOutWithTimeTravel(VerifyCallback));
        }

        [Test]
        public void TestCustomGenericSerialization()
        {
            // Just verifies that the ILPP codegen initialized these values for this type.
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization1>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization1>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization2>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization2>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization3>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization3>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization4>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization4>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization5>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization5>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedTypeSerializer<TypeReferencedOnlyInCustomSerialization6>), NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization6>.Serializer.GetType());
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization1>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization2>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization3>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization4>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization5>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<TypeReferencedOnlyInCustomSerialization6>.AreEqual);

            // Verify no issues with generic values...

            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization1>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization1>>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization2>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization2>>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization3>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization3>>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization4>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization4>>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization5>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization5>>.Serializer.GetType());
            Assert.AreEqual(typeof(UnmanagedArraySerializer<TypeReferencedOnlyInCustomSerialization6>), NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization6>>.Serializer.GetType());
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization1>>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization2>>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization3>>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization4>>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization5>>.AreEqual);
            Assert.IsNotNull(NetworkVariableSerialization<NativeArray<TypeReferencedOnlyInCustomSerialization6>>.AreEqual);
        }

        [Test]
        public void TestUnsupportedManagedTypesThrowExceptions()
        {
            var variable = new NetworkVariable<string>();
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            using var reader = new FastBufferReader(writer, Allocator.None);
            // Just making sure these are null, just in case.
            UserNetworkVariableSerialization<string>.ReadValue = null;
            UserNetworkVariableSerialization<string>.WriteValue = null;
            UserNetworkVariableSerialization<string>.DuplicateValue = null;
            Assert.Throws<ArgumentException>(() =>
            {
                variable.WriteField(writer);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                variable.ReadField(reader);
            });
        }

        [Test]
        // Exceptions should be thrown in DA mode with UserNetworkVariableSerialization
        public void TestUnsupportedManagedTypesWithUserSerializationDoNotThrowExceptionsInClientServerMode()
        {
            var variable = new NetworkVariable<string>();
            UserNetworkVariableSerialization<string>.ReadValue = (FastBufferReader reader, out string value) =>
            {
                reader.ReadValueSafe(out value);
            };
            UserNetworkVariableSerialization<string>.WriteValue = (FastBufferWriter writer, in string value) =>
            {
                writer.WriteValueSafe(value);
            };
            UserNetworkVariableSerialization<string>.DuplicateValue = (in string a, ref string b) =>
            {
                b = string.Copy(a);
            };
            try
            {
                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                variable.Value = "012345";
                variable.WriteField(writer);
                variable.Value = "";

                using var reader = new FastBufferReader(writer, Allocator.None);
                variable.ReadField(reader);
                Assert.AreEqual("012345", variable.Value);
            }
            catch (Exception)
            {
                Assert.True(NetworkVariableSerialization<UserNetworkVariableSerialization<string>>.IsDistributedAuthority);
            }
            finally
            {
                UserNetworkVariableSerialization<string>.ReadValue = null;
                UserNetworkVariableSerialization<string>.WriteValue = null;
                UserNetworkVariableSerialization<string>.DuplicateValue = null;
            }
        }

        [Test]
        public void TestUnsupportedUnmanagedTypesThrowExceptions()
        {
            var variable = new NetworkVariable<Guid>();
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            using var reader = new FastBufferReader(writer, Allocator.None);
            // Just making sure these are null, just in case.
            UserNetworkVariableSerialization<Guid>.ReadValue = null;
            UserNetworkVariableSerialization<Guid>.WriteValue = null;
            UserNetworkVariableSerialization<Guid>.DuplicateValue = null;
            Assert.Throws<ArgumentException>(() =>
            {
                variable.WriteField(writer);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                variable.ReadField(reader);
            });
        }

        [Test]
        public void TestTypesReferencedInSubclassSerializeSuccessfully()
        {
            var variable = new NetworkVariableSubclass<TemplatedValueOnlyReferencedByNetworkVariableSubclass<int>>();
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            var value = new TemplatedValueOnlyReferencedByNetworkVariableSubclass<int> { Value = 12345 };
            variable.Value = value;
            variable.WriteField(writer);
            variable.Value = new TemplatedValueOnlyReferencedByNetworkVariableSubclass<int> { Value = 54321 };

            using var reader = new FastBufferReader(writer, Allocator.None);
            variable.ReadField(reader);
            Assert.AreEqual(value.Value, variable.Value.Value);
        }

        [Test]
        public void TestUnsupportedUnmanagedTypesWithUserSerializationDoNotThrowExceptions()
        {
            var variable = new NetworkVariable<Guid>();
            UserNetworkVariableSerialization<Guid>.ReadValue = (FastBufferReader reader, out Guid value) =>
            {
                var tmpValue = new ForceNetworkSerializeByMemcpy<Guid>();
                reader.ReadValueSafe(out tmpValue);
                value = tmpValue.Value;
            };
            UserNetworkVariableSerialization<Guid>.WriteValue = (FastBufferWriter writer, in Guid value) =>
            {
                var tmpValue = new ForceNetworkSerializeByMemcpy<Guid>(value);
                writer.WriteValueSafe(tmpValue);
            };
            UserNetworkVariableSerialization<Guid>.DuplicateValue = (in Guid a, ref Guid b) =>
            {
                b = a;
            };
            try
            {
                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                var guid = Guid.NewGuid();
                variable.Value = guid;
                variable.WriteField(writer);
                variable.Value = Guid.Empty;

                using var reader = new FastBufferReader(writer, Allocator.None);
                variable.ReadField(reader);
                Assert.AreEqual(guid, variable.Value);
            }
            finally
            {
                UserNetworkVariableSerialization<Guid>.ReadValue = null;
                UserNetworkVariableSerialization<Guid>.WriteValue = null;
                UserNetworkVariableSerialization<Guid>.DuplicateValue = null;
            }
        }
        [Test]
        public void WhenCreatingAnArrayOfNetVars_InitializingVariablesDoesNotThrowAnException()
        {
            var testObjPrefab = CreateNetworkObjectPrefab($"NetVarArrayPrefab");
            var testComp = testObjPrefab.AddComponent<NetworkBehaviourWithNetVarArray>();
            testComp.InitializeVariables();

            // Verify all variables were initialized
            Assert.AreEqual(testComp.InitializedFieldCount, 5);

            Assert.NotNull(testComp.Int0.GetBehaviour());
            Assert.NotNull(testComp.Int1.GetBehaviour());
            Assert.NotNull(testComp.Int2.GetBehaviour());
            Assert.NotNull(testComp.Int3.GetBehaviour());
            Assert.NotNull(testComp.Int4.GetBehaviour());

            Assert.NotNull(testComp.Int0.Name);
            Assert.NotNull(testComp.Int1.Name);
            Assert.NotNull(testComp.Int2.Name);
            Assert.NotNull(testComp.Int3.Name);
            Assert.NotNull(testComp.Int4.Name);

            Assert.AreNotEqual("", testComp.Int0.Name);
            Assert.AreNotEqual("", testComp.Int1.Name);
            Assert.AreNotEqual("", testComp.Int2.Name);
            Assert.AreNotEqual("", testComp.Int3.Name);
            Assert.AreNotEqual("", testComp.Int4.Name);

            Assert.AreSame(testComp.AllInts[0], testComp.Int0);
            Assert.AreSame(testComp.AllInts[1], testComp.Int1);
            Assert.AreSame(testComp.AllInts[2], testComp.Int2);
            Assert.AreSame(testComp.AllInts[3], testComp.Int3);
            Assert.AreSame(testComp.AllInts[4], testComp.Int4);
        }

        private void TestValueType<T>(T testValue, T changedValue) where T : unmanaged
        {
            var serverVariable = new NetworkVariable<T>(testValue);
            var clientVariable = new NetworkVariable<T>();
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            serverVariable.WriteField(writer);

            Assert.IsFalse(NetworkVariableSerialization<T>.AreEqual(ref serverVariable.RefValue(), ref clientVariable.RefValue()));

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            Assert.IsTrue(NetworkVariableSerialization<T>.AreEqual(ref serverVariable.RefValue(), ref clientVariable.RefValue()));

            serverVariable.Value = changedValue;
            Assert.IsFalse(NetworkVariableSerialization<T>.AreEqual(ref serverVariable.RefValue(), ref clientVariable.RefValue()));

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            Assert.IsFalse(NetworkVariableSerialization<T>.AreEqual(ref serverVariable.RefValue(), ref clientVariable.RefValue()));

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            Assert.IsTrue(NetworkVariableSerialization<T>.AreEqual(ref serverVariable.RefValue(), ref clientVariable.RefValue()));
        }

        public void AssertArraysMatch<T>(ref NativeArray<T> a, ref NativeArray<T> b) where T : unmanaged
        {
            Assert.IsTrue(NetworkVariableSerialization<NativeArray<T>>.AreEqual(ref a, ref b),
                $"Lists do not match: {ArrayStr(a)} != {ArrayStr(b)}");
        }
        public void AssertArraysDoNotMatch<T>(ref NativeArray<T> a, ref NativeArray<T> b) where T : unmanaged
        {
            Assert.IsFalse(NetworkVariableSerialization<NativeArray<T>>.AreEqual(ref a, ref b),
                $"Lists match when they should not: {ArrayStr(a)} == {ArrayStr(b)}");
        }

        private void TestValueTypeNativeArray<T>(NativeArray<T> testValue, NativeArray<T> changedValue) where T : unmanaged
        {
            VerboseDebug($"Changing {ArrayStr(testValue)} to {ArrayStr(changedValue)}");
            var serverVariable = new NetworkVariable<NativeArray<T>>(testValue);
            var clientVariable = new NetworkVariable<NativeArray<T>>(new NativeArray<T>(1, Allocator.Persistent));
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertArraysDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertArraysMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());

            serverVariable.ResetDirty();
            serverVariable.Value.Dispose();
            serverVariable.Value = changedValue;
            AssertArraysDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertArraysDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertArraysMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            var cachedValue = changedValue[0];
            var differentValue = changedValue[0];
            foreach (var checkValue in testValue)
            {
                var checkValueRef = checkValue;
                if (!NetworkVariableSerialization<T>.AreEqual(ref checkValueRef, ref differentValue))
                {
                    differentValue = checkValue;
                    break;
                }
            }
            changedValue[0] = differentValue;
            Assert.IsTrue(serverVariable.IsDirty());
            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            changedValue[0] = cachedValue;
            Assert.IsTrue(serverVariable.IsDirty());


            serverVariable.Dispose();
            clientVariable.Dispose();
        }

        public void AssertListsMatch<T>(ref List<T> a, ref List<T> b)
        {
            Assert.IsTrue(NetworkVariableSerialization<List<T>>.AreEqual(ref a, ref b),
                $"Lists do not match: {ListStr(a)} != {ListStr(b)}");
        }
        public void AssertListsDoNotMatch<T>(ref List<T> a, ref List<T> b)
        {
            Assert.IsFalse(NetworkVariableSerialization<List<T>>.AreEqual(ref a, ref b),
                $"Lists match when they should not: {ListStr(a)} == {ListStr(b)}");
        }


        private void TestList<T>(List<T> testValue, List<T> changedValue)
        {
            VerboseDebug($"Changing {ListStr(testValue)} to {ListStr(changedValue)}");
            var serverVariable = new NetworkVariable<List<T>>(testValue);
            var inPlaceList = new List<T>();
            var clientVariable = new NetworkVariable<List<T>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertListsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value = changedValue;
            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertListsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Add(default);
            Assert.IsTrue(serverVariable.IsDirty());
        }


        public void AssertSetsMatch<T>(ref HashSet<T> a, ref HashSet<T> b) where T : IEquatable<T>
        {
            Assert.IsTrue(NetworkVariableSerialization<HashSet<T>>.AreEqual(ref a, ref b),
                $"Sets do not match: {HashSetStr(a)} != {HashSetStr(b)}");
        }
        public void AssertSetsDoNotMatch<T>(ref HashSet<T> a, ref HashSet<T> b) where T : IEquatable<T>
        {
            Assert.IsFalse(NetworkVariableSerialization<HashSet<T>>.AreEqual(ref a, ref b),
                $"Sets match when they should not: {HashSetStr(a)} == {HashSetStr(b)}");
        }

        private void TestHashSet<T>(HashSet<T> testValue, HashSet<T> changedValue) where T : IEquatable<T>
        {
            VerboseDebug($"Changing {HashSetStr(testValue)} to {HashSetStr(changedValue)}");
            var serverVariable = new NetworkVariable<HashSet<T>>(testValue);
            var inPlaceList = new HashSet<T>();
            var clientVariable = new NetworkVariable<HashSet<T>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertSetsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value = changedValue;
            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertSetsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Add(default);
            Assert.IsTrue(serverVariable.IsDirty());
        }


        public void AssertMapsMatch<TKey, TVal>(ref Dictionary<TKey, TVal> a, ref Dictionary<TKey, TVal> b)
            where TKey : IEquatable<TKey>
        {
            Assert.IsTrue(NetworkVariableSerialization<Dictionary<TKey, TVal>>.AreEqual(ref a, ref b),
                $"Maps do not match: {DictionaryStr(a)} != {DictionaryStr(b)}");
        }

        public void AssertMapsDoNotMatch<TKey, TVal>(ref Dictionary<TKey, TVal> a, ref Dictionary<TKey, TVal> b)
            where TKey : IEquatable<TKey>
        {
            Assert.IsFalse(NetworkVariableSerialization<Dictionary<TKey, TVal>>.AreEqual(ref a, ref b),
                $"Maps match when they should not: {DictionaryStr(a)} != {DictionaryStr(b)}");
        }

        private void TestDictionary<TKey, TVal>(Dictionary<TKey, TVal> testValue, Dictionary<TKey, TVal> changedValue)
            where TKey : IEquatable<TKey>
        {
            VerboseDebug($"Changing {DictionaryStr(testValue)} to {DictionaryStr(changedValue)}");
            var serverVariable = new NetworkVariable<Dictionary<TKey, TVal>>(testValue);
            var inPlaceList = new Dictionary<TKey, TVal>();
            var clientVariable = new NetworkVariable<Dictionary<TKey, TVal>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertMapsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value = changedValue;
            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertMapsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            foreach (var kvp in testValue)
            {
                if (!serverVariable.Value.ContainsKey(kvp.Key))
                {
                    serverVariable.Value.Add(kvp.Key, kvp.Value);
                }
            }
            Assert.IsTrue(serverVariable.IsDirty());
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public void AssertListsMatch<T>(ref NativeList<T> a, ref NativeList<T> b) where T : unmanaged
        {
            Assert.IsTrue(NetworkVariableSerialization<NativeList<T>>.AreEqual(ref a, ref b),
                $"Lists do not match: {NativeListStr(a)} != {NativeListStr(b)}");
        }
        public void AssertListsDoNotMatch<T>(ref NativeList<T> a, ref NativeList<T> b) where T : unmanaged
        {
            Assert.IsFalse(NetworkVariableSerialization<NativeList<T>>.AreEqual(ref a, ref b),
                $"Lists match when they should not: {NativeListStr(a)} == {NativeListStr(b)}");
        }


        private void TestValueTypeNativeList<T>(NativeList<T> testValue, NativeList<T> changedValue) where T : unmanaged
        {
            VerboseDebug($"Changing {NativeListStr(testValue)} to {NativeListStr(changedValue)}");
            var serverVariable = new NetworkVariable<NativeList<T>>(testValue);
            var inPlaceList = new NativeList<T>(1, Allocator.Temp);
            var clientVariable = new NetworkVariable<NativeList<T>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertListsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value.Dispose();
            serverVariable.Value = changedValue;
            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertListsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertListsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertListsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Add(default);
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.Dispose();
            clientVariable.Dispose();
        }


        public void AssertSetsMatch<T>(ref NativeHashSet<T> a, ref NativeHashSet<T> b) where T : unmanaged, IEquatable<T>
        {
            Assert.IsTrue(NetworkVariableSerialization<NativeHashSet<T>>.AreEqual(ref a, ref b),
                $"Sets do not match: {NativeHashSetStr(a)} != {NativeHashSetStr(b)}");
        }
        public void AssertSetsDoNotMatch<T>(ref NativeHashSet<T> a, ref NativeHashSet<T> b) where T : unmanaged, IEquatable<T>
        {
            Assert.IsFalse(NetworkVariableSerialization<NativeHashSet<T>>.AreEqual(ref a, ref b),
                $"Sets match when they should not: {NativeHashSetStr(a)} == {NativeHashSetStr(b)}");
        }

        private void TestValueTypeNativeHashSet<T>(NativeHashSet<T> testValue, NativeHashSet<T> changedValue) where T : unmanaged, IEquatable<T>
        {
            VerboseDebug($"Changing {NativeHashSetStr(testValue)} to {NativeHashSetStr(changedValue)}");
            var serverVariable = new NetworkVariable<NativeHashSet<T>>(testValue);
            var inPlaceList = new NativeHashSet<T>(1, Allocator.Temp);
            var clientVariable = new NetworkVariable<NativeHashSet<T>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertSetsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value.Dispose();
            serverVariable.Value = changedValue;
            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertSetsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertSetsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertSetsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Add(default);
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.Dispose();
            clientVariable.Dispose();
        }


        public void AssertMapsMatch<TKey, TVal>(ref NativeHashMap<TKey, TVal> a, ref NativeHashMap<TKey, TVal> b)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            Assert.IsTrue(NetworkVariableSerialization<NativeHashMap<TKey, TVal>>.AreEqual(ref a, ref b),
                $"Maps do not match: {NativeHashMapStr(a)} != {NativeHashMapStr(b)}");
        }

        public void AssertMapsDoNotMatch<TKey, TVal>(ref NativeHashMap<TKey, TVal> a, ref NativeHashMap<TKey, TVal> b)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            Assert.IsFalse(NetworkVariableSerialization<NativeHashMap<TKey, TVal>>.AreEqual(ref a, ref b),
                $"Maps match when they should not: {NativeHashMapStr(a)} != {NativeHashMapStr(b)}");
        }

        private void TestValueTypeNativeHashMap<TKey, TVal>(NativeHashMap<TKey, TVal> testValue, NativeHashMap<TKey, TVal> changedValue)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            VerboseDebug($"Changing {NativeHashMapStr(testValue)} to {NativeHashMapStr(changedValue)}");
            var serverVariable = new NetworkVariable<NativeHashMap<TKey, TVal>>(testValue);
            var inPlaceList = new NativeHashMap<TKey, TVal>(1, Allocator.Temp);
            var clientVariable = new NetworkVariable<NativeHashMap<TKey, TVal>>(inPlaceList);
            using var writer = new FastBufferWriter(1024, Allocator.Temp, int.MaxValue);
            serverVariable.WriteField(writer);

            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadField(reader);

            AssertMapsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            serverVariable.Value.Dispose();
            serverVariable.Value = changedValue;
            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            writer.Seek(0);

            serverVariable.WriteDelta(writer);

            AssertMapsDoNotMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            using var reader2 = new FastBufferReader(writer, Allocator.None);
            clientVariable.ReadDelta(reader2, false);
            AssertMapsMatch(ref serverVariable.RefValue(), ref clientVariable.RefValue());
            // Lists are deserialized in place so this should ALWAYS be true. Checking it every time to make sure!
            AssertMapsMatch(ref clientVariable.RefValue(), ref inPlaceList);

            serverVariable.ResetDirty();
            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Clear();
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.ResetDirty();

            Assert.IsFalse(serverVariable.IsDirty());
            serverVariable.Value.Add(default, default);
            Assert.IsTrue(serverVariable.IsDirty());

            serverVariable.Dispose();
            clientVariable.Dispose();
        }
#endif
        [Test]
        public void WhenSerializingAndDeserializingValueTypeNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                TestValueType<byte>(byte.MinValue + 5, byte.MaxValue);
            }
            else if (testType == typeof(sbyte))
            {
                TestValueType<sbyte>(sbyte.MinValue + 5, sbyte.MaxValue);
            }
            else if (testType == typeof(short))
            {
                TestValueType<short>(short.MinValue + 5, short.MaxValue);
            }
            else if (testType == typeof(ushort))
            {
                TestValueType<ushort>(ushort.MinValue + 5, ushort.MaxValue);
            }
            else if (testType == typeof(int))
            {
                TestValueType(int.MinValue + 5, int.MaxValue);
            }
            else if (testType == typeof(uint))
            {
                TestValueType(uint.MinValue + 5, uint.MaxValue);
            }
            else if (testType == typeof(long))
            {
                TestValueType(long.MinValue + 5, long.MaxValue);
            }
            else if (testType == typeof(ulong))
            {
                TestValueType(ulong.MinValue + 5, ulong.MaxValue);
            }
            else if (testType == typeof(bool))
            {
                TestValueType(true, false);
            }
            else if (testType == typeof(char))
            {
                TestValueType('z', ' ');
            }
            else if (testType == typeof(float))
            {
                TestValueType(float.MinValue + 5.12345678f, float.MaxValue);
            }
            else if (testType == typeof(double))
            {
                TestValueType(double.MinValue + 5.12345678, double.MaxValue);
            }
            else if (testType == typeof(ByteEnum))
            {
                TestValueType(ByteEnum.B, ByteEnum.C);
            }
            else if (testType == typeof(SByteEnum))
            {
                TestValueType(SByteEnum.B, SByteEnum.C);
            }
            else if (testType == typeof(ShortEnum))
            {
                TestValueType(ShortEnum.B, ShortEnum.C);
            }
            else if (testType == typeof(UShortEnum))
            {
                TestValueType(UShortEnum.B, UShortEnum.C);
            }
            else if (testType == typeof(IntEnum))
            {
                TestValueType(IntEnum.B, IntEnum.C);
            }
            else if (testType == typeof(UIntEnum))
            {
                TestValueType(UIntEnum.B, UIntEnum.C);
            }
            else if (testType == typeof(LongEnum))
            {
                TestValueType(LongEnum.B, LongEnum.C);
            }
            else if (testType == typeof(ULongEnum))
            {
                TestValueType(ULongEnum.B, ULongEnum.C);
            }
            else if (testType == typeof(Vector2))
            {
                TestValueType(
                    new Vector2(5, 10),
                    new Vector2(15, 20));
            }
            else if (testType == typeof(Vector3))
            {
                TestValueType(
                    new Vector3(5, 10, 15),
                    new Vector3(20, 25, 30));
            }
            else if (testType == typeof(Vector2Int))
            {
                TestValueType(
                    new Vector2Int(5, 10),
                    new Vector2Int(15, 20));
            }
            else if (testType == typeof(Vector3Int))
            {
                TestValueType(
                    new Vector3Int(5, 10, 15),
                    new Vector3Int(20, 25, 30));
            }
            else if (testType == typeof(Vector4))
            {
                TestValueType(
                    new Vector4(5, 10, 15, 20),
                    new Vector4(25, 30, 35, 40));
            }
            else if (testType == typeof(Quaternion))
            {
                TestValueType(
                    new Quaternion(5, 10, 15, 20),
                    new Quaternion(25, 30, 35, 40));
            }
            else if (testType == typeof(Color))
            {
                TestValueType(
                    new Color(1, 0, 0),
                    new Color(0, 1, 1));
            }
            else if (testType == typeof(Color32))
            {
                TestValueType(
                    new Color32(255, 0, 0, 128),
                    new Color32(0, 255, 255, 255));
            }
            else if (testType == typeof(Ray))
            {
                TestValueType(
                    new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                    new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)));
            }
            else if (testType == typeof(Ray2D))
            {
                TestValueType(
                    new Ray2D(new Vector2(0, 1), new Vector2(2, 3)),
                    new Ray2D(new Vector2(4, 5), new Vector2(6, 7)));
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                TestValueType(NetworkVariableTestStruct.GetTestStruct(), NetworkVariableTestStruct.GetTestStruct());
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                TestValueType(new FixedString32Bytes("foobar"), new FixedString32Bytes("12345678901234567890123456789"));
            }
        }

        [Test]
        public void WhenSerializingAndDeserializingValueTypeNativeArrayNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                TestValueTypeNativeArray(
                    new NativeArray<byte>(new byte[] { byte.MinValue + 5, byte.MaxValue }, Allocator.Temp),
                    new NativeArray<byte>(new byte[] { 0, byte.MinValue + 10, byte.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(sbyte))
            {
                TestValueTypeNativeArray(
                    new NativeArray<sbyte>(new sbyte[] { sbyte.MinValue + 5, sbyte.MaxValue }, Allocator.Temp),
                    new NativeArray<sbyte>(new sbyte[] { 0, sbyte.MinValue + 10, sbyte.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(short))
            {
                TestValueTypeNativeArray(
                    new NativeArray<short>(new short[] { short.MinValue + 5, short.MaxValue }, Allocator.Temp),
                    new NativeArray<short>(new short[] { 0, short.MinValue + 10, short.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(ushort))
            {
                TestValueTypeNativeArray(
                    new NativeArray<ushort>(new ushort[] { ushort.MinValue + 5, ushort.MaxValue }, Allocator.Temp),
                    new NativeArray<ushort>(new ushort[] { 0, ushort.MinValue + 10, ushort.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(int))
            {
                TestValueTypeNativeArray(
                    new NativeArray<int>(new int[] { int.MinValue + 5, int.MaxValue }, Allocator.Temp),
                    new NativeArray<int>(new int[] { 0, int.MinValue + 10, int.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(uint))
            {
                TestValueTypeNativeArray(
                    new NativeArray<uint>(new uint[] { uint.MinValue + 5, uint.MaxValue }, Allocator.Temp),
                    new NativeArray<uint>(new uint[] { 0, uint.MinValue + 10, uint.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(long))
            {
                TestValueTypeNativeArray(
                    new NativeArray<long>(new long[] { long.MinValue + 5, long.MaxValue }, Allocator.Temp),
                    new NativeArray<long>(new long[] { 0, long.MinValue + 10, long.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(ulong))
            {
                TestValueTypeNativeArray(
                    new NativeArray<ulong>(new ulong[] { ulong.MinValue + 5, ulong.MaxValue }, Allocator.Temp),
                    new NativeArray<ulong>(new ulong[] { 0, ulong.MinValue + 10, ulong.MaxValue - 10 }, Allocator.Temp));
            }
            else if (testType == typeof(bool))
            {
                TestValueTypeNativeArray(
                    new NativeArray<bool>(new bool[] { true, false, true }, Allocator.Temp),
                    new NativeArray<bool>(new bool[] { false, true, false, true, false }, Allocator.Temp));
            }
            else if (testType == typeof(char))
            {
                TestValueTypeNativeArray(
                    new NativeArray<char>(new char[] { 'z', ' ', '?' }, Allocator.Temp),
                    new NativeArray<char>(new char[] { 'n', 'e', 'w', ' ', 'v', 'a', 'l', 'u', 'e' }, Allocator.Temp));
            }
            else if (testType == typeof(float))
            {
                TestValueTypeNativeArray(
                    new NativeArray<float>(new float[] { float.MinValue + 5.12345678f, float.MaxValue }, Allocator.Temp),
                    new NativeArray<float>(new float[] { 0, float.MinValue + 10.987654321f, float.MaxValue - 10.135792468f }, Allocator.Temp));
            }
            else if (testType == typeof(double))
            {
                TestValueTypeNativeArray(
                    new NativeArray<double>(new double[] { double.MinValue + 5.12345678, double.MaxValue }, Allocator.Temp),
                    new NativeArray<double>(new double[] { 0, double.MinValue + 10.987654321, double.MaxValue - 10.135792468 }, Allocator.Temp));
            }
            else if (testType == typeof(ByteEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<ByteEnum>(new ByteEnum[] { ByteEnum.C, ByteEnum.B, ByteEnum.A }, Allocator.Temp),
                    new NativeArray<ByteEnum>(new ByteEnum[] { ByteEnum.B, ByteEnum.C, ByteEnum.B, ByteEnum.A, ByteEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(SByteEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<SByteEnum>(new SByteEnum[] { SByteEnum.C, SByteEnum.B, SByteEnum.A }, Allocator.Temp),
                    new NativeArray<SByteEnum>(new SByteEnum[] { SByteEnum.B, SByteEnum.C, SByteEnum.B, SByteEnum.A, SByteEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(ShortEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<ShortEnum>(new ShortEnum[] { ShortEnum.C, ShortEnum.B, ShortEnum.A }, Allocator.Temp),
                    new NativeArray<ShortEnum>(new ShortEnum[] { ShortEnum.B, ShortEnum.C, ShortEnum.B, ShortEnum.A, ShortEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(UShortEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<UShortEnum>(new UShortEnum[] { UShortEnum.C, UShortEnum.B, UShortEnum.A }, Allocator.Temp),
                    new NativeArray<UShortEnum>(new UShortEnum[] { UShortEnum.B, UShortEnum.C, UShortEnum.B, UShortEnum.A, UShortEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(IntEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<IntEnum>(new IntEnum[] { IntEnum.C, IntEnum.B, IntEnum.A }, Allocator.Temp),
                    new NativeArray<IntEnum>(new IntEnum[] { IntEnum.B, IntEnum.C, IntEnum.B, IntEnum.A, IntEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(UIntEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<UIntEnum>(new UIntEnum[] { UIntEnum.C, UIntEnum.B, UIntEnum.A }, Allocator.Temp),
                    new NativeArray<UIntEnum>(new UIntEnum[] { UIntEnum.B, UIntEnum.C, UIntEnum.B, UIntEnum.A, UIntEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(LongEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<LongEnum>(new LongEnum[] { LongEnum.C, LongEnum.B, LongEnum.A }, Allocator.Temp),
                    new NativeArray<LongEnum>(new LongEnum[] { LongEnum.B, LongEnum.C, LongEnum.B, LongEnum.A, LongEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(ULongEnum))
            {
                TestValueTypeNativeArray(
                    new NativeArray<ULongEnum>(new ULongEnum[] { ULongEnum.C, ULongEnum.B, ULongEnum.A }, Allocator.Temp),
                    new NativeArray<ULongEnum>(new ULongEnum[] { ULongEnum.B, ULongEnum.C, ULongEnum.B, ULongEnum.A, ULongEnum.C }, Allocator.Temp));
            }
            else if (testType == typeof(Vector2))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Vector2>(new Vector2[] { new Vector2(5, 10), new Vector2(15, 20) }, Allocator.Temp),
                    new NativeArray<Vector2>(new Vector2[] { new Vector2(25, 30), new Vector2(35, 40), new Vector2(45, 50) }, Allocator.Temp));
            }
            else if (testType == typeof(Vector3))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Vector3>(new Vector3[] { new Vector3(5, 10, 15), new Vector3(20, 25, 30) }, Allocator.Temp),
                    new NativeArray<Vector3>(new Vector3[] { new Vector3(35, 40, 45), new Vector3(50, 55, 60), new Vector3(65, 70, 75) }, Allocator.Temp));
            }
            else if (testType == typeof(Vector2Int))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Vector2Int>(new Vector2Int[] { new Vector2Int(5, 10), new Vector2Int(15, 20) }, Allocator.Temp),
                    new NativeArray<Vector2Int>(new Vector2Int[] { new Vector2Int(25, 30), new Vector2Int(35, 40), new Vector2Int(45, 50) }, Allocator.Temp));
            }
            else if (testType == typeof(Vector3Int))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Vector3Int>(new Vector3Int[] { new Vector3Int(5, 10, 15), new Vector3Int(20, 25, 30) }, Allocator.Temp),
                    new NativeArray<Vector3Int>(new Vector3Int[] { new Vector3Int(35, 40, 45), new Vector3Int(50, 55, 60), new Vector3Int(65, 70, 75) }, Allocator.Temp));
            }
            else if (testType == typeof(Vector4))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Vector4>(new Vector4[] { new Vector4(5, 10, 15, 20), new Vector4(25, 30, 35, 40) }, Allocator.Temp),
                    new NativeArray<Vector4>(new Vector4[] { new Vector4(45, 50, 55, 60), new Vector4(65, 70, 75, 80), new Vector4(85, 90, 95, 100) }, Allocator.Temp));
            }
            else if (testType == typeof(Quaternion))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Quaternion>(new Quaternion[] { new Quaternion(5, 10, 15, 20), new Quaternion(25, 30, 35, 40) }, Allocator.Temp),
                    new NativeArray<Quaternion>(new Quaternion[] { new Quaternion(45, 50, 55, 60), new Quaternion(65, 70, 75, 80), new Quaternion(85, 90, 95, 100) }, Allocator.Temp));
            }
            else if (testType == typeof(Color))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Color>(new Color[] { new Color(.5f, .10f, .15f), new Color(.20f, .25f, .30f) }, Allocator.Temp),
                    new NativeArray<Color>(new Color[] { new Color(.35f, .40f, .45f), new Color(.50f, .55f, .60f), new Color(.65f, .70f, .75f) }, Allocator.Temp));
            }
            else if (testType == typeof(Color32))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Color32>(new Color32[] { new Color32(5, 10, 15, 20), new Color32(25, 30, 35, 40) }, Allocator.Temp),
                    new NativeArray<Color32>(new Color32[] { new Color32(45, 50, 55, 60), new Color32(65, 70, 75, 80), new Color32(85, 90, 95, 100) }, Allocator.Temp));
            }
            else if (testType == typeof(Ray))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Ray>(new Ray[]
                    {
                        new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                        new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)),
                    }, Allocator.Temp),
                    new NativeArray<Ray>(new Ray[]
                    {
                        new Ray(new Vector3(12, 13, 14), new Vector3(15, 16, 17)),
                        new Ray(new Vector3(18, 19, 20), new Vector3(21, 22, 23)),
                        new Ray(new Vector3(24, 25, 26), new Vector3(27, 28, 29)),
                    }, Allocator.Temp));
            }
            else if (testType == typeof(Ray2D))
            {
                TestValueTypeNativeArray(
                    new NativeArray<Ray2D>(new Ray2D[]
                    {
                        new Ray2D(new Vector2(0, 1), new Vector2(3, 4)),
                        new Ray2D(new Vector2(6, 7), new Vector2(9, 10)),
                    }, Allocator.Temp),
                    new NativeArray<Ray2D>(new Ray2D[]
                    {
                        new Ray2D(new Vector2(12, 13), new Vector2(15, 16)),
                        new Ray2D(new Vector2(18, 19), new Vector2(21, 22)),
                        new Ray2D(new Vector2(24, 25), new Vector2(27, 28)),
                    }, Allocator.Temp));
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                TestValueTypeNativeArray(
                    new NativeArray<NetworkVariableTestStruct>(new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    }, Allocator.Temp),
                    new NativeArray<NetworkVariableTestStruct>(new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    }, Allocator.Temp));
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                TestValueTypeNativeArray(
                    new NativeArray<FixedString32Bytes>(new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("foobar"),
                        new FixedString32Bytes("12345678901234567890123456789")
                    }, Allocator.Temp),
                    new NativeArray<FixedString32Bytes>(new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("BazQux"),
                        new FixedString32Bytes("98765432109876543210987654321"),
                        new FixedString32Bytes("FixedString32Bytes")
                    }, Allocator.Temp));
            }
        }

        public delegate T GetRandomElement<T>(System.Random rand);

        public unsafe T RandGenBytes<T>(System.Random rand) where T : unmanaged
        {
            var t = new T();
            T* tPtr = &t;
            var s = new Span<byte>(tPtr, sizeof(T));
            rand.NextBytes(s);
            return t;
        }

        public FixedString32Bytes RandGenFixedString32(System.Random rand)
        {
            var s = new FixedString32Bytes();
            var len = rand.Next(s.Capacity);
            s.Length = len;
            for (var i = 0; i < len; ++i)
            {
                // Ascii visible character range
                s[i] = (byte)rand.Next(32, 126);
            }

            return s;
        }
        public string ArrayStr<T>(NativeArray<T> arr) where T : unmanaged
        {
            var str = "[";
            var comma = false;
            foreach (var item in arr)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item}";
            }

            str += "]";
            return str;
        }

        public (NativeArray<T> original, NativeArray<T> original2, NativeArray<T> changed, NativeArray<T> changed2) GetArarys<T>(GetRandomElement<T> generator) where T : unmanaged
        {

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Changes = rand.Next(12, 16);
            var changed2Adds = rand.Next(-16, 16);

            var original = new NativeArray<T>(originalSize, Allocator.Temp);
            var changed = new NativeArray<T>(changedSize, Allocator.Temp);
            var original2 = new NativeArray<T>(originalSize, Allocator.Temp);
            var changed2 = new NativeArray<T>(originalSize + changed2Adds, Allocator.Temp);


            for (var i = 0; i < originalSize; ++i)
            {
                var item = generator(rand);
                original[i] = item;
                original2[i] = item;
                if (i < changed2.Length)
                {
                    changed2[i] = item;
                }
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var item = generator(rand);
                changed[i] = item;
            }

            for (var i = 0; i < changed2Changes; ++i)
            {
                var idx = rand.Next(changed2.Length - 1);
                var item = generator(rand);
                changed2[idx] = item;
            }

            for (var i = 0; i < changed2Adds; ++i)
            {
                var item = generator(rand);
                changed2[originalSize + i] = item;
            }

            VerboseDebug($"Original: {ArrayStr(original)}");
            VerboseDebug($"Changed: {ArrayStr(changed)}");
            VerboseDebug($"Original2: {ArrayStr(original2)}");
            VerboseDebug($"Changed2: {ArrayStr(changed2)}");
            return (original, original2, changed, changed2);
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeValueTypeNativeArrayNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Ray), typeof(Ray2D),
                typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<byte>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(sbyte))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<sbyte>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(short))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<short>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(ushort))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<ushort>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(int))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<int>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(uint))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<uint>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(long))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<long>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(ulong))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<ulong>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(bool))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<bool>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(char))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<char>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(float))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<float>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(double))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<double>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Vector2))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Vector3))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Vector2Int))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Vector2Int(rand.Next(), rand.Next())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Vector3Int))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Vector4))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Quaternion))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Color))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Color32))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Color32((byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next())
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Ray))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Ray(
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(Ray2D))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(
                    (rand) => new Ray2D(
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenBytes<NetworkVariableTestStruct>);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                (var original, var original2, var changed, var changed2) = GetArarys(RandGenFixedString32);
                TestValueTypeNativeArray(original, changed);
                TestValueTypeNativeArray(original2, changed2);
            }
        }


        public string ListStr<T>(List<T> list)
        {
            var str = "[";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item}";
            }

            str += "]";
            return str;
        }

        public string HashSetStr<T>(HashSet<T> list) where T : IEquatable<T>
        {
            var str = "{";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item}";
            }

            str += "}";
            return str;
        }

        public string DictionaryStr<TKey, TVal>(Dictionary<TKey, TVal> list)
            where TKey : IEquatable<TKey>
        {
            var str = "{";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item.Key}: {item.Value}";
            }

            str += "}";
            return str;
        }

        public (List<T> original, List<T> original2, List<T> changed, List<T> changed2) GetLists<T>(GetRandomElement<T> generator)
        {
            var original = new List<T>();
            var changed = new List<T>();
            var original2 = new List<T>();
            var changed2 = new List<T>();

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Changes = rand.Next(12, 16);
            var changed2Adds = rand.Next(-16, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var item = generator(rand);
                original.Add(item);
                original2.Add(item);
                changed2.Add(item);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var item = generator(rand);
                changed.Add(item);
            }

            for (var i = 0; i < changed2Changes; ++i)
            {
                var idx = rand.Next(changed2.Count - 1);
                var item = generator(rand);
                changed2[idx] = item;
            }

            if (changed2Adds < 0)
            {
                changed2.RemoveRange(changed2.Count + changed2Adds, -changed2Adds);
            }
            else
            {
                for (var i = 0; i < changed2Adds; ++i)
                {
                    var item = generator(rand);
                    changed2.Add(item);
                }

            }

            VerboseDebug($"Original: {ListStr(original)}");
            VerboseDebug($"Changed: {ListStr(changed)}");
            VerboseDebug($"Original2: {ListStr(original2)}");
            VerboseDebug($"Changed2: {ListStr(changed2)}");
            return (original, original2, changed, changed2);
        }

        public (HashSet<T> original, HashSet<T> original2, HashSet<T> changed, HashSet<T> changed2) GetHashSets<T>(GetRandomElement<T> generator) where T : IEquatable<T>
        {
            var original = new HashSet<T>();
            var changed = new HashSet<T>();
            var original2 = new HashSet<T>();
            var changed2 = new HashSet<T>();

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Removes = rand.Next(12, 16);
            var changed2Adds = rand.Next(12, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var item = generator(rand);
                while (original.Contains(item))
                {
                    item = generator(rand);
                }
                original.Add(item);
                original2.Add(item);
                changed2.Add(item);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var item = generator(rand);
                while (changed.Contains(item))
                {
                    item = generator(rand);
                }
                changed.Add(item);
            }

            for (var i = 0; i < changed2Removes; ++i)
            {
                var which = rand.Next(changed2.Count());
                T toRemove = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        toRemove = check;
                        break;
                    }
                    --which;
                }

                changed2.Remove(toRemove);
            }

            for (var i = 0; i < changed2Adds; ++i)
            {
                var item = generator(rand);
                while (changed2.Contains(item))
                {
                    item = generator(rand);
                }
                changed2.Add(item);
            }

            VerboseDebug($"Original: {HashSetStr(original)}");
            VerboseDebug($"Changed: {HashSetStr(changed)}");
            VerboseDebug($"Original2: {HashSetStr(original2)}");
            VerboseDebug($"Changed2: {HashSetStr(changed2)}");
            return (original, original2, changed, changed2);
        }


        public (Dictionary<TKey, TVal> original, Dictionary<TKey, TVal> original2, Dictionary<TKey, TVal> changed, Dictionary<TKey, TVal> changed2) GetDictionaries<TKey, TVal>(GetRandomElement<TKey> keyGenerator, GetRandomElement<TVal> valGenerator)
            where TKey : IEquatable<TKey>
        {
            var original = new Dictionary<TKey, TVal>();
            var changed = new Dictionary<TKey, TVal>();
            var original2 = new Dictionary<TKey, TVal>();
            var changed2 = new Dictionary<TKey, TVal>();

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Removes = rand.Next(12, 16);
            var changed2Adds = rand.Next(12, 16);
            var changed2Changes = rand.Next(12, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var key = keyGenerator(rand);
                while (original.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                original.Add(key, val);
                original2.Add(key, val);
                changed2.Add(key, val);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var key = keyGenerator(rand);
                while (changed.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                changed.Add(key, val);
            }

            for (var i = 0; i < changed2Removes; ++i)
            {
                var which = rand.Next(changed2.Count());
                TKey toRemove = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        toRemove = check.Key;
                        break;
                    }
                    --which;
                }

                changed2.Remove(toRemove);
            }

            for (var i = 0; i < changed2Changes; ++i)
            {
                var which = rand.Next(changed2.Count());
                TKey key = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        key = check.Key;
                        break;
                    }
                    --which;
                }

                var val = valGenerator(rand);
                changed2[key] = val;
            }

            for (var i = 0; i < changed2Adds; ++i)
            {
                var key = keyGenerator(rand);
                while (changed2.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                changed2.Add(key, val);
            }

            VerboseDebug($"Original: {DictionaryStr(original)}");
            VerboseDebug($"Changed: {DictionaryStr(changed)}");
            VerboseDebug($"Original2: {DictionaryStr(original2)}");
            VerboseDebug($"Changed2: {DictionaryStr(changed2)}");
            return (original, original2, changed, changed2);
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeListNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Ray), typeof(Ray2D),
                typeof(NetworkVariableTestClass), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<byte>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(sbyte))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<sbyte>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(short))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<short>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(ushort))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<ushort>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(int))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<int>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(uint))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<uint>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(long))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<long>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(ulong))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<ulong>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(bool))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<bool>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(char))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<char>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(float))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<float>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(double))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenBytes<double>);
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Vector2))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Vector3))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Vector2Int))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Vector2Int(rand.Next(), rand.Next())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Vector3Int))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Vector4))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Quaternion))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Color))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Color32))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Color32((byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next())
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Ray))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Ray(
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(Ray2D))
            {
                (var original, var original2, var changed, var changed2) = GetLists(
                    (rand) => new Ray2D(
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(NetworkVariableTestClass))
            {
                (var original, var original2, var changed, var changed2) = GetLists((rand) =>
                {
                    return new NetworkVariableTestClass { Data = RandGenBytes<NetworkVariableTestStruct>(rand) };
                });
                TestList(original, changed);
                TestList(original2, changed2);
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                (var original, var original2, var changed, var changed2) = GetLists(RandGenFixedString32);
                TestList(original, changed);
                TestList(original2, changed2);
            }
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeHashSetNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(HashableNetworkVariableTestClass), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<byte>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(sbyte))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<sbyte>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(short))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<short>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(ushort))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<ushort>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(int))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<int>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(uint))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<uint>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(long))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<long>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(ulong))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<ulong>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(bool))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<bool>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(char))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<char>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(float))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<float>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(double))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenBytes<double>);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector2))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector3))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector2Int))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Vector2Int(rand.Next(), rand.Next())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector3Int))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector4))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Quaternion))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(Color))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(
                    (rand) => new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(HashableNetworkVariableTestClass))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets((rand) =>
                {
                    return new HashableNetworkVariableTestClass { Data = RandGenBytes<HashableNetworkVariableTestStruct>(rand) };
                });
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                (var original, var original2, var changed, var changed2) = GetHashSets(RandGenFixedString32);
                TestHashSet(original, changed);
                TestHashSet(original2, changed2);
            }
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeDictionaryNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(ulong), typeof(Vector2), typeof(HashMapKeyClass))] Type keyType,
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(HashMapValClass), typeof(FixedString32Bytes))]
            Type valType)
        {
            if (valType == typeof(byte))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<byte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<byte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<byte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<byte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(sbyte))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<sbyte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<sbyte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<sbyte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<sbyte>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(short))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<short>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<short>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<short>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<short>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(ushort))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<ushort>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<ushort>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<ushort>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<ushort>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<int>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<int>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<int>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<int>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(uint))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<uint>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<uint>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<uint>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<uint>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(long))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<long>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<long>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<long>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<long>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(ulong))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<ulong>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<ulong>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<ulong>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<ulong>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(bool))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<bool>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<bool>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<bool>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<bool>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(char))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<char>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<char>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<char>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<char>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(float))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<float>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<float>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<float>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<float>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(double))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenBytes<double>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenBytes<double>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<double>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenBytes<double>);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Vector2))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Vector3))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Vector2Int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Vector3Int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Vector4))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(Quaternion))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(HashMapValClass))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, (rand) => new HashMapValClass { Data = RandGenBytes<HashMapValStruct>(rand) });
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, (rand) => new HashMapValClass { Data = RandGenBytes<HashMapValStruct>(rand) });
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new HashMapValClass { Data = RandGenBytes<HashMapValStruct>(rand) });
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, (rand) => new HashMapValClass { Data = RandGenBytes<HashMapValStruct>(rand) });
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
            else if (valType == typeof(FixedString32Bytes))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<byte>, RandGenFixedString32);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries(RandGenBytes<ulong>, RandGenFixedString32);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenFixedString32);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyClass))
                {
                    (var original, var original2, var changed, var changed2) = GetDictionaries((rand) => new HashMapKeyClass { Data = RandGenBytes<HashMapKeyStruct>(rand) }, RandGenFixedString32);
                    TestDictionary(original, changed);
                    TestDictionary(original2, changed2);
                }
            }
        }


#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        [Test]
        public void WhenSerializingAndDeserializingValueTypeNativeListNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                TestValueTypeNativeList(
                    new NativeList<byte>(Allocator.Temp) { byte.MinValue + 5, byte.MaxValue },
                    new NativeList<byte>(Allocator.Temp) { 0, byte.MinValue + 10, byte.MaxValue - 10 });
            }
            else if (testType == typeof(sbyte))
            {
                TestValueTypeNativeList(
                    new NativeList<sbyte>(Allocator.Temp) { sbyte.MinValue + 5, sbyte.MaxValue },
                    new NativeList<sbyte>(Allocator.Temp) { 0, sbyte.MinValue + 10, sbyte.MaxValue - 10 });
            }
            else if (testType == typeof(short))
            {
                TestValueTypeNativeList(
                    new NativeList<short>(Allocator.Temp) { short.MinValue + 5, short.MaxValue },
                    new NativeList<short>(Allocator.Temp) { 0, short.MinValue + 10, short.MaxValue - 10 });
            }
            else if (testType == typeof(ushort))
            {
                TestValueTypeNativeList(
                    new NativeList<ushort>(Allocator.Temp) { ushort.MinValue + 5, ushort.MaxValue },
                    new NativeList<ushort>(Allocator.Temp) { 0, ushort.MinValue + 10, ushort.MaxValue - 10 });
            }
            else if (testType == typeof(int))
            {
                TestValueTypeNativeList(
                    new NativeList<int>(Allocator.Temp) { int.MinValue + 5, int.MaxValue },
                    new NativeList<int>(Allocator.Temp) { 0, int.MinValue + 10, int.MaxValue - 10 });
            }
            else if (testType == typeof(uint))
            {
                TestValueTypeNativeList(
                    new NativeList<uint>(Allocator.Temp) { uint.MinValue + 5, uint.MaxValue },
                    new NativeList<uint>(Allocator.Temp) { 0, uint.MinValue + 10, uint.MaxValue - 10 });
            }
            else if (testType == typeof(long))
            {
                TestValueTypeNativeList(
                    new NativeList<long>(Allocator.Temp) { long.MinValue + 5, long.MaxValue },
                    new NativeList<long>(Allocator.Temp) { 0, long.MinValue + 10, long.MaxValue - 10 });
            }
            else if (testType == typeof(ulong))
            {
                TestValueTypeNativeList(
                    new NativeList<ulong>(Allocator.Temp) { ulong.MinValue + 5, ulong.MaxValue },
                    new NativeList<ulong>(Allocator.Temp) { 0, ulong.MinValue + 10, ulong.MaxValue - 10 });
            }
            else if (testType == typeof(bool))
            {
                TestValueTypeNativeList(
                    new NativeList<bool>(Allocator.Temp) { true, false, true },
                    new NativeList<bool>(Allocator.Temp) { false, true, false, true, false });
            }
            else if (testType == typeof(char))
            {
                TestValueTypeNativeList(
                    new NativeList<char>(Allocator.Temp) { 'z', ' ', '?' },
                    new NativeList<char>(Allocator.Temp) { 'n', 'e', 'w', ' ', 'v', 'a', 'l', 'u', 'e' });
            }
            else if (testType == typeof(float))
            {
                TestValueTypeNativeList(
                    new NativeList<float>(Allocator.Temp) { float.MinValue + 5.12345678f, float.MaxValue },
                    new NativeList<float>(Allocator.Temp) { 0, float.MinValue + 10.987654321f, float.MaxValue - 10.135792468f });
            }
            else if (testType == typeof(double))
            {
                TestValueTypeNativeList(
                    new NativeList<double>(Allocator.Temp) { double.MinValue + 5.12345678, double.MaxValue },
                    new NativeList<double>(Allocator.Temp) { 0, double.MinValue + 10.987654321, double.MaxValue - 10.135792468 });
            }
            else if (testType == typeof(ByteEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<ByteEnum>(Allocator.Temp) { ByteEnum.C, ByteEnum.B, ByteEnum.A },
                    new NativeList<ByteEnum>(Allocator.Temp) { ByteEnum.B, ByteEnum.C, ByteEnum.B, ByteEnum.A, ByteEnum.C });
            }
            else if (testType == typeof(SByteEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<SByteEnum>(Allocator.Temp) { SByteEnum.C, SByteEnum.B, SByteEnum.A },
                    new NativeList<SByteEnum>(Allocator.Temp) { SByteEnum.B, SByteEnum.C, SByteEnum.B, SByteEnum.A, SByteEnum.C });
            }
            else if (testType == typeof(ShortEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<ShortEnum>(Allocator.Temp) { ShortEnum.C, ShortEnum.B, ShortEnum.A },
                    new NativeList<ShortEnum>(Allocator.Temp) { ShortEnum.B, ShortEnum.C, ShortEnum.B, ShortEnum.A, ShortEnum.C });
            }
            else if (testType == typeof(UShortEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<UShortEnum>(Allocator.Temp) { UShortEnum.C, UShortEnum.B, UShortEnum.A },
                    new NativeList<UShortEnum>(Allocator.Temp) { UShortEnum.B, UShortEnum.C, UShortEnum.B, UShortEnum.A, UShortEnum.C });
            }
            else if (testType == typeof(IntEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<IntEnum>(Allocator.Temp) { IntEnum.C, IntEnum.B, IntEnum.A },
                    new NativeList<IntEnum>(Allocator.Temp) { IntEnum.B, IntEnum.C, IntEnum.B, IntEnum.A, IntEnum.C });
            }
            else if (testType == typeof(UIntEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<UIntEnum>(Allocator.Temp) { UIntEnum.C, UIntEnum.B, UIntEnum.A },
                    new NativeList<UIntEnum>(Allocator.Temp) { UIntEnum.B, UIntEnum.C, UIntEnum.B, UIntEnum.A, UIntEnum.C });
            }
            else if (testType == typeof(LongEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<LongEnum>(Allocator.Temp) { LongEnum.C, LongEnum.B, LongEnum.A },
                    new NativeList<LongEnum>(Allocator.Temp) { LongEnum.B, LongEnum.C, LongEnum.B, LongEnum.A, LongEnum.C });
            }
            else if (testType == typeof(ULongEnum))
            {
                TestValueTypeNativeList(
                    new NativeList<ULongEnum>(Allocator.Temp) { ULongEnum.C, ULongEnum.B, ULongEnum.A },
                    new NativeList<ULongEnum>(Allocator.Temp) { ULongEnum.B, ULongEnum.C, ULongEnum.B, ULongEnum.A, ULongEnum.C });
            }
            else if (testType == typeof(Vector2))
            {
                TestValueTypeNativeList(
                    new NativeList<Vector2>(Allocator.Temp) { new Vector2(5, 10), new Vector2(15, 20) },
                    new NativeList<Vector2>(Allocator.Temp) { new Vector2(25, 30), new Vector2(35, 40), new Vector2(45, 50) });
            }
            else if (testType == typeof(Vector3))
            {
                TestValueTypeNativeList(
                    new NativeList<Vector3>(Allocator.Temp) { new Vector3(5, 10, 15), new Vector3(20, 25, 30) },
                    new NativeList<Vector3>(Allocator.Temp) { new Vector3(35, 40, 45), new Vector3(50, 55, 60), new Vector3(65, 70, 75) });
            }
            else if (testType == typeof(Vector2Int))
            {
                TestValueTypeNativeList(
                    new NativeList<Vector2Int>(Allocator.Temp) { new Vector2Int(5, 10), new Vector2Int(15, 20) },
                    new NativeList<Vector2Int>(Allocator.Temp) { new Vector2Int(25, 30), new Vector2Int(35, 40), new Vector2Int(45, 50) });
            }
            else if (testType == typeof(Vector3Int))
            {
                TestValueTypeNativeList(
                    new NativeList<Vector3Int>(Allocator.Temp) { new Vector3Int(5, 10, 15), new Vector3Int(20, 25, 30) },
                    new NativeList<Vector3Int>(Allocator.Temp) { new Vector3Int(35, 40, 45), new Vector3Int(50, 55, 60), new Vector3Int(65, 70, 75) });
            }
            else if (testType == typeof(Vector4))
            {
                TestValueTypeNativeList(
                    new NativeList<Vector4>(Allocator.Temp) { new Vector4(5, 10, 15, 20), new Vector4(25, 30, 35, 40) },
                    new NativeList<Vector4>(Allocator.Temp) { new Vector4(45, 50, 55, 60), new Vector4(65, 70, 75, 80), new Vector4(85, 90, 95, 100) });
            }
            else if (testType == typeof(Quaternion))
            {
                TestValueTypeNativeList(
                    new NativeList<Quaternion>(Allocator.Temp) { new Quaternion(5, 10, 15, 20), new Quaternion(25, 30, 35, 40) },
                    new NativeList<Quaternion>(Allocator.Temp) { new Quaternion(45, 50, 55, 60), new Quaternion(65, 70, 75, 80), new Quaternion(85, 90, 95, 100) });
            }
            else if (testType == typeof(Color))
            {
                TestValueTypeNativeList(
                    new NativeList<Color>(Allocator.Temp) { new Color(.5f, .10f, .15f), new Color(.20f, .25f, .30f) },
                    new NativeList<Color>(Allocator.Temp) { new Color(.35f, .40f, .45f), new Color(.50f, .55f, .60f), new Color(.65f, .70f, .75f) });
            }
            else if (testType == typeof(Color32))
            {
                TestValueTypeNativeList(
                    new NativeList<Color32>(Allocator.Temp) { new Color32(5, 10, 15, 20), new Color32(25, 30, 35, 40) },
                    new NativeList<Color32>(Allocator.Temp) { new Color32(45, 50, 55, 60), new Color32(65, 70, 75, 80), new Color32(85, 90, 95, 100) });
            }
            else if (testType == typeof(Ray))
            {
                TestValueTypeNativeList(
                    new NativeList<Ray>(Allocator.Temp)
                    {
                        new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                        new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)),
                    },
                    new NativeList<Ray>(Allocator.Temp)
                    {
                        new Ray(new Vector3(12, 13, 14), new Vector3(15, 16, 17)),
                        new Ray(new Vector3(18, 19, 20), new Vector3(21, 22, 23)),
                        new Ray(new Vector3(24, 25, 26), new Vector3(27, 28, 29)),
                    });
            }
            else if (testType == typeof(Ray2D))
            {
                TestValueTypeNativeList(
                    new NativeList<Ray2D>(Allocator.Temp)
                    {
                        new Ray2D(new Vector2(0, 1), new Vector2(3, 4)),
                        new Ray2D(new Vector2(6, 7), new Vector2(9, 10)),
                    },
                    new NativeList<Ray2D>(Allocator.Temp)
                    {
                        new Ray2D(new Vector2(12, 13), new Vector2(15, 16)),
                        new Ray2D(new Vector2(18, 19), new Vector2(21, 22)),
                        new Ray2D(new Vector2(24, 25), new Vector2(27, 28)),
                    });
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                TestValueTypeNativeList(
                    new NativeList<NetworkVariableTestStruct>(Allocator.Temp)
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    },
                    new NativeList<NetworkVariableTestStruct>(Allocator.Temp)
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    });
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                TestValueTypeNativeList(
                    new NativeList<FixedString32Bytes>(Allocator.Temp)
                    {
                        new FixedString32Bytes("foobar"),
                        new FixedString32Bytes("12345678901234567890123456789")
                    },
                    new NativeList<FixedString32Bytes>(Allocator.Temp)
                    {
                        new FixedString32Bytes("BazQux"),
                        new FixedString32Bytes("98765432109876543210987654321"),
                        new FixedString32Bytes("FixedString32Bytes")
                    });
            }
        }

        public string NativeListStr<T>(NativeList<T> list) where T : unmanaged
        {
            var str = "[";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item}";
            }

            str += "]";
            return str;
        }

        public string NativeHashSetStr<T>(NativeHashSet<T> list) where T : unmanaged, IEquatable<T>
        {
            var str = "{";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item}";
            }

            str += "}";
            return str;
        }

        public string NativeHashMapStr<TKey, TVal>(NativeHashMap<TKey, TVal> list)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            var str = "{";
            var comma = false;
            foreach (var item in list)
            {
                if (comma)
                {
                    str += ", ";
                }

                comma = true;
                str += $"{item.Key}: {item.Value}";
            }

            str += "}";
            return str;
        }

        public (NativeList<T> original, NativeList<T> original2, NativeList<T> changed, NativeList<T> changed2) GetNativeLists<T>(GetRandomElement<T> generator) where T : unmanaged
        {
            var original = new NativeList<T>(Allocator.Temp);
            var changed = new NativeList<T>(Allocator.Temp);
            var original2 = new NativeList<T>(Allocator.Temp);
            var changed2 = new NativeList<T>(Allocator.Temp);

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Changes = rand.Next(12, 16);
            var changed2Adds = rand.Next(-16, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var item = generator(rand);
                original.Add(item);
                original2.Add(item);
                changed2.Add(item);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var item = generator(rand);
                changed.Add(item);
            }

            for (var i = 0; i < changed2Changes; ++i)
            {
                var idx = rand.Next(changed2.Length - 1);
                var item = generator(rand);
                changed2[idx] = item;
            }

            if (changed2Adds < 0)
            {
                changed2.Resize(changed2.Length + changed2Adds, NativeArrayOptions.UninitializedMemory);
            }
            else
            {
                for (var i = 0; i < changed2Adds; ++i)
                {
                    var item = generator(rand);
                    changed2.Add(item);
                }

            }

            VerboseDebug($"Original: {NativeListStr(original)}");
            VerboseDebug($"Changed: {NativeListStr(changed)}");
            VerboseDebug($"Original2: {NativeListStr(original2)}");
            VerboseDebug($"Changed2: {NativeListStr(changed2)}");
            return (original, original2, changed, changed2);
        }

        public (NativeHashSet<T> original, NativeHashSet<T> original2, NativeHashSet<T> changed, NativeHashSet<T> changed2) GetNativeHashSets<T>(GetRandomElement<T> generator) where T : unmanaged, IEquatable<T>
        {
            var original = new NativeHashSet<T>(16, Allocator.Temp);
            var changed = new NativeHashSet<T>(16, Allocator.Temp);
            var original2 = new NativeHashSet<T>(16, Allocator.Temp);
            var changed2 = new NativeHashSet<T>(16, Allocator.Temp);

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Removes = rand.Next(12, 16);
            var changed2Adds = rand.Next(12, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var item = generator(rand);
                while (original.Contains(item))
                {
                    item = generator(rand);
                }
                original.Add(item);
                original2.Add(item);
                changed2.Add(item);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var item = generator(rand);
                while (changed.Contains(item))
                {
                    item = generator(rand);
                }
                changed.Add(item);
            }

            for (var i = 0; i < changed2Removes; ++i)
            {
#if UTP_TRANSPORT_2_0_ABOVE
                var which = rand.Next(changed2.Count);
#else
                var which = rand.Next(changed2.Count());
#endif
                T toRemove = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        toRemove = check;
                        break;
                    }
                    --which;
                }

                changed2.Remove(toRemove);
            }

            for (var i = 0; i < changed2Adds; ++i)
            {
                var item = generator(rand);
                while (changed2.Contains(item))
                {
                    item = generator(rand);
                }
                changed2.Add(item);
            }

            VerboseDebug($"Original: {NativeHashSetStr(original)}");
            VerboseDebug($"Changed: {NativeHashSetStr(changed)}");
            VerboseDebug($"Original2: {NativeHashSetStr(original2)}");
            VerboseDebug($"Changed2: {NativeHashSetStr(changed2)}");
            return (original, original2, changed, changed2);
        }


        public (NativeHashMap<TKey, TVal> original, NativeHashMap<TKey, TVal> original2, NativeHashMap<TKey, TVal> changed, NativeHashMap<TKey, TVal> changed2) GetMaps<TKey, TVal>(GetRandomElement<TKey> keyGenerator, GetRandomElement<TVal> valGenerator)
            where TKey : unmanaged, IEquatable<TKey>
            where TVal : unmanaged
        {
            var original = new NativeHashMap<TKey, TVal>(16, Allocator.Temp);
            var changed = new NativeHashMap<TKey, TVal>(16, Allocator.Temp);
            var original2 = new NativeHashMap<TKey, TVal>(16, Allocator.Temp);
            var changed2 = new NativeHashMap<TKey, TVal>(16, Allocator.Temp);

            var rand = new System.Random();
            var originalSize = rand.Next(32, 64);
            var changedSize = rand.Next(32, 64);
            var changed2Removes = rand.Next(12, 16);
            var changed2Adds = rand.Next(12, 16);
            var changed2Changes = rand.Next(12, 16);
            for (var i = 0; i < originalSize; ++i)
            {
                var key = keyGenerator(rand);
                while (original.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                original.Add(key, val);
                original2.Add(key, val);
                changed2.Add(key, val);
            }
            for (var i = 0; i < changedSize; ++i)
            {
                var key = keyGenerator(rand);
                while (changed.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                changed.Add(key, val);
            }

            for (var i = 0; i < changed2Removes; ++i)
            {
#if UTP_TRANSPORT_2_0_ABOVE
                var which = rand.Next(changed2.Count);
#else
                var which = rand.Next(changed2.Count());
#endif
                TKey toRemove = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        toRemove = check.Key;
                        break;
                    }
                    --which;
                }

                changed2.Remove(toRemove);
            }

            for (var i = 0; i < changed2Changes; ++i)
            {
#if UTP_TRANSPORT_2_0_ABOVE
                var which = rand.Next(changed2.Count);
#else
                var which = rand.Next(changed2.Count());
#endif
                TKey key = default;
                foreach (var check in changed2)
                {
                    if (which == 0)
                    {
                        key = check.Key;
                        break;
                    }
                    --which;
                }

                var val = valGenerator(rand);
                changed2[key] = val;
            }

            for (var i = 0; i < changed2Adds; ++i)
            {
                var key = keyGenerator(rand);
                while (changed2.ContainsKey(key))
                {
                    key = keyGenerator(rand);
                }
                var val = valGenerator(rand);
                changed2.Add(key, val);
            }

            VerboseDebug($"Original: {NativeHashMapStr(original)}");
            VerboseDebug($"Changed: {NativeHashMapStr(changed)}");
            VerboseDebug($"Original2: {NativeHashMapStr(original2)}");
            VerboseDebug($"Changed2: {NativeHashMapStr(changed2)}");
            return (original, original2, changed, changed2);
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeValueTypeNativeListNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Ray), typeof(Ray2D),
                typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<byte>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(sbyte))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<sbyte>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(short))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<short>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(ushort))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<ushort>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<int>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(uint))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<uint>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(long))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<long>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(ulong))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<ulong>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(bool))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<bool>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(char))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<char>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(float))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<float>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(double))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<double>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Vector2))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Vector3))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Vector2Int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Vector2Int(rand.Next(), rand.Next())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Vector3Int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Vector4))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Quaternion))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Color))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Color32))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Color32((byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next(), (byte)rand.Next())
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Ray))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Ray(
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(Ray2D))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(
                    (rand) => new Ray2D(
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()),
                        new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                    )
                );
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenBytes<NetworkVariableTestStruct>);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                (var original, var original2, var changed, var changed2) = GetNativeLists(RandGenFixedString32);
                TestValueTypeNativeList(original, changed);
                TestValueTypeNativeList(original2, changed2);
            }
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeValueTypeNativeHashSetNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(HashableNetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<byte>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(sbyte))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<sbyte>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(short))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<short>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(ushort))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<ushort>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<int>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(uint))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<uint>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(long))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<long>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(ulong))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<ulong>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(bool))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<bool>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(char))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<char>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(float))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<float>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(double))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<double>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector2))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector3))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector2Int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Vector2Int(rand.Next(), rand.Next())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector3Int))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Vector4))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Quaternion))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(Color))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(
                    (rand) => new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
                );
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(HashableNetworkVariableTestStruct))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenBytes<HashableNetworkVariableTestStruct>);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                (var original, var original2, var changed, var changed2) = GetNativeHashSets(RandGenFixedString32);
                TestValueTypeNativeHashSet(original, changed);
                TestValueTypeNativeHashSet(original2, changed2);
            }
        }

        [Test]
        [Repeat(5)]
        public void WhenSerializingAndDeserializingVeryLargeValueTypeNativeHashMapNetworkVariables_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(ulong), typeof(Vector2), typeof(HashMapKeyStruct))] Type keyType,
            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(Vector2), typeof(Vector3), typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4),
                typeof(Quaternion), typeof(HashMapValStruct), typeof(FixedString32Bytes))]
            Type valType)
        {
            if (valType == typeof(byte))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<byte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<byte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<byte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<byte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(sbyte))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<sbyte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<sbyte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<sbyte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<sbyte>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(short))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<short>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<short>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<short>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<short>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(ushort))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<ushort>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<ushort>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<ushort>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<ushort>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<int>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<int>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<int>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<int>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(uint))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<uint>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<uint>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<uint>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<uint>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(long))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<long>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<long>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<long>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<long>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(ulong))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<ulong>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<ulong>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<ulong>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<ulong>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(bool))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<bool>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<bool>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<bool>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<bool>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(char))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<char>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<char>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<char>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<char>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(float))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<float>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<float>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<float>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<float>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(double))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<double>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<double>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<double>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<double>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Vector2))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Vector3))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Vector2Int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Vector2Int(rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Vector3Int))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Vector3Int(rand.Next(), rand.Next(), rand.Next()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Vector4))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(Quaternion))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, (rand) => new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(HashMapValStruct))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenBytes<HashMapValStruct>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenBytes<HashMapValStruct>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenBytes<HashMapValStruct>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenBytes<HashMapValStruct>);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
            else if (valType == typeof(FixedString32Bytes))
            {
                if (keyType == typeof(byte))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<byte>, RandGenFixedString32);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(ulong))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<ulong>, RandGenFixedString32);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
                else if (keyType == typeof(Vector2))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps((rand) => new Vector2((float)rand.NextDouble(), (float)rand.NextDouble()), RandGenFixedString32);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);

                }
                else if (keyType == typeof(HashMapKeyStruct))
                {
                    (var original, var original2, var changed, var changed2) = GetMaps(RandGenBytes<HashMapKeyStruct>, RandGenFixedString32);
                    TestValueTypeNativeHashMap(original, changed);
                    TestValueTypeNativeHashMap(original2, changed2);
                }
            }
        }

#endif

        [Test]
        public void TestManagedINetworkSerializableNetworkVariablesDeserializeInPlace()
        {
            var variable = new NetworkVariable<ManagedNetworkSerializableType>
            {
                Value = new ManagedNetworkSerializableType
                {
                    InMemoryValue = 1,
                    Ints = new[] { 2, 3, 4 },
                    Str = "five",
                    Embedded = new EmbeddedManagedNetworkSerializableType { Int = 6 }
                }
            };

            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            variable.WriteField(writer);
            Assert.AreEqual(1, variable.Value.InMemoryValue);
            Assert.AreEqual(new[] { 2, 3, 4 }, variable.Value.Ints);
            Assert.AreEqual("five", variable.Value.Str);
            Assert.AreEqual(6, variable.Value.Embedded.Int);
            variable.Value = new ManagedNetworkSerializableType
            {
                InMemoryValue = 10,
                Ints = new[] { 20, 30, 40, 50 },
                Str = "sixty",
                Embedded = new EmbeddedManagedNetworkSerializableType { Int = 60 }
            };

            using var reader = new FastBufferReader(writer, Allocator.None);
            variable.ReadField(reader);
            Assert.AreEqual(10, variable.Value.InMemoryValue, "In-memory value was not the same - in-place deserialization should not change this");
            Assert.AreEqual(new[] { 2, 3, 4 }, variable.Value.Ints, "Ints were not correctly deserialized");
            Assert.AreEqual("five", variable.Value.Str, "Str was not correctly deserialized");
            Assert.AreEqual(6, variable.Value.Embedded.Int, "Embedded int was not correctly deserialized");
        }

        [Test]
        public void TestUnmnagedINetworkSerializableNetworkVariablesDeserializeInPlace()
        {
            var variable = new NetworkVariable<UnmanagedNetworkSerializableType>
            {
                Value = new UnmanagedNetworkSerializableType
                {
                    InMemoryValue = 1,
                    Int = 2,
                    Str = "three"
                }
            };
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            variable.WriteField(writer);
            Assert.AreEqual(1, variable.Value.InMemoryValue);
            Assert.AreEqual(2, variable.Value.Int);
            Assert.AreEqual("three", variable.Value.Str);
            variable.Value = new UnmanagedNetworkSerializableType
            {
                InMemoryValue = 10,
                Int = 20,
                Str = "thirty"
            };

            using var reader = new FastBufferReader(writer, Allocator.None);
            variable.ReadField(reader);
            Assert.AreEqual(10, variable.Value.InMemoryValue, "In-memory value was not the same - in-place deserialization should not change this");
            Assert.AreEqual(2, variable.Value.Int, "Int was not correctly deserialized");
            Assert.AreEqual("three", variable.Value.Str, "Str was not correctly deserialized");
        }

        private float m_OriginalTimeScale = 1.0f;

        protected override IEnumerator OnSetup()
        {
            m_OriginalTimeScale = Time.timeScale;
            yield return null;
        }

        protected override IEnumerator OnTearDown()
        {
            Time.timeScale = m_OriginalTimeScale;

            m_NetworkListPredicateHandler = null;
            yield return base.OnTearDown();
        }
    }
}
#endif
