using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class ParentingWorldPositionStaysTests : IntegrationTestWithApproximation
    {
        private const int k_NestedChildren = 10;
        private const string k_ParentName = "Parent";
        private const string k_ChildName = "Child";

        protected override int NumberOfClients => 2;

        internal class TestComponentHelper : NetworkBehaviour
        {
            internal class ChildInfo
            {
                public bool HasBeenParented;
                public GameObject Child;
            }

            internal class ParentChildInfo
            {
                public GameObject RootParent;
                public List<ChildInfo> Children = new List<ChildInfo>();
            }

            public static Dictionary<ulong, int> NetworkObjectIdToIndex = new Dictionary<ulong, int>();

            public static Dictionary<ulong, ParentChildInfo> ClientsRegistered = new Dictionary<ulong, ParentChildInfo>();

            public Vector3 Scale;
            public bool WorldPositionStays;

            public override void OnNetworkSpawn()
            {
                if (!IsServer)
                {
                    var localClientId = NetworkManager.LocalClientId;
                    if (!ClientsRegistered.ContainsKey(localClientId))
                    {
                        ClientsRegistered.Add(localClientId, new ParentChildInfo());
                        // Fill the expected entries with null values
                        for (int i = 0; i < k_NestedChildren; i++)
                        {
                            ClientsRegistered[localClientId].Children.Add(new ChildInfo());
                        }
                    }

                    var entryToModify = ClientsRegistered[NetworkManager.LocalClientId];
                    if (gameObject.name.Contains(k_ParentName))
                    {
                        if (entryToModify.RootParent == null)
                        {
                            entryToModify.RootParent = gameObject;
                            return;
                        }
                        else
                        {
                            throw new Exception($"Failed to assigned {gameObject.name} as a parent!  {nameof(GameObject)} {entryToModify.RootParent.name} is already assigned to Client-{localClientId}'s parent entry!");
                        }
                    }


                    if (gameObject.name.Contains(k_ChildName))
                    {
                        if (!NetworkObjectIdToIndex.ContainsKey(NetworkObjectId))
                        {
                            //This should never happen (sanity check)
                            throw new Exception($"Client spawned {NetworkObjectId} but there was no index lookup table!");
                        }
                        var childIndex = NetworkObjectIdToIndex[NetworkObjectId];
                        var childInfo = ClientsRegistered[localClientId].Children[childIndex];
                        if (childInfo.Child == null)
                        {
                            childInfo.Child = gameObject;
                            ClientsRegistered[localClientId].Children[childIndex] = childInfo;
                            return;
                        }
                        else
                        {
                            throw new Exception($"Failed to assigned {gameObject.name} already assigned!  {nameof(GameObject)} {ClientsRegistered[localClientId].Children[childIndex].Child.name} is already assigned to Client-{localClientId}'s child entry!");
                        }
                    }
                    // We should never reach this point
                    throw new Exception($"We spawned {name} but did not assign anything!");
                }
                base.OnNetworkSpawn();
            }

            public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
            {
                base.OnNetworkObjectParentChanged(parentNetworkObject);
                if (parentNetworkObject == null || IsServer)
                {
                    if (WorldPositionStays)
                    {
                        transform.localScale = Scale;
                    }
                    return;
                }
                var localClientId = NetworkManager.LocalClientId;
                if (!ClientsRegistered.ContainsKey(localClientId))
                {
                    throw new Exception($"Parented {gameObject.name} before it was spawned!");
                }

                var netObjId = NetworkObject.NetworkObjectId;
                var childIndex = NetworkObjectIdToIndex[netObjId];
                var childInfo = ClientsRegistered[localClientId].Children[childIndex];

                if (!NetworkObjectIdToIndex.ContainsKey(netObjId))
                {
                    if (netObjId == 0)
                    {
                        return;
                    }
                    //This should never happen (sanity check)
                    throw new Exception($"Client spawned {NetworkObjectId} but there was no index lookup table!");
                }

                childInfo.HasBeenParented = true;
            }
        }

        public enum ParentingTestModes
        {
            LocalPositionStays,
            WorldPositionStays
        }

        public enum NetworkTransformSettings
        {
            None,
            NetworkTransformInterpolate,
            NetworkTransformImmediate
        }

        private GameObject m_ParentPrefabObject;
        private GameObject m_ChildPrefabObject;

        private GameObject m_ServerSideParent;
        private List<GameObject> m_ServerSideChildren = new List<GameObject>();

        private Vector3 m_ParentStartPosition = new Vector3(1.0f, 1.0f, 1.0f);
        private Quaternion m_ParentStartRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
        private Vector3 m_ChildStartPosition = new Vector3(100.0f, -100.0f, 100.0f);
        private Quaternion m_ChildStartRotation = Quaternion.Euler(-35.0f, 0.0f, -180.0f);
        private Vector3 m_ChildStartScale = Vector3.one;

        public ParentingWorldPositionStaysTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override IEnumerator OnSetup()
        {
            TestComponentHelper.ClientsRegistered.Clear();
            TestComponentHelper.NetworkObjectIdToIndex.Clear();
            for (int i = 0; i < k_NestedChildren; i++)
            {
                m_ServerSideChildren.Add(null);
            }
            return base.OnSetup();
        }

        protected override IEnumerator OnTearDown()
        {
            if (m_ServerSideParent != null && m_ServerSideParent.GetComponent<NetworkObject>().IsSpawned)
            {
                // Clean up in reverse order (also makes sure we can despawn parents before children)
                m_ServerSideParent.GetComponent<NetworkObject>().Despawn();
            }

            // Now despawn the children
            // (and clean up our test)
            for (int i = 0; i < k_NestedChildren; i++)
            {
                var serverSideChild = m_ServerSideChildren[i];
                if (serverSideChild != null && serverSideChild.GetComponent<NetworkObject>().IsSpawned)
                {
                    serverSideChild.GetComponent<NetworkObject>().Despawn();
                }
            }

            // Just allow the clients to run through despawning (also assures nothing throws an exception when destroying)
            yield return new WaitForSeconds(0.2f);

            m_ServerSideChildren.Clear();
            TestComponentHelper.ClientsRegistered.Clear();
            TestComponentHelper.NetworkObjectIdToIndex.Clear();
            m_ParentPrefabObject = null;
            m_ChildPrefabObject = null;
            m_ServerSideParent = null;
            yield return base.OnTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ParentPrefabObject = CreateNetworkObjectPrefab(k_ParentName);
            m_ParentPrefabObject.AddComponent<TestComponentHelper>();
            m_ParentPrefabObject.transform.position = m_ParentStartPosition;
            m_ParentPrefabObject.transform.rotation = m_ParentStartRotation;
            m_ChildPrefabObject = CreateNetworkObjectPrefab(k_ChildName);
            m_ChildPrefabObject.AddComponent<TestComponentHelper>();
            m_ChildPrefabObject.transform.position = m_ChildStartPosition;
            m_ChildPrefabObject.transform.rotation = m_ChildStartRotation;
            m_ChildPrefabObject.transform.localScale = m_ChildStartScale;
            m_ServerNetworkManager.LogLevel = m_EnableVerboseDebug ? LogLevel.Developer : LogLevel.Normal;

            base.OnServerAndClientsCreated();
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            foreach (var networkPrefab in m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            }
        }

        private bool HaveAllClientsSpawnedObjects()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                if (!s_GlobalNetworkObjects.ContainsKey(client.LocalClientId))
                {
                    return false;
                }
                var clientSpawnedObjects = s_GlobalNetworkObjects[client.LocalClientId];
                foreach (var gameObject in m_ServerSideChildren)
                {
                    var networkOject = gameObject.GetComponent<NetworkObject>();
                    if (!clientSpawnedObjects.ContainsKey(networkOject.NetworkObjectId))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool HaveAllClientsParentedChild()
        {
            foreach (var clientEntries in TestComponentHelper.ClientsRegistered)
            {
                foreach (var clientInfo in clientEntries.Value.Children)
                {
                    if (!clientInfo.HasBeenParented)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Verifies that using worldPositionStays when parenting via NetworkObject.TrySetParent,
        /// that the client-side transform values match that of the server-side.
        /// This also tests nested parenting and out of hierarchical order child spawning.
        /// </summary>
        [UnityTest]
        public IEnumerator WorldPositionStaysTest([Values] ParentingTestModes mode, [Values] NetworkTransformSettings networkTransformSettings)
        {
            var useNetworkTransform = networkTransformSettings != NetworkTransformSettings.None;
            var interpolate = networkTransformSettings == NetworkTransformSettings.NetworkTransformInterpolate;
            var worldPositionStays = mode == ParentingTestModes.WorldPositionStays;
            var startTime = Time.realtimeSinceStartup;
            m_ServerSideParent = Object.Instantiate(m_ParentPrefabObject);

            var serverSideChildNetworkObjects = new List<NetworkObject>();
            var childPosition = m_ChildStartPosition;
            var childRotation = m_ChildStartRotation;
            var childScale = m_ChildStartScale;
            // Used to store the expected position and rotation for children (local space relative)
            var childPositionList = new List<Vector3>();
            var childRotationList = new List<Vector3>();
            var childScaleList = new List<Vector3>();
            var childLarger = 1.15f;
            var childSmaller = 0.85f;
            if (useNetworkTransform)
            {
                var networkTransform = m_ChildPrefabObject.AddComponent<NetworkTransform>();
                networkTransform.InLocalSpace = !worldPositionStays;
            }

            var serverSideParentNetworkObject = m_ServerSideParent.GetComponent<NetworkObject>();
            serverSideParentNetworkObject.Spawn();

            // Instantiate the children
            for (int i = 0; i < k_NestedChildren; i++)
            {
                m_ServerSideChildren[i] = Object.Instantiate(m_ChildPrefabObject);
                childPositionList.Add(childPosition);
                childRotationList.Add(childRotation.eulerAngles);
                childScaleList.Add(childScale);
                // Change each child's position, rotation, and scale
                childRotation = Quaternion.Euler(childRotation.eulerAngles * 0.80f);
                childPosition = childPosition * 0.80f;
                if ((i % 2) == 0)
                {
                    childScale = m_ChildStartScale * childLarger;
                    childLarger *= childLarger;
                }
                else
                {
                    childScale = m_ChildStartScale * childSmaller;
                    childSmaller *= childSmaller;
                }
                var serverSideChild = m_ServerSideChildren[i];

                var serverSideChildNetworkObject = serverSideChild.GetComponent<NetworkObject>();
                serverSideChild.transform.position = childPositionList[i];
                serverSideChild.transform.rotation = Quaternion.Euler(childRotationList[i]);

                serverSideChild.transform.localScale = childScaleList[i];
                VerboseDebug($"[Server][PreSpawn] Set scale of NetworkObject to ({childScaleList[i]})");
                serverSideChildNetworkObject.Spawn();
                VerboseDebug($"[Server] Set scale of NetworkObjectID ({serverSideChildNetworkObject.NetworkObjectId}) to ({childScaleList[i]}) and is currently {serverSideChild.transform.localScale}");

                TestComponentHelper.NetworkObjectIdToIndex.Add(serverSideChildNetworkObject.NetworkObjectId, i);
                Assert.IsTrue(Approximately(m_ServerSideParent.transform.position, m_ParentStartPosition));
                Assert.IsTrue(Approximately(m_ServerSideParent.transform.rotation.eulerAngles, m_ParentStartRotation.eulerAngles));
                Assert.IsTrue(Approximately(serverSideChild.transform.position, childPositionList[i]));
                Assert.IsTrue(Approximately(serverSideChild.transform.rotation.eulerAngles, childRotationList[i]));
                Assert.IsTrue(Approximately(serverSideChild.transform.localScale, childScaleList[i]), $"[Initial Scale] Server-side child scale ({serverSideChild.transform.localScale}) does not equal the expected scale ({childScaleList[i]})");
            }

            VerboseDebug($"[{Time.realtimeSinceStartup - startTime}] Spawned parent and child objects.");

            // Wait for clients to spawn the NetworkObjects
            yield return WaitForConditionOrTimeOut(HaveAllClientsSpawnedObjects);
            AssertOnTimeout("Timed out waiting for all clients to spawn the respective parent and child objects");
            VerboseDebug($"[{Time.realtimeSinceStartup - startTime}] Clients spawned parent and child objects.");

            // Verify the positions are identical to the default values
            foreach (var clientEntry in TestComponentHelper.ClientsRegistered)
            {
                var children = clientEntry.Value.Children;
                var rootParent = clientEntry.Value.RootParent;
                Assert.IsTrue(Approximately(rootParent.transform.position, m_ParentStartPosition));
                Assert.IsTrue(Approximately(rootParent.transform.rotation.eulerAngles, m_ParentStartRotation.eulerAngles));
                for (int i = 0; i < k_NestedChildren; i++)
                {
                    var clientChildInfo = children[i];
                    var serverChild = m_ServerSideChildren[i];

                    Assert.IsFalse(clientChildInfo.HasBeenParented, $"Client-{clientEntry.Key} has already been parented!");

                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.position, serverChild.transform.position), $"[Client-{clientEntry.Key}][{clientChildInfo.Child.name}] Child position ({clientChildInfo.Child.transform.position}) does not" +
                        $" equal the server-side child's position ({serverChild.transform.position})");
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.eulerAngles, serverChild.transform.eulerAngles), $"[Client-{clientEntry.Key}][{clientChildInfo.Child.name}] Child rotation ({clientChildInfo.Child.transform.eulerAngles}) does not" +
                        $" equal the server-side child's rotation ({serverChild.transform.eulerAngles})");
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localScale, serverChild.transform.localScale), $"[Client-{clientEntry.Key}][{clientChildInfo.Child.name}] Child scale ({clientChildInfo.Child.transform.localScale}) does not" +
                        $" equal the server-side child's scale ({serverChild.transform.localScale})");
                }
            }

            var currentParent = serverSideParentNetworkObject;
            for (int i = 0; i < k_NestedChildren; i++)
            {
                var childNetworkObject = m_ServerSideChildren[i].GetComponent<NetworkObject>();
                VerboseDebug($"[Server Parenting][Before] Scale of NetworkObjectID ({childNetworkObject.NetworkObjectId}) is currently {childNetworkObject.transform.localScale}");
                Assert.True(childNetworkObject.TrySetParent(currentParent, worldPositionStays));
                VerboseDebug($"[Server Parenting][After] Scale of NetworkObjectID ({childNetworkObject.NetworkObjectId}) is now {childNetworkObject.transform.localScale}");
                currentParent = childNetworkObject;
            }

            VerboseDebug($"[{Time.realtimeSinceStartup - startTime}] Parented all children.");

            // Wait for all client instances to have been parented.
            yield return WaitForConditionOrTimeOut(HaveAllClientsParentedChild);
            AssertOnTimeout("Timed out waiting for all clients to parent the child object!");
            VerboseDebug($"[{Time.realtimeSinceStartup - startTime}] All clients parented the child.");

            var serverParentTransform = m_ServerSideParent.transform;
            // Verify the positions are identical to the default values
            foreach (var clientEntry in TestComponentHelper.ClientsRegistered)
            {
                var children = clientEntry.Value.Children;
                var rootParent = clientEntry.Value.RootParent;
                Assert.IsTrue(Approximately(rootParent.transform.position, m_ServerSideParent.transform.position), $"Client-{clientEntry.Key} parent's position ({rootParent.transform.position}) does not equal the server parent's position ({serverParentTransform.position})!");
                Assert.IsTrue(Approximately(rootParent.transform.rotation.eulerAngles, m_ServerSideParent.transform.rotation.eulerAngles), $"Client-{clientEntry.Key} parent's rotation ({rootParent.transform.rotation.eulerAngles}) does not equal the server parent's position ({serverParentTransform.rotation.eulerAngles})!");
                for (int i = 0; i < k_NestedChildren; i++)
                {
                    var clientChildInfo = children[i];
                    var serverChild = m_ServerSideChildren[i];
                    Assert.IsTrue(clientChildInfo.HasBeenParented, $"Client-{clientEntry.Key} has not been parented!");
                    // Assure we mirror the server
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.position, serverChild.transform.position), $"Client-{clientEntry.Key} child's position ({clientChildInfo.Child.transform.position}) does not equal the server child's position ({serverChild.transform.position})!");
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.eulerAngles, serverChild.transform.rotation.eulerAngles), $"Client-{clientEntry.Key} child's rotation ({clientChildInfo.Child.transform.rotation.eulerAngles}) does not equal the server child's rotation ({serverChild.transform.rotation.eulerAngles})!");
                    if (useNetworkTransform)
                    {
                        yield return WaitForConditionOrTimeOut(() => Approximately(clientChildInfo.Child.transform.localScale, serverChild.transform.localScale));
                        AssertOnTimeout($"Timed out waiting for client-{clientEntry.Key} child's scale ({clientChildInfo.Child.transform.localScale}) to equal the server child's scale ({serverChild.transform.localScale}) [Has NetworkTransform]");
                    }
                    else
                    {
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localScale, serverChild.transform.localScale), $"Client-{clientEntry.Key} child's scale ({clientChildInfo.Child.transform.localScale}) does not equal the server child's scale ({serverChild.transform.localScale})");
                    }

                    // Assure we still have the same local space values when not preserving the world position
                    if (!worldPositionStays)
                    {
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localPosition, childPositionList[i]), $"Client-{clientEntry.Key} child's local space position ({clientChildInfo.Child.transform.localPosition}) does not equal the default child's position ({childPositionList[i]})!");
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localRotation.eulerAngles, childRotationList[i]), $"Client-{clientEntry.Key} child's local space rotation ({clientChildInfo.Child.transform.localRotation.eulerAngles}) does not equal the server child's rotation ({childRotationList[i]})!");
                    }
                }
            }

            // Late join a client and run through the same checks
            yield return CreateAndStartNewClient();
            AssertOnTimeout("[Late-Join] Timed out waiting for client to late join!");

            // Wait for clients to spawn the NetworkObjects
            yield return WaitForConditionOrTimeOut(HaveAllClientsSpawnedObjects);
            AssertOnTimeout("[Late-Join] Timed out waiting for all clients to spawn the respective parent and child objects");

            // Wait for all client instances to have been parented.
            yield return WaitForConditionOrTimeOut(HaveAllClientsParentedChild);
            AssertOnTimeout("[Late-Join] Timed out waiting for all clients to parent the child object!");

            // Verify the positions are identical to the default values
            foreach (var clientEntry in TestComponentHelper.ClientsRegistered)
            {
                var children = clientEntry.Value.Children;
                var rootParent = clientEntry.Value.RootParent;
                Assert.IsTrue(Approximately(rootParent.transform.position, m_ServerSideParent.transform.position), $"[LateJoin] Client-{clientEntry.Key} parent's position ({rootParent.transform.position}) does not equal the server parent's position ({serverParentTransform.position})!");
                Assert.IsTrue(Approximately(rootParent.transform.rotation.eulerAngles, m_ServerSideParent.transform.rotation.eulerAngles), $"[LateJoin] Client-{clientEntry.Key} parent's rotation ({rootParent.transform.rotation.eulerAngles}) does not equal the server parent's position ({serverParentTransform.rotation.eulerAngles})!");
                for (int i = 0; i < k_NestedChildren; i++)
                {
                    var clientChildInfo = children[i];
                    var serverChild = m_ServerSideChildren[i];
                    Assert.IsTrue(clientChildInfo.HasBeenParented, $"[LateJoin] Client-{clientEntry.Key} has not been parented!");
                    // Assure we mirror the server
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.position, serverChild.transform.position), $"[LateJoin] Client-{clientEntry.Key} child's position ({clientChildInfo.Child.transform.position}) does not equal the server child's position ({serverChild.transform.position})!");
                    Assert.IsTrue(Approximately(clientChildInfo.Child.transform.eulerAngles, serverChild.transform.rotation.eulerAngles), $"[LateJoin] Client-{clientEntry.Key} child's rotation ({clientChildInfo.Child.transform.rotation.eulerAngles}) does not equal the server child's rotation ({serverChild.transform.rotation.eulerAngles})!");

                    if (useNetworkTransform)
                    {
                        yield return WaitForConditionOrTimeOut(() => Approximately(clientChildInfo.Child.transform.localScale, serverChild.transform.localScale));
                        AssertOnTimeout($"[Late Join] Timed out waiting for client-{clientEntry.Key} child's scale ({clientChildInfo.Child.transform.localScale}) to equal the server child's scale ({serverChild.transform.localScale}) [Has NetworkTransform]");
                    }
                    else
                    {
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localScale, serverChild.transform.localScale), $"[LateJoin] Client-{clientEntry.Key} child's scale ({clientChildInfo.Child.transform.localScale}) does not equal the server child's scale ({serverChild.transform.localScale})");
                    }

                    // Assure we still have the same local space values when not preserving the world position
                    if (!worldPositionStays)
                    {
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localPosition, childPositionList[i]), $"[LateJoin] Client-{clientEntry.Key} child's local space position ({clientChildInfo.Child.transform.localPosition}) does not equal the default child's position ({childPositionList[i]})!");
                        Assert.IsTrue(Approximately(clientChildInfo.Child.transform.localRotation.eulerAngles, childRotationList[i]), $"[LateJoin] Client-{clientEntry.Key} child's local space rotation ({clientChildInfo.Child.transform.localRotation.eulerAngles}) does not equal the server child's rotation ({childRotationList[i]})!");
                    }
                }
            }
            VerboseDebug($"[{Time.realtimeSinceStartup - startTime}] Late joined client was validated. Test completed!");
        }
    }
}
