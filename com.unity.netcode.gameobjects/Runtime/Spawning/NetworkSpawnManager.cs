using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Class that handles object spawning
    /// </summary>
    public class NetworkSpawnManager
    {
        // Stores the objects that need to be shown at end-of-frame
        internal Dictionary<ulong, List<NetworkObject>> ObjectsToShowToClient = new Dictionary<ulong, List<NetworkObject>>();

        internal Dictionary<NetworkObject, List<ulong>> ClientsToShowObject = new Dictionary<NetworkObject, List<ulong>>();

        /// <summary>
        /// The currently spawned objects
        /// </summary>
        public readonly Dictionary<ulong, NetworkObject> SpawnedObjects = new Dictionary<ulong, NetworkObject>();

        /// <summary>
        /// A list of the spawned objects
        /// </summary>
        public readonly HashSet<NetworkObject> SpawnedObjectsList = new HashSet<NetworkObject>();

        /// <summary>
        /// Use to get all NetworkObjects owned by a client
        /// Ownership to Objects Table Format:
        /// [ClientId][NetworkObjectId][NetworkObject]
        /// Server: Keeps track of all clients' ownership
        /// Client: Keeps track of only its ownership
        /// </summary>
        public readonly Dictionary<ulong, Dictionary<ulong, NetworkObject>> OwnershipToObjectsTable = new Dictionary<ulong, Dictionary<ulong, NetworkObject>>();

        /// <summary>
        /// Object to Ownership Table:
        /// [NetworkObjectId][ClientId]
        /// Used internally to find the client Id that currently owns
        /// the NetworkObject
        /// </summary>
        private Dictionary<ulong, ulong> m_ObjectToOwnershipTable = new Dictionary<ulong, ulong>();

        /// <summary>
        /// In distributed authority mode, a list of known spawned player NetworkObject instance is maintained by each client.
        /// </summary>
        public IReadOnlyList<NetworkObject> PlayerObjects => m_PlayerObjects;
        // Since NetworkSpawnManager is destroyed when NetworkManager shuts down, it will always be an empty list for each new network session.
        // DANGO-TODO: We need to add something like a ConnectionStateMessage that is sent by either the DAHost or CMBService to each client when a client
        // is connected and synchronized or when a cient disconnects (but the player objects list we should keep as it is useful to have).
        private List<NetworkObject> m_PlayerObjects = new List<NetworkObject>();

        private Dictionary<ulong, List<NetworkObject>> m_PlayerObjectsTable = new Dictionary<ulong, List<NetworkObject>>();

        public List<ulong> GetConnectedPlayers()
        {
            return m_PlayerObjectsTable.Keys.ToList();
        }

        /// <summary>
        /// Adds a player object and updates all other players' observers list
        /// </summary>
        private void AddPlayerObject(NetworkObject playerObject)
        {
            if (!playerObject.IsPlayerObject)
            {
                if (NetworkManager.LogLevel == LogLevel.Normal)
                {
                    NetworkLog.LogError($"Attempting to register a {nameof(NetworkObject)} as a player object but {nameof(NetworkObject.IsPlayerObject)} is not set!");
                    return;
                }
            }

            foreach (var player in m_PlayerObjects)
            {
                // If the player's SpawnWithObservers is not set then do not add the new player object's owner as an observer.
                if (player.SpawnWithObservers)
                {
                    player.Observers.Add(playerObject.OwnerClientId);
                }

                // If the new player object's SpawnWithObservers is not set then do not add this player as an observer to the new player object.
                if (playerObject.SpawnWithObservers)
                {
                    playerObject.Observers.Add(player.OwnerClientId);
                }
            }

            m_PlayerObjects.Add(playerObject);
            if (!m_PlayerObjectsTable.ContainsKey(playerObject.OwnerClientId))
            {
                m_PlayerObjectsTable.Add(playerObject.OwnerClientId, new List<NetworkObject>());
            }
            m_PlayerObjectsTable[playerObject.OwnerClientId].Add(playerObject);
        }

        internal void UpdateNetworkClientPlayer(NetworkObject playerObject)
        {
            // If the player's client does not already have a NetworkClient entry
            if (!NetworkManager.ConnectionManager.ConnectedClients.ContainsKey(playerObject.OwnerClientId))
            {
                // Add the player's client
                NetworkManager.ConnectionManager.AddClient(playerObject.OwnerClientId);
            }
            var playerNetworkClient = NetworkManager.ConnectionManager.ConnectedClients[playerObject.OwnerClientId];

            // If a client changes their player object, then we should adjust for the client's new player
            if (playerNetworkClient.PlayerObject != null && m_PlayerObjects.Contains(playerNetworkClient.PlayerObject))
            {
                // Just remove the previous player object but keep the assigned observers of the NetworkObject
                RemovePlayerObject(playerNetworkClient.PlayerObject, true);
            }
            // Now update the associated NetworkClient's player object
            NetworkManager.ConnectionManager.ConnectedClients[playerObject.OwnerClientId].AssignPlayerObject(ref playerObject);
            AddPlayerObject(playerObject);
        }

        /// <summary>
        /// Removes a player object and updates all other players' observers list
        /// </summary>
        private void RemovePlayerObject(NetworkObject playerObject, bool keepObservers = false)
        {
            if (!playerObject.IsPlayerObject)
            {
                if (NetworkManager.LogLevel == LogLevel.Normal)
                {
                    NetworkLog.LogError($"Attempting to deregister a {nameof(NetworkObject)} as a player object but {nameof(NetworkObject.IsPlayerObject)} is not set!");
                    return;
                }
            }
            playerObject.IsPlayerObject = false;
            m_PlayerObjects.Remove(playerObject);
            if (m_PlayerObjectsTable.ContainsKey(playerObject.OwnerClientId))
            {
                m_PlayerObjectsTable[playerObject.OwnerClientId].Remove(playerObject);
                if (m_PlayerObjectsTable[playerObject.OwnerClientId].Count == 0)
                {
                    m_PlayerObjectsTable.Remove(playerObject.OwnerClientId);
                }
            }

            // If we want to keep the observers, then exit early
            if (keepObservers)
            {
                return;
            }

            foreach (var player in m_PlayerObjects)
            {
                player.Observers.Remove(playerObject.OwnerClientId);
            }
        }

        internal void MarkObjectForShowingTo(NetworkObject networkObject, ulong clientId)
        {
            if (!ObjectsToShowToClient.ContainsKey(clientId))
            {
                ObjectsToShowToClient.Add(clientId, new List<NetworkObject>());
            }
            ObjectsToShowToClient[clientId].Add(networkObject);
            if (NetworkManager.DistributedAuthorityMode)
            {
                if (!ClientsToShowObject.ContainsKey(networkObject))
                {
                    ClientsToShowObject.Add(networkObject, new List<ulong>());
                }
                ClientsToShowObject[networkObject].Add(clientId);
            }
        }

        // returns whether any matching objects would have become visible and were returned to hidden state
        internal bool RemoveObjectFromShowingTo(NetworkObject networkObject, ulong clientId)
        {
            if (NetworkManager.DistributedAuthorityMode)
            {
                if (ClientsToShowObject.ContainsKey(networkObject))
                {
                    ClientsToShowObject[networkObject].Remove(clientId);
                    if (ClientsToShowObject[networkObject].Count == 0)
                    {
                        ClientsToShowObject.Remove(networkObject);
                    }
                }
            }
            var ret = false;
            if (!ObjectsToShowToClient.ContainsKey(clientId))
            {
                return false;
            }

            // probably overkill, but deals with multiple entries
            while (ObjectsToShowToClient[clientId].Contains(networkObject))
            {
                Debug.LogWarning(
                    "Object was shown and hidden from the same client in the same Network frame. As a result, the client will _not_ receive a NetworkSpawn");
                ObjectsToShowToClient[clientId].Remove(networkObject);
                ret = true;
            }

            if (ret)
            {
                networkObject.Observers.Remove(clientId);
            }

            return ret;
        }

        /// <summary>
        /// Used to update a NetworkObject's ownership
        /// </summary>
        internal void UpdateOwnershipTable(NetworkObject networkObject, ulong newOwner, bool isRemoving = false)
        {
            var previousOwner = newOwner;

            // Use internal lookup table to see if the NetworkObject has a previous owner
            if (m_ObjectToOwnershipTable.ContainsKey(networkObject.NetworkObjectId))
            {
                // Keep track of the previous owner's ClientId
                previousOwner = m_ObjectToOwnershipTable[networkObject.NetworkObjectId];

                // We are either despawning (remove) or changing ownership (assign)
                if (isRemoving)
                {
                    m_ObjectToOwnershipTable.Remove(networkObject.NetworkObjectId);
                }
                else
                {
                    // If we already had this owner in our table then just exit
                    if (NetworkManager.DistributedAuthorityMode && previousOwner == newOwner)
                    {
                        return;
                    }
                    m_ObjectToOwnershipTable[networkObject.NetworkObjectId] = newOwner;
                }
            }
            else
            {
                // Otherwise, just add a new lookup entry
                m_ObjectToOwnershipTable.Add(networkObject.NetworkObjectId, newOwner);
            }

            // Check to see if we had a previous owner
            if (previousOwner != newOwner && OwnershipToObjectsTable.ContainsKey(previousOwner))
            {
                // Before updating the previous owner, assure this entry exists
                if (OwnershipToObjectsTable[previousOwner].ContainsKey(networkObject.NetworkObjectId))
                {
                    // Remove the previous owner's entry
                    OwnershipToObjectsTable[previousOwner].Remove(networkObject.NetworkObjectId);

                    // If we are removing the entry (i.e. despawning or client lost ownership)
                    if (isRemoving)
                    {
                        return;
                    }
                }
                else
                {
                    // Really, as long as UpdateOwnershipTable is invoked when ownership is gained or lost this should never happen
                    throw new Exception($"Client-ID {previousOwner} had a partial {nameof(m_ObjectToOwnershipTable)} entry! Potentially corrupted {nameof(OwnershipToObjectsTable)}?");
                }
            }

            // If the owner doesn't have an entry then create one
            if (!OwnershipToObjectsTable.ContainsKey(newOwner))
            {
                OwnershipToObjectsTable.Add(newOwner, new Dictionary<ulong, NetworkObject>());
            }

            // Sanity check to make sure we don't already have this entry (we shouldn't)
            if (!OwnershipToObjectsTable[newOwner].ContainsKey(networkObject.NetworkObjectId))
            {
                // Add the new ownership entry
                OwnershipToObjectsTable[newOwner].Add(networkObject.NetworkObjectId, networkObject);
            }
            else if (isRemoving)
            {
                OwnershipToObjectsTable[previousOwner].Remove(networkObject.NetworkObjectId);
            }
            else if (NetworkManager.LogLevel == LogLevel.Developer && previousOwner == newOwner)
            {
                NetworkLog.LogWarning($"Setting ownership twice? Client-ID {previousOwner} already owns NetworkObject ID {networkObject.NetworkObjectId}!");
            }
        }

        /// <summary>
        /// Returns an array of all NetworkObjects that belong to a client.
        /// </summary>
        /// <param name="clientId">the client's id <see cref="NetworkManager.LocalClientId"/></param>
        /// <returns>returns an array of the <see cref="NetworkObject"/>s owned by the client</returns>
        public NetworkObject[] GetClientOwnedObjects(ulong clientId)
        {
            if (!OwnershipToObjectsTable.ContainsKey(clientId))
            {
                OwnershipToObjectsTable.Add(clientId, new Dictionary<ulong, NetworkObject>());
            }
            return OwnershipToObjectsTable[clientId].Values.ToArray();
        }

        /// <summary>
        /// Gets the NetworkManager associated with this SpawnManager.
        /// </summary>
        public NetworkManager NetworkManager { get; }

        internal readonly Queue<ReleasedNetworkId> ReleasedNetworkObjectIds = new Queue<ReleasedNetworkId>();
        private ulong m_NetworkObjectIdCounter;

        // A list of target ClientId, use when sending despawn commands. Kept as a member to reduce memory allocations
        private List<ulong> m_TargetClientIds = new List<ulong>();

        internal ulong GetNetworkObjectId()
        {
            if (ReleasedNetworkObjectIds.Count > 0 && NetworkManager.NetworkConfig.RecycleNetworkIds && (NetworkManager.RealTimeProvider.UnscaledTime - ReleasedNetworkObjectIds.Peek().ReleaseTime) >= NetworkManager.NetworkConfig.NetworkIdRecycleDelay)
            {
                return ReleasedNetworkObjectIds.Dequeue().NetworkId;
            }

            m_NetworkObjectIdCounter++;

            // DANGO-TODO: Need a more robust solution here.
            return m_NetworkObjectIdCounter + (NetworkManager.LocalClientId * 10000);
        }

        /// <summary>
        /// Returns the local player object or null if one does not exist
        /// </summary>
        /// <returns>The local player object or null if one does not exist</returns>
        public NetworkObject GetLocalPlayerObject()
        {
            return GetPlayerNetworkObject(NetworkManager.LocalClientId);
        }

        /// <summary>
        /// Returns all <see cref="NetworkObject"/> instances assigned to the client identifier
        /// </summary>
        /// <param name="clientId">the client identifier of the player</param>
        /// <returns>A list of <see cref="NetworkObject"/> instances (if more than one are assigned)</returns>
        public List<NetworkObject> GetPlayerNetworkObjects(ulong clientId)
        {
            if (m_PlayerObjectsTable.ContainsKey(clientId))
            {
                return m_PlayerObjectsTable[clientId];
            }
            return null;
        }

        /// <summary>
        /// Returns the player object with a given clientId or null if one does not exist. This is only valid server side.
        /// </summary>
        /// <param name="clientId">the client identifier of the player</param>
        /// <returns>The player object with a given clientId or null if one does not exist</returns>
        public NetworkObject GetPlayerNetworkObject(ulong clientId)
        {
            if (!NetworkManager.DistributedAuthorityMode)
            {
                if (!NetworkManager.IsServer && NetworkManager.LocalClientId != clientId)
                {
                    throw new NotServerException("Only the server can find player objects from other clients.");
                }
                if (TryGetNetworkClient(clientId, out NetworkClient networkClient))
                {
                    return networkClient.PlayerObject;
                }
            }
            else
            {
                if (m_PlayerObjectsTable.ContainsKey(clientId))
                {
                    return m_PlayerObjectsTable[clientId].First();
                }
            }

            return null;
        }

        /// <summary>
        /// Helper function to get a network client for a clientId from the NetworkManager.
        /// On the server this will check the <see cref="NetworkManager.ConnectedClients"/> list.
        /// On a non-server this will check the <see cref="NetworkManager.LocalClient"/> only.
        /// </summary>
        /// <param name="clientId">The clientId for which to try getting the NetworkClient for.</param>
        /// <param name="networkClient">The found NetworkClient. Null if no client was found.</param>
        /// <returns>True if a NetworkClient with a matching id was found else false.</returns>
        private bool TryGetNetworkClient(ulong clientId, out NetworkClient networkClient)
        {
            if (NetworkManager.IsServer)
            {
                return NetworkManager.ConnectedClients.TryGetValue(clientId, out networkClient);
            }

            if (NetworkManager.LocalClient != null && clientId == NetworkManager.LocalClient.ClientId)
            {
                networkClient = NetworkManager.LocalClient;
                return true;
            }

            networkClient = null;
            return false;
        }

        protected virtual void InternalOnOwnershipChanged(ulong perviousOwner, ulong newOwner)
        {

        }

        internal void RemoveOwnership(NetworkObject networkObject)
        {
            if (NetworkManager.DistributedAuthorityMode && !NetworkManager.ShutdownInProgress)
            {
                if (networkObject.IsOwnershipDistributable || networkObject.IsOwnershipTransferable)
                {
                    if (networkObject.IsOwner || NetworkManager.DAHost)
                    {
                        NetworkLog.LogWarning("DANGO-TODO: Determine if removing ownership should make the CMB Service redistribute ownership or if this just isn't a valid thing in DAMode.");
                        return;
                    }
                    else
                    {
                        NetworkLog.LogError($"Only the owner is allowed to remove ownership in distributed authority mode!");
                        return;
                    }
                }
                else
                {
                    if (!NetworkManager.DAHost)
                    {
                        Debug.LogError($"Only {nameof(NetworkObject)}s with {nameof(NetworkObject.IsOwnershipDistributable)} or {nameof(NetworkObject.IsOwnershipTransferable)} set can perform ownership changes!");
                    }
                    return;
                }
            }
            ChangeOwnership(networkObject, NetworkManager.ServerClientId, true);
        }

        private Dictionary<ulong, float> m_LastChangeInOwnership = new Dictionary<ulong, float>();
        private const int k_MaximumTickOwnershipChangeMultiplier = 6;

        internal void ChangeOwnership(NetworkObject networkObject, ulong clientId, bool isAuthorized, bool isRequestApproval = false)
        {
            // For client-server:
            // If ownership changes faster than the latency between the client-server and there are NetworkVariables being updated during ownership changes,
            // then notify the user they could potentially lose state updates if developer logging is enabled.
            if (!NetworkManager.DistributedAuthorityMode && m_LastChangeInOwnership.ContainsKey(networkObject.NetworkObjectId) && m_LastChangeInOwnership[networkObject.NetworkObjectId] > Time.realtimeSinceStartup)
            {
                var hasNetworkVariables = false;
                for (int i = 0; i < networkObject.ChildNetworkBehaviours.Count; i++)
                {
                    hasNetworkVariables = networkObject.ChildNetworkBehaviours[i].NetworkVariableFields.Count > 0;
                    if (hasNetworkVariables)
                    {
                        break;
                    }
                }
                if (hasNetworkVariables && NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarningServer($"[Rapid Ownership Change Detected][Potential Loss in State] Detected a rapid change in ownership that exceeds a frequency less than {k_MaximumTickOwnershipChangeMultiplier}x the current network tick rate! Provide at least {k_MaximumTickOwnershipChangeMultiplier}x the current network tick rate between ownership changes to avoid NetworkVariable state loss.");
                }
            }

            if (NetworkManager.DistributedAuthorityMode)
            {
                // If are not authorized and this is not an approved ownership change, then check to see if we can change ownership
                if (!isAuthorized && !isRequestApproval)
                {
                    if (networkObject.IsOwnershipLocked)
                    {
                        if (NetworkManager.LogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogErrorServer($"[{networkObject.name}][Locked] You cannot change ownership while a {nameof(NetworkObject)} is locked!");
                        }
                        networkObject.OnOwnershipPermissionsFailure?.Invoke(NetworkObject.OwnershipPermissionsFailureStatus.Locked);
                        return;
                    }
                    if (networkObject.IsRequestInProgress)
                    {
                        if (NetworkManager.LogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogErrorServer($"[{networkObject.name}][Request Pending] You cannot change ownership while a {nameof(NetworkObject)} has a pending ownership request!");
                        }
                        networkObject.OnOwnershipPermissionsFailure?.Invoke(NetworkObject.OwnershipPermissionsFailureStatus.RequestInProgress);
                        return;
                    }
                    if (networkObject.IsOwnershipRequestRequired)
                    {
                        if (NetworkManager.LogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogErrorServer($"[{networkObject.name}][Request Required] You cannot change ownership directly if a {nameof(NetworkObject)} has the {NetworkObject.OwnershipStatus.RequestRequired} flag set!");
                        }
                        networkObject.OnOwnershipPermissionsFailure?.Invoke(NetworkObject.OwnershipPermissionsFailureStatus.RequestRequired);
                        return;
                    }
                    if (!networkObject.IsOwnershipTransferable)
                    {
                        if (NetworkManager.LogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogErrorServer($"[{networkObject.name}][Not transferrable] You cannot change ownership of a {nameof(NetworkObject)} that does not have the {NetworkObject.OwnershipStatus.Transferable} flag set!");
                        }
                        networkObject.OnOwnershipPermissionsFailure?.Invoke(NetworkObject.OwnershipPermissionsFailureStatus.NotTransferrable);
                        return;
                    }
                }
            }
            else if (!isAuthorized)
            {
                throw new NotServerException("Only the server can change ownership");
            }

            if (!networkObject.IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }


            if (networkObject.OwnerClientId == clientId && networkObject.PreviousOwnerId == clientId)
            {
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarningServer($"[Already Owner] Unnecessary ownership change for {networkObject.name} as it is already the owned by client-{clientId}");
                }
                return;
            }

            // Used to distinguish whether a new owner should receive any currently dirty NetworkVariable updates
            networkObject.PreviousOwnerId = networkObject.OwnerClientId;

            // Assign the new owner
            networkObject.OwnerClientId = clientId;

            // Always notify locally on the server when ownership is lost
            networkObject.InvokeBehaviourOnLostOwnership();

            // Authority adds entries for all client ownership
            UpdateOwnershipTable(networkObject, networkObject.OwnerClientId);

            // Always notify locally on the server when a new owner is assigned
            networkObject.InvokeBehaviourOnGainedOwnership();

            if (networkObject.PreviousOwnerId == NetworkManager.LocalClientId)
            {
                // Mark any owner read variables as dirty
                networkObject.MarkOwnerReadVariablesDirty();
                // Immediately queue any pending deltas and order the message before the
                // change in ownership message.
                NetworkManager.BehaviourUpdater.NetworkBehaviourUpdate(true);
            }

            var size = 0;
            if (NetworkManager.DistributedAuthorityMode)
            {
                var message = new ChangeOwnershipMessage
                {
                    NetworkObjectId = networkObject.NetworkObjectId,
                    OwnerClientId = networkObject.OwnerClientId,
                    DistributedAuthorityMode = NetworkManager.DistributedAuthorityMode,
                    RequestApproved = isRequestApproval,
                    OwnershipIsChanging = true,
                    RequestClientId = networkObject.PreviousOwnerId,
                    OwnershipFlags = (ushort)networkObject.Ownership,
                };
                // If we are connected to the CMB service or not the DAHost (i.e. pure DA-Clients only)
                if (NetworkManager.CMBServiceConnection || !NetworkManager.DAHost)
                {
                    // Always update the network properties in distributed authority mode for the client gaining ownership
                    for (int i = 0; i < networkObject.ChildNetworkBehaviours.Count; i++)
                    {
                        networkObject.ChildNetworkBehaviours[i].UpdateNetworkProperties();
                    }

                    size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, NetworkManager.ServerClientId);
                    NetworkManager.NetworkMetrics.TrackOwnershipChangeSent(NetworkManager.LocalClientId, networkObject, size);
                }
                else // We are the DAHost so broadcast the ownership change
                {
                    foreach (var client in NetworkManager.ConnectedClients)
                    {
                        if (client.Value.ClientId == NetworkManager.ServerClientId)
                        {
                            continue;
                        }
                        if (networkObject.IsNetworkVisibleTo(client.Value.ClientId))
                        {
                            size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, client.Value.ClientId);
                            NetworkManager.NetworkMetrics.TrackOwnershipChangeSent(client.Key, networkObject, size);
                        }
                    }
                }
            }
            else // Normal Client-Server mode
            {
                var message = new ChangeOwnershipMessage
                {
                    NetworkObjectId = networkObject.NetworkObjectId,
                    OwnerClientId = networkObject.OwnerClientId,
                };
                foreach (var client in NetworkManager.ConnectedClients)
                {
                    if (networkObject.IsNetworkVisibleTo(client.Value.ClientId))
                    {
                        size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, client.Value.ClientId);
                        NetworkManager.NetworkMetrics.TrackOwnershipChangeSent(client.Key, networkObject, size);
                    }
                }
            }

            // After we have sent the change ownership message to all client observers, invoke the ownership changed notification.
            /// !!Important!!
            /// This gets called specifically *after* sending the ownership message so any additional messages that need to proceed an ownership
            /// change can be sent from NetworkBehaviours that override the <see cref="NetworkBehaviour.OnOwnershipChanged"></see>
            networkObject.InvokeOwnershipChanged(networkObject.PreviousOwnerId, clientId);

            // Keep track of the ownership change frequency to assure a user is not exceeding changes faster than 2x the current Tick Rate.
            if (!NetworkManager.DistributedAuthorityMode)
            {
                if (!m_LastChangeInOwnership.ContainsKey(networkObject.NetworkObjectId))
                {
                    m_LastChangeInOwnership.Add(networkObject.NetworkObjectId, 0.0f);
                }
                var tickFrequency = 1.0f / NetworkManager.NetworkConfig.TickRate;
                m_LastChangeInOwnership[networkObject.NetworkObjectId] = Time.realtimeSinceStartup + (tickFrequency * k_MaximumTickOwnershipChangeMultiplier);
            }
        }

        internal bool HasPrefab(NetworkObject.SceneObject sceneObject)
        {
            if (!NetworkManager.NetworkConfig.EnableSceneManagement || !sceneObject.IsSceneObject)
            {
                if (NetworkManager.PrefabHandler.ContainsHandler(sceneObject.Hash))
                {
                    return true;
                }
                if (NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks.TryGetValue(sceneObject.Hash, out var networkPrefab))
                {
                    switch (networkPrefab.Override)
                    {
                        default:
                        case NetworkPrefabOverride.None:
                            return networkPrefab.Prefab != null;
                        case NetworkPrefabOverride.Hash:
                        case NetworkPrefabOverride.Prefab:
                            return networkPrefab.OverridingTargetPrefab != null;
                    }
                }

                return false;
            }
            var networkObject = NetworkManager.SceneManager.GetSceneRelativeInSceneNetworkObject(sceneObject.Hash, sceneObject.NetworkSceneHandle);
            return networkObject != null;
        }

        internal enum InstantiateAndSpawnErrorTypes
        {
            NetworkPrefabNull,
            NotAuthority,
            InvokedWhenShuttingDown,
            NotRegisteredNetworkPrefab,
            NetworkManagerNull,
            NoActiveSession,
        }

        internal static readonly Dictionary<InstantiateAndSpawnErrorTypes, string> InstantiateAndSpawnErrors = new Dictionary<InstantiateAndSpawnErrorTypes, string>(
            new KeyValuePair<InstantiateAndSpawnErrorTypes, string>[]{
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.NetworkPrefabNull, $"The {nameof(NetworkObject)} prefab parameter was null!"),
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.NotAuthority, $"Only the server has authority to {nameof(InstantiateAndSpawn)}!"),
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.InvokedWhenShuttingDown, $"Invoking {nameof(InstantiateAndSpawn)} while shutting down! Calls to {nameof(InstantiateAndSpawn)} will be ignored."),
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.NotRegisteredNetworkPrefab, $"The {nameof(NetworkObject)} parameter is not a registered network prefab. Did you forget to register it or are you trying to instantiate and spawn an instance of a network prefab?"),
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.NetworkManagerNull, $"The {nameof(NetworkManager)} parameter was null!"),
                new KeyValuePair<InstantiateAndSpawnErrorTypes, string>(InstantiateAndSpawnErrorTypes.NoActiveSession, "You can only invoke this method when you are connected to an existing/in-progress network session!")
            });

        /// <summary>
        /// Use this method to easily instantiate and spawn an instance of a network prefab.
        /// InstantiateAndSpawn will:
        /// - Find any override associated with the <see cref="NetworkObject"/> prefab
        ///   - If there is no override, then the current <see cref="NetworkObject"/> prefab type is used.
        /// - Create an instance of the <see cref="NetworkObject"/> prefab (or its override).
        /// - Spawn the <see cref="NetworkObject"/> prefab instance
        /// </summary>
        /// <param name="networkPrefab">The <see cref="NetworkObject"/> of the pefab asset.</param>
        /// <param name="ownerClientId">The owner of the <see cref="NetworkObject"/> instance (defaults to server).</param>
        /// <param name="destroyWithScene">Whether the <see cref="NetworkObject"/> instance will be destroyed when the scene it is located within is unloaded (default is false).</param>
        /// <param name="isPlayerObject">Whether the <see cref="NetworkObject"/> instance is a player object or not (default is false).</param>
        /// <param name="forceOverride">Whether you want to force spawning the override when running as a host or server or if you want it to spawn the override for host mode and
        /// the source prefab for server. If there is an override, clients always spawn that as opposed to the source prefab (defaults to false).  </param>
        /// <param name="position">The starting poisiton of the <see cref="NetworkObject"/> instance.</param>
        /// <param name="rotation">The starting rotation of the <see cref="NetworkObject"/> instance.</param>
        /// <returns>The newly instantiated and spawned <see cref="NetworkObject"/> prefab instance.</returns>
        public NetworkObject InstantiateAndSpawn(NetworkObject networkPrefab, ulong ownerClientId = NetworkManager.ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)
        {
            if (networkPrefab == null)
            {
                Debug.LogError(InstantiateAndSpawnErrors[InstantiateAndSpawnErrorTypes.NetworkPrefabNull]);
                return null;
            }

            ownerClientId = NetworkManager.DistributedAuthorityMode ? NetworkManager.LocalClientId : ownerClientId;
            // We only need to check for authority when running in client-server mode
            if (!NetworkManager.IsServer && !NetworkManager.DistributedAuthorityMode)
            {
                Debug.LogError(InstantiateAndSpawnErrors[InstantiateAndSpawnErrorTypes.NotAuthority]);
                return null;
            }

            if (NetworkManager.ShutdownInProgress)
            {
                Debug.LogWarning(InstantiateAndSpawnErrors[InstantiateAndSpawnErrorTypes.InvokedWhenShuttingDown]);
                return null;
            }

            // Verify it is actually a valid prefab
            if (!NetworkManager.NetworkConfig.Prefabs.Contains(networkPrefab.gameObject))
            {
                Debug.LogError(InstantiateAndSpawnErrors[InstantiateAndSpawnErrorTypes.NotRegisteredNetworkPrefab]);
                return null;
            }

            return InstantiateAndSpawnNoParameterChecks(networkPrefab, ownerClientId, destroyWithScene, isPlayerObject, forceOverride, position, rotation);
        }

        /// <summary>
        /// !!! Does not perform any parameter checks prior to attempting to instantiate and spawn the NetworkObject !!!
        /// </summary>
        internal NetworkObject InstantiateAndSpawnNoParameterChecks(NetworkObject networkPrefab, ulong ownerClientId = NetworkManager.ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)
        {
            var networkObject = networkPrefab;
            // Host spawns the ovveride and server spawns the original prefab unless forceOverride is set to true where both server or host will spawn the override.
            // In distributed authority mode, we alaways get the override
            if (forceOverride || NetworkManager.IsHost || NetworkManager.DistributedAuthorityMode)
            {
                networkObject = GetNetworkObjectToSpawn(networkPrefab.GlobalObjectIdHash, ownerClientId, position, rotation);
            }
            if (networkObject == null)
            {
                Debug.LogError($"Failed to instantiate and spawn {networkPrefab.name}!");
                return null;
            }
            networkObject.IsPlayerObject = isPlayerObject;
            networkObject.transform.position = position;
            networkObject.transform.rotation = rotation;
            networkObject.SpawnWithOwnership(ownerClientId, destroyWithScene);
            return networkObject;
        }

        /// <summary>
        /// Gets the right NetworkObject prefab instance to spawn. If a handler is registered or there is an override assigned to the 
        /// passed in globalObjectIdHash value, then that is what will be instantiated, spawned, and returned.
        /// </summary>
        internal NetworkObject GetNetworkObjectToSpawn(uint globalObjectIdHash, ulong ownerId, Vector3? position, Quaternion? rotation, bool isScenePlaced = false)
        {
            NetworkObject networkObject = null;
            // If the prefab hash has a registered INetworkPrefabInstanceHandler derived class
            if (NetworkManager.PrefabHandler.ContainsHandler(globalObjectIdHash))
            {
                // Let the handler spawn the NetworkObject
                networkObject = NetworkManager.PrefabHandler.HandleNetworkPrefabSpawn(globalObjectIdHash, ownerId, position ?? default, rotation ?? default);
                networkObject.NetworkManagerOwner = NetworkManager;
            }
            else
            {
                // See if there is a valid registered NetworkPrefabOverrideLink associated with the provided prefabHash
                var networkPrefabReference = (GameObject)null;
                var inScenePlacedWithNoSceneManagement = !NetworkManager.NetworkConfig.EnableSceneManagement && isScenePlaced;

                if (NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks.ContainsKey(globalObjectIdHash))
                {
                    var networkPrefab = NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[globalObjectIdHash];

                    switch (networkPrefab.Override)
                    {
                        default:
                        case NetworkPrefabOverride.None:
                            networkPrefabReference = networkPrefab.Prefab;
                            break;
                        case NetworkPrefabOverride.Hash:
                        case NetworkPrefabOverride.Prefab:
                            {
                                // When scene management is disabled and this is an in-scene placed NetworkObject, we want to always use the 
                                // SourcePrefabToOverride and not any possible prefab override as a user might want to spawn overrides dynamically 
                                // but might want to use the same source network prefab as an in-scene placed NetworkObject.
                                // (When scene management is enabled, clients don't delete their in-scene placed NetworkObjects prior to dynamically
                                // spawning them so the original prefab placed is preserved and this is not needed)
                                if (inScenePlacedWithNoSceneManagement)
                                {
                                    networkPrefabReference = networkPrefab.SourcePrefabToOverride ? networkPrefab.SourcePrefabToOverride : networkPrefab.Prefab;
                                }
                                else
                                {
                                    networkPrefabReference = NetworkManager.NetworkConfig.Prefabs.NetworkPrefabOverrideLinks[globalObjectIdHash].OverridingTargetPrefab;
                                }
                                break;
                            }
                    }
                }

                // If not, then there is an issue (user possibly didn't register the prefab properly?)
                if (networkPrefabReference == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogError($"Failed to create object locally. [{nameof(globalObjectIdHash)}={globalObjectIdHash}]. {nameof(NetworkPrefab)} could not be found. Is the prefab registered with {nameof(NetworkManager)}?");
                    }
                }
                else
                {
                    // Create prefab instance while applying any pre-assigned position and rotation values
                    networkObject = UnityEngine.Object.Instantiate(networkPrefabReference).GetComponent<NetworkObject>();
                    networkObject.transform.position = position ?? networkObject.transform.position;
                    networkObject.transform.rotation = rotation ?? networkObject.transform.rotation;
                    networkObject.NetworkManagerOwner = NetworkManager;
                    networkObject.PrefabGlobalObjectIdHash = globalObjectIdHash;
                }
            }
            return networkObject;
        }

        /// <summary>
        /// Creates a local NetowrkObject to be spawned.
        /// </summary>
        /// <remarks>
        /// For most cases this is client-side only, with the exception of when the server
        /// is spawning a player.
        /// </remarks>
        internal NetworkObject CreateLocalNetworkObject(NetworkObject.SceneObject sceneObject)
        {
            NetworkObject networkObject = null;
            var globalObjectIdHash = sceneObject.Hash;
            var position = sceneObject.HasTransform ? sceneObject.Transform.Position : default;
            var rotation = sceneObject.HasTransform ? sceneObject.Transform.Rotation : default;
            var scale = sceneObject.HasTransform ? sceneObject.Transform.Scale : default;
            var parentNetworkId = sceneObject.HasParent ? sceneObject.ParentObjectId : default;
            var worldPositionStays = (!sceneObject.HasParent) || sceneObject.WorldPositionStays;
            var isSpawnedByPrefabHandler = false;

            // If scene management is disabled or the NetworkObject was dynamically spawned
            if (!NetworkManager.NetworkConfig.EnableSceneManagement || !sceneObject.IsSceneObject)
            {
                networkObject = GetNetworkObjectToSpawn(sceneObject.Hash, sceneObject.OwnerClientId, position, rotation, sceneObject.IsSceneObject);
            }
            else // Get the in-scene placed NetworkObject
            {
                networkObject = NetworkManager.SceneManager.GetSceneRelativeInSceneNetworkObject(globalObjectIdHash, sceneObject.NetworkSceneHandle);
                if (networkObject == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogError($"{nameof(NetworkPrefab)} hash was not found! In-Scene placed {nameof(NetworkObject)} soft synchronization failure for Hash: {globalObjectIdHash}!");
                    }
                }

                // Since this NetworkObject is an in-scene placed NetworkObject, if it is disabled then enable it so
                // NetworkBehaviours will have their OnNetworkSpawn method invoked
                if (networkObject != null && !networkObject.gameObject.activeInHierarchy)
                {
                    networkObject.gameObject.SetActive(true);
                }
            }

            if (networkObject != null)
            {
                networkObject.DestroyWithScene = sceneObject.DestroyWithScene;
                networkObject.NetworkSceneHandle = sceneObject.NetworkSceneHandle;
                networkObject.DontDestroyWithOwner = sceneObject.DontDestroyWithOwner;
                networkObject.Ownership = (NetworkObject.OwnershipStatus)sceneObject.OwnershipFlags;


                var nonNetworkObjectParent = false;
                // SPECIAL CASE FOR IN-SCENE PLACED:  (only when the parent has a NetworkObject)
                // This is a special case scenario where a late joining client has joined and loaded one or
                // more scenes that contain nested in-scene placed NetworkObject children yet the server's
                // synchronization information does not indicate the NetworkObject in question has a parent.
                // Under this scenario, we want to remove the parent before spawning and setting the transform values.
                if (sceneObject.IsSceneObject && networkObject.transform.parent != null)
                {
                    var parentNetworkObject = networkObject.transform.parent.GetComponent<NetworkObject>();
                    // if the in-scene placed NetworkObject has a parent NetworkObject but the synchronization information does not
                    // include parenting, then we need to force the removal of that parent
                    if (!sceneObject.HasParent && parentNetworkObject)
                    {
                        // remove the parent
                        networkObject.ApplyNetworkParenting(true, true);
                    }
                    else if (sceneObject.HasParent && !parentNetworkObject)
                    {
                        nonNetworkObjectParent = true;
                    }
                }

                // Set the transform unless we were spawned by a prefab handler
                // Note: prefab handlers are provided the position and rotation
                // but it is up to the user to set those values
                if (sceneObject.HasTransform && !isSpawnedByPrefabHandler)
                {
                    // If world position stays is true or we have auto object parent synchronization disabled
                    // then we want to apply the position and rotation values world space relative
                    if ((worldPositionStays && !nonNetworkObjectParent) || !networkObject.AutoObjectParentSync)
                    {
                        networkObject.transform.position = position;
                        networkObject.transform.rotation = rotation;
                    }
                    else
                    {
                        networkObject.transform.localPosition = position;
                        networkObject.transform.localRotation = rotation;
                    }

                    // SPECIAL CASE:
                    // Since players are created uniquely we don't apply scale because
                    // the ConnectionApprovalResponse does not currently provide the
                    // ability to specify scale. So, we just use the default scale of
                    // the network prefab used to represent the player.
                    // Note: not doing this would set the player's scale to zero since
                    // that is the default value of Vector3.
                    if (!sceneObject.IsPlayerObject)
                    {
                        // Since scale is always applied to local space scale, we do the transform
                        // space logic during serialization such that it works out whether AutoObjectParentSync
                        // is enabled or not (see NetworkObject.SceneObject)
                        networkObject.transform.localScale = scale;
                    }
                }

                if (sceneObject.HasParent)
                {
                    // Go ahead and set network parenting properties, if the latest parent is not set then pass in null
                    // (we always want to set worldPositionStays)
                    ulong? parentId = null;
                    if (sceneObject.IsLatestParentSet)
                    {
                        parentId = parentNetworkId;
                    }
                    networkObject.SetNetworkParenting(parentId, worldPositionStays);
                }


                // Dynamically spawned NetworkObjects that occur during a LoadSceneMode.Single load scene event are migrated into the DDOL
                // until the scene is loaded. They are then migrated back into the newly loaded and currently active scene.
                if (!sceneObject.IsSceneObject && NetworkSceneManager.IsSpawnedObjectsPendingInDontDestroyOnLoad)
                {
                    UnityEngine.Object.DontDestroyOnLoad(networkObject.gameObject);
                }
            }
            return networkObject;
        }

        /// <summary>
        /// Invoked from:
        /// - ConnectionManager after instantiating a player prefab when running in client-server.
        /// - NetworkObject when spawning a newly instantiated NetworkObject for the first time.
        /// - NetworkSceneManager after a server/session-owner has loaded a scene to locally spawn the newly instantiated in-scene placed NetworkObjects.
        /// - NetworkSpawnManager when spawning any already loaded in-scene placed NetworkObjects (client-server or session owner).
        /// 
        /// Client-Server:
        /// Server is the only instance that invokes this method.
        ///
        /// Distributed Authority:
        /// DAHost client and standard DA clients invoke this method.
        /// </summary>
        internal void SpawnNetworkObjectLocally(NetworkObject networkObject, ulong networkId, bool sceneObject, bool playerObject, ulong ownerClientId, bool destroyWithScene)
        {
            if (networkObject == null)
            {
                throw new ArgumentNullException(nameof(networkObject), "Cannot spawn null object");
            }

            if (networkObject.IsSpawned)
            {
                Debug.LogError($"{networkObject.name} is already spawned!");
                return;
            }

            if (!sceneObject)
            {
                var networkObjectChildren = networkObject.GetComponentsInChildren<NetworkObject>();
                if (networkObjectChildren.Length > 1)
                {
                    Debug.LogError("Spawning NetworkObjects with nested NetworkObjects is only supported for scene objects. Child NetworkObjects will not be spawned over the network!");
                }
            }
            // Invoke NetworkBehaviour.OnPreSpawn methods
            networkObject.InvokeBehaviourNetworkPreSpawn();

            // DANGO-TODO: It would be nice to allow users to specify which clients are observers prior to spawning
            // For now, this is the best place I could find to add all connected clients as observers for newly
            // instantiated and spawned NetworkObjects on the authoritative side.
            if (NetworkManager.DistributedAuthorityMode)
            {
                if (NetworkManager.NetworkConfig.EnableSceneManagement && sceneObject)
                {
                    networkObject.SceneOriginHandle = networkObject.gameObject.scene.handle;
                    networkObject.NetworkSceneHandle = NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle[networkObject.gameObject.scene.handle];
                }

                // Always add the owner/authority even if SpawnWithObservers is false
                // (authority should not take into consideration networkObject.CheckObjectVisibility when SpawnWithObservers is false)
                if (!networkObject.SpawnWithObservers)
                {
                    networkObject.Observers.Add(ownerClientId);
                }
                else
                {
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        // If SpawnWithObservers is enabled, then authority does take networkObject.CheckObjectVisibility into consideration
                        if (networkObject.CheckObjectVisibility != null && !networkObject.CheckObjectVisibility.Invoke(clientId))
                        {
                            continue;
                        }
                        networkObject.Observers.Add(clientId);
                    }

                    // Sanity check to make sure the owner is always included
                    // Itentionally checking as opposed to just assigning in order to generate notification.
                    if (!networkObject.Observers.Contains(ownerClientId))
                    {
                        Debug.LogError($"Client-{ownerClientId} is the owner of {networkObject.name} but is not an observer! Adding owner, but there is a bug in observer synchronization!");
                        networkObject.Observers.Add(ownerClientId);
                    }
                }
            }
            SpawnNetworkObjectLocallyCommon(networkObject, networkId, sceneObject, playerObject, ownerClientId, destroyWithScene);

            // Invoke NetworkBehaviour.OnPostSpawn methods
            networkObject.InvokeBehaviourNetworkPostSpawn();
        }

        /// <summary>
        /// This is only invoked to instantiate a serialized NetworkObject via
        /// <see cref="NetworkObject.AddSceneObject(in NetworkObject.SceneObject, FastBufferReader, NetworkManager, bool)"/>
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Pre spawn methods need to be invoked from within <see cref="NetworkObject.AddSceneObject"/>.
        /// </remarks>
        internal void SpawnNetworkObjectLocally(NetworkObject networkObject, in NetworkObject.SceneObject sceneObject, bool destroyWithScene)
        {
            if (networkObject == null)
            {
                throw new ArgumentNullException(nameof(networkObject), "Cannot spawn null object");
            }

            if (networkObject.IsSpawned)
            {
                throw new SpawnStateException($"[{networkObject.name}] Object-{networkObject.NetworkObjectId} is already spawned!");
            }

            // Do not invoke Pre spawn here (SynchronizeNetworkBehaviours needs to be invoked prior to this)
            SpawnNetworkObjectLocallyCommon(networkObject, sceneObject.NetworkObjectId, sceneObject.IsSceneObject, sceneObject.IsPlayerObject, sceneObject.OwnerClientId, destroyWithScene);

            // It is ok to invoke NetworkBehaviour.OnPostSpawn methods
            networkObject.InvokeBehaviourNetworkPostSpawn();
        }

        private void SpawnNetworkObjectLocallyCommon(NetworkObject networkObject, ulong networkId, bool sceneObject, bool playerObject, ulong ownerClientId, bool destroyWithScene)
        {
            if (SpawnedObjects.ContainsKey(networkId))
            {
                Debug.LogWarning($"[{NetworkManager.name}] Trying to spawn {networkObject.name} with a {nameof(NetworkObject.NetworkObjectId)} of {networkId} but it is already in the spawned list!");
                return;
            }

            networkObject.IsSpawned = true;
            networkObject.IsSceneObject = sceneObject;

            // Always check to make sure our scene of origin is properly set for in-scene placed NetworkObjects
            // Note: Always check SceneOriginHandle directly at this specific location.
            if (networkObject.IsSceneObject != false && networkObject.SceneOriginHandle == 0)
            {
                networkObject.SceneOrigin = networkObject.gameObject.scene;
            }

            // For integration testing, this makes sure that the appropriate NetworkManager is assigned to
            // the NetworkObject since it uses the NetworkManager.Singleton when not set
            if (networkObject.NetworkManagerOwner != NetworkManager)
            {
                networkObject.NetworkManagerOwner = NetworkManager;
            }

            networkObject.NetworkObjectId = networkId;

            networkObject.DestroyWithScene = sceneObject || destroyWithScene;

            networkObject.IsPlayerObject = playerObject;

            networkObject.OwnerClientId = ownerClientId;

            // When spawned, previous owner is always the first assigned owner
            networkObject.PreviousOwnerId = ownerClientId;

            // If this the player and the client is the owner, then lock ownership by default
            if (NetworkManager.DistributedAuthorityMode && NetworkManager.LocalClientId == ownerClientId && playerObject)
            {
                networkObject.SetOwnershipLock();
            }
            SpawnedObjects.Add(networkObject.NetworkObjectId, networkObject);
            SpawnedObjectsList.Add(networkObject);

            // If we are not running in DA mode, this is the server, and the NetworkObject has SpawnWithObservers set,
            // then add all connected clients as observers
            if (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer && networkObject.SpawnWithObservers)
            {
                // Add client observers
                for (int i = 0; i < NetworkManager.ConnectedClientsIds.Count; i++)
                {
                    // If CheckObjectVisibility has a callback, then allow that method determine who the observers are.
                    if (networkObject.CheckObjectVisibility != null && !networkObject.CheckObjectVisibility(NetworkManager.ConnectedClientsIds[i]))
                    {
                        continue;
                    }
                    networkObject.Observers.Add(NetworkManager.ConnectedClientsIds[i]);
                }
            }

            networkObject.ApplyNetworkParenting();
            NetworkObject.CheckOrphanChildren();

            networkObject.InvokeBehaviourNetworkSpawn();

            NetworkManager.DeferredMessageManager.ProcessTriggers(IDeferredNetworkMessageManager.TriggerType.OnSpawn, networkId);

            // propagate the IsSceneObject setting to child NetworkObjects
            var children = networkObject.GetComponentsInChildren<NetworkObject>();
            foreach (var childObject in children)
            {
                // Do not propagate the in-scene object setting if a child was dynamically spawned.
                if (childObject.IsSceneObject.HasValue && !childObject.IsSceneObject.Value)
                {
                    continue;
                }
                childObject.IsSceneObject = sceneObject;
            }

            // Only dynamically spawned NetworkObjects are allowed
            if (!sceneObject)
            {
                networkObject.SubscribeToActiveSceneForSynch();
            }

            if (networkObject.IsPlayerObject)
            {
                UpdateNetworkClientPlayer(networkObject);
            }

            // If we are an in-scene placed NetworkObject and our InScenePlacedSourceGlobalObjectIdHash is set
            // then assign this to the PrefabGlobalObjectIdHash
            if (networkObject.IsSceneObject.Value && networkObject.InScenePlacedSourceGlobalObjectIdHash != 0)
            {
                networkObject.PrefabGlobalObjectIdHash = networkObject.InScenePlacedSourceGlobalObjectIdHash;
            }
        }

        internal void SendSpawnCallForObject(ulong clientId, NetworkObject networkObject)
        {
            // If we are a host and sending to the host's client id, then we can skip sending ourselves the spawn message.
            var updateObservers = NetworkManager.DistributedAuthorityMode && networkObject.SpawnWithObservers;

            // Only skip if distributed authority mode is not enabled
            if (clientId == NetworkManager.ServerClientId && !NetworkManager.DistributedAuthorityMode)
            {
                return;
            }

            var message = new CreateObjectMessage
            {
                ObjectInfo = networkObject.GetMessageSceneObject(clientId, NetworkManager.DistributedAuthorityMode),
                IncludesSerializedObject = true,
                UpdateObservers = NetworkManager.DistributedAuthorityMode,
                ObserverIds = NetworkManager.DistributedAuthorityMode ? networkObject.Observers.ToArray() : null,
            };
            var size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, clientId);
            NetworkManager.NetworkMetrics.TrackObjectSpawnSent(clientId, networkObject, size);
        }

        /// <summary>
        /// Only used to update object visibility/observers.
        /// ** Clients are the only instances that use this method **
        /// </summary>
        internal void SendSpawnCallForObserverUpdate(ulong[] newObservers, NetworkObject networkObject)
        {
            if (!NetworkManager.DistributedAuthorityMode)
            {
                throw new Exception("[SendSpawnCallForObserverUpdate] Invoking a distributed authority only method when distributed authority is not enabled!");
            }

            var message = new CreateObjectMessage
            {
                ObjectInfo = networkObject.GetMessageSceneObject(),
                ObserverIds = networkObject.Observers.ToArray(),
                NewObserverIds = newObservers.ToArray(),
                IncludesSerializedObject = true,
                UpdateObservers = true,
                UpdateNewObservers = true,
            };
            var size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, NetworkManager.ServerClientId);
            foreach (var clientId in newObservers)
            {
                // TODO: We might want to track observer update sent as well?
                NetworkManager.NetworkMetrics.TrackObjectSpawnSent(clientId, networkObject, size);
            }
        }

        internal ulong? GetSpawnParentId(NetworkObject networkObject)
        {
            NetworkObject parentNetworkObject = null;

            if (!networkObject.AlwaysReplicateAsRoot && networkObject.transform.parent != null)
            {
                parentNetworkObject = networkObject.transform.parent.GetComponent<NetworkObject>();
            }

            if (parentNetworkObject == null)
            {
                return null;
            }

            return parentNetworkObject.NetworkObjectId;
        }

        internal void DespawnObject(NetworkObject networkObject, bool destroyObject = false, bool playerDisconnect = false)
        {
            if (!networkObject.IsSpawned)
            {
                NetworkLog.LogErrorServer("Object is not spawned!");
                return;
            }

            if (!NetworkManager.IsServer && !NetworkManager.DistributedAuthorityMode)
            {
                NetworkLog.LogErrorServer("Only server can despawn objects");
                return;
            }

            if (NetworkManager.DistributedAuthorityMode && networkObject.OwnerClientId != NetworkManager.LocalClientId)
            {
                if (!NetworkManager.DAHost || NetworkManager.DAHost && !playerDisconnect)
                {
                    NetworkLog.LogErrorServer($"In distributed authority mode, only the owner of the NetworkObject can despawn it! Local Client is ({NetworkManager.LocalClientId}) while the owner is ({networkObject.OwnerClientId})");
                    return;
                }
            }
            OnDespawnObject(networkObject, destroyObject, playerDisconnect);
        }

        // Makes scene objects ready to be reused
        internal void ServerResetShudownStateForSceneObjects()
        {
#if UNITY_2023_1_OR_NEWER
            var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID).Where((c) => c.IsSceneObject != null && c.IsSceneObject == true);
