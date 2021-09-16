using Unity.Netcode;
using NUnit.Framework;
using UnityEngine;

namespace TestProject.RuntimeTests.Support
{
    public class SpawnRpcDespawnInstanceHandler : INetworkPrefabInstanceHandler
    {
        private uint m_PrefabHash;

        public bool WasSpawned = false;
        public bool WasDestroyed = false;

        public SpawnRpcDespawnInstanceHandler(uint prefabHash)
        {
            m_PrefabHash = prefabHash;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            WasSpawned = true;

            if (NetworkManager.Singleton.NetworkConfig.UseSnapshotSpawn)
            {
                Assert.AreEqual(NetworkUpdateStage.EarlyUpdate, NetworkUpdateLoop.UpdateStage);
            }
            else
            {
                Assert.AreEqual(SpawnRpcDespawn.TestStage, NetworkUpdateLoop.UpdateStage);
            }


            // See if there is a valid registered NetworkPrefabOverrideLink associated with the provided prefabHash
            GameObject networkPrefabReference = null;
            if (NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(m_PrefabHash))
            {
                switch (NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].Override)
                {
                    default:
                    case NetworkPrefabOverride.None:
                        networkPrefabReference = NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].Prefab;
                        break;
                    case NetworkPrefabOverride.Hash:
                    case NetworkPrefabOverride.Prefab:
                        networkPrefabReference = NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].OverridingTargetPrefab;
                        break;
                }
            }

            // If not, then there is an issue (user possibly didn't register the prefab properly?)
            if (networkPrefabReference == null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError($"Failed to create object locally. [{nameof(m_PrefabHash)}={m_PrefabHash}]. {nameof(NetworkPrefab)} could not be found. Is the prefab registered with {nameof(NetworkManager)}?");
                }
                return null;
            }

            // Otherwise, instantiate an instance of the NetworkPrefab linked to the prefabHash
            var networkObject = Object.Instantiate(networkPrefabReference, position, rotation).GetComponent<NetworkObject>();

            return networkObject;
        }

        public void Destroy(NetworkObject networkObject)
        {
            WasDestroyed = true;
            if (networkObject.NetworkManager.IsClient)
            {
                if (NetworkManager.Singleton.NetworkConfig.UseSnapshotSpawn)
                {
                    Assert.AreEqual(NetworkUpdateStage.EarlyUpdate, NetworkUpdateLoop.UpdateStage);
                }
                else
                {
                    Assert.AreEqual(NetworkUpdateStage.PostLateUpdate, NetworkUpdateLoop.UpdateStage);
                }
            }

            Object.Destroy(networkObject.gameObject);
        }
    }
}
