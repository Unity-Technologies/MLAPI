#if !NGO_MINIMALPROJECT
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixtureSource(nameof(TestDataSource))]
    internal class NetworkVariablePermissionTests : NetcodeIntegrationTest
    {
        public static IEnumerable<TestFixtureData> TestDataSource()
        {
            NetworkVariableBase.IgnoreInitializeWarning = true;
            foreach (HostOrServer hostOrServer in Enum.GetValues(typeof(HostOrServer)))
            {
                // DANGO-EXP TODO: Add support for distributed authority mode
                if (hostOrServer == HostOrServer.DAHost)
                {
                    continue;
                }
                yield return new TestFixtureData(hostOrServer);
            }

            NetworkVariableBase.IgnoreInitializeWarning = false;
        }

        protected override int NumberOfClients => 3;

        public NetworkVariablePermissionTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        private GameObject m_TestObjPrefab;
        private ulong m_TestObjId = 0;

        protected override void OnServerAndClientsCreated()
        {
            m_TestObjPrefab = CreateNetworkObjectPrefab($"[{nameof(NetworkVariablePermissionTests)}.{nameof(m_TestObjPrefab)}]");
            var testComp = m_TestObjPrefab.AddComponent<NetVarPermTestComp>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            m_TestObjId = SpawnObject(m_TestObjPrefab, m_ServerNetworkManager).GetComponent<NetworkObject>().NetworkObjectId;
            yield return null;
        }

        private IEnumerator WaitForPositionsAreEqual(NetworkVariable<Vector3> netvar, Vector3 expected)
        {
            yield return WaitForConditionOrTimeOut(() => netvar.Value == expected);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut);
        }

        private IEnumerator WaitForOwnerWritableAreEqualOnAll()
        {
            yield return WaitForConditionOrTimeOut(CheckOwnerWritableAreEqualOnAll);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut);
        }

        private bool CheckOwnerWritableAreEqualOnAll()
        {
            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var testObjClient = clientNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
                var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();
                if (testObjServer.OwnerClientId != testObjClient.OwnerClientId ||
                    testCompServer.OwnerWritable_Position.Value != testCompClient.OwnerWritable_Position.Value ||
                    testCompServer.OwnerWritable_Position.ReadPerm != testCompClient.OwnerWritable_Position.ReadPerm ||
                    testCompServer.OwnerWritable_Position.WritePerm != testCompClient.OwnerWritable_Position.WritePerm)
                {
                    return false;
                }
            }
            return true;
        }

        private IEnumerator WaitForServerWritableAreEqualOnAll()
        {
            yield return WaitForConditionOrTimeOut(CheckServerWritableAreEqualOnAll);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut);
        }

        private bool CheckServerWritableAreEqualOnAll()
        {
            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var testObjClient = clientNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
                var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();
                if (testCompServer.ServerWritable_Position.Value != testCompClient.ServerWritable_Position.Value ||
                    testCompServer.ServerWritable_Position.ReadPerm != testCompClient.ServerWritable_Position.ReadPerm ||
                    testCompServer.ServerWritable_Position.WritePerm != testCompClient.ServerWritable_Position.WritePerm)
                {
                    return false;
                }
            }
            return true;
        }

        private bool CheckOwnerReadWriteAreEqualOnOwnerAndServer()
        {
            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var testObjClient = clientNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
                var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();
                if (testObjServer.OwnerClientId == testObjClient.OwnerClientId &&
                    testCompServer.OwnerReadWrite_Position.Value == testCompClient.ServerWritable_Position.Value &&
                    testCompServer.OwnerReadWrite_Position.ReadPerm == testCompClient.ServerWritable_Position.ReadPerm &&
                    testCompServer.OwnerReadWrite_Position.WritePerm == testCompClient.ServerWritable_Position.WritePerm)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckOwnerReadWriteAreNotEqualOnNonOwnerClients(NetVarPermTestComp ownerReadWriteObject)
        {
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var testObjClient = clientNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
                var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();
                if (testObjClient.OwnerClientId != ownerReadWriteObject.OwnerClientId ||
                    ownerReadWriteObject.OwnerReadWrite_Position.Value == testCompClient.ServerWritable_Position.Value ||
                    ownerReadWriteObject.OwnerReadWrite_Position.ReadPerm != testCompClient.ServerWritable_Position.ReadPerm ||
                    ownerReadWriteObject.OwnerReadWrite_Position.WritePerm != testCompClient.ServerWritable_Position.WritePerm)
                {
                    return false;
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator ServerChangesOwnerWritableNetVar()
        {
            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();

            var oldValue = testCompServer.OwnerWritable_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));

            testCompServer.OwnerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompServer.OwnerWritable_Position, newValue);

            yield return WaitForOwnerWritableAreEqualOnAll();
        }

        [UnityTest]
        public IEnumerator ServerChangesServerWritableNetVar()
        {
            yield return WaitForServerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();

            var oldValue = testCompServer.ServerWritable_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));

            testCompServer.ServerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompServer.ServerWritable_Position, newValue);

            yield return WaitForServerWritableAreEqualOnAll();
        }

        [UnityTest]
        public IEnumerator ClientChangesOwnerWritableNetVar()
        {
            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];

            int clientManagerIndex = m_ClientNetworkManagers.Length - 1;
            var newOwnerClientId = m_ClientNetworkManagers[clientManagerIndex].LocalClientId;
            testObjServer.ChangeOwnership(newOwnerClientId);
            yield return WaitForTicks(m_ServerNetworkManager, 2);

            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjClient = m_ClientNetworkManagers[clientManagerIndex].SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();

            var oldValue = testCompClient.OwnerWritable_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));

            testCompClient.OwnerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompClient.OwnerWritable_Position, newValue);

            yield return WaitForOwnerWritableAreEqualOnAll();
        }

        /// <summary>
        /// This tests the scenario where a client owner has both read and write
        /// permissions set. The server should be the only instance that can read
        /// the NetworkVariable.  ServerCannotChangeOwnerWritableNetVar performs
        /// the same check to make sure the server cannot write to a client owner
        /// NetworkVariable with owner write permissions.
        /// </summary>
        [UnityTest]
        public IEnumerator ClientOwnerWithReadWriteChangesNetVar()
        {
            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];

            int clientManagerIndex = m_ClientNetworkManagers.Length - 1;
            var newOwnerClientId = m_ClientNetworkManagers[clientManagerIndex].LocalClientId;
            testObjServer.ChangeOwnership(newOwnerClientId);
            yield return WaitForTicks(m_ServerNetworkManager, 2);

            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjClient = m_ClientNetworkManagers[clientManagerIndex].SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();

            var oldValue = testCompClient.OwnerReadWrite_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));

            testCompClient.OwnerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompClient.OwnerWritable_Position, newValue);

            // Verify the client owner and server match
            yield return CheckOwnerReadWriteAreEqualOnOwnerAndServer();

            // Verify the non-owner clients do not have the same Value but do have the same permissions
            yield return CheckOwnerReadWriteAreNotEqualOnNonOwnerClients(testCompClient);
        }


        [UnityTest]
        public IEnumerator ClientCannotChangeServerWritableNetVar()
        {
            yield return WaitForServerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();

            int clientManagerIndex = m_ClientNetworkManagers.Length - 1;
            var newOwnerClientId = m_ClientNetworkManagers[clientManagerIndex].LocalClientId;
            testObjServer.ChangeOwnership(newOwnerClientId);
            yield return WaitForTicks(m_ServerNetworkManager, 2);

            yield return WaitForServerWritableAreEqualOnAll();

            var testObjClient = m_ClientNetworkManagers[clientManagerIndex].SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();

            var oldValue = testCompClient.ServerWritable_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));
            LogAssert.Expect(LogType.Error, testCompClient.ServerWritable_Position.GetWritePermissionError());
            testCompClient.ServerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompServer.ServerWritable_Position, oldValue);

            yield return WaitForServerWritableAreEqualOnAll();

            testCompServer.ServerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompServer.ServerWritable_Position, newValue);

            yield return WaitForServerWritableAreEqualOnAll();
        }

        [UnityTest]
        public IEnumerator ServerCannotChangeOwnerWritableNetVar()
        {
            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjServer = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompServer = testObjServer.GetComponent<NetVarPermTestComp>();

            int clientManagerIndex = m_ClientNetworkManagers.Length - 1;
            var newOwnerClientId = m_ClientNetworkManagers[clientManagerIndex].LocalClientId;
            testObjServer.ChangeOwnership(newOwnerClientId);
            yield return WaitForTicks(m_ServerNetworkManager, 4);

            yield return WaitForOwnerWritableAreEqualOnAll();

            var oldValue = testCompServer.OwnerWritable_Position.Value;
            var newValue = oldValue + new Vector3(Random.Range(0, 100.0f), Random.Range(0, 100.0f), Random.Range(0, 100.0f));

            LogAssert.Expect(LogType.Error, testCompServer.OwnerWritable_Position.GetWritePermissionError());
            testCompServer.OwnerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompServer.OwnerWritable_Position, oldValue);

            yield return WaitForOwnerWritableAreEqualOnAll();

            var testObjClient = m_ClientNetworkManagers[clientManagerIndex].SpawnManager.SpawnedObjects[m_TestObjId];
            var testCompClient = testObjClient.GetComponent<NetVarPermTestComp>();

            testCompClient.OwnerWritable_Position.Value = newValue;
            yield return WaitForPositionsAreEqual(testCompClient.OwnerWritable_Position, newValue);

            yield return WaitForOwnerWritableAreEqualOnAll();
        }
    }
}
#endif