#else
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>().Where((c) => c.IsSceneObject != null && c.IsSceneObject == true);
#endif
            foreach (var sobj in networkObjects)
            {
                sobj.IsSpawned = false;
                sobj.DestroyWithScene = false;
                sobj.IsSceneObject = null;
            }
        }

        /// <summary>
        /// Gets called only by NetworkSceneManager.SwitchScene
        /// </summary>
        internal void ServerDestroySpawnedSceneObjects()
        {
            // This Allocation is "OK" for now because this code only executes when a new scene is switched to
            // We need to create a new copy the HashSet of NetworkObjects (SpawnedObjectsList) so we can remove
            // objects from the HashSet (SpawnedObjectsList) without causing a list has been modified exception to occur.
            var spawnedObjects = SpawnedObjectsList.ToList();

            foreach (var sobj in spawnedObjects)
            {
                if (sobj.IsSceneObject != null && sobj.IsSceneObject.Value && sobj.DestroyWithScene && sobj.gameObject.scene != NetworkManager.SceneManager.DontDestroyOnLoadScene)
                {
                    SpawnedObjectsList.Remove(sobj);
                    UnityEngine.Object.Destroy(sobj.gameObject);
                }
            }
        }

        internal void DespawnAndDestroyNetworkObjects()
        {
#if UNITY_2023_1_OR_NEWER
            var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID);
