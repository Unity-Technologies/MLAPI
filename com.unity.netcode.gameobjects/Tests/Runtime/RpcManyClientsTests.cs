using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    internal class RpcManyClientsObject : NetworkBehaviour
    {
        public int Count = 0;
        public List<ulong> ReceivedFrom = new List<ulong>();
        [ServerRpc(RequireOwnership = false)]
        public void ResponseServerRpc(ServerRpcParams rpcParams = default)
        {
            ReceivedFrom.Add(rpcParams.Receive.SenderClientId);
            Count++;
        }

        [ClientRpc]
        public void NoParamsClientRpc()
        {
            ResponseServerRpc();
        }

        [ClientRpc]
        public void OneParamClientRpc(int value)
        {
            ResponseServerRpc();
        }

        [ClientRpc]
        public void TwoParamsClientRpc(int value1, int value2)
        {
            ResponseServerRpc();
        }

        [ClientRpc]
        public void WithParamsClientRpc(ClientRpcParams param)
        {
            ResponseServerRpc();
        }
    }

    internal class RpcManyClientsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 10;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        private GameObject m_PrefabToSpawn;

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = PreparePrefab(typeof(RpcManyClientsObject));
        }

        public GameObject PreparePrefab(Type type)
        {
            var prefabToSpawn = new GameObject();
            prefabToSpawn.AddComponent(type);
            var networkObjectPrefab = prefabToSpawn.AddComponent<NetworkObject>();
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObjectPrefab);
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab() { Prefab = prefabToSpawn });
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab() { Prefab = prefabToSpawn });
            }
            return prefabToSpawn;
        }

        [Test]
        public void RpcManyClientsTest()
        {
            var spawnedObject = UnityEngine.Object.Instantiate(m_PrefabToSpawn);
            var netSpawnedObject = spawnedObject.GetComponent<NetworkObject>();
            netSpawnedObject.NetworkManagerOwner = m_ServerNetworkManager;

            netSpawnedObject.Spawn();

            var messageHookList = new List<MessageHookEntry>();
            var serverMessageHookEntry = new MessageHookEntry(m_ServerNetworkManager);
            serverMessageHookEntry.AssignMessageType<ServerRpcMessage>();
            messageHookList.Add(serverMessageHookEntry);
            foreach (var client in m_ClientNetworkManagers)
            {
                var clientMessageHookEntry = new MessageHookEntry(client);
                clientMessageHookEntry.AssignMessageType<ServerRpcMessage>();
                messageHookList.Add(clientMessageHookEntry);
            }
            var rpcMessageHooks = new MessageHooksConditional(messageHookList);

            var rpcManyClientsObject = netSpawnedObject.GetComponent<RpcManyClientsObject>();

            rpcManyClientsObject.Count = 0;
            rpcManyClientsObject.NoParamsClientRpc(); // RPC with no params

            // Check that all ServerRpcMessages were sent
            WaitForConditionOrTimeOutWithTimeTravel(rpcMessageHooks);

            // Now provide a small window of time to let the server receive and process all messages
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => TotalClients == rpcManyClientsObject.Count);
            Assert.True(success, $"Timed out wait for {nameof(rpcManyClientsObject.NoParamsClientRpc)}! Only {rpcManyClientsObject.Count} of {TotalClients} was received!");

            rpcManyClientsObject.Count = 0;
            rpcManyClientsObject.OneParamClientRpc(0); // RPC with one param
            rpcMessageHooks.Reset();
            WaitForConditionOrTimeOutWithTimeTravel(rpcMessageHooks);

            // Now provide a small window of time to let the server receive and process all messages
            success = WaitForConditionOrTimeOutWithTimeTravel(() => TotalClients == rpcManyClientsObject.Count);
            Assert.True(success, $"Timed out wait for {nameof(rpcManyClientsObject.OneParamClientRpc)}! Only {rpcManyClientsObject.Count} of {TotalClients} was received!");

            var param = new ClientRpcParams();

            rpcManyClientsObject.Count = 0;
            rpcManyClientsObject.TwoParamsClientRpc(0, 0); // RPC with two params

            rpcMessageHooks.Reset();
            WaitForConditionOrTimeOutWithTimeTravel(rpcMessageHooks);
            // Now provide a small window of time to let the server receive and process all messages
            success = WaitForConditionOrTimeOutWithTimeTravel(() => TotalClients == rpcManyClientsObject.Count);
            Assert.True(success, $"Timed out wait for {nameof(rpcManyClientsObject.TwoParamsClientRpc)}! Only {rpcManyClientsObject.Count} of {TotalClients} was received!");

            rpcManyClientsObject.ReceivedFrom.Clear();
            rpcManyClientsObject.Count = 0;
            var target = new List<ulong> { m_ClientNetworkManagers[1].LocalClientId, m_ClientNetworkManagers[2].LocalClientId };
            param.Send.TargetClientIds = target;
            rpcManyClientsObject.WithParamsClientRpc(param);

            messageHookList.Clear();
            var targetedClientMessageHookEntry = new MessageHookEntry(m_ClientNetworkManagers[1]);
            targetedClientMessageHookEntry.AssignMessageType<ServerRpcMessage>();
            messageHookList.Add(targetedClientMessageHookEntry);
            targetedClientMessageHookEntry = new MessageHookEntry(m_ClientNetworkManagers[2]);
            targetedClientMessageHookEntry.AssignMessageType<ServerRpcMessage>();
            messageHookList.Add(targetedClientMessageHookEntry);
            rpcMessageHooks = new MessageHooksConditional(messageHookList);

            WaitForConditionOrTimeOutWithTimeTravel(rpcMessageHooks);

            // Now provide a small window of time to let the server receive and process all messages
            success = WaitForConditionOrTimeOutWithTimeTravel(() => 2 == rpcManyClientsObject.Count);
            Assert.True(success, $"Timed out wait for {nameof(rpcManyClientsObject.TwoParamsClientRpc)}! Only {rpcManyClientsObject.Count} of 2 was received!");

            // either of the 2 selected clients can reply to the server first, due to network timing
            var possibility1 = new List<ulong> { m_ClientNetworkManagers[1].LocalClientId, m_ClientNetworkManagers[2].LocalClientId };
            var possibility2 = new List<ulong> { m_ClientNetworkManagers[2].LocalClientId, m_ClientNetworkManagers[1].LocalClientId };
            Debug.Assert(Enumerable.SequenceEqual(rpcManyClientsObject.ReceivedFrom, possibility1) || Enumerable.SequenceEqual(rpcManyClientsObject.ReceivedFrom, possibility2));
        }
    }
}
