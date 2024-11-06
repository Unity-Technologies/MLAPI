using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class ShowHideObject : NetworkBehaviour
    {
        public static List<ShowHideObject> ClientTargetedNetworkObjects = new List<ShowHideObject>();
        public static ulong ClientIdToTarget;
        public static bool Silent;
        public static int ValueAfterOwnershipChange = 0;
        public static Dictionary<ulong, ShowHideObject> ObjectsPerClientId = new Dictionary<ulong, ShowHideObject>();
        public static List<ulong> ClientIdsRpcCalledOn;
        public static NetworkObject GetNetworkObjectById(ulong networkObjectId)
        {
            foreach (var entry in ClientTargetedNetworkObjects)
            {
                if (entry.NetworkObjectId == networkObjectId)
                {
                    return entry.NetworkObject;
                }
            }
            return null;
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.LocalClientId == ClientIdToTarget)
            {
                ClientTargetedNetworkObjects.Add(this);
            }

            if (IsServer)
            {
                MyListSetOnSpawn.Add(45);
            }
            else
            {
                Debug.Assert(MyListSetOnSpawn.Count == 1);
                Debug.Assert(MyListSetOnSpawn[0] == 45);
            }

            if (ObjectsPerClientId.ContainsKey(NetworkManager.LocalClientId))
            {
                ObjectsPerClientId[NetworkManager.LocalClientId] = this;
            }
            else
            {
                ObjectsPerClientId.Add(NetworkManager.LocalClientId, this);
            }

            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            if (ClientTargetedNetworkObjects.Contains(this))
            {
                ClientTargetedNetworkObjects.Remove(this);
            }
            base.OnNetworkDespawn();
        }

        public NetworkVariable<int> MyNetworkVariable;
        public NetworkList<int> MyListSetOnSpawn;
        public NetworkVariable<int> MyOwnerReadNetworkVariable;
        public NetworkList<int> MyList;
        public static NetworkManager NetworkManagerOfInterest;

        internal static int GainOwnershipCount = 0;

        private void Awake()
        {
            // Debug.Log($"Awake {NetworkManager.LocalClientId}");
            MyNetworkVariable = new NetworkVariable<int>();
            MyNetworkVariable.OnValueChanged += Changed;

            MyListSetOnSpawn = new NetworkList<int>();
            MyList = new NetworkList<int>();

            MyOwnerReadNetworkVariable = new NetworkVariable<int>(readPerm: NetworkVariableReadPermission.Owner);
            MyOwnerReadNetworkVariable.OnValueChanged += OwnerReadChanged;
        }

        public override void OnGainedOwnership()
        {
            GainOwnershipCount++;
            base.OnGainedOwnership();
        }

        public void OwnerReadChanged(int before, int after)
        {
            if (NetworkManager == NetworkManagerOfInterest)
            {
                ValueAfterOwnershipChange = after;
            }
        }

        public void Changed(int before, int after)
        {
            if (!Silent)
            {
                Debug.Log($"Value changed from {before} to {after}");
            }
        }

        [ClientRpc]
        public void SomeRandomClientRPC()
        {
            if (!Silent)
            {
                Debug.Log($"RPC called {NetworkManager.LocalClientId}");
            }
            ClientIdsRpcCalledOn?.Add(NetworkManager.LocalClientId);
        }

        public void TriggerRpc()
        {
            SomeRandomClientRPC();
        }
    }

    [TestFixture(NetworkTopologyTypes.ClientServer)]
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    internal class NetworkShowHideTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 4;

        private ulong m_ClientId0;
        private GameObject m_PrefabToSpawn;
        private GameObject m_PrefabSpawnWithoutObservers;

        private NetworkObject m_NetSpawnedObject1;
        private NetworkObject m_NetSpawnedObject2;
        private NetworkObject m_NetSpawnedObject3;
        private NetworkObject m_Object1OnClient0;
        private NetworkObject m_Object2OnClient0;
        private NetworkObject m_Object3OnClient0;

        public NetworkShowHideTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("ShowHideObject");
            m_PrefabToSpawn.AddComponent<ShowHideObject>();

            m_PrefabSpawnWithoutObservers = CreateNetworkObjectPrefab("ObserversObject");
            m_PrefabSpawnWithoutObservers.GetComponent<NetworkObject>().SpawnWithObservers = false;
        }

        // Check that the first client see them, or not, as expected
        private IEnumerator CheckVisible(bool isVisible)
        {
            int count = 0;
            do
            {
                yield return WaitForTicks(m_ServerNetworkManager, 5);
                count++;

                if (count > 20)
                {
                    // timeout waiting for object to reach the expect visibility
                    Assert.Fail("timeout waiting for object to reach the expect visibility");
                    break;
                }
            } while (m_NetSpawnedObject1.IsNetworkVisibleTo(m_ClientId0) != isVisible ||
                     m_NetSpawnedObject2.IsNetworkVisibleTo(m_ClientId0) != isVisible ||
                     m_NetSpawnedObject3.IsNetworkVisibleTo(m_ClientId0) != isVisible ||
                     m_Object1OnClient0.IsSpawned != isVisible ||
                     m_Object2OnClient0.IsSpawned != isVisible ||
                     m_Object3OnClient0.IsSpawned != isVisible
                     );

            Debug.Assert(m_NetSpawnedObject1.IsNetworkVisibleTo(m_ClientId0) == isVisible);
            Debug.Assert(m_NetSpawnedObject2.IsNetworkVisibleTo(m_ClientId0) == isVisible);
            Debug.Assert(m_NetSpawnedObject3.IsNetworkVisibleTo(m_ClientId0) == isVisible);

            Debug.Assert(m_Object1OnClient0.IsSpawned == isVisible);
            Debug.Assert(m_Object2OnClient0.IsSpawned == isVisible);
            Debug.Assert(m_Object3OnClient0.IsSpawned == isVisible);

            var clientNetworkManager = m_ClientNetworkManagers.Where((c) => c.LocalClientId == m_ClientId0).First();
            if (isVisible)
            {
                Assert.True(ShowHideObject.ClientTargetedNetworkObjects.Count == 3, $"Client-{clientNetworkManager.LocalClientId} should have 3 instances visible but only has {ShowHideObject.ClientTargetedNetworkObjects.Count}!");
            }
            else
            {
                Assert.True(ShowHideObject.ClientTargetedNetworkObjects.Count == 0, $"Client-{clientNetworkManager.LocalClientId} should have no visible instances but still has {ShowHideObject.ClientTargetedNetworkObjects.Count}!");
            }
        }

        // Set the 3 objects visibility
        private void Show(bool individually, bool visibility)
        {
            if (individually)
            {
                if (!visibility)
                {
                    m_NetSpawnedObject1.NetworkHide(m_ClientId0);
                    m_NetSpawnedObject2.NetworkHide(m_ClientId0);
                    m_NetSpawnedObject3.NetworkHide(m_ClientId0);
                }
                else
                {
                    m_NetSpawnedObject1.NetworkShow(m_ClientId0);
                    m_NetSpawnedObject2.NetworkShow(m_ClientId0);
                    m_NetSpawnedObject3.NetworkShow(m_ClientId0);
                }
            }
            else
            {
                var list = new List<NetworkObject>
                {
                    m_NetSpawnedObject1,
                    m_NetSpawnedObject2,
                    m_NetSpawnedObject3
                };

                if (!visibility)
                {
                    NetworkObject.NetworkHide(list, m_ClientId0);
                }
                else
                {
                    NetworkObject.NetworkShow(list, m_ClientId0);
                }
            }
        }

        private bool RefreshNetworkObjects()
        {
            m_Object1OnClient0 = ShowHideObject.GetNetworkObjectById(m_NetSpawnedObject1.NetworkObjectId);
            m_Object2OnClient0 = ShowHideObject.GetNetworkObjectById(m_NetSpawnedObject2.NetworkObjectId);
            m_Object3OnClient0 = ShowHideObject.GetNetworkObjectById(m_NetSpawnedObject3.NetworkObjectId);
            if (m_Object1OnClient0 == null || m_Object2OnClient0 == null || m_Object3OnClient0 == null)
            {
                return false;
            }
            Assert.True(m_Object1OnClient0.NetworkManagerOwner == m_ClientNetworkManagers[0]);
            Assert.True(m_Object2OnClient0.NetworkManagerOwner == m_ClientNetworkManagers[0]);
            Assert.True(m_Object3OnClient0.NetworkManagerOwner == m_ClientNetworkManagers[0]);
            return true;
        }


        [UnityTest]
        public IEnumerator NetworkShowHideTest()
        {
            m_ClientId0 = m_ClientNetworkManagers[0].LocalClientId;
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientId0;


            // create 3 objects
            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject2 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject3 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();
            m_NetSpawnedObject2 = spawnedObject2.GetComponent<NetworkObject>();
            m_NetSpawnedObject3 = spawnedObject3.GetComponent<NetworkObject>();

            for (int mode = 0; mode < 2; mode++)
            {
                // get the NetworkObject on a client instance
                yield return WaitForConditionOrTimeOut(RefreshNetworkObjects);
                AssertOnTimeout($"Could not refresh all NetworkObjects!");

                // check object start visible
                yield return CheckVisible(true);

                // hide them on one client
                Show(mode == 0, false);

                yield return WaitForTicks(m_ServerNetworkManager, 5);

                m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyNetworkVariable.Value = 3;

                yield return WaitForTicks(m_ServerNetworkManager, 5);

                // verify they got hidden
                yield return CheckVisible(false);

                // show them to that client
                Show(mode == 0, true);
                yield return WaitForConditionOrTimeOut(RefreshNetworkObjects);
                AssertOnTimeout($"Could not refresh all NetworkObjects!");

                // verify they become visible
                yield return CheckVisible(true);
            }
        }

        [UnityTest]
        public IEnumerator ConcurrentShowAndHideOnDifferentObjects()
        {
            m_ClientId0 = m_ClientNetworkManagers[0].LocalClientId;
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientId0;


            // create 3 objects
            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject2 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject3 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();
            m_NetSpawnedObject2 = spawnedObject2.GetComponent<NetworkObject>();
            m_NetSpawnedObject3 = spawnedObject3.GetComponent<NetworkObject>();

            // get the NetworkObject on a client instance
            yield return WaitForConditionOrTimeOut(RefreshNetworkObjects);
            AssertOnTimeout($"Could not refresh all NetworkObjects!");

            m_NetSpawnedObject1.NetworkHide(m_ClientId0);

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            m_NetSpawnedObject1.NetworkShow(m_ClientId0);
            m_NetSpawnedObject2.NetworkHide(m_ClientId0);

            yield return WaitForTicks(m_ServerNetworkManager, 5);
        }

        [UnityTest]
        public IEnumerator NetworkShowHideQuickTest()
        {
            m_ClientId0 = m_ClientNetworkManagers[0].LocalClientId;
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientId0;

            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject2 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject3 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();
            m_NetSpawnedObject2 = spawnedObject2.GetComponent<NetworkObject>();
            m_NetSpawnedObject3 = spawnedObject3.GetComponent<NetworkObject>();

            for (int mode = 0; mode < 2; mode++)
            {
                // get the NetworkObject on a client instance
                yield return WaitForConditionOrTimeOut(RefreshNetworkObjects);
                AssertOnTimeout($"Could not refresh all NetworkObjects!");

                // check object start visible
                yield return CheckVisible(true);

                // hide and show them on one client, during the same frame
                Show(mode == 0, false);
                Show(mode == 0, true);

                yield return WaitForTicks(m_ServerNetworkManager, 5);
                yield return WaitForConditionOrTimeOut(RefreshNetworkObjects);
                AssertOnTimeout($"Could not refresh all NetworkObjects!");
                yield return WaitForTicks(m_ServerNetworkManager, 5);

                // verify they become visible
                yield return CheckVisible(true);
            }
        }

        [UnityTest]
        public IEnumerator NetworkHideDespawnTest()
        {
            m_ClientId0 = m_ClientNetworkManagers[0].LocalClientId;
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientId0;
            ShowHideObject.Silent = true;

            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject2 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            var spawnedObject3 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();
            m_NetSpawnedObject2 = spawnedObject2.GetComponent<NetworkObject>();
            m_NetSpawnedObject3 = spawnedObject3.GetComponent<NetworkObject>();

            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyNetworkVariable.Value++;
            m_NetSpawnedObject1.NetworkHide(m_ClientId0);
            m_NetSpawnedObject1.Despawn();

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            LogAssert.NoUnexpectedReceived();
        }


        private List<ulong> m_ClientsWithVisibility = new List<ulong>();
        private NetworkObject m_ObserverTestObject;

        private bool CheckListedClientsVisibility()
        {
            if (m_ClientsWithVisibility.Contains(m_ServerNetworkManager.LocalClientId))
            {
                if (!m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(m_ObserverTestObject.NetworkObjectId))
                {
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (m_ClientsWithVisibility.Contains(client.LocalClientId))
                {
                    if (!client.SpawnManager.SpawnedObjects.ContainsKey(m_ObserverTestObject.NetworkObjectId))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator SpawnWithoutObserversTest()
        {
            var spawnedObject = SpawnObject(m_PrefabSpawnWithoutObservers, m_ServerNetworkManager);

            m_ObserverTestObject = spawnedObject.GetComponent<NetworkObject>();

            yield return WaitForTicks(m_ServerNetworkManager, 3);

            // When in client-server, the server can spawn a NetworkObject without any observers (even when running as a host the host-client should not have visibility)
            // When in distributed authority mode, the owner client has to be an observer of the object
            if (!m_DistributedAuthority)
            {
                // No observers should be assigned at this point
                Assert.True(m_ObserverTestObject.Observers.Count == m_ClientsWithVisibility.Count, $"Expected the observer count to be {m_ClientsWithVisibility.Count} but it was {m_ObserverTestObject.Observers.Count}!");
                m_ObserverTestObject.NetworkShow(m_ServerNetworkManager.LocalClientId);
            }
            m_ClientsWithVisibility.Add(m_ServerNetworkManager.LocalClientId);

            Assert.True(m_ObserverTestObject.Observers.Count == m_ClientsWithVisibility.Count, $"Expected the observer count to be {m_ClientsWithVisibility.Count} but it was {m_ObserverTestObject.Observers.Count}!");

            yield return WaitForConditionOrTimeOut(CheckListedClientsVisibility);
            AssertOnTimeout($"[Authority-Only] Timed out waiting for only the authority to be an observer!");

            foreach (var client in m_ClientNetworkManagers)
            {
                m_ObserverTestObject.NetworkShow(client.LocalClientId);
                m_ClientsWithVisibility.Add(client.LocalClientId);
                Assert.True(m_ObserverTestObject.Observers.Contains(client.LocalClientId), $"[NetworkShow] Client-{client.LocalClientId} is still not an observer!");
                Assert.True(m_ObserverTestObject.Observers.Count == m_ClientsWithVisibility.Count, $"Expected the observer count to be {m_ClientsWithVisibility.Count} but it was {m_ObserverTestObject.Observers.Count}!");

                yield return WaitForConditionOrTimeOut(CheckListedClientsVisibility);
                AssertOnTimeout($"[Client-{client.LocalClientId}] Timed out waiting for the client to be an observer and spawn the {nameof(NetworkObject)}!");
                Assert.False(client.SpawnManager.SpawnedObjects[m_ObserverTestObject.NetworkObjectId].SpawnWithObservers, $"Client-{client.LocalClientId} instance of {m_ObserverTestObject.name} has a {nameof(NetworkObject.SpawnWithObservers)} value of true!");
            }
        }

        private bool ClientsSpawnedObject1()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.SpawnManager.SpawnedObjects.ContainsKey(m_NetSpawnedObject1.NetworkObjectId))
                {
                    return false;
                }
            }
            return true;
        }

        private StringBuilder m_ErrorLog = new StringBuilder();
        private ulong m_ClientWithoutVisibility;
        private bool Object1IsNotVisibileToClient()
        {
            m_ErrorLog.Clear();
            foreach (var client in m_ClientNetworkManagers)
            {
                if (client.LocalClientId == m_ClientWithoutVisibility)
                {
                    if (client.SpawnManager.SpawnedObjects.ContainsKey(m_NetSpawnedObject1.NetworkObjectId))
                    {
                        m_ErrorLog.AppendLine($"{m_NetSpawnedObject1.name} is still visible to Client-{m_ClientWithoutVisibility}!");
                    }
                }
                else
                if (client.SpawnManager.SpawnedObjects[m_NetSpawnedObject1.NetworkObjectId].IsNetworkVisibleTo(m_ClientWithoutVisibility))
                {
                    m_ErrorLog.AppendLine($"Local instance of {m_NetSpawnedObject1.name} on Client-{client.LocalClientId} thinks Client-{m_ClientWithoutVisibility} still has visibility!");
                }
            }
            return m_ErrorLog.Length == 0;
        }

        [UnityTest]
        public IEnumerator NetworkHideChangeOwnership()
        {
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientNetworkManagers[1].LocalClientId;
            ShowHideObject.Silent = true;

            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();

            yield return WaitForConditionOrTimeOut(ClientsSpawnedObject1);
            AssertOnTimeout($"Not all clients spawned object!");

            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyNetworkVariable.Value++;
            // Hide an object to a client
            m_NetSpawnedObject1.NetworkHide(m_ClientNetworkManagers[1].LocalClientId);
            m_ClientWithoutVisibility = m_ClientNetworkManagers[1].LocalClientId;

            yield return WaitForConditionOrTimeOut(Object1IsNotVisibileToClient);
            AssertOnTimeout($"NetworkObject is still visible to Client-{m_ClientWithoutVisibility} or other clients think it is still visible to Client-{m_ClientWithoutVisibility}:\n {m_ErrorLog}");

            yield return WaitForConditionOrTimeOut(() => ShowHideObject.ClientTargetedNetworkObjects.Count == 0);

            foreach (var client in m_ClientNetworkManagers)
            {
                if (m_ClientNetworkManagers[1].LocalClientId == client.LocalClientId)
                {
                    continue;
                }
                var clientInstance = client.SpawnManager.SpawnedObjects[m_NetSpawnedObject1.NetworkObjectId];
                Assert.IsFalse(clientInstance.IsNetworkVisibleTo(m_ClientNetworkManagers[1].LocalClientId), $"Object instance on Client-{client.LocalClientId} is still visible to Client-{m_ClientNetworkManagers[1].LocalClientId}!");
            }

            // Change ownership while the object is hidden to some
            m_NetSpawnedObject1.ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            // The two-second wait is actually needed as there's a potential warning of unhandled message after 1 second
            yield return new WaitForSeconds(1.25f);

            LogAssert.NoUnexpectedReceived();
            // Show the object again to check nothing unexpected happens
            if (m_DistributedAuthority)
            {
                Assert.True(m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects.ContainsKey(m_NetSpawnedObject1.NetworkObjectId), $"Client-{m_ClientNetworkManagers[0].LocalClientId} has no spawned object with an ID of: {m_NetSpawnedObject1.NetworkObjectId}!");
                var clientInstance = m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects[m_NetSpawnedObject1.NetworkObjectId];
                Assert.True(clientInstance.HasAuthority, $"Client-{m_ClientNetworkManagers[0].LocalClientId} does not have authority to hide NetworkObject ID: {m_NetSpawnedObject1.NetworkObjectId}!");
                clientInstance.NetworkShow(m_ClientNetworkManagers[1].LocalClientId);
            }
            else
            {
                m_NetSpawnedObject1.NetworkShow(m_ClientNetworkManagers[1].LocalClientId);
            }

            yield return WaitForConditionOrTimeOut(() => ShowHideObject.ClientTargetedNetworkObjects.Count == 1);

            Assert.True(ShowHideObject.ClientTargetedNetworkObjects[0].OwnerClientId == m_ClientNetworkManagers[0].LocalClientId);
        }

        private bool AllClientsSpawnedObject1()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                if (!ShowHideObject.ObjectsPerClientId.ContainsKey(client.LocalClientId))
                {
                    return false;
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator NetworkHideChangeOwnershipNotHidden()
        {
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ObjectsPerClientId.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientNetworkManagers[1].LocalClientId;
            ShowHideObject.Silent = true;

            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();

            yield return WaitForConditionOrTimeOut(AllClientsSpawnedObject1);
            AssertOnTimeout($"Timed out waiting for all clients to spawn {spawnedObject1.name}!");

            // change the value
            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyOwnerReadNetworkVariable.Value++;

            // wait for three ticks
            yield return WaitForTicks(m_ServerNetworkManager, 3);

            // check we'll actually be changing owners
            Assert.False(ShowHideObject.ClientTargetedNetworkObjects[0].OwnerClientId == m_ClientNetworkManagers[0].LocalClientId);

            // only check for value change on one specific client
            ShowHideObject.NetworkManagerOfInterest = m_ClientNetworkManagers[0];

            // change ownership
            m_NetSpawnedObject1.ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            // wait three ticks
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);

            // verify ownership changed
            Assert.AreEqual(ShowHideObject.ClientTargetedNetworkObjects[0].OwnerClientId, m_ClientNetworkManagers[0].LocalClientId);

            // verify the expected client got the OnValueChanged. (Only client 1 sets this value)
            Assert.AreEqual(1, ShowHideObject.ValueAfterOwnershipChange);
        }

        private string Display(NetworkList<int> list)
        {
            string message = "";
            foreach (var i in list)
            {
                message += $"{i}, ";
            }

            return message;
        }

        private void Compare(NetworkList<int> list1, NetworkList<int> list2)
        {
            if (list1.Count != list2.Count)
            {
                string message = $"{Display(list1)} versus {Display(list2)}";
                Debug.Log(message);
            }
            else
            {
                for (var i = 0; i < list1.Count; i++)
                {
                    if (list1[i] != list2[i])
                    {
                        string message = $"{Display(list1)} versus {Display(list2)}";
                        Debug.Log(message);
                        break;
                    }
                }
            }

            Debug.Assert(list1.Count == list2.Count);
        }

        private IEnumerator HideThenShowAndHideThenModifyAndShow()
        {
            VerboseDebug("Hiding");
            // hide
            m_NetSpawnedObject1.NetworkHide(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);

            VerboseDebug("Showing and Hiding");
            // show and hide
            m_NetSpawnedObject1.NetworkShow(1);
            m_NetSpawnedObject1.NetworkHide(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);

            VerboseDebug("Modifying and Showing");
            // modify and show
            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyList.Add(5);
            m_NetSpawnedObject1.NetworkShow(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);
        }


        private IEnumerator HideThenModifyAndShow()
        {
            // hide
            m_NetSpawnedObject1.NetworkHide(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);

            // modify
            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyList.Add(5);
            // show
            m_NetSpawnedObject1.NetworkShow(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);

        }

        private IEnumerator HideThenShowAndModify()
        {
            // hide
            m_NetSpawnedObject1.NetworkHide(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);

            // show
            m_NetSpawnedObject1.NetworkShow(1);
            // modify
            m_NetSpawnedObject1.GetComponent<ShowHideObject>().MyList.Add(5);
            yield return WaitForTicks(m_ServerNetworkManager, 3);
            yield return WaitForTicks(m_ClientNetworkManagers[0], 3);
        }

        private IEnumerator HideThenShowAndRPC()
        {
            // hide
            m_NetSpawnedObject1.NetworkHide(1);
            yield return WaitForTicks(m_ServerNetworkManager, 3);

            // show
            m_NetSpawnedObject1.NetworkShow(1);
            m_NetSpawnedObject1.GetComponent<ShowHideObject>().TriggerRpc();
            yield return WaitForTicks(m_ServerNetworkManager, 3);
        }

        [UnityTest]
        public IEnumerator NetworkShowHideAroundListModify()
        {
            ShowHideObject.ClientTargetedNetworkObjects.Clear();
            ShowHideObject.ClientIdToTarget = m_ClientNetworkManagers[1].LocalClientId;
            ShowHideObject.Silent = true;

            var spawnedObject1 = SpawnObject(m_PrefabToSpawn, m_ServerNetworkManager);
            m_NetSpawnedObject1 = spawnedObject1.GetComponent<NetworkObject>();

            // wait for host to have spawned and gained ownership
            while (ShowHideObject.GainOwnershipCount == 0)
            {
                yield return new WaitForSeconds(0.0f);
            }

            for (int i = 0; i < 4; i++)
            {
                // wait for three ticks
                yield return WaitForTicks(m_ServerNetworkManager, 3);
                yield return WaitForTicks(m_ClientNetworkManagers[0], 3);

                switch (i)
                {
                    case 0:
                        VerboseDebug("Running HideThenModifyAndShow");
                        yield return HideThenModifyAndShow();
                        break;
                    case 1:
                        VerboseDebug("Running HideThenShowAndModify");
                        yield return HideThenShowAndModify();
                        break;
                    case 2:
                        VerboseDebug("Running HideThenShowAndHideThenModifyAndShow");
                        yield return HideThenShowAndHideThenModifyAndShow();
                        break;
                    case 3:
                        VerboseDebug("Running HideThenShowAndRPC");
                        ShowHideObject.ClientIdsRpcCalledOn = new List<ulong>();
                        yield return HideThenShowAndRPC();
                        // Provide enough time for slower systems or VM systems possibly under a heavy load could fail on this test
                        yield return WaitForConditionOrTimeOut(() => ShowHideObject.ClientIdsRpcCalledOn.Count == NumberOfClients + 1);
                        AssertOnTimeout($"Timed out waiting for ClientIdsRpcCalledOn.Count ({ShowHideObject.ClientIdsRpcCalledOn.Count}) to equal ({NumberOfClients + 1})!");
                        break;

                }

                Compare(ShowHideObject.ObjectsPerClientId[0].MyList, ShowHideObject.ObjectsPerClientId[1].MyList);
                Compare(ShowHideObject.ObjectsPerClientId[0].MyList, ShowHideObject.ObjectsPerClientId[2].MyList);
            }
        }
    }
}