#else
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif

            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    if (NetworkManager.PrefabHandler.ContainsHandler(networkObjects[i]))
                    {
                        OnDespawnObject(networkObjects[i], false);
                        // Leave destruction up to the handler
                        NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(networkObjects[i]);
                    }
                    else
                    {
                        // If it is an in-scene placed NetworkObject then just despawn and let it be destroyed when the scene
                        // is unloaded. Otherwise, despawn and destroy it.
                        var shouldDestroy = !(networkObjects[i].IsSceneObject == null || (networkObjects[i].IsSceneObject != null && networkObjects[i].IsSceneObject.Value));

                        // If we are going to destroy this NetworkObject, check for any in-scene placed children that need to be removed
                        if (shouldDestroy)
                        {
                            // Check to see if there are any in-scene placed children that are marked to be destroyed with the scene
                            var childrenObjects = networkObjects[i].GetComponentsInChildren<NetworkObject>();
                            foreach (var childObject in childrenObjects)
                            {
                                if (childObject == networkObjects[i])
                                {
                                    continue;
                                }

                                // If the child is an in-scene placed NetworkObject then remove the child from the parent (which was dynamically spawned)
                                // and set its parent to root
                                if (childObject.IsSceneObject != null && childObject.IsSceneObject.Value)
                                {
                                    childObject.TryRemoveParent(childObject.WorldPositionStays());
                                }
                            }
                        }

                        // If spawned, then despawn and potentially destroy. 
                        if (networkObjects[i].IsSpawned)
                        {
                            OnDespawnObject(networkObjects[i], shouldDestroy);
                        }
                        else // Otherwise, if we are not spawned and we should destroy...then destroy.
                        if (shouldDestroy)
                        {
                            UnityEngine.Object.Destroy(networkObjects[i].gameObject);
                        }
                    }
                }
            }
        }

        internal void DestroySceneObjects()
        {
#if UNITY_2023_1_OR_NEWER
            var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID);
