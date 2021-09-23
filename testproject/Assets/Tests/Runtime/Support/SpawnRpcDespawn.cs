using System;
using Unity.Netcode;
using NUnit.Framework;
using UnityEngine;

namespace TestProject.RuntimeTests.Support
{
    public class SpawnRpcDespawn : NetworkBehaviour, INetworkUpdateSystem
    {
        public static NetworkUpdateStage TestStage;
        public static int ClientUpdateCount;
        public static int ServerUpdateCount;
        public static bool ClientNetworkSpawnRpcCalled;
        public static NetworkUpdateStage StageExecutedByReceiver;

        private bool m_Active = false;

        [ClientRpc]
        public void SendIncrementUpdateCountClientRpc()
        {
            Assert.AreEqual(NetworkUpdateStage.EarlyUpdate, NetworkUpdateLoop.UpdateStage);

            StageExecutedByReceiver = NetworkUpdateLoop.UpdateStage;
            ++ClientUpdateCount;
            Debug.Log($"Client RPC executed at {NetworkUpdateLoop.UpdateStage}; client count to {ClientUpdateCount.ToString()}");
        }

        public void IncrementUpdateCount()
        {
            ++ServerUpdateCount;
            Debug.Log($"Server count to {ServerUpdateCount.ToString()}");
            SendIncrementUpdateCountClientRpc();
        }

        public void Activate()
        {
            Debug.Log("Activated");
            m_Active = true;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            TestClientRpc();
        }

        [ClientRpc]
        private void TestClientRpc()
        {
            ClientNetworkSpawnRpcCalled = true;
        }

        public void NetworkStart()
        {
            Debug.Log($"Network Start on client {NetworkManager.LocalClientId.ToString()}");
            Assert.AreEqual(NetworkUpdateStage.EarlyUpdate, NetworkUpdateLoop.UpdateStage);
        }

        public void Awake()
        {
            foreach (NetworkUpdateStage stage in Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                NetworkUpdateLoop.RegisterNetworkUpdate(this, stage);
            }
        }

        public void OnDestroy()
        {
            foreach (NetworkUpdateStage stage in Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                NetworkUpdateLoop.UnregisterNetworkUpdate(this, stage);
            }
        }

        private void RunTest()
        {
            Debug.Log("Running test...");
            GetComponent<NetworkObject>().Spawn();
            IncrementUpdateCount();
            Destroy(gameObject);
            m_Active = false;
        }

        public void NetworkUpdate(NetworkUpdateStage stage)
        {
            if (IsServer && m_Active && stage == TestStage)
            {
                RunTest();
            }
        }
    }
}
