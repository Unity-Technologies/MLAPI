using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TestProject.RuntimeTests
{
    internal class NetworkObjectSpawnerForTests : NetworkBehaviour
    {
        internal static Dictionary<ulong, NetworkObjectSpawnerForTests> Instances = new Dictionary<ulong, NetworkObjectSpawnerForTests>();

        internal static Dictionary<ulong, List<NetworkObject>> SpawnedInstances = new Dictionary<ulong, List<NetworkObject>>();

        internal static GameObject ObjectToSpawn;

        internal static void Reset()
        {
            Instances.Clear();
            SpawnedInstances.Clear();
            if (ObjectToSpawn)
            {
                DestroyImmediate(ObjectToSpawn);
            }
            ObjectToSpawn = null;
        }

        internal static bool SpawnAfterInSceneSynchronized;
        internal static bool SpawnAfterSynchronized;
        internal static bool OnlyAuthoritySpawns;

        public override void OnNetworkSpawn()
        {
            if (!Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                Instances.Add(NetworkManager.LocalClientId, this);
            }
            else
            {
                Debug.LogError($"Already have an instance registered for Client-{NetworkManager.LocalClientId}! (did you forget to clear in teardown?)");
            }
            base.OnNetworkSpawn();
        }

        protected override void OnInSceneObjectsSpawned()
        {
            CheckForSpawn();
            base.OnInSceneObjectsSpawned();
        }

        protected override void OnNetworkSessionSynchronized()
        {
            CheckForSpawn();
            base.OnNetworkSessionSynchronized();
        }

        private void CheckForSpawn()
        {
            if (!OnlyAuthoritySpawns || (OnlyAuthoritySpawns && ((NetworkManager.DistributedAuthorityMode && NetworkManager.LocalClient.IsSessionOwner) || (!NetworkManager.DistributedAuthorityMode && IsServer))))
            {
                if (!NetworkManager.DistributedAuthorityMode && !IsServer)
                {
                    SpawnRpc();
                }
                else
                {
                    Spawn(NetworkManager.LocalClientId);
                }
            }
        }

        [Rpc(SendTo.Server)]
        private void SpawnRpc(RpcParams rpcParams = default)
        {
            Spawn(rpcParams.Receive.SenderClientId);
        }

        private void Spawn(ulong ownerId)
        {
            var instance = Instantiate(ObjectToSpawn);
            var networkObject = instance.GetComponent<NetworkObject>();
            networkObject.SpawnWithOwnership(ownerId);
            if (!SpawnedInstances.ContainsKey(ownerId))
            {
                SpawnedInstances.Add(ownerId, new List<NetworkObject>());
            }
            SpawnedInstances[ownerId].Add(networkObject);
        }


    }
}
