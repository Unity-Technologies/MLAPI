using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class HiddenVariableTest : NetworkBehaviour
    {
    }

    internal class HiddenVariableObject : NetworkBehaviour
    {
        public static List<NetworkObject> ClientInstancesSpawned = new List<NetworkObject>();

        public NetworkVariable<int> MyNetworkVariable = new NetworkVariable<int>();
        public NetworkList<int> MyNetworkList = new NetworkList<int>();

        public static Dictionary<ulong, int> ValueOnClient = new Dictionary<ulong, int>();
        public static int ExpectedSize = 0;
        public static int SpawnCount = 0;

        public static bool EnableVerbose;

        public static void VerboseDebug(string message)
        {
            if (!EnableVerbose)
            {
                return;
            }
            Debug.Log(message);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                ClientInstancesSpawned.Add(NetworkObject);
            }
            VerboseDebug($"{nameof(HiddenVariableObject)}.{nameof(OnNetworkSpawn)}() with value {MyNetworkVariable.Value}");

            MyNetworkVariable.OnValueChanged += Changed;
            MyNetworkList.OnListChanged += ListChanged;
            SpawnCount++;

            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                ClientInstancesSpawned.Remove(NetworkObject);
            }
            base.OnNetworkDespawn();
        }

        public void Changed(int before, int after)
        {
            VerboseDebug($"[Client-{NetworkManager.LocalClientId}][{name}][MyNetworkVariable] Value changed from {before} to {after}");
            ValueOnClient[NetworkManager.LocalClientId] = after;
        }
        public void ListChanged(NetworkListEvent<int> listEvent)
        {
            VerboseDebug($"[Client-{NetworkManager.LocalClientId}][{name}][MyNetworkList] ListEvent received: type {listEvent.Type}, index {listEvent.Index}, value {listEvent.Value}");
            Debug.Assert(ExpectedSize == MyNetworkList.Count, $"[{name}] List change failure! Expected Count: {ExpectedSize} Actual Count:{MyNetworkList.Count}");
        }
    }

    internal class HiddenVariableTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 4;

        private NetworkObject m_NetSpawnedObject;
        private List<NetworkObject> m_NetSpawnedObjectOnClient = new List<NetworkObject>();
        private GameObject m_TestNetworkPrefab;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<HiddenVariableTest>();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestNetworkPrefab = CreateNetworkObjectPrefab("MyTestObject");
            m_TestNetworkPrefab.AddComponent<HiddenVariableObject>();
        }

        public IEnumerator WaitForSpawnCount(int targetCount)
        {
            var endTime = Time.realtimeSinceStartup + 1.0;
            while (HiddenVariableObject.SpawnCount != targetCount &&
                   Time.realtimeSinceStartup < endTime)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }

        public bool VerifyLists()
        {
            NetworkList<int> prev = null;
            int numComparison = 0;
            var prevObject = (NetworkObject)null;
            // for all the instances of NetworkList
            foreach (var networkObject in m_NetSpawnedObjectOnClient)
            {
                // this skips despawned/hidden objects
                if (networkObject != null)
                {
                    // if we've seen another one before
                    if (prev != null)
                    {
                        var curr = networkObject.GetComponent<HiddenVariableObject>().MyNetworkList;

                        // check that the two lists are identical
                        if (curr.Count != prev.Count)
                        {
                            return false;
                        }
                        for (int index = 0; index < curr.Count; index++)
                        {
                            if (curr[index] != prev[index])
                            {
                                return false;
                            }
                        }
                        numComparison++;
                    }
                    prevObject = networkObject;
                    // store the list
                    prev = networkObject.GetComponent<HiddenVariableObject>().MyNetworkList;
                }
            }
            return true;
        }

        public IEnumerator RefreshGameObects(int numberToExpect)
        {
            yield return WaitForConditionOrTimeOut(() => numberToExpect == HiddenVariableObject.ClientInstancesSpawned.Count);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for total spawned count to reach {numberToExpect} but is currently {HiddenVariableObject.ClientInstancesSpawned.Count}");
            m_NetSpawnedObjectOnClient = HiddenVariableObject.ClientInstancesSpawned;
        }

        private bool CheckValueOnClient(ulong otherClientId, int value)
        {
            foreach (var id in m_ServerNetworkManager.ConnectedClientsIds)
            {
                if (id != otherClientId)
                {
                    if (!HiddenVariableObject.ValueOnClient.ContainsKey(id) || HiddenVariableObject.ValueOnClient[id] != value)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private IEnumerator SetAndCheckValueSet(ulong otherClientId, int value)
        {
            yield return WaitForConditionOrTimeOut(() => CheckValueOnClient(otherClientId, value));
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for all clients to have a value of {value}");

            yield return WaitForConditionOrTimeOut(VerifyLists);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, "Timed out waiting for all clients to have identical values!");

            VerboseDebug("Value changed");
        }

        [UnityTest]
        public IEnumerator HiddenVariableTest()
        {
            HiddenVariableObject.EnableVerbose = m_EnableVerboseDebug;
            HiddenVariableObject.SpawnCount = 0;
            HiddenVariableObject.ValueOnClient.Clear();
            HiddenVariableObject.ExpectedSize = 0;
            HiddenVariableObject.SpawnCount = 0;

            VerboseDebug("Running test");

            // ==== Spawn object with ownership on one client
            var client = m_ServerNetworkManager.ConnectedClientsList[1];
            var otherClient = m_ServerNetworkManager.ConnectedClientsList[2];
            m_NetSpawnedObject = SpawnObject(m_TestNetworkPrefab, m_ClientNetworkManagers[1]).GetComponent<NetworkObject>();

            yield return RefreshGameObects(NumberOfClients);

            // === Check spawn occurred
            yield return WaitForSpawnCount(NumberOfClients + 1);

            AssertOnTimeout($"Timed out waiting for all clients to spawn {m_NetSpawnedObject.name}");

            Debug.Assert(HiddenVariableObject.SpawnCount == NumberOfClients + 1);
            VerboseDebug("Objects spawned");

            // ==== Set the NetworkVariable value to 2
            HiddenVariableObject.ExpectedSize = 1;
            HiddenVariableObject.SpawnCount = 0;
            var currentValueSet = 2;

            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkVariable.Value = currentValueSet;
            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkList.Add(currentValueSet);

            yield return SetAndCheckValueSet(otherClient.ClientId, currentValueSet);

            // ==== Hide our object to a different client
            HiddenVariableObject.ExpectedSize = 2;
            m_NetSpawnedObject.NetworkHide(otherClient.ClientId);
            currentValueSet = 3;
            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkVariable.Value = currentValueSet;
            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkList.Add(currentValueSet);

            yield return SetAndCheckValueSet(otherClient.ClientId, currentValueSet);

            // ==== Show our object again to this client
            HiddenVariableObject.ExpectedSize = 3;
            m_NetSpawnedObject.NetworkShow(otherClient.ClientId);

            // ==== Wait for object to be spawned
            yield return WaitForSpawnCount(1);
            Debug.Assert(HiddenVariableObject.SpawnCount == 1);
            VerboseDebug("Object spawned");

            // ==== We need a refresh for the newly re-spawned object
            yield return RefreshGameObects(NumberOfClients);

            currentValueSet = 4;
            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkVariable.Value = currentValueSet;
            m_NetSpawnedObject.GetComponent<HiddenVariableObject>().MyNetworkList.Add(currentValueSet);

            yield return SetAndCheckValueSet(otherClient.ClientId, currentValueSet);

            // ==== Hide our object to that different client again, and then destroy it
            m_NetSpawnedObject.NetworkHide(otherClient.ClientId);
            yield return new WaitForSeconds(0.2f);
            m_NetSpawnedObject.Despawn();
            yield return new WaitForSeconds(0.2f);
        }
    }
}
