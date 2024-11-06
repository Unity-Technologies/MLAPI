using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    // This is a bit of a quirky test.
    // Addresses MTT-4386 #2109
    // Where the NetworkVariable updates would be repeated on some clients.
    // The twist comes fom the updates needing to happens very specifically for the issue to repro in tests

    internal class OwnerModifiedObject : NetworkBehaviour, INetworkUpdateSystem
    {
        public NetworkList<int> MyNetworkList;

        internal static int Updates = 0;

        public static bool EnableVerbose;

        private void Awake()
        {
            MyNetworkList = new NetworkList<int>(new List<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            MyNetworkList.OnListChanged += Changed;
        }

        public void Changed(NetworkListEvent<int> listEvent)
        {
            var expected = 0;
            var listString = "";
            foreach (var i in MyNetworkList)
            {
                Assert.AreEqual(i, expected);
                expected++;
                listString += i.ToString();
            }
            if (EnableVerbose)
            {
                Debug.Log($"[{NetworkManager.LocalClientId}] Value changed to {listString}");
            }

            Updates++;
        }

        public bool AddValues;

        public NetworkUpdateStage NetworkUpdateStageToCheck;

        private int m_ValueToUpdate;

        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            if (updateStage == NetworkUpdateStageToCheck)
            {
                if (AddValues)
                {
                    MyNetworkList.Add(m_ValueToUpdate++);
                    AddValues = false;
                }
            }
        }

        public override void OnDestroy()
        {
            NetworkUpdateLoop.UnregisterAllNetworkUpdates(this);
            base.OnDestroy();
        }

        public void InitializeLastCient()
        {
            NetworkUpdateLoop.RegisterAllNetworkUpdates(this);
        }
    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    internal class OwnerModifiedTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        public OwnerModifiedTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<OwnerModifiedObject>();
        }

        [UnityTest]
        public IEnumerator OwnerModifiedTest()
        {
            OwnerModifiedObject.EnableVerbose = m_EnableVerboseDebug;
            // We use this to assure we are the "last client" connected.
            yield return CreateAndStartNewClient();
            var ownerModLastClient = m_ClientNetworkManagers[2].LocalClient.PlayerObject.GetComponent<OwnerModifiedObject>();
            ownerModLastClient.InitializeLastCient();

            // Run through all update loops setting the value once every 5 frames
            foreach (var updateLoopType in System.Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                ownerModLastClient.NetworkUpdateStageToCheck = (NetworkUpdateStage)updateLoopType;
                VerboseDebug($"Testing Update Stage: {ownerModLastClient.NetworkUpdateStageToCheck}");
                ownerModLastClient.AddValues = true;
                yield return WaitForTicks(m_ServerNetworkManager, 5);
            }

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            // We'll have at least one update per stage per client, if all goes well.
            Assert.True(OwnerModifiedObject.Updates > 20);
        }
    }
}
