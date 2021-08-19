using System;
using System.Collections.Generic;
using System.Diagnostics;
using MLAPI.RuntimeTests;
using UnityEngine;
using NUnit.Framework;
using Unity.Netcode.Interest;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode.RuntimeTests
{
    public class TestInterestSettings : InterestSettings
    {
        public int SomeSetting;
    }

    public class InterestTests
    {
        [SetUp]
        public void Setup()
        {
            // Create, instantiate, and host
            NetworkManagerHelper.StartNetworkManager(out NetworkManager networkManager,
                NetworkManagerHelper.NetworkManagerOperatingMode.Server);
        }

        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        public class OddEvenInterestKernel : InterestKernel<NetworkClient, NetworkObject>
        {
            public bool IsOdd = true;
            public override void QueryFor(in NetworkClient client, in NetworkObject obj, HashSet<NetworkObject> results)
            {
                if (obj.NetworkObjectId % 2 == 0 ^ IsOdd)
                {
                    results.Add(obj);
                }
            }
        }

    public class OddsEvensNode : InterestNode<NetworkClient, NetworkObject>
    {
        public OddsEvensNode()
        {
            m_Odds = new InterestNodeStatic<NetworkClient, NetworkObject>();
            m_Evens = new InterestNodeStatic<NetworkClient, NetworkObject>();
        }

        public override void AddObject(in NetworkObject obj)
        {
            if (obj.NetworkObjectId % 2 == 0)
            {
                m_Evens.AddObject(obj);
            }
            else
            {
                m_Odds.AddObject(obj);
            }
        }

        public override void RemoveObject(in NetworkObject obj)
        {
            if (obj.NetworkObjectId % 2 == 0)
            {
                m_Evens.RemoveObject(obj);
            }
            else
            {
                m_Odds.RemoveObject(obj);
            }
        }

        public override void QueryFor(in NetworkClient client, HashSet<NetworkObject> results)
        {
            // if a client with an odd NetworkObjectID queries, we return objects with odd NetworkObjectIDs
            if (client.PlayerObject.NetworkObjectId % 2 == 0)
            {
                m_Evens.QueryFor(client, results);
            }
            else
            {
                m_Odds.QueryFor(client, results);
            }
        }

        public override void UpdateObject(in NetworkObject obj)
        {
            m_Odds.RemoveObject(obj);
            m_Evens.RemoveObject(obj);
            AddObject(obj);
        }

        private InterestNodeStatic<NetworkClient, NetworkObject> m_Odds;
        private InterestNodeStatic<NetworkClient, NetworkObject> m_Evens;
    }

        private (NetworkObject, Guid) MakeGameInterestObjectHelper(InterestNode<NetworkClient, NetworkObject> comn = null)
        {
            Guid objGuid = NetworkManagerHelper.AddGameNetworkObject("");
            NetworkObject no = (NetworkObject)NetworkManagerHelper.InstantiatedNetworkObjects[objGuid];

            if (comn != null)
            {
                no.InterestNodes.Add(comn);
            }

            return (no, objGuid);
        }

        private (NetworkObject, Guid) MakeGameInterestObjectHelper(Vector3 coords, InterestNode<NetworkClient, NetworkObject> comn = null)
        {
            Guid objGuid = NetworkManagerHelper.AddGameNetworkObject("");
            NetworkObject no = (NetworkObject)NetworkManagerHelper.InstantiatedNetworkObjects[objGuid];

            if (comn != null)
            {
                no.InterestNodes.Add(comn);
                no.transform.position = coords;
            }

            return (no, objGuid);
        }

        [Test]
        // Start is called before the first frame update
        public void InterestCustomStorageTests()
        {
            var oddsEvensNode = new OddsEvensNode();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var objectsBeforeAdd = results.Count;

            var numNodes = 4;
            var nodes = new NetworkObject[4];
            for (var i = 0; i < numNodes; i++)
            {
                var (thisObj, thisGuid)  = MakeGameInterestObjectHelper(oddsEvensNode);
                nodes[i] = thisObj;
                nodes[i].NetworkObjectId = (ulong)(i + 100);
                NetworkManagerHelper.SpawnNetworkObject(thisGuid);
            }

            var (obj, guid) = MakeGameInterestObjectHelper();
            nc.PlayerObject = obj;

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2);

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2);

            // now re-number the nodes
            for (var i = 0; i < numNodes; i++)
            {
                nodes[i].NetworkObjectId = (ulong)(2 * i + 101); // 101, 103, 105, 107
            }

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2); // should be unchanged

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2); // should be unchanged

            // now update the nodes
            for (var i = 0; i < numNodes; i++)
            {
                nodes[i].UpdateInterest();
            }

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == 0); // now zero, all are odd

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == nodes.Length); // now 4, all are odd
        }


        [Test]
        // Start is called before the first frame update
        public void InterestRadiusCheck()
        {
            InterestNodeStatic<NetworkClient, NetworkObject> naiveRadiusNode = new InterestNodeStatic<NetworkClient, NetworkObject>();
            var naiveRadiusKernel = new RadiusInterestKernel();
            naiveRadiusKernel.Radius = 1.5f;
            naiveRadiusNode.InterestKernels.Add(naiveRadiusKernel);
            var staticNode = new InterestNodeStatic<NetworkClient, NetworkObject>();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            var (playerObj, playerGuid) = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);
            nc.PlayerObject = playerObj;

            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            int objectsBeforeAdd = results.Count;

            var (ok1Obj, ok1Guid) = MakeGameInterestObjectHelper(new Vector3(0.5f, 0.0f, 0.0f), naiveRadiusNode);
            NetworkManagerHelper.SpawnNetworkObject(ok1Guid);

            var (ok2Obj, ok2Guid) = MakeGameInterestObjectHelper(new Vector3(1.0f, 0.0f, 0.0f), naiveRadiusNode);
            NetworkManagerHelper.SpawnNetworkObject(ok2Guid);

            var (tooFarObj, tooFarGuid) = MakeGameInterestObjectHelper(new Vector3(3.0f, 0.0f, 0.0f), naiveRadiusNode);
            NetworkManagerHelper.SpawnNetworkObject(tooFarGuid);

            var (alwaysObj, alwaysGuid)  = MakeGameInterestObjectHelper(new Vector3(99.0f, 99.0f, 99.0f), staticNode);
            NetworkManagerHelper.SpawnNetworkObject(alwaysGuid);

            NetworkManagerHelper.SpawnNetworkObject(playerGuid);

            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var hits = results.Count - objectsBeforeAdd;
            Assert.True(results.Contains(ok1Obj));
            Assert.True(results.Contains(ok2Obj));
            Assert.True(results.Contains(nc.PlayerObject));
            Assert.True(results.Contains(alwaysObj));
            Assert.False(results.Contains(tooFarObj));
            Assert.True(hits == 4);

            // remove an object, should not be in replication manager
            alwaysObj.Despawn();
            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            hits = results.Count - objectsBeforeAdd;
            Assert.True(results.Contains(ok1Obj));
            Assert.True(results.Contains(ok2Obj));
            Assert.True(results.Contains(nc.PlayerObject));
            Assert.False(results.Contains(alwaysObj));
            Assert.False(results.Contains(tooFarObj));
            Assert.True(hits == 3);
        }

        [Test]
        public void CheckMultipleNodes()
        {
            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var objectsBeforeAdd = results.Count;

            var dualNode = new InterestNodeStatic<NetworkClient, NetworkObject>();
            var oddKernel = new OddEvenInterestKernel();
            oddKernel.IsOdd = true;
            var evenKernel = new OddEvenInterestKernel();
            evenKernel.IsOdd = false;
            dualNode.InterestKernels.Add(oddKernel);
            dualNode.InterestKernels.Add(evenKernel);

            var (object1, object1Guid) = MakeGameInterestObjectHelper(dualNode);
            NetworkManagerHelper.SpawnNetworkObject(object1Guid);

            var (object2, object2Guid) = MakeGameInterestObjectHelper(dualNode);
            NetworkManagerHelper.SpawnNetworkObject(object2Guid);

            var (playerObject, playerObjGuid) = MakeGameInterestObjectHelper(dualNode);
            nc.PlayerObject = playerObject;
            NetworkManagerHelper.SpawnNetworkObject(playerObjGuid);

            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var hits = results.Count;
            Assert.True(hits == (3 + objectsBeforeAdd));
            Assert.True(results.Contains(object1));
            Assert.True(results.Contains(object2));
            Assert.True(results.Contains(nc.PlayerObject));
        }


        [Test]
        public void PerfTest()
        {
            var clock = new HRTClock();
            var duration = TimeSpan.FromSeconds(5);
            var distinctValues = new HashSet<DateTime>();
            var stopWatch = Stopwatch.StartNew();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            var t = clock.UtcNow;
            for (var z = 0; z < 10000; z++)
            {
                NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            }

            var tElapsed = clock.UtcNow - t;
            Debug.Log($"Time: {tElapsed} ms");

            var objectsBeforeAdd = results.Count;

            var objsToMakePerNode = 10;
            var nodesToMake = 100;
            var objsToMake = objsToMakePerNode * nodesToMake;
            List<InterestNode<NetworkClient, NetworkObject>> nodes = new List<InterestNode<NetworkClient, NetworkObject>>();
            List<NetworkObject> objs = new List<NetworkObject>();

            for (var i = 0; i < nodesToMake; ++i)
            {
                nodes.Add(new InterestNodeStatic<NetworkClient, NetworkObject>());
                for (var j = 0; j < objsToMakePerNode; j++)
                {
                    var (obj, guid) = MakeGameInterestObjectHelper(nodes[i]);
                    NetworkManagerHelper.SpawnNetworkObject(guid);
                    objs.Add(obj);
                }
            }

            var (playerObj, playerObjGuid) = MakeGameInterestObjectHelper();
            nc.PlayerObject = playerObj;
            NetworkManagerHelper.SpawnNetworkObject(playerObjGuid);

            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);

            // reality check
            var hits = results.Count;
            Debug.Log(hits);
            Assert.True(hits == objsToMake + objectsBeforeAdd + 1);

            // each of the nodes should have returned objsToMakePerNode
            for (var i = 0; i < nodesToMake; ++i)
            {
                var nodeResults = new HashSet<NetworkObject>();
                nodes[i].QueryFor(nc, nodeResults);
                Assert.True(nodeResults.Count == objsToMakePerNode);
            }
        }

        [Test]
        public void CheckPlainReplication()
        {
            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var objectsBeforeAdd = results.Count;

            var (object1, object1Guid) = MakeGameInterestObjectHelper();
            NetworkManagerHelper.SpawnNetworkObject(object1Guid);

            var (object2, object2Guid) = MakeGameInterestObjectHelper();
            NetworkManagerHelper.SpawnNetworkObject(object2Guid);

            var (playerObj, playerObjGuid) = MakeGameInterestObjectHelper();
            nc.PlayerObject = playerObj;
            NetworkManagerHelper.SpawnNetworkObject(playerObjGuid);

            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            var hits = results.Count;
            Assert.True(hits == (3 + objectsBeforeAdd));
            Assert.True(results.Contains(object1));
            Assert.True(results.Contains(object2));
            Assert.True(results.Contains(nc.PlayerObject));

            // remove an object, should not be in replication manager
            object1.Despawn();
            object2.Despawn();
            playerObj.Despawn();
            results.Clear();
            NetworkManager.Singleton.InterestManager.QueryFor(nc, results);
            hits = results.Count;
            Assert.True(hits == (objectsBeforeAdd));
        }

        [Test]
        public void TestInterestSettings()
        {
            var rootSettings = new TestInterestSettings();
            rootSettings.SomeSetting = 1;
            NetworkManager.Singleton.InterestSettings = rootSettings;

            var objSettings = new TestInterestSettings();
            objSettings.SomeSetting = 2;
            var (object1Obj, object1Guid) = MakeGameInterestObjectHelper();
            object1Obj.InterestSettings = objSettings;

            // no override settings, should receive from NetworkManager
            var (object2Obj, object2Guid) = MakeGameInterestObjectHelper();

            var checkObj1 = (TestInterestSettings)object1Obj.InterestSettings;
            var checkObj2 = (TestInterestSettings)object2Obj.InterestSettings;

            Assert.True(checkObj1.SomeSetting == 2);
            Assert.True(checkObj2.SomeSetting == 1);
        }
    }
}
