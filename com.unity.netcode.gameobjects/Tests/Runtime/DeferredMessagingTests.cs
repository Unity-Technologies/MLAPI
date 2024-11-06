using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class TestDeferredMessageManager : DeferredMessageManager
    {
        public bool DeferMessageCalled;
        public bool ProcessTriggersCalled;

        public delegate void BeforePurgeDelegate(TestDeferredMessageManager manager, ulong key);
        public delegate void BeforeDeferDelegate(TestDeferredMessageManager manager, ulong key);

        public BeforePurgeDelegate OnBeforePurge;
        public BeforeDeferDelegate OnBeforeDefer;

        // The way that OnBeforePurge gets used in these tests involves counting things that are still in the
        // deferred message queue. This is something the base class doesn't support so the way it purges individual
        // messages does not make it safe to access structures during purging. This set contains a list of
        // keys that are unsafe to access because they've been purged/disposed, but not removed from the dictionary
        // yet, so that we can avoid accessing those and record them instead as 0 (which is what they will be once
        // the purge completes)
        private HashSet<ulong> m_PurgedKeys = new HashSet<ulong>();

        public TestDeferredMessageManager(NetworkManager networkManager) :
            base(networkManager)
        {
        }

        public void ClearCallFlags()
        {
            DeferMessageCalled = false;
            ProcessTriggersCalled = false;
        }

        public int DeferredMessageCountTotal()
        {
            int ret = 0;
            foreach (var kvp in m_Triggers)
            {
                ret += DeferredMessageCountForType(kvp.Key);
            }

            return ret;
        }

        public int DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType trigger)
        {
            var count = 0;
            if (m_Triggers.TryGetValue(trigger, out var dict))
            {
                foreach (var kvp in dict)
                {
                    if (m_PurgedKeys.Contains(kvp.Key))
                    {
                        continue;
                    }
                    count += kvp.Value.TriggerData.Length;
                }
            }

            return count;
        }

        public int DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType trigger, ulong key)
        {
            if (m_PurgedKeys.Contains(key))
            {
                return 0;
            }
            if (m_Triggers.TryGetValue(trigger, out var dict))
            {
                if (dict.TryGetValue(key, out var subdict))
                {
                    return subdict.TriggerData.Length;
                }
            }

            return 0;
        }
        public override void DeferMessage(IDeferredNetworkMessageManager.TriggerType trigger, ulong key, FastBufferReader reader, ref NetworkContext context, string messageType)
        {
            OnBeforeDefer?.Invoke(this, key);
            DeferMessageCalled = true;
            base.DeferMessage(trigger, key, reader, ref context, messageType);
        }

        public override void ProcessTriggers(IDeferredNetworkMessageManager.TriggerType trigger, ulong key)
        {
            if (trigger == IDeferredNetworkMessageManager.TriggerType.OnNextFrame)
            {
                return;
            }
            ProcessTriggersCalled = true;
            base.ProcessTriggers(trigger, key);
        }

        protected override void PurgeTrigger(IDeferredNetworkMessageManager.TriggerType triggerType, ulong key, TriggerInfo triggerInfo)
        {
            OnBeforePurge?.Invoke(this, key);
            base.PurgeTrigger(triggerType, key, triggerInfo);
            m_PurgedKeys.Add(key);
        }

        public override void CleanupStaleTriggers()
        {
            base.CleanupStaleTriggers();
            m_PurgedKeys.Clear();
        }
    }

    internal class DeferredMessageTestRpcComponent : NetworkBehaviour
    {
        public bool ClientRpcCalled;

        [ClientRpc]
        public void SendTestClientRpc()
        {
            ClientRpcCalled = true;
        }

        public static readonly List<ulong> ClientInstances = new List<ulong>();
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                ClientInstances.Add(NetworkManager.LocalClientId);
            }
            base.OnNetworkSpawn();
        }
    }

    internal class DeferredMessageTestNetworkVariableComponent : NetworkBehaviour
    {
        public static readonly List<ulong> ClientInstances = new List<ulong>();

        public NetworkVariable<int> TestNetworkVariable;

        public void Awake()
        {
            TestNetworkVariable = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                ClientInstances.Add(NetworkManager.LocalClientId);
            }
            base.OnNetworkSpawn();
        }
    }

    internal class DeferredMessageTestRpcAndNetworkVariableComponent : NetworkBehaviour
    {
        public static readonly List<ulong> ClientInstances = new List<ulong>();
        public bool ClientRpcCalled;
        public NetworkVariable<int> TestNetworkVariable;

        public void Awake()
        {
            TestNetworkVariable = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                ClientInstances.Add(NetworkManager.LocalClientId);
            }
            base.OnNetworkSpawn();
        }

        [ClientRpc]
        public void SendTestClientRpc()
        {
            ClientRpcCalled = true;
        }
    }

    internal class DeferredMessagingTest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 0;

        private List<MessageCatcher<CreateObjectMessage>> m_ClientSpawnCatchers = new List<MessageCatcher<CreateObjectMessage>>();

        private GameObject m_RpcPrefab;
        private GameObject m_NetworkVariablePrefab;
        private GameObject m_RpcAndNetworkVariablePrefab;

        private int m_NumberOfClientsToLateJoin = 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override void OnInlineSetup()
        {
            // Revert back to standard deferred message format for tests (for now)
            DeferredMessageManager.IncludeMessageType = false;

            DeferredMessageTestRpcAndNetworkVariableComponent.ClientInstances.Clear();
            DeferredMessageTestRpcComponent.ClientInstances.Clear();
            DeferredMessageTestNetworkVariableComponent.ClientInstances.Clear();
            m_SkipAddingPrefabsToClient = false;
            // Host is irrelevant, messages don't get sent to the host "client"
            m_UseHost = false;

            // Replace the IDeferredMessageManager component with our test one in the component factory
            ComponentFactory.Register<IDeferredNetworkMessageManager>(networkManager => new TestDeferredMessageManager(networkManager));
        }

        protected override void OnInlineTearDown()
        {
            // Revert the IDeferredMessageManager component to its default (DeferredMessageManager)
            ComponentFactory.Deregister<IDeferredNetworkMessageManager>();
            m_ClientSpawnCatchers.Clear();
        }

        protected override void OnServerAndClientsCreated()
        {
            // Note: This is where prefabs should be created
            m_RpcPrefab = CreateNetworkObjectPrefab("Object With RPC");
            var networkObject = m_RpcPrefab.GetComponent<NetworkObject>();
            m_RpcPrefab.AddComponent<DeferredMessageTestRpcComponent>();

            m_NetworkVariablePrefab = CreateNetworkObjectPrefab("Object With NetworkVariable");
            networkObject = m_NetworkVariablePrefab.GetComponent<NetworkObject>();
            m_NetworkVariablePrefab.AddComponent<DeferredMessageTestNetworkVariableComponent>();

            m_RpcAndNetworkVariablePrefab = CreateNetworkObjectPrefab("Object With NetworkVariable And RPC");
            networkObject = m_RpcAndNetworkVariablePrefab.GetComponent<NetworkObject>();
            m_RpcAndNetworkVariablePrefab.AddComponent<DeferredMessageTestRpcAndNetworkVariableComponent>();

            m_ServerNetworkManager.NetworkConfig.ForceSamePrefabs = false;

        }

        private bool m_SkipAddingPrefabsToClient = false;

        private void AddPrefabsToClient(NetworkManager networkManager)
        {
            networkManager.AddNetworkPrefab(m_RpcPrefab);
            networkManager.AddNetworkPrefab(m_NetworkVariablePrefab);
            networkManager.AddNetworkPrefab(m_RpcAndNetworkVariablePrefab);
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.ForceSamePrefabs = false;
            if (!m_SkipAddingPrefabsToClient)
            {
                AddPrefabsToClient(networkManager);
            }

            base.OnNewClientCreated(networkManager);
        }

        private void SpawnClients(bool clearTestDeferredMessageManagerCallFlags = true)
        {
            for (int i = 0; i < m_NumberOfClientsToLateJoin; i++)
            {
                // Create and join client
                CreateAndStartNewClientWithTimeTravel();
            }

            if (clearTestDeferredMessageManagerCallFlags)
            {
                ClearTestDeferredMessageManagerCallFlags();
            }
        }

        private T GetComponentForClient<T>(ulong clientId) where T : NetworkBehaviour
        {
#if UNITY_2023_1_OR_NEWER
            var componentsToFind = Object.FindObjectsByType<T>(FindObjectsSortMode.InstanceID);
#else
            var componentsToFind = Object.FindObjectsOfType<T>();
#endif

            foreach (var component in componentsToFind)
            {
                if (component.IsSpawned && component.NetworkManager.LocalClientId == clientId)
                {
                    return component;
                }
            }
            return null;
        }

        private void CatchSpawns()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                var catcher = new MessageCatcher<CreateObjectMessage>(client);
                m_ClientSpawnCatchers.Add(catcher);
                client.ConnectionManager.MessageManager.Hook(catcher);
            }
        }

        private void ReleaseSpawns()
        {
            for (var i = 0; i < m_ClientNetworkManagers.Length; ++i)
            {
                // Unhook first so the spawn catcher stops catching spawns
                m_ClientNetworkManagers[i].ConnectionManager.MessageManager.Unhook(m_ClientSpawnCatchers[i]);
                m_ClientSpawnCatchers[i].ReleaseMessages();
            }
            m_ClientSpawnCatchers.Clear();
        }

        protected override void OnTimeTravelServerAndClientsConnected()
        {
            // Clear out these values from whatever might have set them during the initial startup.
            ClearTestDeferredMessageManagerCallFlags();
        }

        private void WaitForClientsToCatchSpawns(int count = 1)
        {
            Assert.IsTrue(WaitForConditionOrTimeOutWithTimeTravel(() =>
            {
                foreach (var catcher in m_ClientSpawnCatchers)
                {
                    if (catcher.CaughtMessageCount != count)
                    {
                        return false;
                    }
                }

                return true;
            }));
        }

        private void ClearTestDeferredMessageManagerCallFlags()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.ClearCallFlags();
            }
        }

        private void AssertSpawnTriggerCountForObject(TestDeferredMessageManager manager, GameObject serverObject, int expectedCount = 1)
        {
            Assert.AreEqual(expectedCount, manager.DeferredMessageCountTotal());
            Assert.AreEqual(expectedCount, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
            Assert.AreEqual(expectedCount, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
            Assert.AreEqual(0, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab));
        }

        private void WaitForAllClientsToReceive<T>() where T : INetworkMessage
        {
            WaitForMessageReceivedWithTimeTravel<T>(m_ClientNetworkManagers.ToList(), ReceiptType.Received);
        }

        private void WaitForAllClientsToReceive<TFirstMessage, TSecondMessage>()
            where TFirstMessage : INetworkMessage
            where TSecondMessage : INetworkMessage
        {
            WaitForMessagesReceivedWithTimeTravel(new List<Type>
            {
                typeof(TFirstMessage),
                typeof(TSecondMessage)
            }, m_ClientNetworkManagers.ToList(), ReceiptType.Received);
        }

        private void WaitForAllClientsToReceive<TFirstMessage, TSecondMessage, TThirdMessage>()
        where TFirstMessage : INetworkMessage
        where TSecondMessage : INetworkMessage
        where TThirdMessage : INetworkMessage
        {
            WaitForMessagesReceivedWithTimeTravel(new List<Type>
            {
                typeof(TFirstMessage),
                typeof(TSecondMessage),
                typeof(TThirdMessage),
            }, m_ClientNetworkManagers.ToList(), ReceiptType.Received);
        }

        private void WaitForAllClientsToReceive<TFirstMessage, TSecondMessage, TThirdMessage, TFourthMessage>()
            where TFirstMessage : INetworkMessage
            where TSecondMessage : INetworkMessage
            where TThirdMessage : INetworkMessage
            where TFourthMessage : INetworkMessage
        {
            WaitForMessagesReceivedWithTimeTravel(new List<Type>
            {
                typeof(TFirstMessage),
                typeof(TSecondMessage),
                typeof(TThirdMessage),
                typeof(TFourthMessage),
            }, m_ClientNetworkManagers.ToList(), ReceiptType.Received);
        }

        [Test]
        public void WhenAnRpcArrivesBeforeASpawnArrives_ItIsDeferred()
        {
            SpawnClients();
            CatchSpawns();
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            serverObject.GetComponent<DeferredMessageTestRpcComponent>().SendTestClientRpc();

            WaitForAllClientsToReceive<ClientRpcMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);
                AssertSpawnTriggerCountForObject(manager, serverObject);
            }
        }

        [Test]
        public void WhenADespawnArrivesBeforeASpawnArrives_ItIsDeferred()
        {
            SpawnClients();
            CatchSpawns();
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            serverObject.GetComponent<NetworkObject>().Despawn(false);

            WaitForAllClientsToReceive<DestroyObjectMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);
                AssertSpawnTriggerCountForObject(manager, serverObject);
            }
        }

        [Test]
        public void WhenAChangeOwnershipMessageArrivesBeforeASpawnArrives_ItIsDeferred()
        {
            SpawnClients();
            CatchSpawns();
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);
            WaitForAllClientsToReceive<ChangeOwnershipMessage>();
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);
                AssertSpawnTriggerCountForObject(manager, serverObject);
            }
        }

        [Test]
        public void WhenANetworkVariableDeltaMessageArrivesBeforeASpawnArrives_ItIsDeferred()
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();


            var serverObject = Object.Instantiate(m_NetworkVariablePrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            serverObject.GetComponent<DeferredMessageTestNetworkVariableComponent>().TestNetworkVariable.Value = 1;

            WaitForAllClientsToReceive<NetworkVariableDeltaMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);
                // TODO: Network Variables generate an extra message immediately at spawn for some reason...
                // Seems like a bug since the network variable data is in the spawn message already.
                AssertSpawnTriggerCountForObject(manager, serverObject, 1);
            }
        }

        [Test]
        //[Ignore("Disabling this temporarily until it is migrated into new integration test.")]
        public void WhenASpawnMessageArrivesBeforeThePrefabIsAvailable_ItIsDeferred()
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();

            WaitForAllClientsToReceive<CreateObjectMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);
                Assert.AreEqual(1, manager.DeferredMessageCountTotal());
                Assert.AreEqual(0, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab, serverObject.GetComponent<NetworkObject>().GlobalObjectIdHash));

                var component = GetComponentForClient<DeferredMessageTestRpcComponent>(client.LocalClientId);
                Assert.IsNull(component);
            }
        }

        [Test]
        public void WhenAnRpcIsDeferred_ItIsProcessedOnSpawn()
        {
            WhenAnRpcArrivesBeforeASpawnArrives_ItIsDeferred();
            ReleaseSpawns();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                var component = GetComponentForClient<DeferredMessageTestRpcComponent>(client.LocalClientId);
                Assert.IsTrue(component.ClientRpcCalled);
            }
        }

        [Test]
        public void WhenADespawnIsDeferred_ItIsProcessedOnSpawn()
        {
            WhenADespawnArrivesBeforeASpawnArrives_ItIsDeferred();
            ReleaseSpawns();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                // Should have been immediately despawned.
                var component = GetComponentForClient<DeferredMessageTestRpcComponent>(client.LocalClientId);
                Assert.IsTrue(!component);
            }
        }

        [Test]
        public void WhenAChangeOwnershipMessageIsDeferred_ItIsProcessedOnSpawn()
        {
            WhenAChangeOwnershipMessageArrivesBeforeASpawnArrives_ItIsDeferred();
            ReleaseSpawns();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                var component = GetComponentForClient<DeferredMessageTestRpcComponent>(client.LocalClientId);
                Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, component.OwnerClientId);
            }
        }

        [Test]
        public void WhenANetworkVariableDeltaMessageIsDeferred_ItIsProcessedOnSpawn()
        {
            WhenANetworkVariableDeltaMessageArrivesBeforeASpawnArrives_ItIsDeferred();

            foreach (var client in m_ClientNetworkManagers)
            {
                AddPrefabsToClient(client);
            }

            ReleaseSpawns();

            // Wait for the clients to spawn the NetworkObjects
            bool HaveAllClientsSpawned()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!DeferredMessageTestNetworkVariableComponent.ClientInstances.Contains(client.LocalClientId))
                    {
                        return false;
                    }
                }
                return true;
            }
            WaitForConditionOrTimeOutWithTimeTravel(HaveAllClientsSpawned);

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                var component = GetComponentForClient<DeferredMessageTestNetworkVariableComponent>(client.LocalClientId);
                Assert.AreEqual(1, component.TestNetworkVariable.Value);
            }
        }

        [Test]
        public void WhenASpawnMessageIsDeferred_ItIsProcessedOnAddPrefab()
        {
            // This will prevent spawned clients from adding prefabs
            m_SkipAddingPrefabsToClient = true;
            WhenASpawnMessageArrivesBeforeThePrefabIsAvailable_ItIsDeferred();

            // Now add the prefabs
            foreach (var client in m_ClientNetworkManagers)
            {
                AddPrefabsToClient(client);
            }

            // Wait for the clients to spawn the NetworkObjects
            bool HaveAllClientsSpawned()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!DeferredMessageTestRpcComponent.ClientInstances.Contains(client.LocalClientId))
                    {
                        return false;
                    }
                }
                return true;
            }
            WaitForConditionOrTimeOutWithTimeTravel(HaveAllClientsSpawned);

            // Validate this test
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                var component = GetComponentForClient<DeferredMessageTestRpcComponent>(client.LocalClientId);
                Assert.IsNotNull(component);
            }
        }

        [Test]
        public void WhenMultipleSpawnTriggeredMessagesAreDeferred_TheyAreAllProcessedOnSpawn()
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();

            var serverObject = Object.Instantiate(m_RpcAndNetworkVariablePrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().SendTestClientRpc();
            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().TestNetworkVariable.Value = 1;

            WaitForAllClientsToReceive<ClientRpcMessage, NetworkVariableDeltaMessage>();

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);

                Assert.AreEqual(3, manager.DeferredMessageCountTotal());
                Assert.AreEqual(3, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(3, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                Assert.AreEqual(0, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab));
                AddPrefabsToClient(client);
            }

            ReleaseSpawns();

            // Wait for the clients to spawn the NetworkObjects
            bool HaveAllClientsSpawned()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!DeferredMessageTestRpcAndNetworkVariableComponent.ClientInstances.Contains(client.LocalClientId))
                    {
                        return false;
                    }
                }
                return true;
            }
            WaitForConditionOrTimeOutWithTimeTravel(HaveAllClientsSpawned);
            TimeTravel(0.1, 1);

            // Validate the spawned objects
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled, "Process triggers were not called!");
                Assert.AreEqual(0, manager.DeferredMessageCountTotal(), $"Deferred message count ({manager.DeferredMessageCountTotal()}) is not zero!");

                var component = GetComponentForClient<DeferredMessageTestRpcAndNetworkVariableComponent>(client.LocalClientId);
                Assert.IsTrue(component.ClientRpcCalled, "Client RPC was not called!");
                Assert.AreEqual(1, component.TestNetworkVariable.Value, $"Test {nameof(NetworkVariable<int>)} ({component.TestNetworkVariable.Value}) does not equal 1!");
                Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, component.OwnerClientId, $"{component.name} owner id ({component.OwnerClientId}) does not equal first client id ({m_ClientNetworkManagers[0].LocalClientId})");
            }
        }

        [Test]
        public void WhenMultipleAddPrefabTriggeredMessagesAreDeferred_TheyAreAllProcessedOnAddNetworkPrefab()
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();

            var serverObject2 = Object.Instantiate(m_RpcPrefab);
            serverObject2.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject2.GetComponent<NetworkObject>().Spawn();

            WaitForAllClientsToReceive<CreateObjectMessage, CreateObjectMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);

                Assert.AreEqual(2, manager.DeferredMessageCountTotal());
                Assert.AreEqual(0, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(2, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab));
                Assert.AreEqual(2, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab, serverObject.GetComponent<NetworkObject>().GlobalObjectIdHash));
                AddPrefabsToClient(client);
            }

            // Wait for the clients to spawn the NetworkObjects
            bool HaveAllClientsSpawned()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!DeferredMessageTestRpcComponent.ClientInstances.Contains(client.LocalClientId))
                    {
                        return false;
                    }
                }
                return true;
            }
            WaitForConditionOrTimeOutWithTimeTravel(HaveAllClientsSpawned);


            foreach (var client in m_ClientNetworkManagers)
            {
                var found1 = false;
                var found2 = false;
#if UNITY_2023_1_OR_NEWER
                var deferredMessageTestRpcComponents = Object.FindObjectsByType<DeferredMessageTestRpcComponent>(FindObjectsSortMode.None);
#else
                var deferredMessageTestRpcComponents = Object.FindObjectsOfType<DeferredMessageTestRpcComponent>();
#endif

                foreach (var component in deferredMessageTestRpcComponents)
                {
                    if (component.IsSpawned && component.NetworkManager.LocalClientId == client.LocalClientId)
                    {
                        if (component.NetworkObjectId == serverObject.GetComponent<NetworkObject>().NetworkObjectId)
                        {
                            found1 = true;
                        }
                        else if (component.NetworkObjectId == serverObject2.GetComponent<NetworkObject>().NetworkObjectId)
                        {
                            found2 = true;
                        }
                    }
                }

                Assert.IsTrue(found1);
                Assert.IsTrue(found2);

                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled);

                Assert.AreEqual(0, manager.DeferredMessageCountTotal());
            }
        }

        [Test]
        public void WhenSpawnTriggeredMessagesAreDeferredBeforeThePrefabIsAdded_AddingThePrefabCausesThemToBeProcessed()
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();

            var serverObject = Object.Instantiate(m_RpcAndNetworkVariablePrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();

            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().SendTestClientRpc();
            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().TestNetworkVariable.Value = 1;
            // TODO: Remove this if we figure out how to work around the NetworkVariableDeltaMessage.Serialized issue at line 59
            // Otherwise, we have to wait for at least 1 tick for the NetworkVariableDeltaMessage to be generated before changing ownership
            WaitForAllClientsToReceive<CreateObjectMessage, ClientRpcMessage, NetworkVariableDeltaMessage>();

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            // Validate messages are deferred and pending
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.DeferMessageCalled);
                Assert.IsFalse(manager.ProcessTriggersCalled);

                Assert.AreEqual(4, manager.DeferredMessageCountTotal());

                Assert.AreEqual(3, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(3, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab, serverObject.GetComponent<NetworkObject>().GlobalObjectIdHash));
                AddPrefabsToClient(client);
            }

            // Wait for the clients to spawn the NetworkObjects
            bool HaveAllClientsSpawned()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    if (!DeferredMessageTestRpcAndNetworkVariableComponent.ClientInstances.Contains(client.LocalClientId))
                    {
                        return false;
                    }
                }
                return true;
            }
            WaitForConditionOrTimeOutWithTimeTravel(HaveAllClientsSpawned);

            TimeTravel(0.1, 1);

            // Validate the test
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.IsTrue(manager.ProcessTriggersCalled);
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());

                var component = GetComponentForClient<DeferredMessageTestRpcAndNetworkVariableComponent>(client.LocalClientId);
                Assert.NotNull(component);
                Assert.IsTrue(component.ClientRpcCalled);
                Assert.AreEqual(1, component.TestNetworkVariable.Value);
                Assert.AreEqual(m_ClientNetworkManagers[0].LocalClientId, component.OwnerClientId);
            }
        }

        [Test]
        public void WhenAMessageIsDeferredForMoreThanTheConfiguredTime_ItIsRemoved([Values(1, 2, 3)] int timeout)
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
            }
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            var start = 0f;

            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforeDeferDelegate beforeDefer = (manager, key) =>
                {
                    if (start == 0)
                    {
                        start = client.RealTimeProvider.RealTimeSinceStartup;
                    }
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforeDefer = beforeDefer;
            }

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            foreach (var unused in m_ClientNetworkManagers)
            {
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
            }

            int purgeCount = 0;
            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforePurgeDelegate beforePurge = (manager, key) =>
                {
                    ++purgeCount;
                    var elapsed = client.RealTimeProvider.RealTimeSinceStartup - start;
                    VerboseDebug(client.RealTimeProvider.GetType().FullName);
                    Assert.GreaterOrEqual(elapsed, timeout);
                    Assert.AreEqual(1, manager.DeferredMessageCountTotal());
                    Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                    Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, key));
                    Assert.AreEqual(serverObject.GetComponent<NetworkObject>().NetworkObjectId, key);
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = beforePurge;
            }

            TimeTravel(timeout - 0.01, 1);

            bool HaveAnyClientsPurged()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                    if (manager.DeferredMessageCountTotal() == 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            bool HaveAllClientsPurged()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                    if (manager.DeferredMessageCountTotal() != 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            Assert.IsFalse(HaveAnyClientsPurged());

            TimeTravel(0.02, 1);

            Assert.IsTrue(HaveAllClientsPurged());
        }

        [Test]
        public void WhenMultipleMessagesForTheSameObjectAreDeferredForMoreThanTheConfiguredTime_TheyAreAllRemoved([Values(1, 2, 3)] int timeout)
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
            }
            var serverObject = Object.Instantiate(m_RpcAndNetworkVariablePrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            var start = 0f;

            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforeDeferDelegate beforeDefer = (manager, key) =>
                {
                    if (start == 0)
                    {
                        start = client.RealTimeProvider.RealTimeSinceStartup;
                    }
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforeDefer = beforeDefer;
            }

            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().SendTestClientRpc();
            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().TestNetworkVariable.Value = 1;
            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForMessagesReceivedWithTimeTravel(
                new List<Type> {typeof(ClientRpcMessage), typeof(NetworkVariableDeltaMessage), typeof(ChangeOwnershipMessage),
                }, m_ClientNetworkManagers.ToList(), ReceiptType.Received);

            foreach (var unused in m_ClientNetworkManagers)
            {
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
            }

            int purgeCount = 0;
            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforePurgeDelegate beforePurge = (manager, key) =>
                {
                    ++purgeCount;
                    var elapsed = client.RealTimeProvider.RealTimeSinceStartup - start;
                    Assert.GreaterOrEqual(elapsed, timeout);
                    Assert.AreEqual(3, manager.DeferredMessageCountTotal());
                    Assert.AreEqual(3, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                    Assert.AreEqual(3, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, key));
                    Assert.AreEqual(serverObject.GetComponent<NetworkObject>().NetworkObjectId, key);
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = beforePurge;
            }

            var timePassedSinceFirstStart = MockTimeProvider.StaticRealTimeSinceStartup - start;
            TimeTravel(timeout - 0.01 - timePassedSinceFirstStart, 1);

            bool HaveAnyClientsPurged()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                    if (manager.DeferredMessageCountTotal() == 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            bool HaveAllClientsPurged()
            {
                foreach (var client in m_ClientNetworkManagers)
                {
                    var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                    if (manager.DeferredMessageCountTotal() != 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            Assert.IsFalse(HaveAnyClientsPurged());

            TimeTravel(0.02 + timePassedSinceFirstStart, 1);

            Assert.IsTrue(HaveAllClientsPurged());
        }

        [Test]
        public void WhenMultipleMessagesForDifferentObjectsAreDeferredForMoreThanTheConfiguredTime_TheyAreAllRemoved([Values(1, 2, 3)] int timeout)
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
            }
            var serverObject = Object.Instantiate(m_RpcAndNetworkVariablePrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();

            var serverObject2 = Object.Instantiate(m_RpcAndNetworkVariablePrefab);
            serverObject2.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject2.GetComponent<NetworkObject>().Spawn();

            WaitForClientsToCatchSpawns(2);

            var start = 0f;

            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforeDeferDelegate beforeDefer = (manager, key) =>
                {
                    if (start == 0)
                    {
                        start = client.RealTimeProvider.RealTimeSinceStartup;
                    }
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforeDefer = beforeDefer;
            }

            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().SendTestClientRpc();
            serverObject.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().TestNetworkVariable.Value = 1;
            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            serverObject2.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().SendTestClientRpc();
            serverObject2.GetComponent<DeferredMessageTestRpcAndNetworkVariableComponent>().TestNetworkVariable.Value = 1;
            serverObject2.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForMessagesReceivedWithTimeTravel(
            new List<Type> {typeof(ClientRpcMessage), typeof(NetworkVariableDeltaMessage), typeof(ChangeOwnershipMessage),typeof(ClientRpcMessage), typeof(NetworkVariableDeltaMessage), typeof(ChangeOwnershipMessage),
            }, m_ClientNetworkManagers.ToList(), ReceiptType.Received);

            foreach (var unused in m_ClientNetworkManagers)
            {
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject2.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
            }

            int purgeCount = 0;
            foreach (var client in m_ClientNetworkManagers)
            {
                var remainingMessagesTotalThisClient = 6;
                TestDeferredMessageManager.BeforePurgeDelegate beforePurge = (manager, key) =>
                {
                    ++purgeCount;
                    var elapsed = client.RealTimeProvider.RealTimeSinceStartup - start;
                    Assert.GreaterOrEqual(elapsed, timeout - 0.25f);
                    Assert.AreEqual(remainingMessagesTotalThisClient, manager.DeferredMessageCountTotal());
                    Assert.AreEqual(remainingMessagesTotalThisClient, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                    Assert.AreEqual(3, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, key));
                    remainingMessagesTotalThisClient -= 3;
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = beforePurge;
            }

            TimeTravel(timeout + 0.1f, 1);
            foreach (var client in m_ClientNetworkManagers)
            {
                AddPrefabsToClient(client);
            }
            Assert.AreEqual(m_NumberOfClientsToLateJoin * 2, purgeCount);
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());
            }
        }

        [Test]
        public void WhenADeferredMessageIsRemoved_OtherMessagesForSameObjectAreRemoved([Values(1, 2, 3)] int timeout)
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
            }
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns();

            var start = 0f;

            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforeDeferDelegate beforeDefer = (manager, key) =>
                {
                    if (start == 0)
                    {
                        start = client.RealTimeProvider.RealTimeSinceStartup;
                    }
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforeDefer = beforeDefer;
            }

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            TimeTravel(timeout - 0.5f, 1);

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(1, manager.DeferredMessageCountTotal());
                Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
            }

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ServerNetworkManager.LocalClientId);
            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(2, manager.DeferredMessageCountTotal());
                Assert.AreEqual(2, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(2, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
            }

            foreach (var unused in m_ClientNetworkManagers)
            {
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
            }

            int purgeCount = 0;
            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforePurgeDelegate beforePurge = (manager, key) =>
                {
                    ++purgeCount;
                    var elapsed = client.RealTimeProvider.RealTimeSinceStartup - start;
                    Assert.GreaterOrEqual(elapsed, timeout - 0.05f);
                    Assert.AreEqual(2, manager.DeferredMessageCountTotal());
                    Assert.AreEqual(2, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                    Assert.AreEqual(2, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, key));
                    Assert.AreEqual(serverObject.GetComponent<NetworkObject>().NetworkObjectId, key);
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = beforePurge;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                AddPrefabsToClient(client);
            }

            TimeTravel(0.6f, 1);

            Assert.AreEqual(m_NumberOfClientsToLateJoin, purgeCount);
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(0, manager.DeferredMessageCountTotal());
            }
        }

        [Test]
        public void WhenADeferredMessageIsRemoved_OtherMessagesForDifferentObjectsAreNotRemoved([Values(1, 2, 3)] int timeout)
        {
            m_SkipAddingPrefabsToClient = true;
            SpawnClients();
            CatchSpawns();
            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.SpawnTimeout = timeout;
            }
            var serverObject = Object.Instantiate(m_RpcPrefab);
            serverObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject.GetComponent<NetworkObject>().Spawn();
            var serverObject2 = Object.Instantiate(m_RpcPrefab);
            serverObject2.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            serverObject2.GetComponent<NetworkObject>().Spawn();
            WaitForClientsToCatchSpawns(2);

            var start = 0f;

            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforeDeferDelegate beforeDefer = (manager, key) =>
                {
                    if (start == 0)
                    {
                        start = client.RealTimeProvider.RealTimeSinceStartup;
                    }
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforeDefer = beforeDefer;
            }

            serverObject.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            TimeTravel(timeout - 0.5f, 1);

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(1, manager.DeferredMessageCountTotal());
                Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                Assert.AreEqual(0, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject2.GetComponent<NetworkObject>().NetworkObjectId));
            }

            // KITTY-TODO: Review this change please:
            // Changing ownership when the owner specified is already an owner should not send any messages
            // The original test was changing ownership to the server when the object was spawned with the server being an owner.
            //serverObject2.GetComponent<NetworkObject>().ChangeOwnership(m_ServerNetworkManager.LocalClientId);
            serverObject2.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[1].LocalClientId);
            WaitForAllClientsToReceive<ChangeOwnershipMessage>();

            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(2, manager.DeferredMessageCountTotal());
                Assert.AreEqual(2, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject2.GetComponent<NetworkObject>().NetworkObjectId));
            }

            foreach (var unused in m_ClientNetworkManagers)
            {
                LogAssert.Expect(LogType.Warning, $"[Netcode] Deferred messages were received for a trigger of type {IDeferredNetworkMessageManager.TriggerType.OnSpawn} associated with id ({serverObject.GetComponent<NetworkObject>().NetworkObjectId}), but the {nameof(NetworkObject)} was not received within the timeout period {timeout} second(s).");
            }

            int purgeCount = 0;
            foreach (var client in m_ClientNetworkManagers)
            {
                TestDeferredMessageManager.BeforePurgeDelegate beforePurge = (manager, key) =>
                {
                    ++purgeCount;
                    var elapsed = client.RealTimeProvider.RealTimeSinceStartup - start;
                    Assert.GreaterOrEqual(elapsed, timeout - 0.05f);
                    Assert.AreEqual(2, manager.DeferredMessageCountTotal());
                    Assert.AreEqual(2, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));

                    Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                    Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject2.GetComponent<NetworkObject>().NetworkObjectId));

                    Assert.AreEqual(serverObject.GetComponent<NetworkObject>().NetworkObjectId, key);
                };
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = beforePurge;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                AddPrefabsToClient(client);
            }

            TimeTravel(0.6f, 1);

            Assert.AreEqual(m_NumberOfClientsToLateJoin, purgeCount);
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                Assert.AreEqual(1, manager.DeferredMessageCountTotal());
                Assert.AreEqual(1, manager.DeferredMessageCountForType(IDeferredNetworkMessageManager.TriggerType.OnSpawn));
                Assert.AreEqual(0, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject.GetComponent<NetworkObject>().NetworkObjectId));
                Assert.AreEqual(1, manager.DeferredMessageCountForKey(IDeferredNetworkMessageManager.TriggerType.OnSpawn, serverObject2.GetComponent<NetworkObject>().NetworkObjectId));
            }
            foreach (var client in m_ClientNetworkManagers)
            {
                var manager = (TestDeferredMessageManager)client.DeferredMessageManager;
                manager.OnBeforePurge = null;
            }
        }
    }
}