#else
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif

            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    if (networkObjects[i].IsSceneObject == null || networkObjects[i].IsSceneObject.Value == true)
                    {
                        if (NetworkManager.PrefabHandler.ContainsHandler(networkObjects[i]))
                        {
                            if (SpawnedObjects.ContainsKey(networkObjects[i].NetworkObjectId))
                            {
                                // This method invokes HandleNetworkPrefabDestroy, we only want to handle this once.
                                OnDespawnObject(networkObjects[i], false);
                            }
                            else // If not spawned, then just invoke the handler
                            {
                                NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(networkObjects[i]);
                            }
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(networkObjects[i].gameObject);
                        }
                    }
                }
            }
        }

        internal void ServerSpawnSceneObjectsOnStartSweep()
        {
#if UNITY_2023_1_OR_NEWER
            var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.InstanceID);
#else
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif
            var isConnectedCMBService = NetworkManager.CMBServiceConnection;
            var networkObjectsToSpawn = new List<NetworkObject>();
            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    // This used to be two loops.
                    // The first added all NetworkObjects to a list and the second spawned all NetworkObjects in the list.
                    // Now, a parent will set its children's IsSceneObject value when spawned, so we check for null or for true.
                    if (networkObjects[i].IsSceneObject == null || (networkObjects[i].IsSceneObject.HasValue && networkObjects[i].IsSceneObject.Value))
                    {
                        var ownerId = networkObjects[i].OwnerClientId;
                        if (NetworkManager.DistributedAuthorityMode)
                        {
                            ownerId = NetworkManager.LocalClientId;
                        }

                        SpawnNetworkObjectLocally(networkObjects[i], GetNetworkObjectId(), true, false, ownerId, true);
                        networkObjectsToSpawn.Add(networkObjects[i]);
                    }
                }
            }

            // Notify all in-scene placed NetworkObjects have been spawned
            foreach (var networkObject in networkObjectsToSpawn)
            {
                networkObject.InternalInSceneNetworkObjectsSpawned();
            }
            networkObjectsToSpawn.Clear();
        }

        internal void OnDespawnObject(NetworkObject networkObject, bool destroyGameObject, bool modeDestroy = false)
        {
            if (NetworkManager == null)
            {
                return;
            }

            // We have to do this check first as subsequent checks assume we can access NetworkObjectId.
            if (networkObject == null)
            {
                Debug.LogWarning($"Trying to destroy network object but it is null");
                return;
            }

            // Removal of spawned object
            if (!SpawnedObjects.ContainsKey(networkObject.NetworkObjectId))
            {
                if (!NetworkManager.ShutdownInProgress)
                {
                    Debug.LogWarning($"Trying to destroy object {networkObject.NetworkObjectId} but it doesn't seem to exist anymore!");
                }
                return;
            }

            // If we are shutting down the NetworkManager, then ignore resetting the parent
            // and only attempt to remove the child's parent on the server-side
            var distributedAuthority = NetworkManager.DistributedAuthorityMode;
            if (!NetworkManager.ShutdownInProgress && (NetworkManager.IsServer || distributedAuthority))
            {
                // Get all child NetworkObjects
                var objectsToRemoveParent = networkObject.GetComponentsInChildren<NetworkObject>();

                // Move child NetworkObjects to the root when parent NetworkObject is destroyed
                foreach (var spawnedNetObj in objectsToRemoveParent)
                {
                    if (spawnedNetObj == networkObject)
                    {
                        continue;
                    }
                    var latestParent = spawnedNetObj.GetNetworkParenting();
                    // Only deparent the first generation children of the NetworkObject being spawned.
                    // Ignore any nested children under first generation children.
                    if (latestParent.HasValue && latestParent.Value != networkObject.NetworkObjectId)
                    {
                        continue;
                    }
                    // For mixed authority hierarchies, if the parent is despawned then any removal of children
                    // is considered "authority approved". If we don't have authority over the object and we are
                    // in distributed authority mode, then set the AuthorityAppliedParenting flag.
                    spawnedNetObj.AuthorityAppliedParenting = distributedAuthority && !spawnedNetObj.HasAuthority;

                    // Try to remove the parent using the cached WorldPositionStays value
                    // Note: WorldPositionStays will still default to true if this was an
                    // in-scene placed NetworkObject and parenting was predefined in the
                    // scene via the editor.
                    if (!spawnedNetObj.TryRemoveParentCachedWorldPositionStays())
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogError($"{nameof(NetworkObject)} #{spawnedNetObj.NetworkObjectId} could not be moved to the root when its parent {nameof(NetworkObject)} #{networkObject.NetworkObjectId} was being destroyed");
                        }
                    }
                    else
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkObject)} #{spawnedNetObj.NetworkObjectId} moved to the root because its parent {nameof(NetworkObject)} #{networkObject.NetworkObjectId} is destroyed");
                    }
                }
            }

            networkObject.InvokeBehaviourNetworkDespawn();

            if (NetworkManager != null && ((NetworkManager.IsServer && (!distributedAuthority ||
                (distributedAuthority && modeDestroy))) ||
                (distributedAuthority && networkObject.OwnerClientId == NetworkManager.LocalClientId)))
            {
                if (NetworkManager.NetworkConfig.RecycleNetworkIds)
                {
                    ReleasedNetworkObjectIds.Enqueue(new ReleasedNetworkId()
                    {
                        NetworkId = networkObject.NetworkObjectId,
                        ReleaseTime = NetworkManager.RealTimeProvider.UnscaledTime
                    });
                }
                m_TargetClientIds.Clear();

                // If clients are not allowed to spawn locally then go ahead and send the despawn message or if we are in distributed authority mode, we are the server, we own this NetworkObject
                // send the despawn message, and as long as we have any remaining clients, then notify of the object being destroy.
                if (NetworkManager.IsServer && NetworkManager.ConnectedClientsList.Count > 0 && (!distributedAuthority ||
                    (NetworkManager.DAHost && distributedAuthority &&
                    (networkObject.OwnerClientId == NetworkManager.LocalClientId || modeDestroy))))
                {
                    // We keep only the client for which the object is visible
                    // as the other clients have them already despawned
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if ((distributedAuthority && clientId == networkObject.OwnerClientId) || clientId == NetworkManager.LocalClientId)
                        {
                            continue;
                        }
                        if (networkObject.IsNetworkVisibleTo(clientId))
                        {
                            m_TargetClientIds.Add(clientId);
                        }
                    }
                }
                else // DANGO-TODO: If we are not the server, distributed authority mode is enabled, and we are the owner then inform the DAHost to despawn the NetworkObject
                if (!NetworkManager.IsServer && distributedAuthority && networkObject.OwnerClientId == NetworkManager.LocalClientId)
                {
                    // DANGO-TODO: If a shutdown is not in progress or a shutdown is in progress and we can destroy with the owner then notify the DAHost
                    if (!NetworkManager.ShutdownInProgress || (NetworkManager.ShutdownInProgress && !networkObject.DontDestroyWithOwner))
                    {
                        m_TargetClientIds.Add(NetworkManager.ServerClientId);
                    }
                }

                if (m_TargetClientIds.Count > 0 && !NetworkManager.ShutdownInProgress)
                {
                    var message = new DestroyObjectMessage
                    {
                        NetworkObjectId = networkObject.NetworkObjectId,
                        DeferredDespawnTick = networkObject.DeferredDespawnTick,
                        DestroyGameObject = networkObject.IsSceneObject != false ? destroyGameObject : true,
                        IsTargetedDestroy = false,
                        IsDistributedAuthority = distributedAuthority,
                    };
                    foreach (var clientId in m_TargetClientIds)
                    {
                        var size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
                        NetworkManager.NetworkMetrics.TrackObjectDestroySent(clientId, networkObject, size);
                    }
                }
            }

            networkObject.IsSpawned = false;

            if (SpawnedObjects.Remove(networkObject.NetworkObjectId))
            {
                SpawnedObjectsList.Remove(networkObject);
            }

            // DANGO-TODO: When we fix the issue with observers not being applied to NetworkObjects,
            // (client connect/disconnect) we can remove this hacky way of doing this.
            // Basically, when a player disconnects and/or is destroyed they are removed as an observer from all other client
            // NetworkOject instances.
            if (networkObject.IsPlayerObject && !networkObject.IsOwner && networkObject.OwnerClientId != NetworkManager.LocalClientId)
            {
                foreach (var netObject in SpawnedObjects)
                {
                    if (netObject.Value.Observers.Contains(networkObject.OwnerClientId))
                    {
                        netObject.Value.Observers.Remove(networkObject.OwnerClientId);
                    }
                }
            }
            if (networkObject.IsPlayerObject)
            {
                RemovePlayerObject(networkObject);
            }

            // Always clear out the observers list when despawned
            networkObject.Observers.Clear();

            var gobj = networkObject.gameObject;
            if (destroyGameObject && gobj != null)
            {
                if (NetworkManager.PrefabHandler.ContainsHandler(networkObject))
                {
                    NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(networkObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(gobj);
                }
            }
        }

        /// <summary>
        /// Updates all spawned <see cref="NetworkObject.Observers"/> for the specified newly connected client
        /// Note: if the clientId is the server then it is observable to all spawned <see cref="NetworkObject"/>'s
        /// </summary>
        /// <remarks>
        /// This method is to only to be used for newly connected clients in order to update the observers list for
        /// each NetworkObject instance.
        /// </remarks>
        internal void UpdateObservedNetworkObjects(ulong clientId)
        {
            foreach (var sobj in SpawnedObjectsList)
            {
                // If the NetworkObject has no visibility check then prepare to add this client as an observer
                if (sobj.CheckObjectVisibility == null)
                {
                    // If the client is not part of the observers and spawn with observers is enabled on this instance or the clientId is the server/host/DAHost
                    if (!sobj.Observers.Contains(clientId) && (sobj.SpawnWithObservers || clientId == NetworkManager.ServerClientId))
                    {
                        sobj.Observers.Add(clientId);
                    }
                }
                else
                {
                    // CheckObject visibility overrides SpawnWithObservers under this condition
                    if (sobj.CheckObjectVisibility(clientId))
                    {
                        sobj.Observers.Add(clientId);
                    }
                    else // Otherwise, if the observers contains the clientId (shouldn't happen) then remove it since CheckObjectVisibility returned false
                    {
                        sobj.Observers.Remove(clientId);
                    }
                }
            }
        }

        /// <summary>
        /// See <see cref="NetworkBehaviourUpdater.NetworkBehaviourUpdater_Tick"/>
        /// </summary>
        internal void HandleNetworkObjectShow()
        {
            // In distributed authority mode, we send a single message that is broadcasted to all clients
            // that will be shown the object (i.e. 1 message to service that then broadcasts that to the
            // targeted clients). When using a DAHost, we skip this and send like we do in client-server
            if (NetworkManager.DistributedAuthorityMode && !NetworkManager.DAHost)
            {
                foreach (var entry in ClientsToShowObject)
                {
                    if (entry.Key != null && entry.Key.IsSpawned)
                    {
                        try
                        {
                            SendSpawnCallForObserverUpdate(entry.Value.ToArray(), entry.Key);
                        }
                        catch (Exception ex)
                        {
                            if (NetworkManager.LogLevel <= LogLevel.Developer)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }
                }
                ClientsToShowObject.Clear();
                ObjectsToShowToClient.Clear();
                return;
            }

            // Server or Host handling of NetworkObjects to show
            foreach (var client in ObjectsToShowToClient)
            {
                ulong clientId = client.Key;
                foreach (var networkObject in client.Value)
                {
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        try
                        {
                            SendSpawnCallForObject(clientId, networkObject);
                        }
                        catch (Exception ex)
                        {
                            if (NetworkManager.LogLevel <= LogLevel.Developer)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }
                }
            }
            ObjectsToShowToClient.Clear();
        }

        internal NetworkSpawnManager(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
        }

        /// <summary>
        /// DANGO-TODO: Until we have the CMB Server end-to-end with all features verified working via integration tests,
        /// I am keeping this debug toggle available. (NSS)
        /// </summary>
        internal bool EnableDistributeLogging = false;

        /// <summary>
        /// Fills the first table passed in with the current distribution of prefab types relative to their owners
        /// Fills the second table passed in with the total number of spawned objects of that particular type.
        /// The second table allows us to calculate how many objects per client there should be in order to determine
        /// how many of that type should be distributed.
        /// </summary>
        /// <param name="objectByTypeAndOwner">the table to populate</param>
        /// <param name="objectTypeCount">the total number of the specific object type to distribute</param>
        internal void GetObjectDistribution(ref Dictionary<uint, Dictionary<ulong, List<NetworkObject>>> objectByTypeAndOwner, ref Dictionary<uint, int> objectTypeCount)
        {
            // DANGO-TODO-MVP: Remove this once the service handles object distribution
            var onlyIncludeOwnedObjects = NetworkManager.CMBServiceConnection;

            foreach (var networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject.IsOwnershipDistributable && !networkObject.IsOwnershipLocked)
                {
                    if (networkObject.transform.parent != null)
                    {
                        var parentNetworkObject = networkObject.transform.parent.GetComponent<NetworkObject>();
                        if (parentNetworkObject != null && parentNetworkObject.OwnerClientId == networkObject.OwnerClientId)
                        {
                            continue;
                        }
                    }

                    if (networkObject.IsSceneObject.Value)
                    {
                        continue;
                    }

                    if (!objectTypeCount.ContainsKey(networkObject.GlobalObjectIdHash))
                    {
                        objectTypeCount.Add(networkObject.GlobalObjectIdHash, 0);
                    }
                    objectTypeCount[networkObject.GlobalObjectIdHash] += 1;

                    // DANGO-TODO-MVP: Remove this once the service handles object distribution
                    if (onlyIncludeOwnedObjects && !networkObject.IsOwner)
                    {
                        continue;
                    }

                    // Divide up by prefab type (GlobalObjectIdHash) to get a better distribution of object types
                    if (!objectByTypeAndOwner.ContainsKey(networkObject.GlobalObjectIdHash))
                    {
                        objectByTypeAndOwner.Add(networkObject.GlobalObjectIdHash, new Dictionary<ulong, List<NetworkObject>>());
                    }

                    // Sub-divide each type by owner
                    if (!objectByTypeAndOwner[networkObject.GlobalObjectIdHash].ContainsKey(networkObject.OwnerClientId))
                    {
                        objectByTypeAndOwner[networkObject.GlobalObjectIdHash].Add(networkObject.OwnerClientId, new List<NetworkObject>());
                    }

                    // Add to the client's spawned object list
                    objectByTypeAndOwner[networkObject.GlobalObjectIdHash][networkObject.OwnerClientId].Add(networkObject);
                }
            }
        }

        internal void DistributeNetworkObjects(ulong clientId)
        {
            // Distributed authority mode ownership distribution
            // DANGO-TODO-MVP: Remove the session owner object distribution check once the service handles object distribution
            if (NetworkManager.DistributedAuthorityMode && (NetworkManager.DAHost || NetworkManager.CMBServiceConnection))
            {
                // DA-NGO CMB SERVICE NOTES: 
                // The most basic object distribution should be broken up into a table of spawned object types
                // where each type contains a list of each client's owned objects of that type that can be
                // distributed.
                // The table format:
                // [GlobalObjectIdHashValue][ClientId][List of Owned Objects]
                var distributedNetworkObjects = new Dictionary<uint, Dictionary<ulong, List<NetworkObject>>>();

                // DA-NGO CMB SERVICE NOTES:
                // This is optional, but I found it easier to get the total count of spawned objects for each prefab
                // type contained in the previous table in order to be able to calculate the targeted object distribution
                // count of that type per client.
                var objectTypeCount = new Dictionary<uint, int>();

                // Get all spawned objects by type and then by client owner that are spawned and can be distributed
                GetObjectDistribution(ref distributedNetworkObjects, ref objectTypeCount);

                var clientCount = NetworkManager.ConnectedClientsIds.Count;

                // Cycle through each prefab type 
                foreach (var objectTypeEntry in distributedNetworkObjects)
                {
                    // Calculate the number of objects that should be distributed amongst the clients
                    var totalObjectsToDistribute = objectTypeCount[objectTypeEntry.Key];
                    var objPerClientF = totalObjectsToDistribute * (1.0f / clientCount);
                    var floorValue = (int)Math.Floor(objPerClientF);
                    var fractional = objPerClientF - floorValue;
                    var objPerClient = 0;
                    if (fractional >= 0.556f)
                    {
                        objPerClient = (int)Math.Round(totalObjectsToDistribute * (1.0f / clientCount));
                    }
                    else
                    {
                        objPerClient = floorValue;
                    }

                    // If the object per client count is zero, then move to the next type.
                    if (objPerClient <= 0)
                    {
                        continue;
                    }

                    // Evenly distribute this object type amongst the clients
                    foreach (var ownerList in objectTypeEntry.Value)
                    {
                        if (ownerList.Value.Count <= 1)
                        {
                            continue;
                        }

                        var maxDistributeCount = Mathf.Max(ownerList.Value.Count - objPerClient, 1);
                        var distributed = 0;

                        // For now when we have more players then distributed NetworkObjects that
                        // a specific client owns, just assign half of the NetworkObjects to the new client
                        var offsetCount = Mathf.Max((int)Math.Round((float)(ownerList.Value.Count / objPerClient)), 1);
                        if (EnableDistributeLogging)
                        {
                            Debug.Log($"[{objPerClient} of {totalObjectsToDistribute}][Client-{ownerList.Key}] Count: {ownerList.Value.Count} | ObjPerClient: {objPerClient} | maxD: {maxDistributeCount} | Offset: {offsetCount}");
                        }

                        for (int i = 0; i < ownerList.Value.Count; i++)
                        {
                            if ((i % offsetCount) == 0)
                            {
                                ChangeOwnership(ownerList.Value[i], clientId, true);
                                if (EnableDistributeLogging)
                                {
                                    Debug.Log($"[Client-{ownerList.Key}][NetworkObjectId-{ownerList.Value[i].NetworkObjectId} Distributed to Client-{clientId}");
                                }
                                distributed++;
                            }
                            if (distributed == maxDistributeCount)
                            {
                                break;
                            }
                        }
                    }
                }

                // If EnableDistributeLogging is enabled, log the object type distribution counts per client
                if (EnableDistributeLogging)
                {
                    var builder = new StringBuilder();
                    distributedNetworkObjects.Clear();
                    objectTypeCount.Clear();
                    GetObjectDistribution(ref distributedNetworkObjects, ref objectTypeCount);
                    builder.AppendLine($"Client Relative Distributed Object Count: (distribution follows)");
                    // Cycle through each prefab type 
                    foreach (var objectTypeEntry in distributedNetworkObjects)
                    {
                        builder.AppendLine($"[GID: {objectTypeEntry.Key} | {objectTypeEntry.Value.First().Value.First().name}][Total Count: {objectTypeCount[objectTypeEntry.Key]}]");
                        builder.AppendLine($"[GID: {objectTypeEntry.Key} | {objectTypeEntry.Value.First().Value.First().name}] Distribution:");
                        // Evenly distribute this type amongst clients
                        foreach (var ownerList in objectTypeEntry.Value)
                        {
                            builder.AppendLine($"[Client-{ownerList.Key}] Count: {ownerList.Value.Count}");
                        }
                    }
                    Debug.Log(builder.ToString());
                }
            }
        }

        internal struct DeferredDespawnObject
        {
            public int TickToDespawn;
            public bool HasDeferredDespawnCheck;
            public ulong NetworkObjectId;
        }

        internal List<DeferredDespawnObject> DeferredDespawnObjects = new List<DeferredDespawnObject>();

        /// <summary>
        /// Adds a deferred despawn entry to be processed
        /// </summary>
        /// <param name="networkObjectId">associated NetworkObject</param>
        /// <param name="tickToDespawn">when to despawn the NetworkObject</param>
        /// <param name="hasDeferredDespawnCheck">if true, user script is to be invoked to determine when to despawn</param>
        internal void DeferDespawnNetworkObject(ulong networkObjectId, int tickToDespawn, bool hasDeferredDespawnCheck)
        {
            var deferredDespawnObject = new DeferredDespawnObject()
            {
                TickToDespawn = tickToDespawn,
                HasDeferredDespawnCheck = hasDeferredDespawnCheck,
                NetworkObjectId = networkObjectId,
            };
            DeferredDespawnObjects.Add(deferredDespawnObject);
        }

        /// <summary>
        /// Processes any deferred despawn entries
        /// </summary>
        internal void DeferredDespawnUpdate(NetworkTime serverTime)
        {
            // Exit early if there is nothing to process
            if (DeferredDespawnObjects.Count == 0)
            {
                return;
            }
            var currentTick = serverTime.Tick;
            var deferredCallbackCount = DeferredDespawnObjects.Count();
            for (int i = 0; i < deferredCallbackCount; i++)
            {
                var deferredObjectEntry = DeferredDespawnObjects[i];
                if (!deferredObjectEntry.HasDeferredDespawnCheck)
                {
                    continue;
                }
                var networkObject = SpawnedObjects[deferredObjectEntry.NetworkObjectId];
                // Double check to make sure user did not remove the callback
                if (networkObject.OnDeferredDespawnComplete != null)
                {
                    // If the user callback returns true, then we despawn it this tick
                    if (networkObject.OnDeferredDespawnComplete.Invoke())
                    {
                        deferredObjectEntry.TickToDespawn = currentTick;
                    }
                    else
                    {
                        // If the user callback does not verify the NetworkObject can be despawned,
                        // continue setting this value in the event user script adjusts it.
                        deferredObjectEntry.TickToDespawn = networkObject.DeferredDespawnTick;
                    }
                }
                else
                {
                    // If it was removed, then in case it is being left to defer naturally exclude it
                    // from the next query.
                    deferredObjectEntry.HasDeferredDespawnCheck = false;
                }
            }

            // Parse backwards so we can remove objects as we parse through them
            for (int i = DeferredDespawnObjects.Count - 1; i >= 0; i--)
            {
                var deferredObjectEntry = DeferredDespawnObjects[i];
                if (deferredObjectEntry.TickToDespawn >= currentTick)
                {
                    continue;
                }

                if (!SpawnedObjects.ContainsKey(deferredObjectEntry.NetworkObjectId))
                {
                    DeferredDespawnObjects.Remove(deferredObjectEntry);
                    continue;
                }
                var networkObject = SpawnedObjects[deferredObjectEntry.NetworkObjectId];
                // Local instance despawns the instance
                OnDespawnObject(networkObject, true);
                DeferredDespawnObjects.Remove(deferredObjectEntry);
            }
        }

        internal void NotifyNetworkObjectsSynchronized()
        {
            // Users could spawn NetworkObjects during these notifications.
            // Create a separate list from the hashset to avoid list modification errors.
            var spawnedObjects = SpawnedObjectsList.ToList();
            foreach (var networkObject in spawnedObjects)
            {
                networkObject.InternalNetworkSessionSynchronized();
            }
        }

        /// <summary>
        /// Distributed Authority Only
        /// Should be invoked on non-session owner clients when a newly joined client is finished
        /// synchronizing in order to "show" (spawn) anything that might be currently hidden from
        /// the session owner.
        /// </summary>
        internal void ShowHiddenObjectsToNewlyJoinedClient(ulong newClientId)
        {
            if (!NetworkManager.DistributedAuthorityMode)
            {
                if (NetworkManager == null || !NetworkManager.ShutdownInProgress && NetworkManager.LogLevel <= LogLevel.Developer)
                {
                    Debug.LogWarning($"[Internal Error] {nameof(ShowHiddenObjectsToNewlyJoinedClient)} invoked while !");
                }
                return;
            }

            if (!NetworkManager.DistributedAuthorityMode)
            {
                Debug.LogError($"[Internal Error] {nameof(ShowHiddenObjectsToNewlyJoinedClient)} should only be invoked when using a distributed authority network topology!");
                return;
            }

            if (NetworkManager.LocalClient.IsSessionOwner)
            {
                Debug.LogError($"[Internal Error] {nameof(ShowHiddenObjectsToNewlyJoinedClient)} should only be invoked on a non-session owner client!");
                return;
            }
            var localClientId = NetworkManager.LocalClient.ClientId;
            var sessionOwnerId = NetworkManager.CurrentSessionOwner;
            foreach (var networkObject in SpawnedObjectsList)
            {
                if (networkObject.SpawnWithObservers && networkObject.OwnerClientId == localClientId && !networkObject.Observers.Contains(sessionOwnerId))
                {
                    if (networkObject.Observers.Contains(newClientId))
                    {
                        if (NetworkManager.LogLevel <= LogLevel.Developer)
                        {
                            // Track if there is some other location where the client is being added to the observers list when the object is hidden from the session owner
                            Debug.LogWarning($"[{networkObject.name}] Has new client as an observer but it is hidden from the session owner!");
                        }
                        // For now, remove the client (impossible for the new client to have an instance since the session owner doesn't) to make sure newly added
                        // code to handle this edge case works.
                        networkObject.Observers.Remove(newClientId);
                    }
                    networkObject.NetworkShow(newClientId);
                }
            }
        }
    }
}
