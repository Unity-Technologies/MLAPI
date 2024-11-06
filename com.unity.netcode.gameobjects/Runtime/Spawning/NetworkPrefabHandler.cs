using System;
using System.Collections.Generic;
using TrollKing.Core;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Interface for customizing, overriding, spawning, and destroying Network Prefabs
    /// Used by <see cref="NetworkPrefabHandler"/>
    /// </summary>
    public interface INetworkPrefabInstanceHandler
    {
        /// <summary>
        /// Client Side Only
        /// Once an implementation is registered with the <see cref="NetworkPrefabHandler"/>, this method will be called every time
        /// a Network Prefab associated <see cref="NetworkObject"/> is spawned on clients
        ///
        /// Note On Hosts: Use the <see cref="NetworkPrefabHandler.RegisterHostGlobalObjectIdHashValues(GameObject, List{GameObject})"/>
        /// method to register all targeted NetworkPrefab overrides manually since the host will be acting as both a server and client.
        ///
        /// Note on Pooling:  If you are using a NetworkObject pool, don't forget to make the NetworkObject active
        /// via the  <see cref="GameObject.SetActive(bool)"/> method.
        /// </summary>
        /// <param name="ownerClientId">the owner for the <see cref="NetworkObject"/> to be instantiated</param>
        /// <param name="position">the initial/default position for the <see cref="NetworkObject"/> to be instantiated</param>
        /// <param name="rotation">the initial/default rotation for the <see cref="NetworkObject"/> to be instantiated</param>
        /// <returns></returns>
        NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation);

        /// <summary>
        /// Invoked on Client and Server
        /// Once an implementation is registered with the <see cref="NetworkPrefabHandler"/>, this method will be called when
        /// a Network Prefab associated <see cref="NetworkObject"/> is:
        ///
        /// Server Side: destroyed or despawned with the destroy parameter equal to true
        /// If <see cref="NetworkObject.Despawn(bool)"/> is invoked with the default destroy parameter (i.e. false) then this method will NOT be invoked!
        ///
        /// Client Side: destroyed when the client receives a destroy object message from the server or host.
        ///
        /// Note on Pooling: When this method is invoked, you do not need to destroy the NetworkObject as long as you want your pool to persist.
        /// The most common approach is to make the <see cref="NetworkObject"/> inactive by calling <see cref="GameObject.SetActive(bool)"/>.
        /// </summary>
        /// <param name="networkObject">The <see cref="NetworkObject"/> being destroyed</param>
        void Destroy(NetworkObject networkObject);
    }

    /// <summary>
    /// Primary handler to add or remove customized spawn and destroy handlers for a network prefab (i.e. a prefab with a NetworkObject component)
    /// Register custom prefab handlers by implementing the <see cref="INetworkPrefabInstanceHandler"/> interface.
    /// </summary>
    public class NetworkPrefabHandler
    {
        private static readonly NetworkLogScope k_Log = new NetworkLogScope(nameof(NetworkPrefabHandler));

        private NetworkManager m_NetworkManager;

        /// <summary>
        /// Links a network prefab asset to a class with the INetworkPrefabInstanceHandler interface
        /// </summary>
        private readonly Dictionary<uint, INetworkPrefabInstanceHandler> m_PrefabAssetToPrefabHandler = new Dictionary<uint, INetworkPrefabInstanceHandler>();

        /// <summary>
        /// Links the custom prefab instance's GlobalNetworkObjectId to the original prefab asset's GlobalNetworkObjectId.  (Needed for HandleNetworkPrefabDestroy)
        /// [PrefabInstance][PrefabAsset]
        /// </summary>
        private readonly Dictionary<uint, uint> m_PrefabInstanceToPrefabAsset = new Dictionary<uint, uint>();

        internal static string PrefabDebugHelper(NetworkPrefab networkPrefab) => $"{nameof(NetworkPrefab)} \"{networkPrefab.Prefab.name}\"";

        /// <summary>
        /// Use a <see cref="GameObject"/> to register a class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface with the <see cref="NetworkPrefabHandler"/>
        /// </summary>
        /// <param name="networkPrefabAsset">the <see cref="GameObject"/> of the network prefab asset to be overridden</param>
        /// <param name="instanceHandler">class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface to be registered</param>
        /// <returns>true (registered) false (failed to register)</returns>
        public bool AddHandler(GameObject networkPrefabAsset, INetworkPrefabInstanceHandler instanceHandler)
        {
            return AddHandler(networkPrefabAsset.GetComponent<NetworkObject>().GlobalObjectIdHash, instanceHandler);
        }

        /// <summary>
        /// Use a  <see cref="NetworkObject"/> to register a class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface with the <see cref="NetworkPrefabHandler"/>
        /// </summary>
        /// <param name="prefabAssetNetworkObject"> the <see cref="NetworkObject"/> of the network prefab asset to be overridden</param>
        /// <param name="instanceHandler">the class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface to be registered</param>
        /// <returns></returns>
        public bool AddHandler(NetworkObject prefabAssetNetworkObject, INetworkPrefabInstanceHandler instanceHandler)
        {
            return AddHandler(prefabAssetNetworkObject.GlobalObjectIdHash, instanceHandler);
        }

        /// <summary>
        /// Use a <see cref="NetworkObject.GlobalObjectIdHash"/> to register a class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface with the <see cref="NetworkPrefabHandler"/>
        /// </summary>
        /// <param name="globalObjectIdHash"> the <see cref="NetworkObject.GlobalObjectIdHash"/> value of the network prefab asset being overridden</param>
        /// <param name="instanceHandler">a class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface</param>
        /// <returns></returns>
        public bool AddHandler(uint globalObjectIdHash, INetworkPrefabInstanceHandler instanceHandler)
        {
            if (!m_PrefabAssetToPrefabHandler.ContainsKey(globalObjectIdHash))
            {
                m_PrefabAssetToPrefabHandler.Add(globalObjectIdHash, instanceHandler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// HOST ONLY!
        /// Since a host is unique and is considered both a client and a server, for each source NetworkPrefab you must manually
        /// register all potential <see cref="GameObject"/> target overrides that have the <see cref="NetworkObject"/> component.
        /// </summary>
        /// <param name="sourceNetworkPrefab">source NetworkPrefab to be overridden</param>
        /// <param name="networkPrefabOverrides">one or more NetworkPrefabs could be used to override the source NetworkPrefab</param>
        public void RegisterHostGlobalObjectIdHashValues(GameObject sourceNetworkPrefab, List<GameObject> networkPrefabOverrides)
        {
            if (NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    var sourceNetworkObject = sourceNetworkPrefab.GetComponent<NetworkObject>();
                    if (sourceNetworkPrefab != null)
                    {
                        var sourceGlobalObjectIdHash = sourceNetworkObject.GlobalObjectIdHash;
                        // Now we register all
                        foreach (var gameObject in networkPrefabOverrides)
                        {
                            if (gameObject.TryGetComponent<NetworkObject>(out var targetNetworkObject))
                            {
                                if (!m_PrefabInstanceToPrefabAsset.ContainsKey(targetNetworkObject.GlobalObjectIdHash))
                                {
                                    m_PrefabInstanceToPrefabAsset.Add(targetNetworkObject.GlobalObjectIdHash, sourceGlobalObjectIdHash);
                                }
                                else
                                {
                                    Debug.LogWarning($"{targetNetworkObject.name} appears to be a duplicate entry!");
                                }
                            }
                            else
                            {
                                throw new Exception($"{targetNetworkObject.name} does not have a {nameof(NetworkObject)} component!");
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"{sourceNetworkPrefab.name} does not have a {nameof(NetworkObject)} component!");
                    }
                }
                else
                {
                    throw new Exception($"You should only call {nameof(RegisterHostGlobalObjectIdHashValues)} as a Host!");
                }
            }
            else
            {
                throw new Exception($"You can only call {nameof(RegisterHostGlobalObjectIdHashValues)} once NetworkManager is listening!");
            }
        }

        /// <summary>
        /// Use the <see cref="GameObject"/> of the overridden network prefab asset to remove a registered class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface.
        /// </summary>
        /// <param name="networkPrefabAsset"><see cref="GameObject"/> of the network prefab asset that was being overridden</param>
        /// <returns>true (success) or false (failure)</returns>
        public bool RemoveHandler(GameObject networkPrefabAsset)
        {
            return RemoveHandler(networkPrefabAsset.GetComponent<NetworkObject>().GlobalObjectIdHash);
        }

        /// <summary>
        /// Use the <see cref="NetworkObject"/> of the overridden network prefab asset to remove a registered class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface.
        /// </summary>
        /// <param name="networkObject"><see cref="NetworkObject"/> of the source NetworkPrefab that was being overridden</param>
        /// <returns>true (success) or false (failure)</returns>
        public bool RemoveHandler(NetworkObject networkObject)
        {
            return RemoveHandler(networkObject.GlobalObjectIdHash);
        }

        /// <summary>
        /// Use the <see cref="NetworkObject.GlobalObjectIdHash"/> of the overridden network prefab asset to remove a registered class that implements the <see cref="INetworkPrefabInstanceHandler"/> interface.
        /// </summary>
        /// <param name="globalObjectIdHash"><see cref="NetworkObject.GlobalObjectIdHash"/> of the source NetworkPrefab that was being overridden</param>
        /// <returns>true (success) or false (failure)</returns>
        public bool RemoveHandler(uint globalObjectIdHash)
        {
            if (m_PrefabInstanceToPrefabAsset.ContainsValue(globalObjectIdHash))
            {
                uint networkPrefabHashKey = 0;
                foreach (var kvp in m_PrefabInstanceToPrefabAsset)
                {
                    if (kvp.Value == globalObjectIdHash)
                    {
                        networkPrefabHashKey = kvp.Key;
                        break;
                    }
                }

                m_PrefabInstanceToPrefabAsset.Remove(networkPrefabHashKey);
            }

            return m_PrefabAssetToPrefabHandler.Remove(globalObjectIdHash);
        }

        /// <summary>
        /// Check to see if a <see cref="GameObject"/> with a <see cref="NetworkObject"/> is registered to an <see cref="INetworkPrefabInstanceHandler"/> implementation
        /// </summary>
        /// <param name="networkPrefab"></param>
        /// <returns>true or false</returns>
        internal bool ContainsHandler(GameObject networkPrefab) => ContainsHandler(networkPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash);

        /// <summary>
        /// Check to see if a <see cref="NetworkObject"/> is registered to an <see cref="INetworkPrefabInstanceHandler"/> implementation
        /// </summary>
        /// <param name="networkObject"></param>
        /// <returns>true or false</returns>
        internal bool ContainsHandler(NetworkObject networkObject) => ContainsHandler(networkObject.GlobalObjectIdHash);

        /// <summary>
        /// Check to see if a <see cref="NetworkObject.GlobalObjectIdHash"/> is registered to an <see cref="INetworkPrefabInstanceHandler"/> implementation
        /// </summary>
        /// <param name="networkPrefabHash"></param>
        /// <returns>true or false</returns>
        internal bool ContainsHandler(uint networkPrefabHash) => m_PrefabAssetToPrefabHandler.ContainsKey(networkPrefabHash) || m_PrefabInstanceToPrefabAsset.ContainsKey(networkPrefabHash);

        /// <summary>
        /// Returns the source NetworkPrefab's <see cref="NetworkObject.GlobalObjectIdHash"/>
        /// </summary>
        /// <param name="networkPrefabHash"></param>
        /// <returns></returns>
        internal uint GetSourceGlobalObjectIdHash(uint networkPrefabHash)
        {
            if (m_PrefabAssetToPrefabHandler.ContainsKey(networkPrefabHash))
            {
                return networkPrefabHash;
            }

            if (m_PrefabInstanceToPrefabAsset.TryGetValue(networkPrefabHash, out var hash))
            {
                return hash;
            }

            return 0;
        }

        /// <summary>
        /// Will return back a <see cref="NetworkObject"/> generated via an <see cref="INetworkPrefabInstanceHandler"/> implementation
        /// Note: Invoked only on the client side and called within NetworkSpawnManager.CreateLocalNetworkObject
        /// </summary>
        /// <param name="networkPrefabAssetHash">typically the "server-side" asset's prefab hash</param>
        /// <param name="ownerClientId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        internal NetworkObject HandleNetworkPrefabSpawn(uint networkPrefabAssetHash, ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            if (m_PrefabAssetToPrefabHandler.TryGetValue(networkPrefabAssetHash, out var prefabInstanceHandler))
            {
                var networkObjectInstance = prefabInstanceHandler.Instantiate(ownerClientId, position, rotation);

                //Now we must make sure this alternate PrefabAsset spawned in place of the prefab asset with the networkPrefabAssetHash (GlobalObjectIdHash)
                //is registered and linked to the networkPrefabAssetHash so during the HandleNetworkPrefabDestroy process we can identify the alternate prefab asset.
                if (networkObjectInstance != null && !m_PrefabInstanceToPrefabAsset.ContainsKey(networkObjectInstance.GlobalObjectIdHash))
                {
                    m_PrefabInstanceToPrefabAsset.Add(networkObjectInstance.GlobalObjectIdHash, networkPrefabAssetHash);
                }

                return networkObjectInstance;
            }

            return null;
        }

        /// <summary>
        /// Will invoke the <see cref="INetworkPrefabInstanceHandler"/> implementation's Destroy method
        /// </summary>
        /// <param name="networkObjectInstance"></param>
        internal void HandleNetworkPrefabDestroy(NetworkObject networkObjectInstance)
        {
            var networkObjectInstanceHash = networkObjectInstance.GlobalObjectIdHash;

            // Do we have custom overrides registered?
            if (m_PrefabInstanceToPrefabAsset.TryGetValue(networkObjectInstanceHash, out var networkPrefabAssetHash))
            {
                if (m_PrefabAssetToPrefabHandler.TryGetValue(networkPrefabAssetHash, out var prefabInstanceHandler))
                {
                    prefabInstanceHandler.Destroy(networkObjectInstance);
                }
            }
            else // Otherwise the NetworkObject is the source NetworkPrefab
            if (m_PrefabAssetToPrefabHandler.TryGetValue(networkObjectInstanceHash, out var prefabInstanceHandler))
            {
                prefabInstanceHandler.Destroy(networkObjectInstance);
            }
        }

        /// <summary>
        /// Returns the <see cref="GameObject"/> to use as the override as could be defined within the NetworkPrefab list
        /// Note: This should be used to create <see cref="GameObject"/> pools (with <see cref="NetworkObject"/> components)
        /// under the scenario where you are using the Host model as it spawns everything locally. As such, the override
        /// will not be applied when spawning locally on a Host.
        /// Related Classes and Interfaces:
        /// <see cref="INetworkPrefabInstanceHandler"/>
        /// </summary>
        /// <param name="gameObject">the <see cref="GameObject"/> to be checked for a <see cref="NetworkManager"/> defined NetworkPrefab override</param>
        /// <returns>a <see cref="GameObject"/> that is either the override or if no overrides exist it returns the same as the one passed in as a parameter</returns>
        public GameObject GetNetworkPrefabOverride(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out var networkObject))
            {
                if (m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks.ContainsKey(networkObject.GlobalObjectIdHash))
                {
                    switch (m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].Override)
                    {
                        case NetworkPrefabOverride.Hash:
                        case NetworkPrefabOverride.Prefab:
                            {
                                var res = m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].OverridingTargetPrefab;
                                k_Log.Debug(() => $"NetworkPrefabHandler GetNetworkPrefabOverride [gameObject={gameObject.name}] [networkPrefab={networkObject.GlobalObjectIdHash}]" +
                                          $"[overrideType={m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].Override}]" +
                                          $"[overrideObj={m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[networkObject.GlobalObjectIdHash].OverridingTargetPrefab}]");

                                return res;
                            }
                    }
                }
            }

            return gameObject;
        }

        /// <summary>
        /// Adds a new prefab to the network prefab list.
        /// This can be any GameObject with a NetworkObject component, from any source (addressables, asset
        /// bundles, Resource.Load, dynamically created, etc)
        ///
        /// There are three limitations to this method:
        /// - If you have NetworkConfig.ForceSamePrefabs enabled, you can only do this before starting
        /// networking, and the server and all connected clients must all have the same exact set of prefabs
        /// added via this method before connecting
        /// - Adding a prefab on the server does not automatically add it on the client - it's up to you
        /// to make sure the client and server are synchronized via whatever method makes sense for your game
        /// (RPCs, configurations, deterministic loading, etc)
        /// - If the server sends a Spawn message to a client that has not yet added a prefab for, the spawn message
        /// and any other relevant messages will be held for a configurable time (default 1 second, configured via
        /// NetworkConfig.SpawnTimeout) before an error is logged. This is intended to enable the SDK to gracefully
        /// handle unexpected conditions (slow disks, slow network, etc) that slow down asset loading. This timeout
        /// should not be relied on and code shouldn't be written around it - your code should be written so that
        /// the asset is expected to be loaded before it's needed.
        /// </summary>
        /// <param name="prefab"></param>
        /// <exception cref="Exception"></exception>
        public void AddNetworkPrefab(GameObject prefab)
        {
            if (m_NetworkManager.IsListening && m_NetworkManager.NetworkConfig.ForceSamePrefabs)
            {
                throw new Exception($"All prefabs must be registered before starting {nameof(NetworkManager)} when {nameof(NetworkConfig.ForceSamePrefabs)} is enabled.");
            }

            var networkObject = prefab.GetComponent<NetworkObject>();
            if (!networkObject)
            {
                throw new Exception($"All {nameof(NetworkPrefab)}s must contain a {nameof(NetworkObject)} component.");
            }

            var networkPrefab = new NetworkPrefab { Prefab = prefab };
            bool added = m_NetworkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            k_Log.Debug(() => $"NetworkPrefabHandler AddNetworkPrefab prefab={prefab.name} hash={networkObject.GlobalObjectIdHash}");
            if (m_NetworkManager.IsListening && added)
            {
                m_NetworkManager.DeferredMessageManager.ProcessTriggers(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab, networkObject.GlobalObjectIdHash);
            }
        }

        public IReadOnlyList<NetworkPrefab> GetPrefabs()
        {
            return m_NetworkManager.NetworkConfig.Prefabs.Prefabs;
        }

        /// <summary>
        /// Remove a prefab from the prefab list.
        /// As with AddNetworkPrefab, this is specific to the client it's called on -
        /// calling it on the server does not automatically remove anything on any of the
        /// client processes.
        ///
        /// Like AddNetworkPrefab, when NetworkConfig.ForceSamePrefabs is enabled,
        /// this cannot be called after connecting.
        /// </summary>
        /// <param name="prefab"></param>
        public void RemoveNetworkPrefab(GameObject prefab)
        {
            if (m_NetworkManager.IsListening && m_NetworkManager.NetworkConfig.ForceSamePrefabs)
            {
                throw new Exception($"Prefabs cannot be removed after starting {nameof(NetworkManager)} when {nameof(NetworkConfig.ForceSamePrefabs)} is enabled.");
            }

            var globalObjectIdHash = prefab.GetComponent<NetworkObject>().GlobalObjectIdHash;
            m_NetworkManager.NetworkConfig.Prefabs.Remove(prefab);
            if (ContainsHandler(globalObjectIdHash))
            {
                RemoveHandler(globalObjectIdHash);
            }
        }

        /// <summary>
        /// If one exists, registers the player prefab
        /// </summary>
        internal void RegisterPlayerPrefab()
        {
            var networkConfig = m_NetworkManager.NetworkConfig;
            // If we have a player prefab, then we need to verify it is in the list of NetworkPrefabOverrideLinks for client side spawning.
            if (networkConfig.PlayerPrefab != null)
            {
                if (networkConfig.PlayerPrefab.TryGetComponent<NetworkObject>(out var playerPrefabNetworkObject))
                {
                    //In the event there is no NetworkPrefab entry (i.e. no override for default player prefab)
                    if (!networkConfig.Prefabs.NetworkPrefabOverrideLinks.ContainsKey(playerPrefabNetworkObject.GlobalObjectIdHash))
                    {
                        k_Log.Debug(() => $"[NetworkPrefabHandler] RegisterPlayerPrefab - PlayerPrefab={networkConfig.PlayerPrefab.name} hash={playerPrefabNetworkObject.GlobalObjectIdHash}");
                        //Then add a new entry for the player prefab
                        AddNetworkPrefab(networkConfig.PlayerPrefab);
                    }
                }
                else
                {
                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    Debug.LogError($"{nameof(NetworkConfig.PlayerPrefab)} (\"{networkConfig.PlayerPrefab.name}\") has no NetworkObject assigned to it!.");
                }
            }
        }

        internal void Initialize(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
        }
    }
}
