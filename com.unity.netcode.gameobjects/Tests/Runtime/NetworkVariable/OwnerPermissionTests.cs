using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class OwnerPermissionObject : NetworkBehaviour
    {
        // indexed by [object, machine]
        public static OwnerPermissionObject[,] Objects = new OwnerPermissionObject[3, 3];
        public static int CurrentlySpawning = 0;

        public static List<OwnerPermissionObject> ClientTargetedNetworkObjects = new List<OwnerPermissionObject>();
        // a client-owned NetworkVariable
        public NetworkVariable<int> MyNetworkVariableOwner;
        // a server-owned NetworkVariable
        public NetworkVariable<int> MyNetworkVariableServer;

        // a client-owned NetworkVariable
        public NetworkList<int> MyNetworkListOwner;
        // a server-owned NetworkVariable
        public NetworkList<int> MyNetworkListServer;

        // verifies two lists are identical
        public static void CheckLists(NetworkList<int> listA, NetworkList<int> listB)
        {
            Debug.Assert(listA.Count == listB.Count);
            for (var i = 0; i < listA.Count; i++)
            {
                Debug.Assert(listA[i] == listB[i]);
            }
        }

        // verifies all objects have consistent lists on all clients
        public static void VerifyConsistency()
        {
            for (var objectIndex = 0; objectIndex < 3; objectIndex++)
            {
                CheckLists(Objects[objectIndex, 0].MyNetworkListOwner, Objects[objectIndex, 1].MyNetworkListOwner);
                CheckLists(Objects[objectIndex, 0].MyNetworkListOwner, Objects[objectIndex, 2].MyNetworkListOwner);

                CheckLists(Objects[objectIndex, 0].MyNetworkListServer, Objects[objectIndex, 1].MyNetworkListServer);
                CheckLists(Objects[objectIndex, 0].MyNetworkListServer, Objects[objectIndex, 2].MyNetworkListServer);
            }
        }

        public override void OnNetworkSpawn()
        {
            Objects[CurrentlySpawning, NetworkManager.LocalClientId] = GetComponent<OwnerPermissionObject>();
        }

        private void Awake()
        {
            MyNetworkVariableOwner = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Owner);
            MyNetworkVariableOwner.OnValueChanged += OwnerChanged;

            MyNetworkVariableServer = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Server);
            MyNetworkVariableServer.OnValueChanged += ServerChanged;

            MyNetworkListOwner = new NetworkList<int>(writePerm: NetworkVariableWritePermission.Owner);
            MyNetworkListOwner.OnListChanged += ListOwnerChanged;

            MyNetworkListServer = new NetworkList<int>(writePerm: NetworkVariableWritePermission.Server);
            MyNetworkListServer.OnListChanged += ListServerChanged;
        }

        public void OwnerChanged(int before, int after)
        {
        }

        public void ServerChanged(int before, int after)
        {
        }

        public void ListOwnerChanged(NetworkListEvent<int> listEvent)
        {
        }

        public void ListServerChanged(NetworkListEvent<int> listEvent)
        {
        }
    }



    internal class OwnerPermissionHideTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private GameObject m_PrefabToSpawn;

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("OwnerPermissionObject");
            m_PrefabToSpawn.AddComponent<OwnerPermissionObject>();
        }

        [UnityTest]
        public IEnumerator OwnerPermissionTest()
        {
            // create 3 objects
            for (var objectIndex = 0; objectIndex < 3; objectIndex++)
            {
                OwnerPermissionObject.CurrentlySpawning = objectIndex;

                NetworkManager ownerManager = m_ServerNetworkManager;
                if (objectIndex != 0)
                {
                    ownerManager = m_ClientNetworkManagers[objectIndex - 1];
                }
                SpawnObject(m_PrefabToSpawn, ownerManager);

                // wait for each object to spawn on each client
                for (var clientIndex = 0; clientIndex < 3; clientIndex++)
                {
                    while (OwnerPermissionObject.Objects[objectIndex, clientIndex] == null)
                    {
                        yield return new WaitForSeconds(0.0f);
                    }
                }
            }

            var nextValueToWrite = 1;
            var serverIndex = 0;

            for (var objectIndex = 0; objectIndex < 3; objectIndex++)
            {
                for (var clientWriting = 0; clientWriting < 3; clientWriting++)
                {
                    // ==== Server-writable NetworkVariable ====
                    VerboseDebug($"Writing to server-write variable on object {objectIndex} on client {clientWriting}");

                    nextValueToWrite++;
                    if (clientWriting != serverIndex)
                    {
                        LogAssert.Expect(LogType.Error, OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkVariableServer.GetWritePermissionError());
                    }
                    OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkVariableServer.Value = nextValueToWrite;

                    // ==== Owner-writable NetworkVariable ====
                    VerboseDebug($"Writing to owner-write variable on object {objectIndex} on client {clientWriting}");

                    nextValueToWrite++;
                    if (clientWriting != objectIndex)
                    {
                        LogAssert.Expect(LogType.Error, OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkVariableOwner.GetWritePermissionError());
                    }
                    OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkVariableOwner.Value = nextValueToWrite;

                    // ==== Server-writable NetworkList ====
                    VerboseDebug($"Writing to [Add] server-write NetworkList on object {objectIndex} on client {clientWriting}");

                    nextValueToWrite++;
                    if (clientWriting != serverIndex)
                    {
                        LogAssert.Expect(LogType.Error, OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkListServer.GetWritePermissionError());
                    }
                    OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkListServer.Add(nextValueToWrite);

                    // ==== Owner-writable NetworkList ====
                    VerboseDebug($"Writing to [Add] owner-write NetworkList on object {objectIndex} on client {clientWriting}");

                    nextValueToWrite++;
                    if (clientWriting != objectIndex)
                    {
                        LogAssert.Expect(LogType.Error, OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkListOwner.GetWritePermissionError());
                    }
                    OwnerPermissionObject.Objects[objectIndex, clientWriting].MyNetworkListOwner.Add(nextValueToWrite);

                    yield return WaitForTicks(m_ServerNetworkManager, 5);
                    yield return WaitForTicks(m_ClientNetworkManagers[0], 5);
                    yield return WaitForTicks(m_ClientNetworkManagers[1], 5);

                    OwnerPermissionObject.VerifyConsistency();
                }
            }
        }
    }
}
