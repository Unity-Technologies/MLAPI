using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Validates that distributable NetworkObjects are distributed upon
    /// a client connecting or disconnecting.
    /// </summary>
    internal class DistributeObjectsTests : IntegrationTestWithApproximation
    {
        private GameObject m_DistributeObject;

        private StringBuilder m_ErrorLog = new StringBuilder();

        private const int k_LateJoinClientCount = 4;
        protected override int NumberOfClients => 0;

        public DistributeObjectsTests() : base(HostOrServer.DAHost)
        {
        }

        protected override IEnumerator OnSetup()
        {
            m_ObjectToValidate = null;
            return base.OnSetup();
        }

        protected override void OnServerAndClientsCreated()
        {
            var serverTransport = m_ServerNetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
            // I hate having to add time to our tests, but in case a VM is running slow the disconnect timeout needs to be reasonably high
            serverTransport.DisconnectTimeoutMS = 1000;
            m_DistributeObject = CreateNetworkObjectPrefab("DisObject");
            m_DistributeObject.AddComponent<DistributeObjectsTestHelper>();
            m_DistributeObject.AddComponent<DistributeTestTransform>();

            // Set baseline to be distributable
            var networkObject = m_DistributeObject.GetComponent<NetworkObject>();
            networkObject.SetOwnershipStatus(NetworkObject.OwnershipStatus.Distributable);
            networkObject.DontDestroyWithOwner = true;
            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            m_ServerNetworkManager.SpawnManager.EnableDistributeLogging = m_EnableVerboseDebug;
            m_ServerNetworkManager.ConnectionManager.EnableDistributeLogging = m_EnableVerboseDebug;
            return base.OnServerAndClientsConnected();
        }

        private NetworkObject m_ObjectToValidate;

        private bool ValidateObjectSpawnedOnAllClients()
        {
            m_ErrorLog.Clear();

            var networkObjectId = m_ObjectToValidate.NetworkObjectId;
            var name = m_ObjectToValidate.name;
            if (!UseCMBService() && !m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
            {
                m_ErrorLog.Append($"Client-{m_ServerNetworkManager.LocalClientId} has not spawned {name}!");
                return false;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                {
                    m_ErrorLog.Append($"Client-{client.LocalClientId} has not spawned {name}!");
                    return false;
                }
            }
            return true;
        }

        private const int k_ObjectCount = 20;

        private bool ValidateDistributedObjectsSpawned(bool lateJoining)
        {
            m_ErrorLog.Clear();
            var hostId = m_ServerNetworkManager.LocalClientId;
            if (!DistributeObjectsTestHelper.DistributedObjects.ContainsKey(hostId))
            {
                m_ErrorLog.AppendLine($"[Client-{hostId}] Does not have an entry in the root of the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                return false;
            }
            var daHostObjectTracking = DistributeObjectsTestHelper.DistributedObjects[hostId];
            if (!daHostObjectTracking.ContainsKey(hostId))
            {
                m_ErrorLog.AppendLine($"[Client-{hostId}] Does not have a local an entry in the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                return false;
            }

            var daHostObjects = daHostObjectTracking[hostId];
            var expected = 0;
            if (lateJoining)
            {
                expected = k_ObjectCount / (m_ClientNetworkManagers.Count() + 1);
            }
            else
            {
                expected = k_ObjectCount / (m_ClientNetworkManagers.Where((c) => c.IsConnectedClient).Count() + 1);
            }

            // It should theoretically be the expected or...
            if (daHostObjects.Count != expected)
            {
                // due to not rounding one more than the expected
                expected++;
                if (daHostObjects.Count != expected)
                {
                    m_ErrorLog.AppendLine($"[Client-{hostId}][General] Expected {expected} spawned objects, but only {daHostObjects.Count} exist!");
                    return false;
                }
            }

            foreach (var networkObject in daHostObjects)
            {
                m_ObjectToValidate = networkObject.Value;
                if (!ValidateObjectSpawnedOnAllClients())
                {
                    m_ErrorLog.AppendLine($"[{m_ObjectToValidate.name}] Was not spawned on all clients!");
                    return false;
                }
            }
            return true;
        }

        private bool ValidateOwnershipTablesMatch()
        {
            m_ErrorLog.Clear();
            var hostId = m_ServerNetworkManager.LocalClientId;
            var expectedEntries = m_ClientNetworkManagers.Where((c) => c.IsListening && c.IsConnectedClient).Count() + 1;
            // Make sure all clients have an table created
            if (DistributeObjectsTestHelper.DistributedObjects.Count < expectedEntries)
            {
                m_ErrorLog.AppendLine($"[General] Expected {expectedEntries} entries in the root of the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table but only {DistributeObjectsTestHelper.DistributedObjects.Count} exist!");
                return false;
            }

            if (!DistributeObjectsTestHelper.DistributedObjects.ContainsKey(hostId))
            {
                m_ErrorLog.AppendLine($"[Client-{hostId}] Does not have an entry in the root of the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                return false;
            }
            var daHostEntries = DistributeObjectsTestHelper.DistributedObjects[hostId];
            if (!daHostEntries.ContainsKey(hostId))
            {
                m_ErrorLog.AppendLine($"[Client-{hostId}] Does not have a local an entry in the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                return false;
            }
            var clients = m_ServerNetworkManager.ConnectedClientsIds.ToList();
            clients.Remove(0);

            // Cycle through each client's entry on the DAHost to run a comparison
            foreach (var hostClientEntry in daHostEntries)
            {
                foreach (var ownerEntry in hostClientEntry.Value)
                {
                    foreach (var client in clients)
                    {
                        var clientOwnerTable = DistributeObjectsTestHelper.DistributedObjects[client];
                        if (!clientOwnerTable.ContainsKey(hostClientEntry.Key))
                        {
                            m_ErrorLog.AppendLine($"[Client-{client}] No ownership table exists the client relative section of the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                            return false;
                        }
                        var clientEntry = clientOwnerTable[hostClientEntry.Key];
                        if (!clientEntry.ContainsKey(ownerEntry.Key))
                        {
                            m_ErrorLog.AppendLine($"[Client-{client}] {ownerEntry.Value.name} does not exists in Client-{client}'s sub-section for Owner-{hostClientEntry.Key} relative section of the {nameof(DistributeObjectsTestHelper.DistributedObjects)} table!");
                            return false;
                        }
                        var clientObjectEntry = clientEntry[ownerEntry.Key];
                        if (clientObjectEntry.OwnerClientId != ownerEntry.Value.OwnerClientId)
                        {
                            m_ErrorLog.AppendLine($"[Client-{client}][Owner Mismatch] {clientObjectEntry.OwnerClientId} does equal {ownerEntry.Value.OwnerClientId}!");
                            return false;
                        }
                        // Assure the observers match
                        foreach (var observer in ownerEntry.Value.Observers)
                        {
                            if (!clientObjectEntry.Observers.Contains(observer))
                            {
                                m_ErrorLog.AppendLine($"[Client-{client}][Observer Mismatch] {nameof(NetworkObject)} {clientObjectEntry.name}'s observers does not contain {observer}, but the authority instance does!");
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        private bool ValidateTransformsMatch()
        {
            m_ErrorLog.Clear();
            var hostId = m_ServerNetworkManager.LocalClientId;
            var daHostEntries = DistributeObjectsTestHelper.DistributedObjects[hostId];
            var clients = m_ServerNetworkManager.ConnectedClientsIds.ToList();
            foreach (var clientOwner in daHostEntries.Keys)
            {
                // Cycle through the owner's objects
                foreach (var entry in DistributeObjectsTestHelper.DistributedObjects[clientOwner][clientOwner].Values)
                {
                    var ownerTestTransform = entry.GetComponent<DistributeTestTransform>();
                    // Compare against the other client instances of that object
                    foreach (var client in clients)
                    {
                        if (client == clientOwner)
                        {
                            continue;
                        }

                        var clientObjectInstance = DistributeObjectsTestHelper.DistributedObjects[client][clientOwner][entry.NetworkObjectId];
                        if (!ownerTestTransform.IsPositionClose(clientObjectInstance.transform.position))
                        {
                            m_ErrorLog.AppendLine($"[Position Mismatch] Client-{client} Instance: {GetVector3Values(clientObjectInstance.transform.position)} !=  Owner Instance: {GetVector3Values(ownerTestTransform.transform.position)}!");
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.Prefabs = m_ServerNetworkManager.NetworkConfig.Prefabs;
            base.OnNewClientCreated(networkManager);
        }

        private bool SpawnCountsMatch()
        {
            var passed = true;
            var spawnCount = 0;
            m_ErrorLog.Clear();
            if (!UseCMBService())
            {
                spawnCount = m_ServerNetworkManager.SpawnManager.SpawnedObjects.Count;
            }
            else
            {
                spawnCount = m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects.Count;
            }
            foreach (var client in m_ClientNetworkManagers)
            {
                var clientCount = client.SpawnManager.SpawnedObjects.Count;
                if (clientCount != spawnCount)
                {
                    m_ErrorLog.AppendLine($"[Client-{client.LocalClientId}] Has a spawn count of {clientCount} but {spawnCount} was expected!");
                    passed = false;
                }
            }
            return passed;
        }

        /// <summary>
        /// This is a straight forward validation for the distribution of NetworkObjects
        /// upon a client connecting or disconnecting. It also validates that the observers
        /// on each non-authority instance matches the authority instance's. Finally, it
        /// also includes validation that NetworkTransform updates continue to update and
        /// synchronize properly after ownership for a set number of objects has changed.
        /// </summary>
        [UnityTest]
        public IEnumerator DistributeNetworkObjects()
        {
            for (int i = 0; i < k_ObjectCount; i++)
            {
                SpawnObject(m_DistributeObject, m_ServerNetworkManager);
            }

            // Validate NetworkObjects get redistributed properly when a client joins
            for (int j = 0; j < k_LateJoinClientCount; j++)
            {
                yield return CreateAndStartNewClient();

                yield return WaitForConditionOrTimeOut(() => ValidateDistributedObjectsSpawned(true));
                AssertOnTimeout($"[Client-{j + 1}][Initial Spawn] Not all clients spawned all objects!\n {m_ErrorLog}");

                yield return WaitForConditionOrTimeOut(ValidateOwnershipTablesMatch);
                AssertOnTimeout($"[Client-{j + 1}][OnwershipTable Mismatch] {m_ErrorLog}");

                // When ownership changes, the new owner will randomly pick a new target to move towards and will move towards the target.
                // Validate all other instances of the NetworkObjects that have had newly assigned owners have matching positions to the
                // newly assigned owenr's instance.
                yield return WaitForConditionOrTimeOut(ValidateTransformsMatch);
                AssertOnTimeout($"[Client-{j + 1}][Transform Mismatch] {m_ErrorLog}");
                DisplayOwnership();

                yield return WaitForConditionOrTimeOut(SpawnCountsMatch);
                AssertOnTimeout($"[Spawn Count Mismatch] {m_ErrorLog}");
            }

            // Validate NetworkObjects get redistributed properly when a client disconnects
            for (int j = k_LateJoinClientCount - 1; j >= 0; j--)
            {
                var client = m_ClientNetworkManagers[j];

                // Remove the client from the other clients' ownership tracking table
                DistributeObjectsTestHelper.RemoveClient(client.LocalClientId);

                // Disconnect the client
                yield return StopOneClient(client, true);

                //yield return new WaitForSeconds(0.1f);

                // Validate all tables match
                yield return WaitForConditionOrTimeOut(ValidateOwnershipTablesMatch);

                AssertOnTimeout($"[Client-{j + 1}][OnwershipTable Mismatch] {m_ErrorLog}");

                // When ownership changes, the new owner will randomly pick a new target to move towards and will move towards the target.
                // Validate all other instances of the NetworkObjects that have had newly assigned owners have matching positions to the
                // newly assigned owenr's instance.
                yield return WaitForConditionOrTimeOut(ValidateTransformsMatch);
                AssertOnTimeout($"[Client-{j + 1}][Transform Mismatch] {m_ErrorLog}");

                // DANGO-TODO: Make this tied to verbose mode once we know the CMB Service integration works properly
                DisplayOwnership();

                yield return WaitForConditionOrTimeOut(SpawnCountsMatch);
                AssertOnTimeout($"[Spawn Count Mismatch] {m_ErrorLog}");
            }
        }

        private void DisplayOwnership()
        {
            m_ErrorLog.Clear();
            var daHostEntries = DistributeObjectsTestHelper.DistributedObjects[0];

            foreach (var entry in daHostEntries)
            {
                m_ErrorLog.AppendLine($"[Client-{entry.Key}][Owned Objects: {entry.Value.Count}]");
            }

            VerboseDebug($"{m_ErrorLog}");
        }

        /// <summary>
        /// This keeps track of each clients perspective of which NetworkObjects are owned by which client.
        /// It is used to validate that all clients are in synch with ownership updates.
        /// </summary>
        internal class DistributeObjectsTestHelper : NetworkBehaviour
        {
            /// <summary>
            /// [Client Context][Client Owners][NetworkObjectId][NetworkObject]
            /// </summary>
            public static Dictionary<ulong, Dictionary<ulong, Dictionary<ulong, NetworkObject>>> DistributedObjects = new Dictionary<ulong, Dictionary<ulong, Dictionary<ulong, NetworkObject>>>();

            public static void RemoveClient(ulong clientId)
            {
                foreach (var clients in DistributedObjects.Values)
                {
                    clients.Remove(clientId);
                }
                DistributedObjects.Remove(clientId);
            }

            internal ulong ClientId;

            public override void OnNetworkSpawn()
            {
                ClientId = NetworkManager.LocalClientId;
                UpdateOwnerTableAdd();
                base.OnNetworkSpawn();
            }

            private void UpdateOwnerTableAdd()
            {
                if (!DistributedObjects.ContainsKey(ClientId))
                {
                    DistributedObjects.Add(ClientId, new Dictionary<ulong, Dictionary<ulong, NetworkObject>>());
                }
                if (!DistributedObjects[ClientId].ContainsKey(OwnerClientId))
                {
                    DistributedObjects[ClientId].Add(OwnerClientId, new Dictionary<ulong, NetworkObject>());
                }

                if (DistributedObjects[ClientId][OwnerClientId].ContainsKey(NetworkObject.NetworkObjectId))
                {
                    throw new Exception($"[Client-{ClientId}][{name}] {nameof(NetworkObject)} already exists in Client-{ClientId}'s " +
                        $"DistributedObjects being tracking under Client-{OwnerClientId}'s list of owned {nameof(NetworkObject)}s!");
                }
                DistributedObjects[ClientId][OwnerClientId].Add(NetworkObject.NetworkObjectId, NetworkObject);
            }

            private void UpdateOwnerTableRemove(ulong previous)
            {
                // This does not need to exist when first starting, but will (at one point in testing)
                // become valid.
                if (DistributedObjects[ClientId].ContainsKey(previous))
                {
                    if (DistributedObjects[ClientId][previous].ContainsKey(NetworkObject.NetworkObjectId))
                    {
                        DistributedObjects[ClientId][previous].Remove(NetworkObject.NetworkObjectId);
                    }
                }
            }

            protected override void OnOwnershipChanged(ulong previous, ulong current)
            {
                // At start, if NetworkSpawn has not been completed the local client ignores this
                if (!DistributedObjects.ContainsKey(ClientId))
                {
                    return;
                }
                UpdateOwnerTableRemove(previous);
                UpdateOwnerTableAdd();
                base.OnOwnershipChanged(previous, current);
            }
        }

        /// <summary>
        /// This is used to validate that upon distributed ownership changes NetworkTransform sycnhronization
        /// still works properly.
        /// </summary>
        internal class DistributeTestTransform : NetworkTransform
        {
            private float m_DeltaVarPosition = 0.15f;
            private float m_DeltaVarQauternion = 0.015f;
            protected Vector3 GetRandomVector3(float min, float max, Vector3 baseLine, bool randomlyApplySign = false)
            {
                var retValue = new Vector3(baseLine.x * Random.Range(min, max), baseLine.y * Random.Range(min, max), baseLine.z * Random.Range(min, max));
                if (!randomlyApplySign)
                {
                    return retValue;
                }

                retValue.x *= Random.Range(1, 100) >= 50 ? -1 : 1;
                retValue.y *= Random.Range(1, 100) >= 50 ? -1 : 1;
                retValue.z *= Random.Range(1, 100) >= 50 ? -1 : 1;
                return retValue;
            }

            protected override bool OnIsServerAuthoritative()
            {
                var isOwnerAuth = base.OnIsServerAuthoritative();
                Assert.IsFalse(isOwnerAuth, $"Base {nameof(NetworkTransform)} did not automatically return false in distributed authority mode!");
                return isOwnerAuth;
            }

            public override void OnNetworkSpawn()
            {
                base.OnNetworkSpawn();

                if (CanCommitToTransform)
                {
                    var randomPos = GetRandomVector3(1.0f, 10.0f, Vector3.one, true);
                    SetState(randomPos, null, null, false);
                    m_TargetPosition = randomPos;
                }
            }

            private Vector3 m_TargetPosition;
            private Vector3 m_DirToTarget;
            private bool m_ReachedTarget;

            protected override void OnOwnershipChanged(ulong previous, ulong current)
            {
                base.OnOwnershipChanged(previous, current);
                m_TargetPosition = transform.position + GetRandomVector3(4.0f, 8.0f, Vector3.one, true);
                m_DirToTarget = (m_TargetPosition - transform.position).normalized;
                m_ReachedTarget = false;
            }

            public override void OnUpdate()
            {
                if (CanCommitToTransform)
                {
                    if (!m_ReachedTarget)
                    {
                        var distance = Vector3.Distance(transform.position, m_TargetPosition);

                        var speed = Mathf.Clamp(distance, 0.10f, 2.0f);

                        transform.position += m_DirToTarget * speed * Time.deltaTime;

                        m_ReachedTarget = IsPositionClose(m_TargetPosition);
                    }
                }
            }

            public bool IsPositionClose(Vector3 position)
            {
                return Approximately(transform.position, position);
            }

            protected bool Approximately(Vector3 a, Vector3 b)
            {
                var deltaVariance = m_DeltaVarPosition;
                return Math.Round(Mathf.Abs(a.x - b.x), 2) <= deltaVariance &&
                    Math.Round(Mathf.Abs(a.y - b.y), 2) <= deltaVariance &&
                    Math.Round(Mathf.Abs(a.z - b.z), 2) <= deltaVariance;
            }

            protected bool Approximately(Quaternion a, Quaternion b)
            {
                var deltaVariance = m_DeltaVarQauternion;
                return Mathf.Abs(a.x - b.x) <= deltaVariance &&
                    Mathf.Abs(a.y - b.y) <= deltaVariance &&
                    Mathf.Abs(a.z - b.z) <= deltaVariance &&
                    Mathf.Abs(a.w - b.w) <= deltaVariance;
            }
        }
    }
}
