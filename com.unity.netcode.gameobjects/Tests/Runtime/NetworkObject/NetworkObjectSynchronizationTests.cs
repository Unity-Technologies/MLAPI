using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Unity.Netcode.RuntimeTests
{

    [TestFixture(VariableLengthSafety.DisableNetVarSafety, HostOrServer.DAHost)]
    [TestFixture(VariableLengthSafety.DisableNetVarSafety, HostOrServer.Host)]
    [TestFixture(VariableLengthSafety.EnabledNetVarSafety, HostOrServer.Host)]
    [TestFixture(VariableLengthSafety.DisableNetVarSafety, HostOrServer.Server)]
    [TestFixture(VariableLengthSafety.EnabledNetVarSafety, HostOrServer.Server)]
    internal class NetworkObjectSynchronizationTests : NetcodeIntegrationTest
    {
        private const int k_NumberToSpawn = 30;
        protected override int NumberOfClients => 0;

        private GameObject m_NetworkPrefab;
        private GameObject m_InValidNetworkPrefab;
        private GameObject m_SynchronizationPrefab;
        private GameObject m_OnSynchronizePrefab;
        private VariableLengthSafety m_VariableLengthSafety;

        private LogLevel m_CurrentLogLevel;

        public enum VariableLengthSafety
        {
            DisableNetVarSafety,
            EnabledNetVarSafety,
        };

        public NetworkObjectSynchronizationTests(VariableLengthSafety variableLengthSafety, HostOrServer hostOrServer) : base(hostOrServer)
        {
            m_VariableLengthSafety = variableLengthSafety;
        }

        protected override void OnCreatePlayerPrefab()
        {
            var component = m_PlayerPrefab.AddComponent<NetworkBehaviourWithOwnerNetworkVariables>();
            if (m_DistributedAuthority)
            {
                component.SetWritePermissions(NetworkVariableWritePermission.Owner);
            }
            base.OnCreatePlayerPrefab();
        }

        protected override void OnServerAndClientsCreated()
        {

            // Set the NetworkVariable Safety Check setting
            m_ServerNetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety = m_VariableLengthSafety == VariableLengthSafety.EnabledNetVarSafety;

            // Ignore the errors generated during this test (they are expected)
            m_ServerNetworkManager.LogLevel = LogLevel.Nothing;

            // Disable forcing the same prefabs to avoid failed connections
            m_ServerNetworkManager.NetworkConfig.ForceSamePrefabs = false;

            // Create the valid network prefab
            m_NetworkPrefab = CreateNetworkObjectPrefab("ValidObject");
            m_NetworkPrefab.AddComponent<NetworkBehaviourWithNetworkVariables>();

            // Create the invalid network prefab (that will fail on client side)
            m_InValidNetworkPrefab = CreateNetworkObjectPrefab("InvalidObject");
            m_InValidNetworkPrefab.AddComponent<NetworkBehaviourWithNetworkVariables>();

            // Create the synchronization network prefab (some pass and some fail)
            m_SynchronizationPrefab = CreateNetworkObjectPrefab("SyncObject");
            m_SynchronizationPrefab.AddComponent<NetworkBehaviourSynchronizeFailureComponent>();
            m_SynchronizationPrefab.AddComponent<NetworkBehaviourWithNetworkVariables>();

            m_OnSynchronizePrefab = CreateNetworkObjectPrefab("OnSyncObject");
            m_OnSynchronizePrefab.AddComponent<NetworkBehaviourOnSynchronizeComponent>();

            base.OnServerAndClientsCreated();
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety = m_VariableLengthSafety == VariableLengthSafety.EnabledNetVarSafety;
            foreach (var networkPrefab in m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                // To simulate a failure, we exclude the m_InValidNetworkPrefab from the connecting
                // client's side.
                if (networkPrefab.Prefab.name != m_InValidNetworkPrefab.name)
                {
                    networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
                }
            }
            // Disable forcing the same prefabs to avoid failed connections
            networkManager.NetworkConfig.ForceSamePrefabs = false;
            networkManager.LogLevel = m_CurrentLogLevel;
            base.OnNewClientCreated(networkManager);
        }

        [UnityTest]
        public IEnumerator NetworkObjectDeserializationFailure()
        {
            m_CurrentLogLevel = LogLevel.Nothing;
            var validSpawnedNetworkObjects = new List<GameObject>();
            NetworkBehaviourWithNetworkVariables.ResetSpawnCount();

            // Spawn NetworkObjects on the server side with half of them being the
            // invalid network prefabs to simulate NetworkObject synchronization failure
            for (int i = 0; i < k_NumberToSpawn; i++)
            {
                if (i % 2 == 0)
                {
                    SpawnObject(m_InValidNetworkPrefab, m_ServerNetworkManager);
                }
                else
                {
                    // Keep track of the prefabs that should successfully spawn on the client side
                    validSpawnedNetworkObjects.Add(SpawnObject(m_NetworkPrefab, m_ServerNetworkManager));
                }
            }

            // Assure the server-side spawned all NetworkObjects
            yield return WaitForConditionOrTimeOut(() => NetworkBehaviourWithNetworkVariables.ServerSpawnCount == k_NumberToSpawn);

            // Now spawn and connect a client that will fail to spawn half of the NetworkObjects spawned
            yield return CreateAndStartNewClient();

            if (m_UseHost)
            {
                var delayCounter = 0;
                while (m_ClientNetworkManagers.Length == 0)
                {
                    delayCounter++;
                    Assert.True(delayCounter < 30, "TimeOut waiting for client to spawn!");
                    yield return s_DefaultWaitForTick;
                }
                delayCounter = 0;
                while (!m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId].ContainsKey(m_ClientNetworkManagers[0].LocalClientId))
                {
                    delayCounter++;
                    if (delayCounter >= 30)
                    {
                        VerboseDebug("Trap!");
                    }
                    Assert.True(delayCounter < 30, "TimeOut waiting for client to spawn!");
                    yield return s_DefaultWaitForTick;
                }


                var serverSideClientPlayerComponent = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var serverSideHostPlayerComponent = m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientSidePlayerComponent = m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientSideHostPlayerComponent = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ServerNetworkManager.LocalClientId].GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var modeText = m_DistributedAuthority ? "owner" : "server";
                // Validate that the client side player values match the server side value of the client's player
                Assert.IsTrue(serverSideClientPlayerComponent.NetworkVariableData1.Value == clientSidePlayerComponent.NetworkVariableData1.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData1)}][Client Player-{clientSidePlayerComponent.OwnerClientId}] Client side value ({clientSidePlayerComponent.NetworkVariableData1.Value})" +
                    $" does not equal the {modeText} side value ({serverSideClientPlayerComponent.NetworkVariableData1.Value})!");
                Assert.IsTrue(serverSideClientPlayerComponent.NetworkVariableData2.Value == clientSidePlayerComponent.NetworkVariableData2.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData2)}][Client Player-{clientSidePlayerComponent.OwnerClientId}] Client side value ({clientSidePlayerComponent.NetworkVariableData2.Value})" +
                    $" does not equal the {modeText} side value ({serverSideClientPlayerComponent.NetworkVariableData2.Value})!");
                Assert.IsTrue(serverSideClientPlayerComponent.NetworkVariableData3.Value == clientSidePlayerComponent.NetworkVariableData3.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData3)}][Client Player-{clientSidePlayerComponent.OwnerClientId}] Client side value ({clientSidePlayerComponent.NetworkVariableData3.Value})" +
                    $" does not equal the {modeText} side value ({serverSideClientPlayerComponent.NetworkVariableData3.Value})!");
                Assert.IsTrue(serverSideClientPlayerComponent.NetworkVariableData4.Value == clientSidePlayerComponent.NetworkVariableData4.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData4)}][Client Player-{clientSidePlayerComponent.OwnerClientId}] Client side value ({clientSidePlayerComponent.NetworkVariableData4.Value})" +
                    $" does not equal the {modeText} side value ({serverSideClientPlayerComponent.NetworkVariableData4.Value})!");

                // DANGO-TODO: This scenario is only possible to do if we add a DA-Server to mock the CMB Service or we integrate the CMB Service AND we have updated NetworkVariable permissions
                // to only allow the service to write. For now, we will skip this validation for distributed authority
                if (!m_DistributedAuthority)
                {
                    // Validate that only the 2nd and 4th NetworkVariable on the client side instance of the host's player is the same and the other two do not match
                    // (i.e. NetworkVariables owned by the server should not get synchronized on client)
                    Assert.IsTrue(serverSideHostPlayerComponent.NetworkVariableData1.Value != clientSideHostPlayerComponent.NetworkVariableData1.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData1)}][Host Player] Client side value ({clientSideHostPlayerComponent.NetworkVariableData1.Value})" +
                    $" should not be equal to the server side value ({serverSideHostPlayerComponent.NetworkVariableData1.Value})!");
                    Assert.IsTrue(serverSideHostPlayerComponent.NetworkVariableData2.Value == clientSideHostPlayerComponent.NetworkVariableData2.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData2)}][Host Player] Client side value ({clientSideHostPlayerComponent.NetworkVariableData2.Value})" +
                        $" does not equal the server side value ({serverSideHostPlayerComponent.NetworkVariableData2.Value})!");
                    Assert.IsTrue(serverSideHostPlayerComponent.NetworkVariableData3.Value != clientSideHostPlayerComponent.NetworkVariableData3.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData3)}][Host Player] Client side value ({clientSideHostPlayerComponent.NetworkVariableData3.Value})" +
                        $" should not be equal to the server side value ({serverSideHostPlayerComponent.NetworkVariableData3.Value})!");
                    Assert.IsTrue(serverSideHostPlayerComponent.NetworkVariableData4.Value == serverSideHostPlayerComponent.NetworkVariableData4.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData4)}][Host Player] Client side value ({clientSideHostPlayerComponent.NetworkVariableData4.Value})" +
                        $" does not equal the server side value ({serverSideHostPlayerComponent.NetworkVariableData4.Value})!");
                }
            }
            else
            {
                // Spawn and connect another client when running as a server
                yield return CreateAndStartNewClient();
                yield return WaitForConditionOrTimeOut(() => m_PlayerNetworkObjects[2].Count > 1);
                AssertOnTimeout($"Timed out waiting for second client to have access to the first client's cloned player object!");

                var clientSide1PlayerComponent = m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientSide2Player1Clone = m_PlayerNetworkObjects[2][clientSide1PlayerComponent.OwnerClientId].GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientOneId = clientSide1PlayerComponent.OwnerClientId;

                var clientSide2PlayerComponent = m_ClientNetworkManagers[1].LocalClient.PlayerObject.GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientSide1Player2Clone = m_PlayerNetworkObjects[1][clientSide2PlayerComponent.OwnerClientId].GetComponent<NetworkBehaviourWithOwnerNetworkVariables>();
                var clientTwoId = clientSide2PlayerComponent.OwnerClientId;

                // Validate that client one's 2nd and 4th NetworkVariables for the local and clone instances match and the other two do not
                Assert.IsTrue(clientSide1PlayerComponent.NetworkVariableData1.Value != clientSide2Player1Clone.NetworkVariableData1.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData1)}][Player-{clientOneId}] Client-{clientOneId} value ({clientSide1PlayerComponent.NetworkVariableData1.Value})" +
                    $" should not be equal to Client-{clientTwoId}'s clone side value ({clientSide2Player1Clone.NetworkVariableData1.Value})!");

                Assert.IsTrue(clientSide1PlayerComponent.NetworkVariableData2.Value == clientSide2Player1Clone.NetworkVariableData2.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData2)}][Player-{clientOneId}] Client-{clientOneId} value ({clientSide1PlayerComponent.NetworkVariableData2.Value})" +
                    $" does not equal Client-{clientTwoId}'s clone side value ({clientSide2Player1Clone.NetworkVariableData2.Value})!");

                Assert.IsTrue(clientSide1PlayerComponent.NetworkVariableData3.Value != clientSide2Player1Clone.NetworkVariableData3.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData3)}][Player-{clientOneId}] Client-{clientOneId} value ({clientSide1PlayerComponent.NetworkVariableData3.Value})" +
                    $" should not be equal to Client-{clientTwoId}'s clone side value ({clientSide2Player1Clone.NetworkVariableData3.Value})!");

                Assert.IsTrue(clientSide1PlayerComponent.NetworkVariableData4.Value == clientSide2Player1Clone.NetworkVariableData4.Value,
                    $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData4)}][Player-{clientOneId}] Client-{clientOneId} value ({clientSide1PlayerComponent.NetworkVariableData4.Value})" +
                    $" does not equal Client-{clientTwoId}'s clone side value ({clientSide2Player1Clone.NetworkVariableData4.Value})!");

                // DANGO-TODO: This scenario is only possible to do if we add a DA-Server to mock the CMB Service or we integrate the CMB Service AND we have updated NetworkVariable permissions
                // to only allow the service to write. For now, we will skip this validation for distributed authority
                if (!m_DistributedAuthority)
                {
                    // Validate that client two's 2nd and 4th NetworkVariables for the local and clone instances match and the other two do not
                    Assert.IsTrue(clientSide2PlayerComponent.NetworkVariableData1.Value != clientSide1Player2Clone.NetworkVariableData1.Value,
                       $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData1)}][Player-{clientTwoId}] Client-{clientTwoId} value ({clientSide2PlayerComponent.NetworkVariableData1.Value})" +
                       $" should not be equal to Client-{clientOneId}'s clone side value ({clientSide1Player2Clone.NetworkVariableData1.Value})!");

                    Assert.IsTrue(clientSide2PlayerComponent.NetworkVariableData2.Value == clientSide1Player2Clone.NetworkVariableData2.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData2)}][Player-{clientTwoId}] Client-{clientTwoId} value ({clientSide2PlayerComponent.NetworkVariableData2.Value})" +
                        $" does not equal Client-{clientOneId}'s clone side value ({clientSide1Player2Clone.NetworkVariableData2.Value})!");

                    Assert.IsTrue(clientSide2PlayerComponent.NetworkVariableData3.Value != clientSide1Player2Clone.NetworkVariableData3.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData3)}][Player-{clientTwoId}] Client-{clientTwoId} value ({clientSide2PlayerComponent.NetworkVariableData3.Value})" +
                        $" should not be equal to Client-{clientOneId}'s clone side value ({clientSide1Player2Clone.NetworkVariableData3.Value})!");

                    Assert.IsTrue(clientSide2PlayerComponent.NetworkVariableData4.Value == clientSide1Player2Clone.NetworkVariableData4.Value,
                        $"[{nameof(NetworkBehaviourWithOwnerNetworkVariables.NetworkVariableData4)}][Player-{clientTwoId}] Client-{clientTwoId} value ({clientSide2PlayerComponent.NetworkVariableData4.Value})" +
                        $" does not equal Client-{clientOneId}'s clone side value ({clientSide1Player2Clone.NetworkVariableData4.Value})!");
                }
            }

            // DANGO-TODO: This scenario is only possible to do if we add a DA-Server to mock the CMB Service or we integrate the CMB Service AND we have updated NetworkVariable permissions
            // to only allow the service to write. For now, we will skip this validation for distributed authority
            if (!m_DistributedAuthority)
            {
                // Now validate all of the NetworkVariable values match to assure everything synchronized properly
                foreach (var spawnedObject in validSpawnedNetworkObjects)
                {
                    foreach (var clientNetworkManager in m_ClientNetworkManagers)
                    {
                        //Validate that the connected client has spawned all of the instances that shouldn't have failed.
                        var clientSideNetworkObjects = s_GlobalNetworkObjects[clientNetworkManager.LocalClientId];

                        Assert.IsTrue(NetworkBehaviourWithNetworkVariables.ClientSpawnCount[clientNetworkManager.LocalClientId] == validSpawnedNetworkObjects.Count, $"Client-{clientNetworkManager.LocalClientId} spawned " +
                            $"({NetworkBehaviourWithNetworkVariables.ClientSpawnCount}) {nameof(NetworkObject)}s but the expected number of {nameof(NetworkObject)}s should have been ({validSpawnedNetworkObjects.Count})!");

                        var spawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();
                        Assert.IsTrue(clientSideNetworkObjects.ContainsKey(spawnedNetworkObject.NetworkObjectId), $"Failed to find valid spawned {nameof(NetworkObject)} on the client-side with a " +
                            $"{nameof(NetworkObject.NetworkObjectId)} of {spawnedNetworkObject.NetworkObjectId}");

                        var clientSideObject = clientSideNetworkObjects[spawnedNetworkObject.NetworkObjectId];
                        Assert.IsTrue(clientSideObject.NetworkManager == clientNetworkManager, $"Client-side object {clientSideObject}'s {nameof(NetworkManager)} is not valid!");

                        ValidateNetworkBehaviourWithNetworkVariables(spawnedNetworkObject, clientSideObject);
                    }
                }
            }
        }

        private void ValidateNetworkBehaviourWithNetworkVariables(NetworkObject serverSideNetworkObject, NetworkObject clientSideNetworkObject)
        {
            var serverSideComponent = serverSideNetworkObject.GetComponent<NetworkBehaviourWithNetworkVariables>();
            var clientSideComponent = clientSideNetworkObject.GetComponent<NetworkBehaviourWithNetworkVariables>();

            string netVarName1 = nameof(NetworkBehaviourWithNetworkVariables.NetworkVariableData1);
            string netVarName2 = nameof(NetworkBehaviourWithNetworkVariables.NetworkVariableData1);
            string netVarName3 = nameof(NetworkBehaviourWithNetworkVariables.NetworkVariableData1);
            string netVarName4 = nameof(NetworkBehaviourWithNetworkVariables.NetworkVariableData1);

            Assert.IsTrue(serverSideComponent.NetworkVariableData1.Count == clientSideComponent.NetworkVariableData1.Count, $"[{serverSideComponent.name}:{netVarName1}] Server side {nameof(NetworkList<byte>)} " +
                $"count ({serverSideComponent.NetworkVariableData1.Count}) does not match the client side {nameof(NetworkList<byte>)} count ({clientSideComponent.NetworkVariableData1.Count})!");

            for (int i = 0; i < serverSideComponent.NetworkVariableData1.Count; i++)
            {
                Assert.IsTrue(serverSideComponent.NetworkVariableData1[i] == clientSideComponent.NetworkVariableData1[i], $"[{serverSideComponent.name}:{netVarName1}][Index:{i}] Server side instance value " +
                    $"({serverSideComponent.NetworkVariableData1[i]}) does not match the client side instance value ({clientSideComponent.NetworkVariableData1[i]})!");
            }

            Assert.IsTrue(serverSideComponent.NetworkVariableData2.Value == clientSideComponent.NetworkVariableData2.Value, $"[{serverSideComponent.name}:{netVarName2}] Server side instance value ({serverSideComponent.NetworkVariableData2.Value}) " +
                $"does not match the client side instance value ({clientSideComponent.NetworkVariableData2.Value})!");
            Assert.IsTrue(serverSideComponent.NetworkVariableData3.Value == clientSideComponent.NetworkVariableData3.Value, $"[{serverSideComponent.name}:{netVarName3}] Server side instance value ({serverSideComponent.NetworkVariableData3.Value}) " +
                $"does not match the client side instance value ({clientSideComponent.NetworkVariableData3.Value})!");
            Assert.IsTrue(serverSideComponent.NetworkVariableData4.Value == clientSideComponent.NetworkVariableData4.Value, $"[{serverSideComponent.name}:{netVarName4}] Server side instance value ({serverSideComponent.NetworkVariableData4.Value}) " +
                $"does not match the client side instance value ({clientSideComponent.NetworkVariableData4.Value})!");
        }


        private bool ClientSpawnedNetworkObjects(List<GameObject> spawnedObjectList)
        {
            var clientSideNetworkObjects = s_GlobalNetworkObjects[m_ClientNetworkManagers[0].LocalClientId];

            foreach (var spawnedObject in spawnedObjectList)
            {
                var serverSideSpawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();
                if (!clientSideNetworkObjects.ContainsKey(serverSideSpawnedNetworkObject.NetworkObjectId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// This validates that when a NetworkBehaviour fails serialization or deserialization during synchronizations that other NetworkBehaviours
        /// will still be initialized properly
        /// </summary>
        [UnityTest]
        public IEnumerator NetworkBehaviourSynchronization()
        {
            m_ServerNetworkManager.LogLevel = LogLevel.Normal;
            m_CurrentLogLevel = LogLevel.Normal;
            NetworkBehaviourSynchronizeFailureComponent.ResetBehaviour();

            var spawnedObjectList = new List<GameObject>();
            var numberOfObjectsToSpawn = NetworkBehaviourSynchronizeFailureComponent.NumberOfFailureTypes * 4;
            // Spawn 11 more NetworkObjects where there should be 4 of each failure type
            for (int i = 0; i < numberOfObjectsToSpawn; i++)
            {
                var synchronizationObject = SpawnObject(m_SynchronizationPrefab, m_ServerNetworkManager);
                var synchronizationBehaviour = synchronizationObject.GetComponent<NetworkBehaviourSynchronizeFailureComponent>();
                synchronizationBehaviour.AssignNextFailureType();
                spawnedObjectList.Add(synchronizationObject);
            }

            // Now spawn and connect a client that will fail to spawn half of the NetworkObjects spawned
            yield return CreateAndStartNewClient();

            // Validate that when a NetworkBehaviour fails to synchronize and is skipped over it does not
            // impact the rest of the NetworkBehaviours.
            var clientSideNetworkObjects = s_GlobalNetworkObjects[m_ClientNetworkManagers[0].LocalClientId];
            yield return WaitForConditionOrTimeOut(() => ClientSpawnedNetworkObjects(spawnedObjectList));
            AssertOnTimeout($"Timed out waiting for newly joined client to spawn all NetworkObjects!");

            foreach (var spawnedObject in spawnedObjectList)
            {
                var serverSideSpawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();
                var clientSideObject = clientSideNetworkObjects[serverSideSpawnedNetworkObject.NetworkObjectId];
                var clientSideSpawnedNetworkObject = clientSideObject.GetComponent<NetworkObject>();

                ValidateNetworkBehaviourWithNetworkVariables(serverSideSpawnedNetworkObject, clientSideSpawnedNetworkObject);
            }
        }

        /// <summary>
        /// A basic validation for the NetworkBehaviour.OnSynchronize method
        /// </summary>
        [UnityTest]
        public IEnumerator NetworkBehaviourOnSynchronize()
        {
            var serverSideInstance = SpawnObject(m_OnSynchronizePrefab, m_ServerNetworkManager).GetComponent<NetworkBehaviourOnSynchronizeComponent>();

            // Now spawn and connect a client that will have custom serialized data applied during the client synchronization process.
            yield return CreateAndStartNewClient();

            var clientSideNetworkObjects = s_GlobalNetworkObjects[m_ClientNetworkManagers[0].LocalClientId];
            var clientSideInstance = clientSideNetworkObjects[serverSideInstance.NetworkObjectId].GetComponent<NetworkBehaviourOnSynchronizeComponent>();

            // Validate the values match
            Assert.IsTrue(serverSideInstance.CustomSerializationData.Value1 == clientSideInstance.CustomSerializationData.Value1, $"Client-side instance Value1 ({serverSideInstance.CustomSerializationData.Value1}) does not equal server-side instance Value1 ({clientSideInstance.CustomSerializationData.Value1})");
            Assert.IsTrue(serverSideInstance.CustomSerializationData.Value2 == clientSideInstance.CustomSerializationData.Value2, $"Client-side instance Value1 ({serverSideInstance.CustomSerializationData.Value2}) does not equal server-side instance Value1 ({clientSideInstance.CustomSerializationData.Value2})");
            Assert.IsTrue(serverSideInstance.CustomSerializationData.Value3 == clientSideInstance.CustomSerializationData.Value3, $"Client-side instance Value1 ({serverSideInstance.CustomSerializationData.Value3}) does not equal server-side instance Value1 ({clientSideInstance.CustomSerializationData.Value3})");
            Assert.IsTrue(serverSideInstance.CustomSerializationData.Value4 == clientSideInstance.CustomSerializationData.Value4, $"Client-side instance Value1 ({serverSideInstance.CustomSerializationData.Value4}) does not equal server-side instance Value1 ({clientSideInstance.CustomSerializationData.Value4})");
        }
    }

    /// <summary>
    /// A test NetworkBeahviour that provides a varying NetworkList size as well as
    /// additional NetworkVariables to assure if a NetworkObject fails to be created
    /// the synchronization process will continue (i.e. it will skip over that block
    /// of the reader buffer).
    /// </summary>
    internal class NetworkBehaviourWithNetworkVariables : NetworkBehaviour
    {
        public static int ServerSpawnCount { get; internal set; }
        public static readonly Dictionary<ulong, int> ClientSpawnCount = new Dictionary<ulong, int>();

        public static void ResetSpawnCount()
        {
            ServerSpawnCount = 0;
            ClientSpawnCount.Clear();
        }

        private const uint k_MinDataBlocks = 1;
        private const uint k_MaxDataBlocks = 64;

        // Add various types of NetworkVariables
        public NetworkList<ulong> NetworkVariableData1;
        public NetworkVariable<int> NetworkVariableData2;
        public NetworkVariable<long> NetworkVariableData3;
        public NetworkVariable<byte> NetworkVariableData4;

        private void Awake()
        {
            var dataBlocksAssigned = new List<ulong>();
            var numberDataBlocks = Random.Range(k_MinDataBlocks, k_MaxDataBlocks);
            for (var i = 0; i < numberDataBlocks; i++)
            {
                dataBlocksAssigned.Add((ulong)Random.Range(0.0f, float.MaxValue));
            }

            NetworkVariableData1 = new NetworkList<ulong>(dataBlocksAssigned);
            NetworkVariableData2 = new NetworkVariable<int>(Random.Range(1, 1000));
            NetworkVariableData3 = new NetworkVariable<long>(Random.Range(1, 1000));
            NetworkVariableData4 = new NetworkVariable<byte>((byte)Random.Range(1, 255));

        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerSpawnCount++;
            }
            else
            {
                if (!ClientSpawnCount.ContainsKey(NetworkManager.LocalClientId))
                {
                    ClientSpawnCount.Add(NetworkManager.LocalClientId, 0);
                }
                ClientSpawnCount[NetworkManager.LocalClientId]++;
            }

            base.OnNetworkSpawn();
        }
    }

    /// <summary>
    /// A test NetworkBeahviour that has varying permissions in order to validate that
    /// when variable length safety checks are off NetworkVariables still are updated
    /// properly.
    /// </summary>
    internal class NetworkBehaviourWithOwnerNetworkVariables : NetworkBehaviour
    {
        private NetworkVariableWritePermission m_NetworkVariableWritePermission = NetworkVariableWritePermission.Server;
        /// <summary>
        /// For distributed authority, there is no such thing as a server and only owners
        /// DANGO-TODO: When NetworkVariable permissions are updated, this test might need to be updated
        /// </summary>
        /// <param name="networkVariableWritePermission"></param>
        public void SetWritePermissions(NetworkVariableWritePermission networkVariableWritePermission)
        {
            m_NetworkVariableWritePermission = networkVariableWritePermission;
            // Should synchronize with everyone
            NetworkVariableData1 = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, networkVariableWritePermission);
            // Should synchronize with everyone
            NetworkVariableData2 = new NetworkVariable<long>(default, NetworkVariableReadPermission.Everyone, networkVariableWritePermission);
            // Should synchronize with everyone
            NetworkVariableData3 = new NetworkVariable<byte>(default, NetworkVariableReadPermission.Everyone, networkVariableWritePermission);
            // Should synchronize with everyone
            NetworkVariableData4 = new NetworkVariable<ushort>(default, NetworkVariableReadPermission.Everyone, networkVariableWritePermission);
        }

        // Should not synchronize on non-owners
        public NetworkVariable<int> NetworkVariableData1 = new NetworkVariable<int>(default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        // Should synchronize with everyone
        public NetworkVariable<long> NetworkVariableData2 = new NetworkVariable<long>();
        // Should not synchronize on non-owners
        public NetworkVariable<byte> NetworkVariableData3 = new NetworkVariable<byte>(default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        // Should synchronize with everyone
        public NetworkVariable<ushort> NetworkVariableData4 = new NetworkVariable<ushort>();

        public override void OnNetworkSpawn()
        {
            // Adjustment for distributed authority mode
            if ((m_NetworkVariableWritePermission == NetworkVariableWritePermission.Server && IsServer && !NetworkManager.DistributedAuthorityMode) ||
                (m_NetworkVariableWritePermission == NetworkVariableWritePermission.Owner && IsOwner && NetworkManager.DistributedAuthorityMode))
            {
                NetworkVariableData1.Value = Random.Range(1, 1000);
                NetworkVariableData2.Value = Random.Range(1, 1000);
                NetworkVariableData3.Value = (byte)Random.Range(1, 255);
                NetworkVariableData4.Value = (ushort)Random.Range(1, ushort.MaxValue);
            }
        }
    }

    /// <summary>
    /// A test NetworkBeahviour that simulates various types of synchronization failures
    /// and provides a synchronization success version to validate that synchronization
    /// will continue if user synchronization code fails.
    /// </summary>
    internal class NetworkBehaviourSynchronizeFailureComponent : NetworkBehaviour
    {
        public static int NumberOfFailureTypes { get; internal set; }
        public static int ServerSpawnCount { get; internal set; }
        public static int ClientSpawnCount { get; internal set; }

        private static FailureTypes s_FailureType = FailureTypes.None;

        public enum FailureTypes
        {
            None,
            DuringWriting,
            DuringReading,
            DontReadAnything,
            ThrowWriteSideException,
            ThrowReadSideException
        }

        public static void ResetBehaviour()
        {
            ServerSpawnCount = 0;
            ClientSpawnCount = 0;
            s_FailureType = FailureTypes.None;
            NumberOfFailureTypes = System.Enum.GetValues(typeof(FailureTypes)).Length;
        }

        private MyCustomData m_MyCustomData;

        private struct MyCustomData : INetworkSerializable
        {
            public FailureTypes FailureType;
            private ushort m_DataSize;
            private byte[] m_DataBlock;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsWriter)
                {
                    var writer = serializer.GetFastBufferWriter();
                    switch (FailureType)
                    {
                        case FailureTypes.None:
                        // We want to write something for these two cases
                        case FailureTypes.DuringReading:
                        case FailureTypes.DontReadAnything:
                            {
                                writer.WriteValueSafe(m_DataSize);
                                for (int i = 0; i < m_DataSize; i++)
                                {
                                    writer.WriteValueSafe(m_DataBlock[i]);
                                }
                                break;
                            }
                        case FailureTypes.DuringWriting:
                            {
                                writer.WriteValueSafe(m_DataSize);
                                // Try to write past the allocated size to generate an exception
                                // while also filling the buffer to verify that the buffer will be
                                // reset back to the original position.
                                for (int i = 0; i <= m_DataSize; i++)
                                {
                                    writer.WriteValueSafe(m_DataBlock[i]);
                                }
                                break;
                            }
                        case FailureTypes.ThrowWriteSideException:
                            {
                                throw new System.Exception("Write side exception!");
                            }
                    }
                }
                else
                {
                    var reader = serializer.GetFastBufferReader();
                    switch (FailureType)
                    {
                        case FailureTypes.None:
                            {
                                reader.ReadValueSafe(out m_DataSize);
                                m_DataBlock = new byte[m_DataSize];
                                for (int i = 0; i < m_DataSize; i++)
                                {
                                    reader.ReadValueSafe(out m_DataBlock[i]);
                                }
                                break;
                            }
                        case FailureTypes.DuringReading:
                            {
                                reader.ReadValueSafe(out m_DataSize);
                                // Allocate more space than needed
                                m_DataBlock = new byte[(int)(m_DataSize * 1.5f)];
                                // Now read past the size of this message to verify
                                // that the reader will get rest back to the appropriate
                                // position and an error will be generated for this
                                for (int i = 0; i < m_DataBlock.Length; i++)
                                {
                                    reader.ReadValueSafe(out m_DataBlock[i]);
                                }
                                break;
                            }
                        case FailureTypes.DontReadAnything:
                            {
                                // Don't read anything
                                break;
                            }
                        case FailureTypes.ThrowReadSideException:
                            {
                                throw new System.Exception("Read side exception!");
                            }

                    }
                }
            }

            public void GenerateData(ushort size)
            {
                m_DataSize = size;
                m_DataBlock = new byte[size];
                for (int i = 0; i < m_DataSize; i++)
                {
                    m_DataBlock[i] = (byte)Random.Range(0, 512);
                }
            }
        }

        // This NetworkVariable is synchronized before OnSynchronize is invoked
        // which enables us to perform the tests.
        // Users could follow the same pattern for game assets and synchronize
        // clients based on NetworkVariable settings. (i.e. a specific NPC type or the like)
        private NetworkVariable<FailureTypes> m_FailureType;

        public void AssignNextFailureType()
        {
            var currentPosition = (int)s_FailureType;
            currentPosition = (++currentPosition) % NumberOfFailureTypes;
            s_FailureType = (FailureTypes)currentPosition;
            m_FailureType.Value = s_FailureType;
        }


        private void Awake()
        {
            m_FailureType = new NetworkVariable<FailureTypes>();
            m_MyCustomData = new MyCustomData();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerSpawnCount++;
                m_MyCustomData.GenerateData((ushort)Random.Range(1, 512));
            }
            else
            {
                ClientSpawnCount++;
            }

            base.OnNetworkSpawn();
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            // Assign the failure type first
            m_MyCustomData.FailureType = m_FailureType.Value;
            // Now handle the serialization for this failure type
            m_MyCustomData.NetworkSerialize(serializer);
        }
    }

    internal class NetworkBehaviourOnSynchronizeComponent : NetworkBehaviour
    {
        public SomeCustomSerializationData CustomSerializationData = new SomeCustomSerializationData();

        internal struct SomeCustomSerializationData : INetworkSerializable
        {
            public uint Value1;
            public bool Value2;
            public long Value3;
            public float Value4;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Value1);
                serializer.SerializeValue(ref Value2);
                serializer.SerializeValue(ref Value3);
                serializer.SerializeValue(ref Value4);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CustomSerializationData.Value1 = (uint)Random.Range(0, 10000);
                CustomSerializationData.Value2 = true;
                CustomSerializationData.Value3 = Random.Range(0, 10000);
                CustomSerializationData.Value4 = Random.Range(-1000.0f, 1000.0f);
            }
            base.OnNetworkSpawn();
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            serializer.SerializeNetworkSerializable(ref CustomSerializationData);
            base.OnSynchronize(ref serializer);
        }
    }
}
