using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Class that handles object spawning
    /// </summary>
    public class NetworkSpawnManager
    {
        /// <summary>
        /// The currently spawned objects
        /// </summary>
        public readonly Dictionary<ulong, NetworkObject> SpawnedObjects = new Dictionary<ulong, NetworkObject>();

        /// <summary>
        /// A list of the spawned objects
        /// </summary>
        public readonly HashSet<NetworkObject> SpawnedObjectsList = new HashSet<NetworkObject>();


        /// <summary>
        /// Gets the NetworkManager associated with this SpawnManager.
        /// </summary>
        public NetworkManager NetworkManager { get; }

        internal NetworkSpawnManager(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
        }

        internal readonly Queue<ReleasedNetworkId> ReleasedNetworkObjectIds = new Queue<ReleasedNetworkId>();
        private ulong m_NetworkObjectIdCounter;

        internal ulong GetNetworkObjectId()
        {
            if (ReleasedNetworkObjectIds.Count > 0 && NetworkManager.NetworkConfig.RecycleNetworkIds && (Time.unscaledTime - ReleasedNetworkObjectIds.Peek().ReleaseTime) >= NetworkManager.NetworkConfig.NetworkIdRecycleDelay)
            {
                return ReleasedNetworkObjectIds.Dequeue().NetworkId;
            }

            m_NetworkObjectIdCounter++;

            return m_NetworkObjectIdCounter;
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
        /// Returns the player object with a given clientId or null if one does not exist. This is only valid server side.
        /// </summary>
        /// <returns>The player object with a given clientId or null if one does not exist</returns>
        public NetworkObject GetPlayerNetworkObject(ulong clientId)
        {
            if (!NetworkManager.IsServer && NetworkManager.LocalClientId != clientId)
            {
                throw new NotServerException("Only the server can find player objects from other clients.");
            }

            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
            {
                return networkClient.PlayerObject;
            }

            return null;
        }

        internal void RemoveOwnership(NetworkObject networkObject)
        {
            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only the server can change ownership");
            }

            if (!networkObject.IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            for (int i = NetworkManager.ConnectedClients[networkObject.OwnerClientId].OwnedObjects.Count - 1;
                i > -1;
                i--)
            {
                if (NetworkManager.ConnectedClients[networkObject.OwnerClientId].OwnedObjects[i] == networkObject)
                {
                    NetworkManager.ConnectedClients[networkObject.OwnerClientId].OwnedObjects.RemoveAt(i);
                }
            }

            networkObject.OwnerClientIdInternal = null;

            var context = NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.ChangeOwner, NetworkChannel.Internal,
                NetworkManager.ConnectedClientsIds, NetworkUpdateLoop.UpdateStage);
            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                    bufferSizeCapture.StartMeasureSegment();

                    nonNullContext.NetworkWriter.WriteUInt64Packed(networkObject.NetworkObjectId);
                    nonNullContext.NetworkWriter.WriteUInt64Packed(networkObject.OwnerClientId);

                    var size = bufferSizeCapture.StopMeasureSegment();

                    foreach (var client in NetworkManager.ConnectedClients)
                    {
                        NetworkManager.NetworkMetrics.TrackOwnershipChangeSent(client.Key, networkObject.NetworkObjectId, networkObject.name, size);
                    }
                }
            }
        }

        internal void ChangeOwnership(NetworkObject networkObject, ulong clientId)
        {
            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only the server can change ownership");
            }

            if (!networkObject.IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (NetworkManager.ConnectedClients.TryGetValue(networkObject.OwnerClientId, out NetworkClient networkClient))
            {
                for (int i = networkClient.OwnedObjects.Count - 1; i >= 0; i--)
                {
                    if (networkClient.OwnedObjects[i] == networkObject)
                    {
                        networkClient.OwnedObjects.RemoveAt(i);
                    }
                }

                networkClient.OwnedObjects.Add(networkObject);
            }

            networkObject.OwnerClientId = clientId;

            ulong[] clientIds = NetworkManager.ConnectedClientsIds;
            var messageQueueContainer = NetworkManager.MessageQueueContainer;
            var context = messageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.ChangeOwner, NetworkChannel.Internal,
                clientIds, NetworkUpdateLoop.UpdateStage);
            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                    bufferSizeCapture.StartMeasureSegment();

                    nonNullContext.NetworkWriter.WriteUInt64Packed(networkObject.NetworkObjectId);
                    nonNullContext.NetworkWriter.WriteUInt64Packed(clientId);

                    var size = bufferSizeCapture.StopMeasureSegment();
                    foreach (var client in NetworkManager.ConnectedClients)
                    {
                        NetworkManager.NetworkMetrics.TrackOwnershipChangeSent(client.Key, networkObject.NetworkObjectId, networkObject.name, size);
                    }
                }
            }
        }

        /// <summary>
        /// Should only run on the client
        /// </summary>
        internal NetworkObject CreateLocalNetworkObject(bool isSceneObject, uint globalObjectIdHash, ulong ownerClientId, ulong? parentNetworkId, Vector3? position, Quaternion? rotation, bool isReparented = false)
        {
            NetworkObject parentNetworkObject = null;

            if (parentNetworkId != null && !isReparented)
            {
                if (SpawnedObjects.TryGetValue(parentNetworkId.Value, out NetworkObject networkObject))
                {
                    parentNetworkObject = networkObject;
                }
                else
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("Cannot find parent. Parent objects always have to be spawned and replicated BEFORE the child");
                    }
                }
            }

            if (!NetworkManager.NetworkConfig.EnableSceneManagement || !isSceneObject)
            {
                // If the prefab hash has a registered INetworkPrefabInstanceHandler derived class
                if (NetworkManager.PrefabHandler.ContainsHandler(globalObjectIdHash))
                {
                    // Let the handler spawn the NetworkObject
                    var networkObject = NetworkManager.PrefabHandler.HandleNetworkPrefabSpawn(globalObjectIdHash, ownerClientId, position.GetValueOrDefault(Vector3.zero), rotation.GetValueOrDefault(Quaternion.identity));

                    networkObject.NetworkManagerOwner = NetworkManager;

                    if (parentNetworkObject != null)
                    {
                        networkObject.transform.SetParent(parentNetworkObject.transform, true);
                    }

                    if (NetworkSceneManager.IsSpawnedObjectsPendingInDontDestroyOnLoad)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(networkObject.gameObject);
                    }

                    return networkObject;
                }
                else
                {
                    // See if there is a valid registered NetworkPrefabOverrideLink associated with the provided prefabHash
                    GameObject networkPrefabReference = null;
                    if (NetworkManager.NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(globalObjectIdHash))
                    {
                        switch (NetworkManager.NetworkConfig.NetworkPrefabOverrideLinks[globalObjectIdHash].Override)
                        {
                            default:
                            case NetworkPrefabOverride.None:
                                networkPrefabReference = NetworkManager.NetworkConfig.NetworkPrefabOverrideLinks[globalObjectIdHash].Prefab;
                                break;
                            case NetworkPrefabOverride.Hash:
                            case NetworkPrefabOverride.Prefab:
                                networkPrefabReference = NetworkManager.NetworkConfig.NetworkPrefabOverrideLinks[globalObjectIdHash].OverridingTargetPrefab;
                                break;
                        }
                    }

                    // If not, then there is an issue (user possibly didn't register the prefab properly?)
                    if (networkPrefabReference == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogError($"Failed to create object locally. [{nameof(globalObjectIdHash)}={globalObjectIdHash}]. {nameof(NetworkPrefab)} could not be found. Is the prefab registered with {nameof(NetworkManager)}?");
                        }

                        return null;
                    }

                    // Otherwise, instantiate an instance of the NetworkPrefab linked to the prefabHash
                    var networkObject = ((position == null && rotation == null) ? UnityEngine.Object.Instantiate(networkPrefabReference) : UnityEngine.Object.Instantiate(networkPrefabReference, position.GetValueOrDefault(Vector3.zero), rotation.GetValueOrDefault(Quaternion.identity))).GetComponent<NetworkObject>();

                    networkObject.NetworkManagerOwner = NetworkManager;

                    if (parentNetworkObject != null)
                    {
                        networkObject.transform.SetParent(parentNetworkObject.transform, true);
                    }

                    if (NetworkSceneManager.IsSpawnedObjectsPendingInDontDestroyOnLoad)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(networkObject.gameObject);
                    }

                    return networkObject;
                }
            }
            else
            {
                if (!NetworkManager.SceneManager.ScenePlacedObjects.TryGetValue(globalObjectIdHash, out NetworkObject networkObject))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogError($"{nameof(NetworkPrefab)} hash was not found! In-Scene placed {nameof(NetworkObject)} soft synchronization failure for Hash: {globalObjectIdHash}!");
                    }

                    return null;
                }
                else
                {
                    NetworkManager.SceneManager.ScenePlacedObjects.Remove(globalObjectIdHash);
                }

                if (parentNetworkObject != null)
                {
                    networkObject.transform.SetParent(parentNetworkObject.transform, true);
                }

                return networkObject;
            }
        }

        // Ran on both server and client
        internal void SpawnNetworkObjectLocally(NetworkObject networkObject, ulong networkId, bool sceneObject, bool playerObject, ulong? ownerClientId, Stream dataStream, bool readNetworkVariable, bool destroyWithScene)
        {
            if (networkObject == null)
            {
                throw new ArgumentNullException(nameof(networkObject), "Cannot spawn null object");
            }

            if (networkObject.IsSpawned)
            {
                throw new SpawnStateException("Object is already spawned");
            }

            if (readNetworkVariable && NetworkManager.NetworkConfig.EnableNetworkVariable)
            {
                networkObject.SetNetworkVariableData(dataStream);
            }

            if (SpawnedObjects.ContainsKey(networkId))
            {
                return;
            }

            networkObject.IsSpawned = true;

            networkObject.IsSceneObject = sceneObject;
            networkObject.NetworkObjectId = networkId;

            networkObject.DestroyWithScene = sceneObject || destroyWithScene;

            networkObject.OwnerClientIdInternal = ownerClientId;
            networkObject.IsPlayerObject = playerObject;

            SpawnedObjects.Add(networkObject.NetworkObjectId, networkObject);
            SpawnedObjectsList.Add(networkObject);

            NetworkManager.NetworkMetrics.TrackNetworkObject(networkObject);

            if (ownerClientId != null)
            {
                if (NetworkManager.IsServer)
                {
                    if (playerObject)
                    {
                        NetworkManager.ConnectedClients[ownerClientId.Value].PlayerObject = networkObject;
                    }
                    else
                    {
                        NetworkManager.ConnectedClients[ownerClientId.Value].OwnedObjects.Add(networkObject);
                    }
                }
                else if (playerObject && ownerClientId.Value == NetworkManager.LocalClientId)
                {
                    NetworkManager.ConnectedClients[ownerClientId.Value].PlayerObject = networkObject;
                }
            }

            if (NetworkManager.IsServer)
            {
                for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
                {
                    if (networkObject.CheckObjectVisibility == null || networkObject.CheckObjectVisibility(NetworkManager.ConnectedClientsList[i].ClientId))
                    {
                        networkObject.Observers.Add(NetworkManager.ConnectedClientsList[i].ClientId);
                    }
                }
            }

            networkObject.SetCachedParent(networkObject.transform.parent);
            networkObject.ApplyNetworkParenting();
            NetworkObject.CheckOrphanChildren();
            networkObject.InvokeBehaviourNetworkSpawn();
			NetworkManager.InterestManager.AddObject(networkObject);
        }

        internal void SendSpawnCallForObject(ulong clientId, ulong ownerClientId, NetworkObject networkObject)
        {
            if (!NetworkManager.NetworkConfig.UseSnapshotSpawn)

            {
                //Currently, if this is called and the clientId (destination) is the server's client Id, this case
                //will be checked within the below Send function.  To avoid unwarranted allocation of a PooledNetworkBuffer
                //placing this check here. [NSS]
                if (NetworkManager.IsServer && clientId == NetworkManager.ServerClientId)
                {
                    return;
                }

                var messageQueueContainer = NetworkManager.MessageQueueContainer;

                var context = messageQueueContainer.EnterInternalCommandContext(
                    MessageQueueContainer.MessageType.CreateObject, NetworkChannel.Internal,
                    new ulong[] { clientId }, NetworkUpdateLoop.UpdateStage);
                if (context != null)
                {
                    using (var nonNullContext = (InternalCommandContext)context)
                    {
                        var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                        bufferSizeCapture.StartMeasureSegment();

                        WriteSpawnCallForObject(nonNullContext.NetworkWriter, clientId, networkObject);

                        var size = bufferSizeCapture.StopMeasureSegment();
                        NetworkManager.NetworkMetrics.TrackObjectSpawnSent(clientId, networkObject.NetworkObjectId, networkObject.name, size);
                    }
                }
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

        internal void WriteSpawnCallForObject(PooledNetworkWriter writer, ulong clientId, NetworkObject networkObject)
        {
            writer.WriteBool(networkObject.IsPlayerObject);
            writer.WriteUInt64Packed(networkObject.NetworkObjectId);
            writer.WriteUInt64Packed(networkObject.OwnerClientId);

            var parent = GetSpawnParentId(networkObject);
            if (parent == null)
            {
                writer.WriteBool(false);
            }
            else
            {
                writer.WriteBool(true);
                writer.WriteUInt64Packed(parent.Value);
            }

            writer.WriteBool(networkObject.IsSceneObject ?? true);
            writer.WriteUInt32Packed(networkObject.HostCheckForGlobalObjectIdHashOverride());

            if (networkObject.IncludeTransformWhenSpawning == null || networkObject.IncludeTransformWhenSpawning(clientId))
            {
                writer.WriteBool(true);
                writer.WriteSinglePacked(networkObject.transform.position.x);
                writer.WriteSinglePacked(networkObject.transform.position.y);
                writer.WriteSinglePacked(networkObject.transform.position.z);

                writer.WriteSinglePacked(networkObject.transform.rotation.eulerAngles.x);
                writer.WriteSinglePacked(networkObject.transform.rotation.eulerAngles.y);
                writer.WriteSinglePacked(networkObject.transform.rotation.eulerAngles.z);
            }
            else
            {
                writer.WriteBool(false);
            }

            {
                var (isReparented, latestParent) = networkObject.GetNetworkParenting();
                NetworkObject.WriteNetworkParenting(writer, isReparented, latestParent);
            }
            if (NetworkManager.NetworkConfig.EnableNetworkVariable)
            {
                networkObject.WriteNetworkVariableData(writer.GetStream(), clientId);
            }
        }

        internal void DespawnObject(NetworkObject networkObject, bool destroyObject = false)
        {
            if (!networkObject.IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can despawn objects");
            }

            OnDespawnObject(networkObject, destroyObject);
        }

        // Makes scene objects ready to be reused
        internal void ServerResetShudownStateForSceneObjects()
        {
            foreach (var sobj in SpawnedObjectsList)
            {
                if ((sobj.IsSceneObject != null && sobj.IsSceneObject == true) || sobj.DestroyWithScene)
                {
                    sobj.IsSpawned = false;
                    sobj.DestroyWithScene = false;
                    sobj.IsSceneObject = null;
                }
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
                if ((sobj.IsSceneObject != null && sobj.IsSceneObject == true) || sobj.DestroyWithScene)
                {
                    // This **needs** to be here until we overhaul NetworkSceneManager due to dependencies
                    // that occur shortly after NetworkSceneManager invokes ServerDestroySpawnedSceneObjects
                    // within the NetworkSceneManager.SwitchScene method.

                    if (NetworkManager.PrefabHandler != null && NetworkManager.PrefabHandler.ContainsHandler(sobj))
                    {
                        NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(sobj);
                    }
                    else
                    {
                        SpawnedObjectsList.Remove(sobj);
                        UnityEngine.Object.Destroy(sobj.gameObject);
                    }
                }
            }
        }

        internal void DestroyNonSceneObjects()
        {
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();

            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    if (networkObjects[i].IsSceneObject != null && networkObjects[i].IsSceneObject.Value == false)
                    {
                        if (NetworkManager.PrefabHandler.ContainsHandler(networkObjects[i]))
                        {
                            NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(networkObjects[i]);

                            if (SpawnedObjects.ContainsKey(networkObjects[i].NetworkObjectId))
                            {
                                OnDespawnObject(networkObjects[i], false);
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

        internal void DestroySceneObjects()
        {
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();

            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    if (networkObjects[i].IsSceneObject == null || networkObjects[i].IsSceneObject.Value == true)
                    {
                        if (NetworkManager.PrefabHandler.ContainsHandler(networkObjects[i]))
                        {
                            NetworkManager.PrefabHandler.HandleNetworkPrefabDestroy(networkObjects[i]);
                            if (SpawnedObjects.ContainsKey(networkObjects[i].NetworkObjectId))
                            {
                                OnDespawnObject(networkObjects[i], false);
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
            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();

            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i].NetworkManager == NetworkManager)
                {
                    if (networkObjects[i].IsSceneObject == null)
                    {
                        SpawnNetworkObjectLocally(networkObjects[i], GetNetworkObjectId(), true, false, null, null, false, true);
                    }
                }
            }
        }

        internal void OnDespawnObject(NetworkObject networkObject, bool destroyGameObject)
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
                Debug.LogWarning($"Trying to destroy object {networkObject.NetworkObjectId} but it doesn't seem to exist anymore!");
                return;
            }

            // Move child NetworkObjects to the root when parent NetworkObject is destroyed
            foreach (var spawnedNetObj in SpawnedObjectsList)
            {
                var (isReparented, latestParent) = spawnedNetObj.GetNetworkParenting();
                if (isReparented && latestParent == networkObject.NetworkObjectId)
                {
                    spawnedNetObj.gameObject.transform.parent = null;

                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkObject)} #{spawnedNetObj.NetworkObjectId} moved to the root because its parent {nameof(NetworkObject)} #{networkObject.NetworkObjectId} is destroyed");
                    }
                }
            }

            if (!networkObject.IsOwnedByServer && !networkObject.IsPlayerObject && NetworkManager.Singleton.ConnectedClients.TryGetValue(networkObject.OwnerClientId, out NetworkClient networkClient))
            {
                //Someone owns it.
                for (int i = networkClient.OwnedObjects.Count - 1; i > -1; i--)
                {
                    if (networkClient.OwnedObjects[i].NetworkObjectId == networkObject.NetworkObjectId)
                    {
                        networkClient.OwnedObjects.RemoveAt(i);
                    }
                }
            }

            networkObject.IsSpawned = false;
            networkObject.InvokeBehaviourNetworkDespawn();

            if (NetworkManager != null && NetworkManager.IsServer)
            {
                if (NetworkManager.NetworkConfig.RecycleNetworkIds)
                {
                    ReleasedNetworkObjectIds.Enqueue(new ReleasedNetworkId()
                    {
                        NetworkId = networkObject.NetworkObjectId,
                        ReleaseTime = Time.unscaledTime
                    });
                }

                var messageQueueContainer = NetworkManager.MessageQueueContainer;
                if (messageQueueContainer != null)
                {
                    if (networkObject != null)
                    {
                        // As long as we have any remaining clients, then notify of the object being destroy.
                        if (NetworkManager.ConnectedClientsList.Count > 0)
                        {

                            ulong[] clientIds = NetworkManager.ConnectedClientsIds;
                            var context = messageQueueContainer.EnterInternalCommandContext(
                                MessageQueueContainer.MessageType.DestroyObject, NetworkChannel.Internal,
                                clientIds, NetworkUpdateStage.PostLateUpdate);
                            if (context != null)
                            {
                                using (var nonNullContext = (InternalCommandContext)context)
                                {
                                    var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                                    bufferSizeCapture.StartMeasureSegment();

                                    nonNullContext.NetworkWriter.WriteUInt64Packed(networkObject.NetworkObjectId);

                                    var size = bufferSizeCapture.StopMeasureSegment();
                                    NetworkManager.NetworkMetrics.TrackObjectDestroySent(clientIds, networkObject.NetworkObjectId, networkObject.name, size);
                                }
                            }
                        }
                    }
                }
            }

            var gobj = networkObject.gameObject;
            NetworkObject no = gobj.GetComponent<NetworkObject>();
            NetworkManager.InterestManager.RemoveObject(no);

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

            // for some reason, we can get down here and SpawnedObjects for this
            //  networkId will no longer be here, even as we check this at the start
            //  of the function
            if (SpawnedObjects.Remove(networkObject.NetworkObjectId))
            {
                SpawnedObjectsList.Remove(networkObject);
            }
        }
    }
}
