using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Client-Server only test
    /// Validates using managed collections with NetworkVariable.
    /// Managed Collections Tested:
    /// - List
    /// - Dictionary
    /// - HashSet
    /// This also does some testing on nested collections, but does
    /// not test every possible combination.
    /// </summary>    
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class NetworkVariableCollectionsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private bool m_EnableDebug;

        public NetworkVariableCollectionsTests(HostOrServer hostOrServer) : base(hostOrServer)
        {
            m_EnableDebug = false;
        }

        protected override bool OnSetVerboseDebug()
        {
            return m_EnableDebug;
        }

        protected override IEnumerator OnSetup()
        {
            ListTestHelperInt.ResetState();
            ListTestHelperListInt.ResetState();
            ListTestHelperSerializableObject.ResetState();
            ListTestHelperListSerializableObject.ResetState();
            DictionaryTestHelper.ResetState();
            NestedDictionaryTestHelper.ResetState();
            HashSetBaseTypeTestHelper.ResetState();
            return base.OnSetup();
        }

        private void AddPlayerComponent<T>() where T : ListTestHelperBase
        {
            var component = m_PlayerPrefab.AddComponent<T>();
            component.SetDebugMode(m_EnableDebug);
        }

        protected override void OnCreatePlayerPrefab()
        {
            AddPlayerComponent<ListTestHelperInt>();
            AddPlayerComponent<ListTestHelperListInt>();
            AddPlayerComponent<ListTestHelperSerializableObject>();
            AddPlayerComponent<ListTestHelperListSerializableObject>();
            AddPlayerComponent<DictionaryTestHelper>();
            AddPlayerComponent<NestedDictionaryTestHelper>();
            AddPlayerComponent<HashSetBaseTypeTestHelper>();
            base.OnCreatePlayerPrefab();
        }

        private List<int> GetRandomIntList(int count)
        {
            var list = new List<int>();
            for (int i = 0; i < count; i++)
            {
                list.Add(Random.Range(int.MinValue, int.MaxValue));
            }
            return list;
        }

        [UnityTest]
        public IEnumerator TestListBuiltInTypeCollections()
        {
            var compInt = (ListTestHelperInt)null;
            var compListInt = (ListTestHelperListInt)null;
            var compIntServer = (ListTestHelperInt)null;
            var compListIntServer = (ListTestHelperListInt)null;

            var clientList = m_ClientNetworkManagers.ToList();
            if (m_ServerNetworkManager.IsHost)
            {
                clientList.Insert(0, m_ServerNetworkManager);
            }

            foreach (var client in clientList)
            {
                ///////////////////////////////////////////////////////////////////////////
                // List<int> Single dimension list

                compInt = client.LocalClient.PlayerObject.GetComponent<ListTestHelperInt>();
                compIntServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<ListTestHelperInt>();
                yield return WaitForConditionOrTimeOut(() => compInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compIntServer.name} component match!");
                var randomInt = Random.Range(int.MinValue, int.MaxValue);

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    //////////////////////////////////
                    // No Write Owner Add Int
                    compIntServer.Add(randomInt, ListTestHelperBase.Targets.Owner);
                }

                //////////////////////////////////
                // Owner Add int
                compInt.Add(randomInt, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    //////////////////////////////////
                    // No Write Server Add Int
                    compInt.Add(randomInt, ListTestHelperBase.Targets.Server);
                }

                //////////////////////////////////
                // Server Add int
                compIntServer.Add(randomInt, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                //////////////////////////////////
                // Owner Remove int
                var index = Random.Range(0, compInt.ListCollectionOwner.Value.Count - 1);
                var valueIntRemove = compInt.ListCollectionOwner.Value[index];
                compInt.Remove(valueIntRemove, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");
                //////////////////////////////////
                // Server Remove int
                compIntServer.Remove(valueIntRemove, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compIntServer.name} component match!");

                ////////////////////////////////////
                // Owner Change int
                var valueIntChange = Random.Range(int.MinValue, int.MaxValue);

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // No Write Server Change int with IsDirty restore
                    compIntServer.ListCollectionOwner.Value[index] = valueIntChange;
                    compIntServer.ListCollectionOwner.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Server change failed to restore on {nameof(ListTestHelperInt)} {compInt.name}!");

                    // No Write Server Change int with owner state update override
                    compIntServer.ListCollectionOwner.Value[index] = valueIntChange;
                }
                compInt.ListCollectionOwner.Value[index] = valueIntChange;
                compInt.ListCollectionOwner.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} change failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");

                //////////////////////////////////
                // Server Change int

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // No Write Client Change int with IsDirty restore
                    compInt.ListCollectionServer.Value[index] = valueIntChange;
                    compInt.ListCollectionServer.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Client-{client.LocalClientId} change failed to restore on {nameof(ListTestHelperInt)} {compInt.name}!");

                    // No Write Client Change int with owner state update override
                    compInt.ListCollectionServer.Value[index] = valueIntChange;
                }
                compIntServer.ListCollectionServer.Value[index] = valueIntChange;
                compIntServer.ListCollectionServer.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server change failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                ////////////////////////////////////
                // Owner Add Range
                compInt.AddRange(GetRandomIntList(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add range failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");
                //////////////////////////////////
                // Server Add Range
                compIntServer.AddRange(GetRandomIntList(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add range failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compIntServer.name} component match!");

                ////////////////////////////////////
                // Owner Full Set
                compInt.FullSet(GetRandomIntList(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");
                //////////////////////////////////
                // Server Full Set    
                compIntServer.FullSet(GetRandomIntList(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                ////////////////////////////////////
                // Owner Clear
                compInt.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {nameof(ListTestHelperInt)} {compInt.name}!");
                //////////////////////////////////
                // Server Clear
                compIntServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {nameof(ListTestHelperInt)} {compIntServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compIntServer.name} component match!");

                ///////////////////////////////////////////////////////////////////////////
                // List<List<int>> Nested List Validation
                compListInt = client.LocalClient.PlayerObject.GetComponent<ListTestHelperListInt>();
                compListIntServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<ListTestHelperListInt>();
                yield return WaitForConditionOrTimeOut(() => compListInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListInt.name} component match! {compListInt.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListIntServer.name} component match! {compListIntServer.GetLog()}");

                //////////////////////////////////
                // Owner Add List<int> item
                compListInt.Add(GetRandomIntList(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");
                //////////////////////////////////
                // Server Add List<int> item
                compListIntServer.Add(GetRandomIntList(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                //////////////////////////////////
                // Owner Remove List<int> item
                index = Random.Range(0, compListInt.ListCollectionOwner.Value.Count - 1);

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    compListIntServer.ListCollectionOwner.Value.Remove(compListIntServer.ListCollectionOwner.Value[index]);
                    compListIntServer.ListCollectionOwner.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Server remove failed to restore on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");
                    // No Write Server Remove List<int> item with update restore
                    compListIntServer.ListCollectionOwner.Value.Remove(compListIntServer.ListCollectionOwner.Value[index]);
                }
                compListInt.Remove(compListInt.ListCollectionOwner.Value[index], ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");

                //////////////////////////////////
                // Server Remove List<int> item
                index = Random.Range(0, compListIntServer.ListCollectionServer.Value.Count - 1);
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // No Write Client Remove List<int> item with CheckDirtyState restore                
                    compListInt.Remove(compListInt.ListCollectionServer.Value[index], ListTestHelperBase.Targets.Server);
                    yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Client-{client.LocalClientId} remove failed to restore on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                    // No Write Client Remove List<int> item with update restore
                    compListInt.Remove(compListInt.ListCollectionServer.Value[index], ListTestHelperBase.Targets.Server);
                }
                compListIntServer.Remove(compListIntServer.ListCollectionServer.Value[index], ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");


                yield return WaitForConditionOrTimeOut(() => compListInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListInt.name} component match! {compListInt.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListIntServer.name} component match! {compListIntServer.GetLog()}");

                ////////////////////////////////////
                // Owner Change List<int> item
                index = Random.Range(0, compListInt.ListCollectionOwner.Value.Count - 1);
                compListInt.ListCollectionOwner.Value[index] = GetRandomIntList(5);
                compListInt.ListCollectionOwner.CheckDirtyState();
                Assert.True(compListInt.ListCollectionOwner.IsDirty(), "Client Should be dirty!");
                yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} change index ({index}) failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");

                //////////////////////////////////
                // Server Change List<int> item
                index = Random.Range(0, compListIntServer.ListCollectionServer.Value.Count - 1);
                compListIntServer.ListCollectionServer.Value[index] = GetRandomIntList(5);
                compListIntServer.ListCollectionServer.CheckDirtyState();
                Assert.True(compListIntServer.ListCollectionServer.IsDirty(), "Server Should be dirty!");
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server change failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                ////////////////////////////////////
                // Owner Add Range of List<int> items
                var randomintListOfList = new List<List<int>>();
                for (int i = 0; i < 5; i++)
                {
                    randomintListOfList.Add(GetRandomIntList(5));
                }
                compListInt.AddRange(randomintListOfList, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add range failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");
                //////////////////////////////////
                // Server Add Range of List<int> items
                randomintListOfList = new List<List<int>>();
                for (int i = 0; i < 5; i++)
                {
                    randomintListOfList.Add(GetRandomIntList(5));
                }
                compListIntServer.AddRange(randomintListOfList, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add range failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compListIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListIntServer.name} component match!");

                ////////////////////////////////////
                // Owner Full Set List<List<int>>
                randomintListOfList = new List<List<int>>();
                for (int i = 0; i < 5; i++)
                {
                    randomintListOfList.Add(GetRandomIntList(5));
                }
                compListInt.FullSet(randomintListOfList, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");
                //////////////////////////////////
                // Server Full Set List<List<int>>
                randomintListOfList = new List<List<int>>();
                for (int i = 0; i < 5; i++)
                {
                    randomintListOfList.Add(GetRandomIntList(5));
                }
                compListIntServer.FullSet(randomintListOfList, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                ////////////////////////////////////
                // Owner Clear List<List<int>>
                compListInt.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListInt.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {nameof(ListTestHelperListInt)} {compListInt.name}! {compListInt.GetLog()}");
                //////////////////////////////////
                // Server Clear List<List<int>>
                compListIntServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListIntServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {nameof(ListTestHelperListInt)} {compListIntServer.name}! {compListIntServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListInt.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListInt.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListInt.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compListIntServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListIntServer.OwnerClientId}'s {nameof(ListTestHelperInt)} {compListIntServer.name} component match!");
            }
        }

        [UnityTest]
        public IEnumerator TestListSerializableObjectCollections()
        {
            var compObject = (ListTestHelperSerializableObject)null;
            var compObjectServer = (ListTestHelperSerializableObject)null;
            var compListObject = (ListTestHelperListSerializableObject)null;
            var compListObjectServer = (ListTestHelperListSerializableObject)null;

            var clientList = m_ClientNetworkManagers.ToList();
            if (m_ServerNetworkManager.IsHost)
            {
                clientList.Insert(0, m_ServerNetworkManager);
            }

            foreach (var client in clientList)
            {
                ///////////////////////////////////////////////////////////////////////////
                // List<SerializableObject> Single dimension list
                compObject = client.LocalClient.PlayerObject.GetComponent<ListTestHelperSerializableObject>();
                compObjectServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<ListTestHelperSerializableObject>();
                yield return WaitForConditionOrTimeOut(() => compObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compObject.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compObjectServer.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObjectServer.name} component match!");

                //////////////////////////////////
                // Owner Add SerializableObject
                compObject.Add(SerializableObject.GetRandomObject(), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Add SerializableObject
                compObjectServer.Add(SerializableObject.GetRandomObject(), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");
                //////////////////////////////////
                // Owner Remove SerializableObject
                var index = Random.Range(0, compObject.ListCollectionOwner.Value.Count - 1);
                var valueIntRemove = compObject.ListCollectionOwner.Value[index];
                compObject.Remove(valueIntRemove, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Remove SerializableObject
                compObjectServer.Remove(valueIntRemove, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compObject.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compObjectServer.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObjectServer.name} component match!");

                ////////////////////////////////////
                // Owner Change SerializableObject

                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // No Write Server Remove Serializable item with IsDirty restore
                    compObjectServer.ListCollectionOwner.Value[index] = SerializableObject.GetRandomObject();
                    compObjectServer.ListCollectionOwner.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Server change failed to restore on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                    // No Write Server Remove Serializable item with owner state update restore
                    compObjectServer.ListCollectionOwner.Value[index] = SerializableObject.GetRandomObject();
                }
                compObject.ListCollectionOwner.Value[index] = SerializableObject.GetRandomObject();
                compObject.ListCollectionOwner.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} change failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Change SerializableObject
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // No Write Client Remove Serializable item with IsDirty restore
                    compObject.ListCollectionServer.Value[index] = SerializableObject.GetRandomObject();
                    compObject.ListCollectionServer.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Client-{client.LocalClientId} change failed to restore on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                    // No Write Client Remove Serializable item with owner state update restore                
                    compObject.ListCollectionServer.Value[index] = SerializableObject.GetRandomObject();
                }
                compObjectServer.ListCollectionServer.Value[index] = SerializableObject.GetRandomObject();
                compObjectServer.ListCollectionServer.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server change failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                ////////////////////////////////////
                // Owner Add Range SerializableObjects
                compObject.AddRange(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add range failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Add Range SerializableObjects
                compObjectServer.AddRange(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add range failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compObject.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compObjectServer.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObjectServer.name} component match!");

                ////////////////////////////////////
                // Owner Full Set SerializableObjects
                compObject.FullSet(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Full Set SerializableObjects
                compObjectServer.FullSet(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                ////////////////////////////////////
                // Owner Clear
                compObject.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObject.name}!");
                //////////////////////////////////
                // Server Clear
                compObjectServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {nameof(ListTestHelperSerializableObject)} {compObjectServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compObject.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compObjectServer.OwnerClientId}'s {nameof(ListTestHelperSerializableObject)} {compObjectServer.name} component match!");

                ///////////////////////////////////////////////////////////////////////////
                // List<List<INetworkSerializable>> Nested List Validation
                compListObject = client.LocalClient.PlayerObject.GetComponent<ListTestHelperListSerializableObject>();
                compListObjectServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<ListTestHelperListSerializableObject>();
                yield return WaitForConditionOrTimeOut(() => compListObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListObject.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObject.name} component match! {compListObject.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListObjectServer.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name} component match! {compListObjectServer.GetLog()}");

                //////////////////////////////////
                // Owner Add List<INetworkSerializable> item
                compListObject.Add(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}! {compListObject.GetLog()}");
                //////////////////////////////////
                // Server Add List<INetworkSerializable> item
                compListObjectServer.Add(SerializableObject.GetListOfRandomObjects(5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}! {compListObjectServer.GetLog()}");

                //////////////////////////////////
                // Owner Remove List<INetworkSerializable> item
                index = Random.Range(0, compListObject.ListCollectionOwner.Value.Count - 1);
                compListObject.Remove(compListObject.ListCollectionOwner.Value[index], ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}! {compListObject.GetLog()}");
                //////////////////////////////////
                // Server Remove List<INetworkSerializable> item
                index = Random.Range(0, compListObjectServer.ListCollectionServer.Value.Count - 1);
                compListObjectServer.Remove(compListObjectServer.ListCollectionServer.Value[index], ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}! {compListObjectServer.GetLog()}");


                yield return WaitForConditionOrTimeOut(() => compListObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListObject.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObject.name} component match! {compListObject.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListObjectServer.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name} component match! {compListObjectServer.GetLog()}");

                ////////////////////////////////////
                // Owner Change List<INetworkSerializable> item
                index = Random.Range(0, compListObject.ListCollectionOwner.Value.Count - 1);
                compListObject.ListCollectionOwner.Value[index] = SerializableObject.GetListOfRandomObjects(5);
                compListObject.ListCollectionOwner.CheckDirtyState();
                Assert.True(compListObject.ListCollectionOwner.IsDirty(), "Client Should be dirty!");
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} change index ({index}) failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}! {compListObject.GetLog()}");

                //////////////////////////////////
                // Server Change List<INetworkSerializable> item
                index = Random.Range(0, compListObjectServer.ListCollectionServer.Value.Count - 1);
                compListObjectServer.ListCollectionServer.Value[index] = SerializableObject.GetListOfRandomObjects(5);
                compListObjectServer.ListCollectionServer.CheckDirtyState();
                Assert.True(compListObjectServer.ListCollectionServer.IsDirty(), "Server Should be dirty!");
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server change failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}! {compListObjectServer.GetLog()}");

                ////////////////////////////////////
                // Owner Add Range of List<INetworkSerializable> items
                compListObject.AddRange(SerializableObject.GetListOfListOfRandomObjects(5, 5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add range failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}! {compListObject.GetLog()}");
                //////////////////////////////////
                // Server Add Range of List<INetworkSerializable> items
                compListObjectServer.AddRange(SerializableObject.GetListOfListOfRandomObjects(5, 5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add range failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}! {compListObjectServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compListObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListObject.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compListObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListObjectServer.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name} component match!");

                ////////////////////////////////////
                // Owner Full Set List<List<INetworkSerializable>>
                compListObject.FullSet(SerializableObject.GetListOfListOfRandomObjects(5, 5), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}!");
                //////////////////////////////////
                // Server Full Set List<List<INetworkSerializable>>
                compListObjectServer.FullSet(SerializableObject.GetListOfListOfRandomObjects(5, 5), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}!");

                ////////////////////////////////////
                // Owner Clear List<List<INetworkSerializable>>
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // Server Clear List<List<INetworkSerializable>> with IsDirty restore
                    compListObjectServer.ListCollectionOwner.Value.Clear();
                    compListObjectServer.ListCollectionOwner.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Server clear owner collection failed to restore back to last known valid state on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}!");
                    // Server Clear List<List<INetworkSerializable>> with update state restore
                    compListObjectServer.ListCollectionOwner.Value.Clear();
                }
                compListObject.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}!");
                //////////////////////////////////
                // Server Clear List<List<INetworkSerializable>>
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // Client Clear List<List<INetworkSerializable>> with IsDirty restore
                    compListObject.ListCollectionServer.Value.Clear();
                    compListObject.ListCollectionServer.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compListObject.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Client clear owner collection failed to restore back to last known valid state on {nameof(ListTestHelperListSerializableObject)} {compListObject.name}!");

                    // Client Clear List<List<INetworkSerializable>> with update state restore
                    compListObject.ListCollectionServer.Value.Clear();
                }
                compListObjectServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compListObjectServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name}!");

                yield return WaitForConditionOrTimeOut(() => compListObject.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compListObject.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObject.name} component match!");

                yield return WaitForConditionOrTimeOut(() => compListObjectServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compListObjectServer.OwnerClientId}'s {nameof(ListTestHelperListSerializableObject)} {compListObjectServer.name} component match!");
            }
        }

        private int m_CurrentKey;
        private int GetNextKey()
        {
            m_CurrentKey++;
            return m_CurrentKey;
        }

        private int m_Stage;

        private List<NetworkManager> m_Clients;

        private bool m_IsInitialized = false;
        private StringBuilder m_InitializedStatus = new StringBuilder();

        private IEnumerator ValidateClients(NetworkManager clientBeingTested, bool initialize = false)
        {
            VerboseDebug($">>>>>>>>>>>>>>>>>>>>>>>>>[Client-{clientBeingTested.LocalClientId}][{m_Stage}][Validation]<<<<<<<<<<<<<<<<<<<<<<<<< ");
            m_Stage++;
            var compDictionary = (DictionaryTestHelper)null;
            var compDictionaryServer = (DictionaryTestHelper)null;
            var className = $"{nameof(DictionaryTestHelper)}";
            var clientsInitialized = new Dictionary<ulong, bool>();

            var validateTimeout = new TimeoutHelper(0.25f);

            foreach (var client in m_Clients)
            {
                var ownerInitialized = false;
                var serverInitialized = false;
                ///////////////////////////////////////////////////////////////////////////
                // Dictionary<int, Dictionary<int,SerializableObject>> nested dictionaries
                compDictionary = client.LocalClient.PlayerObject.GetComponent<DictionaryTestHelper>();
                compDictionaryServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<DictionaryTestHelper>();
                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances(), validateTimeout);
                if (initialize)
                {
                    if (validateTimeout.HasTimedOut())
                    {
                        m_InitializedStatus.AppendLine($"[Client -{client.LocalClientId}][Owner] Failed validation: {compDictionary.GetLog()}");
                    }
                    else
                    {
                        m_InitializedStatus.AppendLine($"[Client -{client.LocalClientId}][Owner] Passed validation!");
                    }
                    ownerInitialized = !validateTimeout.HasTimedOut();
                }
                else
                {
                    AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");
                }

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances(), validateTimeout);
                if (initialize)
                {
                    if (validateTimeout.HasTimedOut())
                    {
                        m_InitializedStatus.AppendLine($"[Client -{client.LocalClientId}][Server] Failed validation: {compDictionaryServer.GetLog()}");
                    }
                    else
                    {
                        m_InitializedStatus.AppendLine($"[Client -{client.LocalClientId}][Server] Passed validation!");
                    }
                    serverInitialized = !validateTimeout.HasTimedOut();
                }
                else
                {
                    AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");
                }

                if (initialize)
                {
                    clientsInitialized.Add(client.LocalClientId, ownerInitialized & serverInitialized);
                }
            }

            if (initialize)
            {
                m_IsInitialized = true;
                foreach (var entry in clientsInitialized)
                {
                    if (!entry.Value)
                    {
                        m_IsInitialized = false;
                        break;
                    }
                }
            }
        }

        private void ValidateClientsFlat(NetworkManager clientBeingTested)
        {
            if (!m_EnableDebug)
            {
                return;
            }
            VerboseDebug($">>>>>>>>>>>>>>>>>>>>>>>>>[{clientBeingTested.name}][{m_Stage}][Validation]<<<<<<<<<<<<<<<<<<<<<<<<< ");
            m_Stage++;
            var compDictionary = (DictionaryTestHelper)null;
            var compDictionaryServer = (DictionaryTestHelper)null;
            var className = $"{nameof(DictionaryTestHelper)}";
            foreach (var client in m_Clients)
            {
                ///////////////////////////////////////////////////////////////////////////
                // Dictionary<int, Dictionary<int,SerializableObject>> nested dictionaries
                compDictionary = client.LocalClient.PlayerObject.GetComponent<DictionaryTestHelper>();
                compDictionaryServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<DictionaryTestHelper>();
                Assert.True(compDictionary.ValidateInstances(), $"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");
                Assert.True(compDictionaryServer.ValidateInstances(), $"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");
            }
        }


        [UnityTest]
        public IEnumerator TestDictionaryCollections()
        {
            var compDictionary = (DictionaryTestHelper)null;
            var compDictionaryServer = (DictionaryTestHelper)null;
            var className = $"{nameof(DictionaryTestHelper)}";

            m_Clients = m_ClientNetworkManagers.ToList();
            if (m_ServerNetworkManager.IsHost)
            {
                m_Clients.Insert(0, m_ServerNetworkManager);
            }

            m_CurrentKey = 1000;

            if (m_EnableDebug)
            {
                VerboseDebug(">>>>>>>>>>>>>>>>>>>>>>>>>>>>> Init Values <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                foreach (var client in m_Clients)
                {
                    compDictionary = client.LocalClient.PlayerObject.GetComponent<DictionaryTestHelper>();
                    compDictionary.InitValues();
                    compDictionaryServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<DictionaryTestHelper>();
                    compDictionaryServer.InitValues();
                }
                VerboseDebug(">>>>>>>>>>>>>>>>>>>>>>>>>>>>> Init Check <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                var count = 0;
                while (count < 3)
                {
                    m_InitializedStatus.Clear();
                    foreach (var client in m_Clients)
                    {
                        yield return ValidateClients(client, true);
                    }
                    if (m_IsInitialized)
                    {
                        break;
                    }
                    count++;
                    m_Stage = 0;
                }

                Assert.IsTrue(m_IsInitialized, $"Not all clients synchronized properly!\n {m_InitializedStatus.ToString()}");
                VerboseDebug(m_InitializedStatus.ToString());
            }

            VerboseDebug(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> BEGIN <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
            foreach (var client in m_Clients)
            {
                ///////////////////////////////////////////////////////////////////////////
                // Dictionary<int, Dictionary<int,SerializableObject>> nested dictionaries
                compDictionary = client.LocalClient.PlayerObject.GetComponent<DictionaryTestHelper>();
                compDictionaryServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<DictionaryTestHelper>();
                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");

                //////////////////////////////////
                // Owner Add SerializableObject Entry
                var newEntry = (GetNextKey(), SerializableObject.GetRandomObject());
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // Server-side add same key and SerializableObject prior to being added to the owner side
                    compDictionaryServer.ListCollectionOwner.Value.Add(newEntry.Item1, newEntry.Item2);
                    // Checking if dirty on server side should revert back to origina known current dictionary state
                    compDictionaryServer.ListCollectionOwner.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Server add to owner write collection property failed to restore on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");
                    // Server-side add the same key and SerializableObject to owner write permission (would throw key exists exception too if previous failed)                    
                    compDictionaryServer.ListCollectionOwner.Value.Add(newEntry.Item1, newEntry.Item2);
                    // Server-side add a completely new key and SerializableObject to to owner write permission property
                    compDictionaryServer.ListCollectionOwner.Value.Add(GetNextKey(), SerializableObject.GetRandomObject());
                    // Both should be overridden by the owner-side update

                }
                VerboseDebug($"[{compDictionary.name}][Owner] Adding Key: {newEntry.Item1}");
                // Add key and SerializableObject to owner side
                compDictionary.Add(newEntry, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");

                ValidateClientsFlat(client);
                //////////////////////////////////
                // Server Add SerializableObject Entry
                newEntry = (GetNextKey(), SerializableObject.GetRandomObject());
                // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                if (!client.IsServer)
                {
                    // Client-side add same key and SerializableObject to server write permission property
                    compDictionary.ListCollectionServer.Value.Add(newEntry.Item1, newEntry.Item2);
                    // Checking if dirty on client side should revert back to origina known current dictionary state
                    compDictionary.ListCollectionServer.IsDirty();
                    yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Client-{client.LocalClientId} add to server write collection property failed to restore on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                    // Client-side add the same key and SerializableObject to server write permission property (would throw key exists exception too if previous failed)                    
                    compDictionary.ListCollectionServer.Value.Add(newEntry.Item1, newEntry.Item2);
                    // Client-side add a completely new key and SerializableObject to to server write permission property
                    compDictionary.ListCollectionServer.Value.Add(GetNextKey(), SerializableObject.GetRandomObject());
                    // Both should be overridden by the server-side update
                }
                VerboseDebug($"[{compDictionaryServer.name}][Server] Adding Key: {newEntry.Item1}");
                compDictionaryServer.Add(newEntry, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");
                //////////////////////////////////
                // Owner Remove SerializableObject Entry
                var index = Random.Range(0, compDictionary.ListCollectionOwner.Value.Keys.Count - 1);
                var valueInt = compDictionary.ListCollectionOwner.Value.Keys.ToList()[index];
                compDictionary.Remove(valueInt, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");

                //////////////////////////////////
                // Server Remove SerializableObject Entry
                index = Random.Range(0, compDictionary.ListCollectionServer.Value.Keys.Count - 1);
                valueInt = compDictionary.ListCollectionServer.Value.Keys.ToList()[index];
                compDictionaryServer.Remove(valueInt, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");

                ValidateClientsFlat(client);
                ////////////////////////////////////
                // Owner Change SerializableObject Entry
                var randomObject = SerializableObject.GetRandomObject();
                if (compDictionary.ListCollectionOwner.Value.Keys.Count != 0)
                {
                    if (compDictionary.ListCollectionOwner.Value.Keys.Count == 1)
                    {
                        index = 0;
                        valueInt = compDictionary.ListCollectionOwner.Value.Keys.ToList()[0];
                    }
                    else
                    {
                        index = Random.Range(0, compDictionary.ListCollectionOwner.Value.Keys.Count - 1);
                        valueInt = compDictionary.ListCollectionOwner.Value.Keys.ToList()[index];
                    }

                    // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                    if (!client.IsServer)
                    {
                        // Server-side update same key value prior to being updated to the owner side
                        compDictionaryServer.ListCollectionOwner.Value[valueInt] = randomObject;
                        // Checking if dirty on server side should revert back to origina known current dictionary state
                        compDictionaryServer.ListCollectionOwner.IsDirty();
                        yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                        AssertOnTimeout($"Server update collection entry value to local owner write collection property failed to restore on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                        // Server-side update same key but with different value prior to being updated to the owner side
                        compDictionaryServer.ListCollectionOwner.Value[valueInt] = SerializableObject.GetRandomObject();
                        if (compDictionaryServer.ListCollectionOwner.Value.Keys.Count > 1)
                        {
                            // Server-side update different key with different value prior to being updated to the owner side
                            compDictionaryServer.ListCollectionOwner.Value[compDictionaryServer.ListCollectionOwner.Value.Keys.ToList()[(index + 1) % compDictionaryServer.ListCollectionOwner.Value.Keys.Count]] = SerializableObject.GetRandomObject();
                        }
                        // Owner-side update should force restore to current known value before updating to the owner's state update of the original index and SerializableObject
                    }

                    compDictionary.ListCollectionOwner.Value[valueInt] = randomObject;
                    compDictionary.ListCollectionOwner.CheckDirtyState();
                    yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                    AssertOnTimeout($"Client-{client.LocalClientId} change failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                }

                //////////////////////////////////
                // Server Change SerializableObject
                if (compDictionaryServer.ListCollectionServer.Value.Keys.Count != 0)
                {
                    if (compDictionaryServer.ListCollectionServer.Value.Keys.Count == 1)
                    {
                        index = 0;
                        valueInt = compDictionaryServer.ListCollectionServer.Value.Keys.ToList()[0];
                    }
                    else
                    {
                        index = Random.Range(0, compDictionaryServer.ListCollectionServer.Value.Keys.Count - 1);
                        valueInt = compDictionaryServer.ListCollectionServer.Value.Keys.ToList()[index];
                    }

                    // Only test restore on non-host clients (otherwise a host is both server and client/owner)
                    if (!client.IsServer)
                    {
                        // Owner-side update same key value prior to being updated to the server side
                        compDictionary.ListCollectionServer.Value[valueInt] = randomObject;
                        // Checking if dirty on owner side should revert back to origina known current dictionary state
                        compDictionary.ListCollectionServer.IsDirty();
                        yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                        AssertOnTimeout($"Client-{client.LocalClientId} update collection entry value to local server write collection property failed to restore on {className} {compDictionary.name}! {compDictionary.GetLog()}");

                        // Owner-side update same key but with different value prior to being updated to the server side
                        compDictionary.ListCollectionServer.Value[valueInt] = SerializableObject.GetRandomObject();

                        if (compDictionary.ListCollectionServer.Value.Keys.Count > 1)
                        {
                            // Owner-side update different key with different value prior to being updated to the server side
                            compDictionary.ListCollectionServer.Value[compDictionary.ListCollectionServer.Value.Keys.ToList()[(index + 1) % compDictionary.ListCollectionServer.Value.Keys.Count]] = SerializableObject.GetRandomObject();
                        }
                        // Server-side update should force restore to current known value before updating to the server's state update of the original index and SerializableObject
                    }

                    compDictionaryServer.ListCollectionServer.Value[valueInt] = SerializableObject.GetRandomObject();
                    compDictionaryServer.ListCollectionServer.CheckDirtyState();
                    yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                    AssertOnTimeout($"Server change failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");
                }
                ValidateClientsFlat(client);

                ////////////////////////////////////
                // Owner Clear
                compDictionary.Clear(ListTestHelperBase.Targets.Owner);
                VerboseDebug($"[{compDictionary.name}] Clearing dictionary..");
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Clear
                VerboseDebug($"[{compDictionaryServer.name}] Clearing dictionary..");
                compDictionaryServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");

                ////////////////////////////////////
                // Owner Full Set Dictionary
                compDictionary.FullSet(DictionaryTestHelper.GetDictionaryValues(), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Full Set Dictionary
                compDictionaryServer.FullSet(DictionaryTestHelper.GetDictionaryValues(), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");
                if (m_EnableDebug)
                {
                    yield return ValidateClients(client);
                    m_Stage = 0;
                }
            }
        }

        [UnityTest]
        public IEnumerator TestDictionaryNestedCollections()
        {
            var compDictionary = (NestedDictionaryTestHelper)null;
            var compDictionaryServer = (NestedDictionaryTestHelper)null;
            var className = $"{nameof(NestedDictionaryTestHelper)}";

            var clientList = m_ClientNetworkManagers.ToList();
            if (m_ServerNetworkManager.IsHost)
            {
                clientList.Insert(0, m_ServerNetworkManager);
            }

            m_CurrentKey = 1000;

            foreach (var client in clientList)
            {
                ///////////////////////////////////////////////////////////////////////////
                // Dictionary<int, Dictionary<int,SerializableObject>> nested dictionaries
                compDictionary = client.LocalClient.PlayerObject.GetComponent<NestedDictionaryTestHelper>();
                compDictionaryServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<NestedDictionaryTestHelper>();
                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");

                //////////////////////////////////
                // Owner Add Dictionary
                compDictionary.Add((GetNextKey(), NestedDictionaryTestHelper.GetDictionaryValues()), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Add Dictionary
                compDictionaryServer.Add((GetNextKey(), NestedDictionaryTestHelper.GetDictionaryValues()), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");
                //////////////////////////////////
                // Owner Remove Dictionary
                var index = Random.Range(0, compDictionary.ListCollectionOwner.Value.Keys.Count - 1);
                var valueInt = compDictionary.ListCollectionOwner.Value.Keys.ToList()[index];
                compDictionary.Remove(valueInt, ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Remove Dictionary
                index = Random.Range(0, compDictionaryServer.ListCollectionOwner.Value.Keys.Count - 1);
                valueInt = compDictionaryServer.ListCollectionOwner.Value.Keys.ToList()[index];
                compDictionaryServer.Remove(valueInt, ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");

                ////////////////////////////////////
                // Owner Change Dictionary
                index = Random.Range(0, compDictionary.ListCollectionOwner.Value.Keys.Count - 1);
                valueInt = compDictionary.ListCollectionOwner.Value.Keys.ToList()[index];
                compDictionary.ListCollectionOwner.Value[valueInt] = NestedDictionaryTestHelper.GetDictionaryValues();
                compDictionary.ListCollectionOwner.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} change failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Change Dictionary
                index = Random.Range(0, compDictionaryServer.ListCollectionOwner.Value.Keys.Count - 1);
                valueInt = compDictionaryServer.ListCollectionOwner.Value.Keys.ToList()[index];
                compDictionaryServer.ListCollectionServer.Value[index] = NestedDictionaryTestHelper.GetDictionaryValues();
                compDictionaryServer.ListCollectionServer.CheckDirtyState();
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server change failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                ////////////////////////////////////
                // Owner Full Set Nested Dictionaries
                compDictionary.FullSet(NestedDictionaryTestHelper.GetNestedDictionaryValues(), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Full Set Nested Dictionaries
                compDictionaryServer.FullSet(NestedDictionaryTestHelper.GetNestedDictionaryValues(), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                ////////////////////////////////////
                // Owner Clear
                compDictionary.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compDictionary.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {className} {compDictionary.name}! {compDictionary.GetLog()}");
                //////////////////////////////////
                // Server Clear
                compDictionaryServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {className} {compDictionaryServer.name}! {compDictionaryServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionary.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compDictionary.OwnerClientId}'s {className} {compDictionary.name} component match! {compDictionary.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compDictionaryServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compDictionaryServer.OwnerClientId}'s {className} {compDictionaryServer.name} component match! {compDictionaryServer.GetLog()}");
            }
        }

        [UnityTest]
        public IEnumerator TestHashSetBuiltInTypeCollections()
        {
            var compHashSet = (HashSetBaseTypeTestHelper)null;
            var compHashSetServer = (HashSetBaseTypeTestHelper)null;
            var className = $"{nameof(HashSetBaseTypeTestHelper)}";

            var clientList = m_ClientNetworkManagers.ToList();
            if (m_ServerNetworkManager.IsHost)
            {
                clientList.Insert(0, m_ServerNetworkManager);
            }

            m_CurrentKey = 1000;

            foreach (var client in clientList)
            {
                ///////////////////////////////////////////////////////////////////////////
                // HashSet<int> Single dimension list
                compHashSet = client.LocalClient.PlayerObject.GetComponent<HashSetBaseTypeTestHelper>();
                compHashSetServer = m_PlayerNetworkObjects[NetworkManager.ServerClientId][client.LocalClientId].GetComponent<HashSetBaseTypeTestHelper>();
                yield return WaitForConditionOrTimeOut(() => compHashSet.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compHashSet.OwnerClientId}'s {className} {compHashSet.name} component match! {compHashSet.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compHashSetServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compHashSetServer.OwnerClientId}'s {className} {compHashSetServer.name} component match! {compHashSetServer.GetLog()}");

                //////////////////////////////////
                // Owner Add Item
                compHashSet.Add(Random.Range(ushort.MinValue, ushort.MaxValue), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compHashSet.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} add failed to synchronize on {className} {compHashSet.name}! {compHashSet.GetLog()}");
                //////////////////////////////////
                // Server Add Item
                compHashSetServer.Add(Random.Range(ushort.MinValue, ushort.MaxValue), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compHashSetServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server add failed to synchronize on {className} {compHashSetServer.name}! {compHashSetServer.GetLog()}");
                //////////////////////////////////
                // Owner Remove Item
                var index = Random.Range(0, compHashSet.ListCollectionOwner.Value.Count - 1);
                compHashSet.Remove(compHashSet.ListCollectionOwner.Value.ElementAt(index), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compHashSet.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} remove failed to synchronize on {className} {compHashSet.name}! {compHashSet.GetLog()}");
                //////////////////////////////////
                // Server Remove Item
                index = Random.Range(0, compHashSetServer.ListCollectionOwner.Value.Count - 1);
                compHashSetServer.Remove(compHashSetServer.ListCollectionOwner.Value.ElementAt(index), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compHashSetServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server remove failed to synchronize on {className} {compHashSetServer.name}! {compHashSetServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compHashSet.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compHashSet.OwnerClientId}'s {className} {compHashSet.name} component match! {compHashSet.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compHashSetServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compHashSetServer.OwnerClientId}'s {className} {compHashSetServer.name} component match! {compHashSetServer.GetLog()}");

                ////////////////////////////////////
                // Owner Full Set HashSet Values
                compHashSet.FullSet(HashSetBaseTypeTestHelper.GetHashSetValues(), ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compHashSet.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} full set failed to synchronize on {className} {compHashSet.name}! {compHashSet.GetLog()}");
                //////////////////////////////////
                // Server Full Set HashSet Values
                compHashSetServer.FullSet(HashSetBaseTypeTestHelper.GetHashSetValues(), ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compHashSetServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server full set failed to synchronize on {className} {compHashSetServer.name}! {compHashSetServer.GetLog()}");

                ////////////////////////////////////
                // Owner Clear
                compHashSet.Clear(ListTestHelperBase.Targets.Owner);
                yield return WaitForConditionOrTimeOut(() => compHashSet.CompareTrackedChanges(ListTestHelperBase.Targets.Owner));
                AssertOnTimeout($"Client-{client.LocalClientId} clear failed to synchronize on {className} {compHashSet.name}! {compHashSet.GetLog()}");
                //////////////////////////////////
                // Server Clear
                compHashSetServer.Clear(ListTestHelperBase.Targets.Server);
                yield return WaitForConditionOrTimeOut(() => compHashSetServer.CompareTrackedChanges(ListTestHelperBase.Targets.Server));
                AssertOnTimeout($"Server clear failed to synchronize on {className} {compHashSetServer.name}! {compHashSetServer.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compHashSet.ValidateInstances());
                AssertOnTimeout($"[Owner] Not all instances of client-{compHashSet.OwnerClientId}'s {className} {compHashSet.name} component match! {compHashSet.GetLog()}");

                yield return WaitForConditionOrTimeOut(() => compHashSetServer.ValidateInstances());
                AssertOnTimeout($"[Server] Not all instances of client-{compHashSetServer.OwnerClientId}'s {className} {compHashSetServer.name} component match! {compHashSetServer.GetLog()}");
            }

        }
    }

    [TestFixture(HostOrServer.DAHost, CollectionTypes.List)]
    [TestFixture(HostOrServer.DAHost, CollectionTypes.Dictionary)]
    [TestFixture(HostOrServer.Host, CollectionTypes.List)]
    [TestFixture(HostOrServer.Host, CollectionTypes.Dictionary)]
    [TestFixture(HostOrServer.Server, CollectionTypes.List)]
    [TestFixture(HostOrServer.Server, CollectionTypes.Dictionary)]
    public class NetworkVariableCollectionsChangingTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;
        public enum CollectionTypes
        {
            Dictionary,
            List,
        }
        private StringBuilder m_ErrorLog = new StringBuilder();
        private CollectionTypes m_CollectionType;
        private GameObject m_TestPrefab;
        private NetworkObject m_Instance;

        public NetworkVariableCollectionsChangingTests(HostOrServer hostOrServer, CollectionTypes collectionType) : base(hostOrServer)
        {
            m_CollectionType = collectionType;
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestPrefab = CreateNetworkObjectPrefab("TestObject");
            if (m_CollectionType == CollectionTypes.Dictionary)
            {
                m_TestPrefab.AddComponent<DictionaryCollectionUpdateHelper>();
            }
            else
            {
                m_TestPrefab.AddComponent<ListCollectionUpdateHelper>();
            }
            if (m_DistributedAuthority)
            {
                var networkObject = m_TestPrefab.GetComponent<NetworkObject>();
                networkObject.SetOwnershipStatus(NetworkObject.OwnershipStatus.Transferable);
            }
            base.OnServerAndClientsCreated();
        }

        private bool AllInstancesSpawned()
        {
            if (!UseCMBService())
            {
                if (!m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(m_Instance.NetworkObjectId))
                {
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.SpawnManager.SpawnedObjects.ContainsKey(m_Instance.NetworkObjectId))
                {
                    return false;
                }
            }
            return true;
        }

        private Dictionary<ulong, NetworkManager> m_NetworkManagers = new Dictionary<ulong, NetworkManager>();

        private bool ValidateAllInstances()
        {
            if (!m_NetworkManagers.ContainsKey(m_Instance.OwnerClientId))
            {
                return false;
            }

            if (!m_NetworkManagers[m_Instance.OwnerClientId].SpawnManager.SpawnedObjects.ContainsKey(m_Instance.NetworkObjectId))
            {
                return false;
            }

            var ownerNetworkManager = m_NetworkManagers[m_Instance.OwnerClientId];

            var ownerClientInstance = m_NetworkManagers[m_Instance.OwnerClientId].SpawnManager.SpawnedObjects[m_Instance.NetworkObjectId].GetComponent<BaseCollectionUpdateHelper>();

            foreach (var client in m_NetworkManagers)
            {
                if (client.Value == ownerNetworkManager)
                {
                    continue;
                }

                var otherInstance = client.Value.SpawnManager.SpawnedObjects[m_Instance.NetworkObjectId].GetComponent<BaseCollectionUpdateHelper>();
                if (!ownerClientInstance.ValidateAgainst(otherInstance))
                {
                    return false;
                }
            }
            return true;
        }

        private bool OwnershipChangedOnAllClients(ulong expectedOwner)
        {
            m_ErrorLog.Clear();
            foreach (var client in m_NetworkManagers)
            {
                var otherInstance = client.Value.SpawnManager.SpawnedObjects[m_Instance.NetworkObjectId].GetComponent<BaseCollectionUpdateHelper>();
                if (otherInstance.OwnerClientId != expectedOwner)
                {
                    m_ErrorLog.AppendLine($"Client-{client.Value.LocalClientId} instance of {m_Instance.name} still shows the owner is Client-{otherInstance.OwnerClientId} when it should be Client-{expectedOwner}!");
                    return false;
                }
            }
            return true;
        }

        private BaseCollectionUpdateHelper GetOwnerInstance()
        {
            var ownerNetworkManager = m_NetworkManagers[m_Instance.OwnerClientId];
            return m_NetworkManagers[m_Instance.OwnerClientId].SpawnManager.SpawnedObjects[m_Instance.NetworkObjectId].GetComponent<BaseCollectionUpdateHelper>();
        }

        /// <summary>
        /// Gets the authority instance.
        /// Client-Server: will always return the server-side instance
        /// Distributed Authority: will always return the owner
        /// </summary>
        /// <returns>authority instance</returns>
        private BaseCollectionUpdateHelper GetAuthorityInstance()
        {
            if (m_DistributedAuthority)
            {
                return GetOwnerInstance();
            }
            else
            {
                return m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_Instance.NetworkObjectId].GetComponent<BaseCollectionUpdateHelper>();
            }
        }

        [UnityTest]
        public IEnumerator CollectionAndOwnershipChangingTest()
        {
            BaseCollectionUpdateHelper.VerboseMode = m_EnableVerboseDebug;
            var runWaitPeriod = new WaitForSeconds(0.5f);
            m_NetworkManagers.Clear();
            if (!UseCMBService() && m_UseHost)
            {
                m_NetworkManagers.Add(m_ServerNetworkManager.LocalClientId, m_ServerNetworkManager);
            }
            foreach (var client in m_ClientNetworkManagers)
            {
                m_NetworkManagers.Add(client.LocalClientId, client);
            }

            var authorityNetworkManager = UseCMBService() || !m_UseHost ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;

            var instance = SpawnObject(m_TestPrefab, authorityNetworkManager);
            m_Instance = instance.GetComponent<NetworkObject>();
            var helper = instance.GetComponent<BaseCollectionUpdateHelper>();
            var currentOwner = helper.OwnerClientId;
            yield return WaitForConditionOrTimeOut(AllInstancesSpawned);
            AssertOnTimeout($"[Pre][1st Phase] Timed out waiting for all clients to spawn {m_Instance.name}!");
            helper.SetState(BaseCollectionUpdateHelper.HelperStates.Start);
            yield return runWaitPeriod;

            // Update values, validate values, change owner, updates values, and repeat until all clients have been the owner at least once
            for (int i = 0; i < 4; i++)
            {
                helper.SetState(BaseCollectionUpdateHelper.HelperStates.Pause);
                yield return WaitForConditionOrTimeOut(ValidateAllInstances);
                AssertOnTimeout($"[1st Phase] Timed out waiting for all clients to validdate their values!");
                helper.SetState(BaseCollectionUpdateHelper.HelperStates.Start);
                yield return s_DefaultWaitForTick;

                currentOwner = GetAuthorityInstance().ChangeOwner();
                Assert.IsFalse(currentOwner == ulong.MaxValue, "A non-authority instance attempted to change ownership!");

                yield return WaitForConditionOrTimeOut(() => OwnershipChangedOnAllClients(currentOwner));
                AssertOnTimeout($"[1st Phase] Timed out waiting for all clients to change ownership!\n {m_ErrorLog.ToString()}");
                helper = GetOwnerInstance();
                yield return runWaitPeriod;
            }

            // Now reset the values
            helper.SetState(BaseCollectionUpdateHelper.HelperStates.Pause);
            helper.Clear();

            // Validate all instances are reset
            yield return WaitForConditionOrTimeOut(ValidateAllInstances);
            AssertOnTimeout($"[Pre][2nd Phase]Timed out waiting for all clients to validdate their values!");
            helper.SetState(BaseCollectionUpdateHelper.HelperStates.Start);

            // Update, change ownership, and repeat until all clients have been the owner at least once
            for (int i = 0; i < 4; i++)
            {
                yield return runWaitPeriod;
                currentOwner = GetAuthorityInstance().ChangeOwner();
                Assert.IsFalse(currentOwner == ulong.MaxValue, "A non-authority instance attempted to change ownership!");
                yield return WaitForConditionOrTimeOut(() => OwnershipChangedOnAllClients(currentOwner));
                AssertOnTimeout($"[2nd Phase] Timed out waiting for all clients to change ownership!");
                helper = GetOwnerInstance();
            }

            helper.SetState(BaseCollectionUpdateHelper.HelperStates.Pause);
            yield return WaitForConditionOrTimeOut(ValidateAllInstances);
            AssertOnTimeout($"[Last Validate] Timed out waiting for all clients to validdate their values!");
        }
    }

    #region COLLECTION CHANGING COMPONENTS
    /// <summary>
    /// Helper class to test adding <see cref="NetworkVariable{T}"/> dictionary entries rapidly with frequent ownership changes.
    /// This includes a companion <see cref="NetworkVariable{T}"/> integer that is continually incremented and used as the key value for each entry.
    /// </summary>
    public class DictionaryCollectionUpdateHelper : BaseCollectionUpdateHelper
    {
        private NetworkVariable<Dictionary<int, int>> m_DictionaryCollection = new NetworkVariable<Dictionary<int, int>>(new Dictionary<int, int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> m_CurrentKeyValue = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        protected override bool OnValidateAgainst(BaseCollectionUpdateHelper otherHelper)
        {
            var otherListHelper = otherHelper as DictionaryCollectionUpdateHelper;
            var localValues = m_DictionaryCollection.Value;
            var otherValues = otherListHelper.m_DictionaryCollection.Value;

            if (localValues.Count != otherValues.Count)
            {
                return false;
            }

            foreach (var entry in m_DictionaryCollection.Value)
            {
                if (!otherValues.ContainsKey(entry.Key))
                {
                    return false;
                }

                if (entry.Value != otherValues[entry.Key])
                {
                    return false;
                }
            }
            return true;
        }
        protected override void OnClear()
        {
            m_DictionaryCollection.Value.Clear();
            m_DictionaryCollection.CheckDirtyState();
            base.OnClear();
        }

        protected override void AddItem()
        {
            m_DictionaryCollection.Value.Add(m_CurrentKeyValue.Value, m_CurrentKeyValue.Value);
            m_DictionaryCollection.CheckDirtyState();
            m_CurrentKeyValue.Value++;
        }
    }

    /// <summary>
    /// Helper class to test adding list entries rapidly with frequent ownership changes
    /// </summary>
    public class ListCollectionUpdateHelper : BaseCollectionUpdateHelper
    {
        private NetworkVariable<List<int>> m_ListCollection = new NetworkVariable<List<int>>(new List<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


        protected override bool OnValidateAgainst(BaseCollectionUpdateHelper otherHelper)
        {
            var otherListHelper = otherHelper as ListCollectionUpdateHelper;
            var localValues = m_ListCollection.Value;
            var otherValues = otherListHelper.m_ListCollection.Value;

            if (localValues.Count != otherValues.Count)
            {
                return false;
            }

            for (int i = 0; i < localValues.Count - 1; i++)
            {
                if (localValues[i] != i)
                {
                    return false;
                }

                if (localValues[i] != otherValues[i])
                {
                    return false;
                }
            }
            return true;
        }

        protected override void OnClear()
        {
            m_ListCollection.Value.Clear();
            m_ListCollection.CheckDirtyState();
            base.OnClear();
        }

        protected override void AddItem()
        {
            m_ListCollection.Value.Add(m_ListCollection.Value.Count);
            m_ListCollection.CheckDirtyState();
        }
    }

    /// <summary>
    /// The base class to test rapidly adding items to a collection type
    /// </summary>
    public class BaseCollectionUpdateHelper : NetworkBehaviour
    {
        public static bool VerboseMode;
        private const int k_OwnershipTickDelay = 1;

        public enum HelperStates
        {
            Stop,
            Start,
            Pause,
            ClearToChangeOwner,
            ChangingOwner
        }
        public HelperStates HelperState { get; private set; }

        private int m_SendClearForOwnershipOnTick;
        private ulong m_NextClient = 0;
        private ulong m_ClientToSendClear = 0;

        public void SetState(HelperStates helperState)
        {
            HelperState = helperState;
        }

        protected virtual bool OnValidateAgainst(BaseCollectionUpdateHelper otherHelper)
        {
            return true;
        }

        public bool ValidateAgainst(BaseCollectionUpdateHelper otherHelper)
        {
            return OnValidateAgainst(otherHelper);
        }

        public override void OnNetworkSpawn()
        {
            // Register for tick updates
            NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;

            base.OnNetworkSpawn();
        }
        public override void OnNetworkDespawn()
        {
            NetworkManager.NetworkTickSystem.Tick -= OnNetworkTick;
            base.OnNetworkDespawn();
        }

        protected virtual void OnClear()
        {
        }

        public void Clear()
        {
            OnClear();
        }

        protected virtual void AddItem()
        {
        }

        private bool CanUpdate()
        {
            return HelperState == HelperStates.Start;
        }

        private void Update()
        {
            // Exit early if not spawn, updating is not enabled, or is not the owner
            if (!IsSpawned || !CanUpdate() || !IsOwner)
            {
                return;
            }

            AddItem();
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            // When the ownership changes and the client is the owner, then immediately add an item to the collection
            if (NetworkManager.LocalClientId == current)
            {
                AddItem();
            }
            base.OnOwnershipChanged(previous, current);
        }


        /// <summary>
        /// Sets the tick delay period of time to provide all in-flight deltas to be processed.
        /// </summary>
        private void SetTickDelay()
        {
            m_SendClearForOwnershipOnTick = NetworkManager.ServerTime.Tick + k_OwnershipTickDelay;
        }

        /// <summary>
        /// Changes the ownership
        /// </summary>
        /// <returns>next owner or ulong.MaxValue that means the authority did not invoke this method</returns>
        public ulong ChangeOwner()
        {
            if (HasAuthority && !IsOwnershipChanging())
            {
                var index = NetworkManager.ConnectedClientsIds.ToList().IndexOf(OwnerClientId);
                index++;
                index = index % NetworkManager.ConnectedClientsIds.Count;
                m_NextClient = NetworkManager.ConnectedClientsIds[index];

                // If we are in distributed authority and the authority or we are in client-server and the server, then make the change ourselves.
                if (OwnerClientId == NetworkManager.LocalClientId && (NetworkManager.DistributedAuthorityMode || (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer)))
                {
                    HelperState = HelperStates.ChangingOwner;
                    SetTickDelay();
                    Log($"Locally changing ownership to Client-{m_NextClient}");
                }

                if (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer && OwnerClientId != NetworkManager.LocalClientId)
                {
                    // If we are transitioning between a client to the host or client to client,
                    // send a "heads-up" Rpc to the client prior to changing ownership. The client
                    // will stop updating for the tick delay period and then send a confirmation
                    // to the host that it is clear to change ownership.
                    ChangingOwnershipRpc(RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
                    Log($"Remotely changing ownership to Client-{m_NextClient}");
                }

                return m_NextClient;
            }

            return ulong.MaxValue;
        }

        /// <summary>
        /// Sent by the host to a client when ownership is transitioning from a client to
        /// the host or to another client.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void ChangingOwnershipRpc(RpcParams rpcParams = default)
        {
            // The sender is who we respond to that it is clear to change ownership
            m_ClientToSendClear = rpcParams.Receive.SenderClientId;
            HelperState = HelperStates.ClearToChangeOwner;
            SetTickDelay();
        }

        /// <summary>
        /// Notification that the current owner has stopped updating and ownership
        /// updates can occur without missed updates.
        /// </summary>
        /// <param name="rpcParams"></param>
        [Rpc(SendTo.SpecifiedInParams)]
        private void ChangingOwnershipClearRpc(RpcParams rpcParams = default)
        {
            HelperState = HelperStates.ChangingOwner;
            SetTickDelay();
            Log($"Changing ownership to Client-{m_NextClient} based on ready request.");
        }

        private bool IsOwnershipChanging()
        {
            return HelperState == HelperStates.ClearToChangeOwner || HelperState == HelperStates.ChangingOwner;
        }

        private void OnNetworkTick()
        {
            if (!IsSpawned || !IsOwnershipChanging() || m_SendClearForOwnershipOnTick > NetworkManager.ServerTime.Tick)
            {
                return;
            }

            if (HelperState == HelperStates.ChangingOwner)
            {
                NetworkObject.ChangeOwnership(m_NextClient);
                Log($"Local Change ownership to Client-{m_NextClient} complete! New Owner is {NetworkObject.OwnerClientId} | Expected {m_NextClient}");
            }
            else
            {
                ChangingOwnershipClearRpc(RpcTarget.Single(m_ClientToSendClear, RpcTargetUse.Temp));
            }
            HelperState = HelperStates.Stop;
        }

        protected void Log(string msg)
        {
            if (VerboseMode)
            {
                Debug.Log($"[Client-{NetworkManager.LocalClientId}] {msg}");
            }
        }
    }
    #endregion

    #region HASHSET COMPONENT HELPERS
    public class HashSetBaseTypeTestHelper : ListTestHelperBase, IHashSetTestHelperBase<int>
    {
        public static Dictionary<ulong, Dictionary<ulong, HashSetBaseTypeTestHelper>> Instances = new Dictionary<ulong, Dictionary<ulong, HashSetBaseTypeTestHelper>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<HashSet<int>> ListCollectionServer = new NetworkVariable<HashSet<int>>(new HashSet<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<HashSet<int>> ListCollectionOwner = new NetworkVariable<HashSet<int>>(new HashSet<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, HashSet<int>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, HashSet<int>>>();

        public bool ValidateInstances()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!ListCollectionOwner.Value.SequenceEqual(otherOwnerCollection.Value))
                {
                    return false;
                }
                if (!ListCollectionServer.Value.SequenceEqual(otherServerCollection.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(Dictionary<DeltaTypes, HashSet<int>> local, Dictionary<DeltaTypes, HashSet<int>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (local[deltaType].Count != other[deltaType].Count)
                {
                    LogMessage($"{deltaType}s did not match!");
                    return false;
                }
                foreach (var value in local[deltaType])
                {
                    if (!other[deltaType].Contains(value))
                    {
                        LogMessage($"Value ({value}) in local was not found on remote!");
                        return false;
                    }
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} did not match!");
                    return false;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return true;
        }

        public static HashSet<int> GetHashSetValues(int count = 5)
        {
            var hashSet = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                hashSet.Add(Random.Range(ushort.MinValue, ushort.MaxValue));
            }
            return hashSet;
        }

        public NetworkVariable<HashSet<int>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public HashSet<int> OnSetServerValues()
        {
            return GetHashSetValues();
        }

        public HashSet<int> OnSetOwnerValues()
        {
            return GetHashSetValues();
        }

        public void Add(int value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value);
            netVar.CheckDirtyState();
        }

        public void AddRange(HashSet<int> values, Targets target)
        {
            var netVar = GetNetVar(target);
            foreach (var value in values)
            {
                netVar.Value.Add(value);
            }
            netVar.CheckDirtyState();
        }

        public void Remove(int value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(value);
            netVar.CheckDirtyState();
        }

        public void FullSet(HashSet<int> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, HashSet<int> previous, HashSet<int> current)
        {
            var contextTable = NetworkVariableChanges[target];

            var whatWasAdded = current.Except(previous).ToHashSet();
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToHashSet();
            var whatWasNeitherAddedOrRemoved = current.Where((c) => previous.Contains(c) && !whatWasAdded.Contains(c)).ToHashSet();
            var whatChanged = whatWasNeitherAddedOrRemoved.Where((c) => previous.Contains(c) && !previous.Where((d) => d.Equals(c)).FirstOrDefault().Equals(c)).ToHashSet();
            var whatRemainedTheSame = whatWasNeitherAddedOrRemoved.Where((c) => !whatChanged.Contains(c)).ToHashSet();

            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed] = whatChanged;
            contextTable[DeltaTypes.UnChanged] = whatRemainedTheSame;
        }

        public void OnServerListValuesChanged(HashSet<int> previous, HashSet<int> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(HashSet<int> previous, HashSet<int> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, HashSetBaseTypeTestHelper>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }
            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, HashSet<int>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new HashSet<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new HashSet<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new HashSet<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new HashSet<int>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, HashSet<int>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new HashSet<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new HashSet<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new HashSet<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new HashSet<int>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionOwner.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }
    #endregion

    #region DICTIONARY COMPONENT HELPERS
    public class NestedDictionaryTestHelper : ListTestHelperBase, IDictionaryTestHelperBase<int, Dictionary<int, SerializableObject>>
    {
        public static Dictionary<ulong, Dictionary<ulong, NestedDictionaryTestHelper>> Instances = new Dictionary<ulong, Dictionary<ulong, NestedDictionaryTestHelper>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<Dictionary<int, Dictionary<int, SerializableObject>>> ListCollectionServer = new NetworkVariable<Dictionary<int, Dictionary<int, SerializableObject>>>(new Dictionary<int, Dictionary<int, SerializableObject>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<Dictionary<int, Dictionary<int, SerializableObject>>> ListCollectionOwner = new NetworkVariable<Dictionary<int, Dictionary<int, SerializableObject>>>(new Dictionary<int, Dictionary<int, SerializableObject>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>>>();

        private bool CompareDictionaries(ulong clientId, Dictionary<int, SerializableObject> first, Dictionary<int, SerializableObject> second)
        {
            foreach (var entry in first)
            {
                if (!second.ContainsKey(entry.Key))
                {
                    LogMessage($"Client-{clientId} has no key entry for ({entry.Key})!");
                    return false;
                }
                var seconValue = second[entry.Key];
                if (!entry.Value.Equals(seconValue))
                {
                    LogMessage($"Client-{clientId} value ({seconValue} does not equal ({entry.Value})!");
                    return false;
                }
            }
            return true;
        }

        private bool CompareNestedDictionaries(ulong clientId, Dictionary<int, Dictionary<int, SerializableObject>> first, Dictionary<int, Dictionary<int, SerializableObject>> second)
        {
            foreach (var entry in first)
            {
                if (!second.ContainsKey(entry.Key))
                {
                    LogMessage($"Client-{clientId} has no key entry for ({entry.Key})!");
                    return false;
                }
                var secondValue = second[entry.Key];
                if (!CompareDictionaries(clientId, entry.Value, secondValue))
                {
                    LogMessage($"Client-{clientId} value root Key ({entry.Key}) dictionary does not equal the local dictionary!");
                    return false;
                }
            }
            return true;
        }

        public bool ValidateInstances()
        {
            LogStart();
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {

                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    LogMessage($"Client-{clientId} has no entry!");
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    LogMessage($"Client-{clientId} has no instance entry of NetworkObject ({NetworkObjectId})!");
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;

                if (!CompareNestedDictionaries(clientId, ListCollectionOwner.Value, otherOwnerCollection.Value))
                {
                    LogMessage($"Client-{clientId} did not synchronize properly with the owner collection!");
                    return false;
                }

                if (!CompareNestedDictionaries(clientId, ListCollectionServer.Value, otherServerCollection.Value))
                {
                    LogMessage($"Client-{clientId} did not synchronize properly with the server collection!");
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(ulong clientId, Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>> local, Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (local[deltaType].Count != other[deltaType].Count)
                {
                    LogMessage($"{deltaType}s count did not match!");
                    return false;
                }
                if (!CompareNestedDictionaries(clientId, local[deltaType], other[deltaType]))
                {
                    LogMessage($"{deltaType}s values did not match!");
                    return false;
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(clientId, localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} failed to synchronize properly!");
                    return false;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return true;
        }

        public static Dictionary<int, SerializableObject> GetDictionaryValues(int count = 5)
        {
            var dictionary = new Dictionary<int, SerializableObject>();
            for (int i = 0; i < count; i++)
            {
                dictionary.Add(i, SerializableObject.GetRandomObject());
            }
            return dictionary;
        }

        public static Dictionary<int, Dictionary<int, SerializableObject>> GetNestedDictionaryValues(int count = 5)
        {
            var dictionary = new Dictionary<int, Dictionary<int, SerializableObject>>();
            for (int i = 0; i < count; i++)
            {
                dictionary.Add(i, GetDictionaryValues());
            }
            return dictionary;
        }

        public NetworkVariable<Dictionary<int, Dictionary<int, SerializableObject>>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public Dictionary<int, Dictionary<int, SerializableObject>> OnSetServerValues()
        {

            return GetNestedDictionaryValues();
        }

        public Dictionary<int, Dictionary<int, SerializableObject>> OnSetOwnerValues()
        {
            return GetNestedDictionaryValues();
        }


        public bool UpdateValue((int, Dictionary<int, SerializableObject>) value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            if (netVar.Value.ContainsKey(value.Item1))
            {
                netVar.Value[value.Item1] = value.Item2;
                if (checkDirty)
                {
                    netVar.CheckDirtyState();
                }
                return true;
            }
            return false;
        }

        public void Add((int, Dictionary<int, SerializableObject>) value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value.Item1, value.Item2);
            netVar.CheckDirtyState();
        }

        public void Remove(int key, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(key);
            netVar.CheckDirtyState();
        }

        public void FullSet(Dictionary<int, Dictionary<int, SerializableObject>> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, Dictionary<int, Dictionary<int, SerializableObject>> previous, Dictionary<int, Dictionary<int, SerializableObject>> current)
        {
            var contextTable = NetworkVariableChanges[target];

            var whatWasAdded = current.Except(previous).ToDictionary(item => item.Key, item => item.Value);
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatWasNeitherAddedOrRemoved = current.Where((c) => previous.Contains(c) && !whatWasAdded.Contains(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatChanged = whatWasNeitherAddedOrRemoved.Where((c) => previous.Contains(c) && !previous.Where((d) => d.Equals(c)).FirstOrDefault().Equals(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatRemainedTheSame = whatWasNeitherAddedOrRemoved.Where((c) => !whatChanged.Contains(c)).ToDictionary(item => item.Key, item => item.Value);

            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed] = whatChanged;
            contextTable[DeltaTypes.UnChanged] = whatRemainedTheSame;
        }

        public void OnServerListValuesChanged(Dictionary<int, Dictionary<int, SerializableObject>> previous, Dictionary<int, Dictionary<int, SerializableObject>> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(Dictionary<int, Dictionary<int, SerializableObject>> previous, Dictionary<int, Dictionary<int, SerializableObject>> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, NestedDictionaryTestHelper>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }
            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, Dictionary<int, Dictionary<int, SerializableObject>>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new Dictionary<int, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new Dictionary<int, Dictionary<int, SerializableObject>>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionOwner.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }

    public class DictionaryTestHelper : ListTestHelperBase, IDictionaryTestHelperBase<int, SerializableObject>
    {
        public static Dictionary<ulong, Dictionary<ulong, DictionaryTestHelper>> Instances = new Dictionary<ulong, Dictionary<ulong, DictionaryTestHelper>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<Dictionary<int, SerializableObject>> ListCollectionServer = new NetworkVariable<Dictionary<int, SerializableObject>>(new Dictionary<int, SerializableObject>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<Dictionary<int, SerializableObject>> ListCollectionOwner = new NetworkVariable<Dictionary<int, SerializableObject>>(new Dictionary<int, SerializableObject>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, Dictionary<int, SerializableObject>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, Dictionary<int, SerializableObject>>>();

        private bool CompareDictionaries(ulong clientId, Dictionary<int, SerializableObject> first, Dictionary<int, SerializableObject> second)
        {
            foreach (var entry in first)
            {
                if (!second.ContainsKey(entry.Key))
                {
                    LogMessage($"Client-{clientId} has no key entry for ({entry.Key})!");
                    return false;
                }
                var seconValue = second[entry.Key];
                if (!entry.Value.Equals(seconValue))
                {
                    LogMessage($"Client-{clientId} value ({seconValue} does not equal ({entry.Value})!");
                    return false;
                }
            }
            return true;
        }

        public bool ValidateInstances()
        {
            LogStart();
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {

                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    LogMessage($"Client-{clientId} has no entry!");
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    LogMessage($"Client-{clientId} has no instance entry of NetworkObject ({NetworkObjectId})!");
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!CompareDictionaries(clientId, ListCollectionOwner.Value, otherOwnerCollection.Value))
                {
                    LogMessage($"Client-{clientId} did not synchronize properly with the owner collection!");
                    return false;
                }

                if (!CompareDictionaries(clientId, ListCollectionServer.Value, otherServerCollection.Value))
                {
                    LogMessage($"Client-{clientId} did not synchronize properly with the server collection!");
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(ulong clientId, Dictionary<DeltaTypes, Dictionary<int, SerializableObject>> local, Dictionary<DeltaTypes, Dictionary<int, SerializableObject>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (local[deltaType].Count != other[deltaType].Count)
                {
                    LogMessage($"{deltaType}s count did not match!");
                    return false;
                }
                if (!CompareDictionaries(clientId, local[deltaType], other[deltaType]))
                {
                    LogMessage($"{deltaType}s values did not match!");
                    return false;
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(clientId, localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} failed to synchronize properly!");
                    return false;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return true;
        }

        public static Dictionary<int, SerializableObject> GetDictionaryValues(int count = 5)
        {
            var dictionary = new Dictionary<int, SerializableObject>();
            for (int i = 0; i < count; i++)
            {
                dictionary.Add(i, SerializableObject.GetRandomObject());
            }
            return dictionary;
        }

        public NetworkVariable<Dictionary<int, SerializableObject>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public Dictionary<int, SerializableObject> OnSetServerValues()
        {
            return GetDictionaryValues();
        }

        public Dictionary<int, SerializableObject> OnSetOwnerValues()
        {
            return GetDictionaryValues();
        }


        public bool UpdateValue((int, SerializableObject) value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            if (netVar.Value.ContainsKey(value.Item1))
            {
                netVar.Value[value.Item1] = value.Item2;
                if (checkDirty)
                {
                    netVar.CheckDirtyState();
                }
                return true;
            }
            return false;
        }

        public void Add((int, SerializableObject) value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value.Item1, value.Item2);
            netVar.CheckDirtyState();
        }

        public void Remove(int key, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(key);
            netVar.CheckDirtyState();
        }

        public void FullSet(Dictionary<int, SerializableObject> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, Dictionary<int, SerializableObject> previous, Dictionary<int, SerializableObject> current)
        {
            var contextTable = NetworkVariableChanges[target];

            var whatWasAdded = current.Except(previous).ToDictionary(item => item.Key, item => item.Value);
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatWasNeitherAddedOrRemoved = current.Where((c) => previous.Contains(c) && !whatWasAdded.Contains(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatChanged = whatWasNeitherAddedOrRemoved.Where((c) => previous.Contains(c) && !previous.Where((d) => d.Equals(c)).FirstOrDefault().Equals(c)).ToDictionary(item => item.Key, item => item.Value);
            var whatRemainedTheSame = whatWasNeitherAddedOrRemoved.Where((c) => !whatChanged.Contains(c)).ToDictionary(item => item.Key, item => item.Value);

            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed] = whatChanged;
            contextTable[DeltaTypes.UnChanged] = whatRemainedTheSame;
        }

        public void OnServerListValuesChanged(Dictionary<int, SerializableObject> previous, Dictionary<int, SerializableObject> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(Dictionary<int, SerializableObject> previous, Dictionary<int, SerializableObject> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, DictionaryTestHelper>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }
            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, Dictionary<int, SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new Dictionary<int, SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new Dictionary<int, SerializableObject>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (!IsDebugMode)
            {
                InitValues();
            }
        }

        public void InitValues()
        {
            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionOwner.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
        }

        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }
    #endregion

    #region INETWORKSERIALIZABLE LIST TEST COMPONENT HELPERS
    public class SerializableObject : INetworkSerializable, IEquatable<SerializableObject>
    {
        public static SerializableObject GetRandomObject()
        {
            var serializableObject = new SerializableObject()
            {
                FloatValue = Random.Range(float.MinValue, float.MaxValue),
                IntValue = Random.Range(ushort.MinValue, ushort.MaxValue),
                LongValue = Random.Range(int.MinValue, int.MaxValue),
            };
            return serializableObject;
        }

        public static List<SerializableObject> GetListOfRandomObjects(int count)
        {
            var list = new List<SerializableObject>();
            for (int i = 0; i < count; i++)
            {
                list.Add(GetRandomObject());
            }
            return list;
        }

        public static List<List<SerializableObject>> GetListOfListOfRandomObjects(int numberOfLists, int countPerList)
        {
            var list = new List<List<SerializableObject>>();
            for (int i = 0; i < numberOfLists; i++)
            {
                list.Add(GetListOfRandomObjects(countPerList));
            }
            return list;
        }

        public int IntValue;
        public long LongValue;
        public float FloatValue;

        public override string ToString()
        {
            return $"{IntValue},{LongValue},{FloatValue}";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IntValue);
            serializer.SerializeValue(ref LongValue);
            serializer.SerializeValue(ref FloatValue);
        }

        public bool Equals(SerializableObject other)
        {
            return IntValue.Equals(other.IntValue) && LongValue.Equals(other.LongValue) && FloatValue.Equals(other.FloatValue);
        }

    }

    public class ListTestHelperListSerializableObject : ListTestHelperBase, IListTestHelperBase<List<SerializableObject>>
    {
        public static Dictionary<ulong, Dictionary<ulong, ListTestHelperListSerializableObject>> Instances = new Dictionary<ulong, Dictionary<ulong, ListTestHelperListSerializableObject>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<List<List<SerializableObject>>> ListCollectionServer = new NetworkVariable<List<List<SerializableObject>>>(new List<List<SerializableObject>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<List<List<SerializableObject>>> ListCollectionOwner = new NetworkVariable<List<List<SerializableObject>>>(new List<List<SerializableObject>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, List<List<SerializableObject>>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, List<List<SerializableObject>>>>();

        public bool ValidateInstances()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!CompareBothItems(ListCollectionOwner.Value, otherOwnerCollection.Value))
                {
                    return false;
                }
                if (!CompareBothItems(ListCollectionServer.Value, otherServerCollection.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CompareBothItems(List<List<SerializableObject>> first, List<List<SerializableObject>> second)
        {
            if (first.Count != second.Count)
            {
                LogMessage($"Local count ({first.Count}) did not match remote count ({second.Count})!");
                return false;
            }
            for (int i = 0; i < first.Count; i++)
            {
                if (!first[i].SequenceEqual(second[i]))
                {
                    LogMessage($"Sequence set ({i}) does not match! Local[{i}].Count = {first[i].Count} Remote[{i}].Count = {second[i].Count}.");
                    if (first[i].Count == second[i].Count)
                    {
                        var subBuilder = new StringBuilder();
                        for (int j = 0; j < first[i].Count; j++)
                        {
                            subBuilder.Append($"[{first[i][j]}][{second[i][j]}]");
                        }

                        LogMessage($"Compared: {subBuilder}");
                    }
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(Dictionary<DeltaTypes, List<List<SerializableObject>>> local, Dictionary<DeltaTypes, List<List<SerializableObject>>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (!CompareBothItems(local[deltaType], other[deltaType]))
                {
                    LogMessage($"{deltaType}s did not match!");
                    return false;
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            var trackChangesSuccess = true;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    trackChangesSuccess = false;
                    break;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    trackChangesSuccess = false;
                    break;
                }

                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} did not match!");
                    trackChangesSuccess = false;
                    break;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return trackChangesSuccess;
        }

        private List<List<SerializableObject>> GetInitialValues()
        {
            var rootList = new List<List<SerializableObject>>();
            for (int i = 0; i < 10; i++)
            {
                rootList.Add(SerializableObject.GetListOfRandomObjects(5));
            }
            return rootList;
        }

        public NetworkVariable<List<List<SerializableObject>>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public List<List<SerializableObject>> OnSetServerValues()
        {
            return GetInitialValues();
        }

        public List<List<SerializableObject>> OnSetOwnerValues()
        {
            return GetInitialValues();
        }


        public void UpdateValue(List<SerializableObject> value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            var index = netVar.Value.IndexOf(value);
            netVar.Value[index] = value;
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Add(List<SerializableObject> value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value);
            netVar.CheckDirtyState();
        }

        public void AddRange(List<List<SerializableObject>> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.AddRange(values);
            netVar.CheckDirtyState();
        }

        public void Insert(List<SerializableObject> value, int index, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Insert(index, value);
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Remove(List<SerializableObject> value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(value);
            netVar.CheckDirtyState();
        }

        public void FullSet(List<List<SerializableObject>> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, List<List<SerializableObject>> previous, List<List<SerializableObject>> current)
        {
            var contextTable = NetworkVariableChanges[target];
            var whatWasAdded = current.Except(previous).ToList();
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToList();
            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed].Clear();
            contextTable[DeltaTypes.UnChanged].Clear();
            for (int i = 0; i < current.Count; i++)
            {
                if (previous.Count > i && !current[i].SequenceEqual(previous[i]))
                {
                    contextTable[DeltaTypes.Changed].Add(current[i]);
                }
                else if (!whatWasAdded.Contains(current[i]) && previous.Contains(current[i]))
                {
                    contextTable[DeltaTypes.UnChanged].Add(current[i]);
                }
            }

        }

        public void OnServerListValuesChanged(List<List<SerializableObject>> previous, List<List<SerializableObject>> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(List<List<SerializableObject>> previous, List<List<SerializableObject>> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, ListTestHelperListSerializableObject>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }

            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, List<List<SerializableObject>>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new List<List<SerializableObject>>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, List<List<SerializableObject>>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new List<List<SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new List<List<SerializableObject>>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionServer.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }

    public class ListTestHelperSerializableObject : ListTestHelperBase, IListTestHelperBase<SerializableObject>
    {
        public static Dictionary<ulong, Dictionary<ulong, ListTestHelperSerializableObject>> Instances = new Dictionary<ulong, Dictionary<ulong, ListTestHelperSerializableObject>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<List<SerializableObject>> ListCollectionServer = new NetworkVariable<List<SerializableObject>>(new List<SerializableObject>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<List<SerializableObject>> ListCollectionOwner = new NetworkVariable<List<SerializableObject>>(new List<SerializableObject>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, List<SerializableObject>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, List<SerializableObject>>>();

        public bool ValidateInstances()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!ListCollectionOwner.Value.SequenceEqual(otherOwnerCollection.Value))
                {
                    return false;
                }
                if (!ListCollectionServer.Value.SequenceEqual(otherServerCollection.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(Dictionary<DeltaTypes, List<SerializableObject>> local, Dictionary<DeltaTypes, List<SerializableObject>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (local[deltaType].Count != other[deltaType].Count)
                {
                    LogMessage($"{deltaType}s did not match!");
                    return false;
                }

                for (int i = 0; i < local[deltaType].Count; i++)
                {
                    if (!local[deltaType][i].Equals(other[deltaType][i]))
                    {
                        LogMessage($"Sequence set ({i}) does not match! Local[{i}] = {local[deltaType][i]} Remote[{i}].Count = {other[deltaType][i]}.");
                        return false;
                    }
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} did not match!");
                    return false;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return true;
        }

        private List<SerializableObject> GetInitialValues()
        {
            return SerializableObject.GetListOfRandomObjects(10);
        }

        public NetworkVariable<List<SerializableObject>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public List<SerializableObject> OnSetServerValues()
        {
            return GetInitialValues();
        }

        public List<SerializableObject> OnSetOwnerValues()
        {
            return GetInitialValues();
        }


        public void UpdateValue(SerializableObject value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            var index = netVar.Value.IndexOf(value);
            netVar.Value[index] = value;
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Add(SerializableObject value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value);
            netVar.CheckDirtyState();
        }

        public void AddRange(List<SerializableObject> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.AddRange(values);
            netVar.CheckDirtyState();
        }

        public void Insert(SerializableObject value, int index, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Insert(index, value);
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Remove(SerializableObject value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(value);
            netVar.CheckDirtyState();
        }

        public void FullSet(List<SerializableObject> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, List<SerializableObject> previous, List<SerializableObject> current)
        {
            var contextTable = NetworkVariableChanges[target];

            var whatWasAdded = current.Except(previous).ToList();
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToList();
            var whatWasNeitherAddedOrRemoved = current.Where((c) => previous.Contains(c) && !whatWasAdded.Contains(c)).ToList();
            var whatChanged = whatWasNeitherAddedOrRemoved.Where((c) => previous.Contains(c) && !previous.Where((d) => d.Equals(c)).FirstOrDefault().Equals(c)).ToList();
            var whatRemainedTheSame = whatWasNeitherAddedOrRemoved.Where((c) => !whatChanged.Contains(c)).ToList();

            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed] = whatChanged;
            contextTable[DeltaTypes.UnChanged] = whatRemainedTheSame;
        }

        public void OnServerListValuesChanged(List<SerializableObject> previous, List<SerializableObject> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(List<SerializableObject> previous, List<SerializableObject> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, ListTestHelperSerializableObject>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }
            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, List<SerializableObject>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new List<SerializableObject>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, List<SerializableObject>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new List<SerializableObject>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new List<SerializableObject>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionOwner.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }

    #endregion

    #region BUILT-IN LIST TEST COMPONENT HELPERS
    public class ListTestHelperListInt : ListTestHelperBase, IListTestHelperBase<List<int>>
    {
        public static Dictionary<ulong, Dictionary<ulong, ListTestHelperListInt>> Instances = new Dictionary<ulong, Dictionary<ulong, ListTestHelperListInt>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<List<List<int>>> ListCollectionServer = new NetworkVariable<List<List<int>>>(new List<List<int>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<List<List<int>>> ListCollectionOwner = new NetworkVariable<List<List<int>>>(new List<List<int>>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, List<List<int>>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, List<List<int>>>>();

        public bool ValidateInstances()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!CompareBothItems(ListCollectionOwner.Value, otherOwnerCollection.Value))
                {
                    return false;
                }
                if (!CompareBothItems(ListCollectionServer.Value, otherServerCollection.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CompareBothItems(List<List<int>> first, List<List<int>> second)
        {
            if (first.Count != second.Count)
            {
                LogMessage($"Local count ({first.Count}) did not match remote count ({second.Count})!");
                return false;
            }
            for (int i = 0; i < first.Count; i++)
            {
                if (!first[i].SequenceEqual(second[i]))
                {
                    LogMessage($"Sequence set ({i}) does not match! Local[{i}].Count = {first[i].Count} Remote[{i}].Count = {second[i].Count}.");
                    if (first[i].Count == second[i].Count)
                    {
                        var subBuilder = new StringBuilder();
                        for (int j = 0; j < first[i].Count; j++)
                        {
                            subBuilder.Append($"[{first[i][j]}][{second[i][j]}]");
                        }

                        LogMessage($"Compared: {subBuilder}");
                    }
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(Dictionary<DeltaTypes, List<List<int>>> local, Dictionary<DeltaTypes, List<List<int>>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (!CompareBothItems(local[deltaType], other[deltaType]))
                {
                    LogMessage($"{deltaType}s did not match!");
                    return false;
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            var trackChangesSuccess = true;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    trackChangesSuccess = false;
                    break;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    trackChangesSuccess = false;
                    break;
                }

                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} did not match!");
                    trackChangesSuccess = false;
                    break;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return trackChangesSuccess;
        }

        private List<List<int>> GetInitialValues()
        {
            var rootList = new List<List<int>>();
            for (int i = 0; i < 10; i++)
            {
                var childList = new List<int>();
                for (int j = 0; j < 10; j++)
                {
                    childList.Add(Random.Range(short.MinValue, short.MaxValue));
                }
                rootList.Add(childList);
            }
            return rootList;
        }

        public NetworkVariable<List<List<int>>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public List<List<int>> OnSetServerValues()
        {
            return GetInitialValues();
        }

        public List<List<int>> OnSetOwnerValues()
        {
            return GetInitialValues();
        }


        public void UpdateValue(List<int> value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            var index = netVar.Value.IndexOf(value);
            netVar.Value[index] = value;
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Add(List<int> value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value);
            netVar.CheckDirtyState();
        }

        public void AddRange(List<List<int>> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.AddRange(values);
            netVar.CheckDirtyState();
        }

        public void Insert(List<int> value, int index, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Insert(index, value);
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Remove(List<int> value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(value);
            netVar.CheckDirtyState();
        }

        public void FullSet(List<List<int>> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, List<List<int>> previous, List<List<int>> current)
        {
            var contextTable = NetworkVariableChanges[target]; var whatWasAdded = current.Except(previous).ToList();
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToList();
            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed].Clear();
            contextTable[DeltaTypes.UnChanged].Clear();
            for (int i = 0; i < current.Count; i++)
            {
                if (previous.Count > i && !current[i].SequenceEqual(previous[i]))
                {
                    contextTable[DeltaTypes.Changed].Add(current[i]);
                }
                else if (!whatWasAdded.Contains(current[i]) && previous.Contains(current[i]))
                {
                    contextTable[DeltaTypes.UnChanged].Add(current[i]);
                }
            }

        }

        public void OnServerListValuesChanged(List<List<int>> previous, List<List<int>> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(List<List<int>> previous, List<List<int>> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, ListTestHelperListInt>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }

            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, List<List<int>>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new List<List<int>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new List<List<int>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new List<List<int>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new List<List<int>>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, List<List<int>>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new List<List<int>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new List<List<int>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new List<List<int>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new List<List<int>>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionServer.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }

    }

    public class ListTestHelperInt : ListTestHelperBase, IListTestHelperBase<int>
    {
        public static Dictionary<ulong, Dictionary<ulong, ListTestHelperInt>> Instances = new Dictionary<ulong, Dictionary<ulong, ListTestHelperInt>>();

        public static void ResetState()
        {
            Instances.Clear();
        }

        public NetworkVariable<List<int>> ListCollectionServer = new NetworkVariable<List<int>>(new List<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<List<int>> ListCollectionOwner = new NetworkVariable<List<int>>(new List<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // This tracks what has changed per instance which is used to compare to all other instances
        public Dictionary<Targets, Dictionary<DeltaTypes, List<int>>> NetworkVariableChanges = new Dictionary<Targets, Dictionary<DeltaTypes, List<int>>>();

        public bool ValidateInstances()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var otherOwnerCollection = Instances[clientId][NetworkObjectId].ListCollectionOwner;
                var otherServerCollection = Instances[clientId][NetworkObjectId].ListCollectionServer;
                if (!ListCollectionOwner.Value.SequenceEqual(otherOwnerCollection.Value))
                {
                    return false;
                }
                if (!ListCollectionServer.Value.SequenceEqual(otherServerCollection.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ChangesMatch(Dictionary<DeltaTypes, List<int>> local, Dictionary<DeltaTypes, List<int>> other)
        {
            var deltaTypes = Enum.GetValues(typeof(DeltaTypes)).OfType<DeltaTypes>().ToList();
            foreach (var deltaType in deltaTypes)
            {
                LogMessage($"Comparing {deltaType}:");
                if (local[deltaType].Count != other[deltaType].Count)
                {
                    LogMessage($"{deltaType}s did not match!");
                    return false;
                }

                for (int i = 0; i < local[deltaType].Count; i++)
                {
                    if (!local[deltaType][i].Equals(other[deltaType][i]))
                    {
                        LogMessage($"Sequence set ({i}) does not match! Local[{i}] = {local[deltaType][i]} Remote[{i}].Count = {other[deltaType][i]}.");
                        return false;
                    }
                }
            }
            return true;
        }

        public override bool CompareTrackedChanges(Targets target)
        {
            LogStart();
            var localChanges = NetworkVariableChanges[target];
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }
                if (!Instances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!Instances[clientId].ContainsKey(NetworkObjectId))
                {
                    return false;
                }
                var entry = Instances[clientId][NetworkObjectId];
                var otherChanges = entry.NetworkVariableChanges[target];
                LogMessage($"Comparing against client-{clientId} {entry.name}:");
                if (!ChangesMatch(localChanges, otherChanges))
                {
                    LogMessage($"Client-{clientId} {entry.name} did not match!");
                    return false;
                }
                LogMessage($"Client-{clientId} {entry.name} matched!");
            }
            return true;
        }

        private List<int> GetInitialValues()
        {
            var list = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                list.Add(Random.Range(0, ushort.MaxValue));
            }
            return list;
        }

        public NetworkVariable<List<int>> GetNetVar(Targets target)
        {
            return target == Targets.Server ? ListCollectionServer : ListCollectionOwner;
        }

        public List<int> OnSetServerValues()
        {
            return GetInitialValues();
        }

        public List<int> OnSetOwnerValues()
        {
            return GetInitialValues();
        }


        public void UpdateValue(int value, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            var index = netVar.Value.IndexOf(value);
            netVar.Value[index] = value;
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Add(int value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Add(value);
            netVar.CheckDirtyState();
        }

        public void AddRange(List<int> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.AddRange(values);
            netVar.CheckDirtyState();
        }

        public void Insert(int value, int index, Targets target, bool checkDirty = true)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Insert(index, value);
            if (checkDirty)
            {
                netVar.CheckDirtyState();
            }
        }

        public void Remove(int value, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Remove(value);
            netVar.CheckDirtyState();
        }

        public void FullSet(List<int> values, Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value = values;
            netVar.CheckDirtyState();
        }

        public void Clear(Targets target)
        {
            var netVar = GetNetVar(target);
            netVar.Value.Clear();
            netVar.CheckDirtyState();
        }

        public void TrackChanges(Targets target, List<int> previous, List<int> current)
        {
            var contextTable = NetworkVariableChanges[target];

            var whatWasAdded = current.Except(previous).ToList();
            var whatWasRemoved = previous.Where((c) => !current.Contains(c)).ToList();
            var whatWasNeitherAddedOrRemoved = current.Where((c) => previous.Contains(c) && !whatWasAdded.Contains(c)).ToList();
            var whatChanged = whatWasNeitherAddedOrRemoved.Where((c) => previous.Contains(c) && !previous.Where((d) => d.Equals(c)).FirstOrDefault().Equals(c)).ToList();
            var whatRemainedTheSame = whatWasNeitherAddedOrRemoved.Where((c) => !whatChanged.Contains(c)).ToList();

            contextTable[DeltaTypes.Added] = whatWasAdded;
            contextTable[DeltaTypes.Removed] = whatWasRemoved;
            contextTable[DeltaTypes.Changed] = whatChanged;
            contextTable[DeltaTypes.UnChanged] = whatRemainedTheSame;
        }

        public void OnServerListValuesChanged(List<int> previous, List<int> current)
        {
            TrackChanges(Targets.Server, previous, current);
        }

        public void OnOwnerListValuesChanged(List<int> previous, List<int> current)
        {
            TrackChanges(Targets.Owner, previous, current);
        }

        /// <summary>
        /// Keeps track of each client instsnce releative player instance with this component
        /// </summary>
        private void TrackRelativeInstances()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, ListTestHelperInt>());
            }

            if (!Instances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
            {
                Instances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
            }
            ResetTrackedChanges();
        }

        public void ResetTrackedChanges()
        {
            NetworkVariableChanges.Clear();
            NetworkVariableChanges.Add(Targets.Owner, new Dictionary<DeltaTypes, List<int>>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Added, new List<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Changed, new List<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.Removed, new List<int>());
            NetworkVariableChanges[Targets.Owner].Add(DeltaTypes.UnChanged, new List<int>());
            NetworkVariableChanges.Add(Targets.Server, new Dictionary<DeltaTypes, List<int>>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Added, new List<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Changed, new List<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.Removed, new List<int>());
            NetworkVariableChanges[Targets.Server].Add(DeltaTypes.UnChanged, new List<int>());
        }

        protected override void OnNetworkPostSpawn()
        {
            TrackRelativeInstances();

            ListCollectionServer.OnValueChanged += OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged += OnOwnerListValuesChanged;

            if (IsServer)
            {
                ListCollectionServer.Value = OnSetServerValues();
                ListCollectionOwner.CheckDirtyState();
            }

            if (IsOwner)
            {
                ListCollectionOwner.Value = OnSetOwnerValues();
                ListCollectionOwner.CheckDirtyState();
            }
            base.OnNetworkPostSpawn();
        }
        public override void OnNetworkDespawn()
        {
            ListCollectionServer.OnValueChanged -= OnServerListValuesChanged;
            ListCollectionOwner.OnValueChanged -= OnOwnerListValuesChanged;
            base.OnNetworkDespawn();
        }
    }
    #endregion

    #region BASE TEST COMPONENT HELPERS
    public class ListTestHelperBase : NetworkBehaviour
    {
        protected static bool IsDebugMode { get; private set; }

        public enum Targets
        {
            Server,
            Owner
        }

        public enum DeltaTypes
        {
            Added,
            Removed,
            Changed,
            UnChanged
        }

        private StringBuilder m_StringBuilder = new StringBuilder();

        public string GetLog()
        {
            return m_StringBuilder.ToString();
        }

        protected void LogMessage(string message)
        {
            m_StringBuilder.AppendLine(message);
        }

        protected void LogStart()
        {
            m_StringBuilder.Clear();
            m_StringBuilder.AppendLine($"[Client-{NetworkManager.LocalClientId}][{name}] Log Started.");
        }

        public void SetDebugMode(bool isDebug)
        {
            IsDebugMode = isDebug;
        }

        public virtual bool CompareTrackedChanges(Targets target)
        {
            return false;
        }
    }

    public interface IListTestHelperBase<T>
    {
        public bool ValidateInstances();

        public NetworkVariable<List<T>> GetNetVar(ListTestHelperBase.Targets target);

        public List<T> OnSetServerValues();

        public List<T> OnSetOwnerValues();

        public void UpdateValue(T value, ListTestHelperBase.Targets target, bool checkDirty = true);

        public void Add(T value, ListTestHelperBase.Targets target);

        public void AddRange(List<T> values, ListTestHelperBase.Targets target);

        public void Insert(T value, int index, ListTestHelperBase.Targets target, bool checkDirty = true);

        public void Remove(T value, ListTestHelperBase.Targets target);

        public void FullSet(List<T> values, ListTestHelperBase.Targets target);

        public void Clear(ListTestHelperBase.Targets target);

        public void TrackChanges(ListTestHelperBase.Targets target, List<T> previous, List<T> current);

        public void OnServerListValuesChanged(List<T> previous, List<T> current);

        public void OnOwnerListValuesChanged(List<T> previous, List<T> current);

        public void ResetTrackedChanges();
    }

    public interface IDictionaryTestHelperBase<TKey, TValue>
    {
        public bool ValidateInstances();

        public NetworkVariable<Dictionary<TKey, TValue>> GetNetVar(ListTestHelperBase.Targets target);

        public Dictionary<TKey, TValue> OnSetServerValues();

        public Dictionary<TKey, TValue> OnSetOwnerValues();

        public bool UpdateValue((TKey, TValue) value, ListTestHelperBase.Targets target, bool checkDirty = true);

        public void Add((TKey, TValue) value, ListTestHelperBase.Targets target);

        public void Remove(TKey key, ListTestHelperBase.Targets target);

        public void FullSet(Dictionary<TKey, TValue> values, ListTestHelperBase.Targets target);

        public void Clear(ListTestHelperBase.Targets target);

        public void TrackChanges(ListTestHelperBase.Targets target, Dictionary<TKey, TValue> previous, Dictionary<TKey, TValue> current);

        public void OnServerListValuesChanged(Dictionary<TKey, TValue> previous, Dictionary<TKey, TValue> current);

        public void OnOwnerListValuesChanged(Dictionary<TKey, TValue> previous, Dictionary<TKey, TValue> current);

        public void ResetTrackedChanges();
    }

    public interface IHashSetTestHelperBase<T>
    {
        public bool ValidateInstances();

        public NetworkVariable<HashSet<T>> GetNetVar(ListTestHelperBase.Targets target);

        public HashSet<T> OnSetServerValues();

        public HashSet<T> OnSetOwnerValues();

        public void Add(T value, ListTestHelperBase.Targets target);

        public void Remove(T value, ListTestHelperBase.Targets target);

        public void Clear(ListTestHelperBase.Targets target);

        public void TrackChanges(ListTestHelperBase.Targets target, HashSet<T> previous, HashSet<T> current);

        public void OnServerListValuesChanged(HashSet<T> previous, HashSet<T> current);

        public void OnOwnerListValuesChanged(HashSet<T> previous, HashSet<T> current);

        public void ResetTrackedChanges();
    }
    #endregion
}
