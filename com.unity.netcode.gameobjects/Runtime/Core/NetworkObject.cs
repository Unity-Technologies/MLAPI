using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Netcode.Interest;

using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// A component used to identify that a GameObject in the network
    /// </summary>
    [AddComponentMenu("Netcode/" + nameof(NetworkObject), -99)]
    [DisallowMultipleComponent]

    public sealed class NetworkObject : MonoBehaviour
    {
        public List<InterestNode<NetworkClient, NetworkObject>> InterestNodes = new List<InterestNode<NetworkClient, NetworkObject>>();

        [HideInInspector]
        [SerializeField]
        internal uint GlobalObjectIdHash;

#if UNITY_EDITOR
        // HEAD: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED
        internal uint TempGlobalObjectIdHashOverride = 0;
        // TAIL: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED

        private void OnValidate()
        {
            GenerateGlobalObjectIdHash();
        }

        internal void GenerateGlobalObjectIdHash()
        {
            // HEAD: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED
            if (TempGlobalObjectIdHashOverride != 0)
            {
                GlobalObjectIdHash = TempGlobalObjectIdHashOverride;
                return;
            }
            // TAIL: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED

            // do NOT regenerate GlobalObjectIdHash for NetworkPrefabs while Editor is in PlayMode
            if (UnityEditor.EditorApplication.isPlaying && !string.IsNullOrEmpty(gameObject.scene.name))
            {
                return;
            }

            // do NOT regenerate GlobalObjectIdHash if Editor is transitioning into or out of PlayMode
            if (!UnityEditor.EditorApplication.isPlaying && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var globalObjectIdString = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this).ToString();
            GlobalObjectIdHash = XXHash.Hash32(globalObjectIdString);
        }
#endif

        /// <summary>
        /// Gets the NetworkManager that owns this NetworkObject instance
        /// </summary>
        public NetworkManager NetworkManager => NetworkManagerOwner ?? NetworkManager.Singleton;

        /// <summary>
        /// The NetworkManager that owns this NetworkObject.
        /// This property controls where this NetworkObject belongs.
        /// This property is null by default currently, which means that the above NetworkManager getter will return the Singleton.
        /// In the future this is the path where alternative NetworkManagers should be injected for running multi NetworkManagers
        /// </summary>
        internal NetworkManager NetworkManagerOwner;

        /// <summary>
        /// Gets the unique Id of this object that is synced across the network
        /// </summary>
        public ulong NetworkObjectId { get; internal set; }

        /// <summary>
        /// Gets the ClientId of the owner of this NetworkObject
        /// </summary>
        public ulong OwnerClientId
        {
            get
            {
                if (OwnerClientIdInternal == null)
                {
                    return NetworkManager != null ? NetworkManager.ServerClientId : 0;
                }
                else
                {
                    return OwnerClientIdInternal.Value;
                }
            }
            internal set
            {
                if (NetworkManager != null && value == NetworkManager.ServerClientId)
                {
                    OwnerClientIdInternal = null;
                }
                else
                {
                    OwnerClientIdInternal = value;
                }
            }
        }

        internal ulong? OwnerClientIdInternal = null;

        /// <summary>
        /// If true, the object will always be replicated as root on clients and the parent will be ignored.
        /// </summary>
        public bool AlwaysReplicateAsRoot;

        /// <summary>
        /// Gets if this object is a player object
        /// </summary>
        public bool IsPlayerObject { get; internal set; }

        /// <summary>
        /// Gets if the object is the the personal clients player object
        /// </summary>
        public bool IsLocalPlayer => NetworkManager != null && IsPlayerObject && OwnerClientId == NetworkManager.LocalClientId;

        /// <summary>
        /// Gets if the object is owned by the local player or if the object is the local player object
        /// </summary>
        public bool IsOwner => NetworkManager != null && OwnerClientId == NetworkManager.LocalClientId;

        /// <summary>
        /// Gets Whether or not the object is owned by anyone
        /// </summary>
        public bool IsOwnedByServer => NetworkManager != null && OwnerClientId == NetworkManager.ServerClientId;

        /// <summary>
        /// Gets if the object has yet been spawned across the network
        /// </summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>
        /// Gets if the object is a SceneObject, null if it's not yet spawned but is a scene object.
        /// </summary>
        public bool? IsSceneObject { get; internal set; }

        /// <summary>
        /// Gets whether or not the object should be automatically removed when the scene is unloaded.
        /// </summary>
        public bool DestroyWithScene { get; internal set; }

        /// <summary>
        /// Delegate type for checking visibility
        /// </summary>
        /// <param name="clientId">The clientId to check visibility for</param>
        public delegate bool VisibilityDelegate(ulong clientId);

        /// <summary>
        /// Delegate invoked when the netcode needs to know if the object should be visible to a client, if null it will assume true
        /// </summary>
        public VisibilityDelegate CheckObjectVisibility = null;

        /// <summary>
        /// Delegate type for checking spawn options
        /// </summary>
        /// <param name="clientId">The clientId to check spawn options for</param>
        public delegate bool SpawnDelegate(ulong clientId);

        /// <summary>
        /// Delegate invoked when the netcode needs to know if it should include the transform when spawning the object, if null it will assume true
        /// </summary>
        public SpawnDelegate IncludeTransformWhenSpawning = null;

        /// <summary>
        /// Whether or not to destroy this object if it's owner is destroyed.
        /// If false, the objects ownership will be given to the server.
        /// </summary>
        public bool DontDestroyWithOwner;

        /// <summary>
        /// Whether or not to enable automatic NetworkObject parent synchronization.
        /// </summary>
        public bool AutoObjectParentSync = true;

        public InterestSettings InterestSettingsOverride;
        public InterestSettings InterestSettings
        {
            get
            {
                InterestSettings result = null;
                if (InterestSettingsOverride != null)
                {
                    result = InterestSettingsOverride;
                }
                else if (NetworkManager.InterestSettings != null)
                {
                    result = NetworkManager.InterestSettings;
                }

                return result;
            }
            set => InterestSettingsOverride = value;
        }

        internal readonly HashSet<ulong> Observers = new HashSet<ulong>();

        /// <summary>
        /// Returns Observers enumerator
        /// </summary>
        /// <returns>Observers enumerator</returns>
        public HashSet<ulong>.Enumerator GetObservers()
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            return Observers.GetEnumerator();
        }


        /// <summary>
        /// Whether or not this object is visible to a specific client
        /// </summary>
        /// <param name="clientId">The clientId of the client</param>
        /// <returns>True if the client knows about the object</returns>
        public bool IsNetworkVisibleTo(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            return Observers.Contains(clientId);
        }

        private void Awake()
        {
            SetCachedParent(transform.parent);
        }

        /// <summary>
        /// Shows a previously hidden <see cref="NetworkObject"/> to a client
        /// </summary>
        /// <param name="clientId">The client to show the <see cref="NetworkObject"/> to</param>
        public void NetworkShow(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (Observers.Contains(clientId))
            {
                throw new VisibilityChangeException("The object is already visible");
            }

            Observers.Add(clientId);

            NetworkManager.SpawnManager.SendSpawnCallForObject(clientId, OwnerClientId, this);
        }

        /// <summary>
        /// Shows a list of previously hidden <see cref="NetworkObject"/>s to a client
        /// </summary>
        /// <param name="networkObjects">The <see cref="NetworkObject"/>s to show</param>
        /// <param name="clientId">The client to show the objects to</param>
        public static void NetworkShow(List<NetworkObject> networkObjects, ulong clientId)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                throw new ArgumentNullException("At least one " + nameof(NetworkObject) + " has to be provided");
            }

            NetworkManager networkManager = networkObjects[0].NetworkManager;

            if (!networkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            // Do the safety loop first to prevent putting the netcode in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                if (!networkObjects[i].IsSpawned)
                {
                    throw new SpawnStateException("Object is not spawned");
                }

                if (networkObjects[i].Observers.Contains(clientId))
                {
                    throw new VisibilityChangeException($"{nameof(NetworkObject)} with NetworkId: {networkObjects[i].NetworkObjectId} is already visible");
                }

                if (networkObjects[i].NetworkManager != networkManager)
                {
                    throw new ArgumentNullException("All " + nameof(NetworkObject) + "s must belong to the same " + nameof(NetworkManager));
                }
            }

            foreach (var networkObject in networkObjects)
            {
                networkObject.NetworkShow(clientId);
            }
        }

        /// <summary>
        /// Hides a object from a specific client
        /// </summary>
        /// <param name="clientId">The client to hide the object for</param>
        public void NetworkHide(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (!Observers.Contains(clientId))
            {
                throw new VisibilityChangeException("The object is already hidden");
            }

            if (clientId == NetworkManager.ServerClientId)
            {
                throw new VisibilityChangeException("Cannot hide an object from the server");
            }


            // Send destroy call
            Observers.Remove(clientId);

            var context = NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.DestroyObject, NetworkChannel.Internal,
                new[] { clientId }, NetworkUpdateStage.PostLateUpdate);
            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    var bufferSizeCapture = new CommandContextSizeCapture(nonNullContext);
                    bufferSizeCapture.StartMeasureSegment();

                    nonNullContext.NetworkWriter.WriteUInt64Packed(NetworkObjectId);

                    var size = bufferSizeCapture.StopMeasureSegment();
                    NetworkManager.NetworkMetrics.TrackObjectDestroySent(clientId, NetworkObjectId, name, size);
                }
            }
        }

        /// <summary>
        /// Hides a list of objects from a client
        /// </summary>
        /// <param name="networkObjects">The objects to hide</param>
        /// <param name="clientId">The client to hide the objects from</param>
        public static void NetworkHide(List<NetworkObject> networkObjects, ulong clientId)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                throw new ArgumentNullException("At least one " + nameof(NetworkObject) + " has to be provided");
            }

            NetworkManager networkManager = networkObjects[0].NetworkManager;

            if (!networkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (clientId == networkManager.ServerClientId)
            {
                throw new VisibilityChangeException("Cannot hide an object from the server");
            }

            // Do the safety loop first to prevent putting the netcode in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                if (!networkObjects[i].IsSpawned)
                {
                    throw new SpawnStateException("Object is not spawned");
                }

                if (!networkObjects[i].Observers.Contains(clientId))
                {
                    throw new VisibilityChangeException($"{nameof(NetworkObject)} with {nameof(NetworkObjectId)}: {networkObjects[i].NetworkObjectId} is already hidden");
                }

                if (networkObjects[i].NetworkManager != networkManager)
                {
                    throw new ArgumentNullException("All " + nameof(NetworkObject) + "s must belong to the same " + nameof(NetworkManager));
                }
            }

            foreach (var networkObject in networkObjects)
            {
                networkObject.NetworkHide(clientId);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager != null && NetworkManager.IsListening && NetworkManager.IsServer == false && IsSpawned)
            {
                throw new NotServerException($"Destroy a spawned {nameof(NetworkObject)} on a non-host client is not valid. Call {nameof(Destroy)} or {nameof(Despawn)} on the server/host instead.");
            }

            if (NetworkManager != null && NetworkManager.SpawnManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out var networkObject))
            {
                NetworkManager.SpawnManager.OnDespawnObject(networkObject, false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SpawnInternal(bool destroyWithScene, ulong? ownerClientId, bool playerObject)
        {
            if (!NetworkManager.IsListening)
            {
                throw new NotListeningException($"{nameof(NetworkManager)} is not listening, start a server or host before spawning objects");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException($"Only server can spawn {nameof(NetworkObject)}s");
            }

            NetworkManager.SpawnManager.SpawnNetworkObjectLocally(this, NetworkManager.SpawnManager.GetNetworkObjectId(), false, playerObject, ownerClientId, null, false, destroyWithScene);

            if (NetworkManager.NetworkConfig.UseSnapshotSpawn)
            {
                SnapshotSpawnCommand command;
                command.NetworkObjectId = NetworkObjectId;
                command.OwnerClientId = OwnerClientId;
                command.IsPlayerObject = IsPlayerObject;
                command.IsSceneObject = (IsSceneObject == null) || IsSceneObject.Value;

                ulong? parent = NetworkManager.SpawnManager.GetSpawnParentId(this);
                if (parent != null)
                {
                    command.ParentNetworkId = parent.Value;
                }
                else
                {
                    // write own network id, when no parents. todo: optimize this.
                    command.ParentNetworkId = command.NetworkObjectId;
                }

                command.GlobalObjectIdHash = HostCheckForGlobalObjectIdHashOverride();
                // todo: check if (IncludeTransformWhenSpawning == null || IncludeTransformWhenSpawning(clientId)) for any clientId
                command.ObjectPosition = transform.position;
                command.ObjectRotation = transform.rotation;
                command.ObjectScale = transform.localScale;
                command.TickWritten = 0; // will be reset in Spawn
                command.TargetClientIds = default;

                NetworkManager.SnapshotSystem.Spawn(command);
            }

            ulong ownerId = ownerClientId != null ? ownerClientId.Value : NetworkManager.ServerClientId;
            for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
            {
                if (Observers.Contains(NetworkManager.ConnectedClientsList[i].ClientId))
                {
                    NetworkManager.SpawnManager.SendSpawnCallForObject(NetworkManager.ConnectedClientsList[i].ClientId, ownerId, this);
                }
            }
        }

        /// <summary>
        /// Spawns this <see cref="NetworkObject"/> across the network. Can only be called from the Server
        /// </summary>
        /// <param name="destroyWithScene">Should the object be destroyed when the scene is changed</param>
        public void Spawn(bool destroyWithScene = false)
        {
            SpawnInternal(destroyWithScene, null, false);
        }

        /// <summary>
        /// Spawns a <see cref="NetworkObject"/> across the network with a given owner. Can only be called from server
        /// </summary>
        /// <param name="clientId">The clientId to own the object</param>
        /// <param name="destroyWithScene">Should the object be destroyed when the scene is changed</param>
        public void SpawnWithOwnership(ulong clientId, bool destroyWithScene = false)
        {
            SpawnInternal(destroyWithScene, clientId, false);
        }

        /// <summary>
        /// Spawns a <see cref="NetworkObject"/> across the network and makes it the player object for the given client
        /// </summary>
        /// <param name="clientId">The clientId whos player object this is</param>
        /// <param name="destroyWithScene">Should the object be destroyd when the scene is changed</param>
        public void SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false)
        {
            SpawnInternal(destroyWithScene, clientId, true);
        }

        /// <summary>
        /// Despawns this GameObject and destroys it for other clients. This should be used if the object should be kept on the server
        /// </summary>
        public void Despawn(bool destroy = false)
        {
            NetworkManager.SpawnManager.DespawnObject(this, destroy);
        }


        /// <summary>
        /// Removes all ownership of an object from any client. Can only be called from server
        /// </summary>
        public void RemoveOwnership()
        {
            NetworkManager.SpawnManager.RemoveOwnership(this);
        }

        /// <summary>
        /// Changes the owner of the object. Can only be called from server
        /// </summary>
        /// <param name="newOwnerClientId">The new owner clientId</param>
        public void ChangeOwnership(ulong newOwnerClientId)
        {
            NetworkManager.SpawnManager.ChangeOwnership(this, newOwnerClientId);
        }

        internal void InvokeBehaviourOnLostOwnership()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].OnLostOwnership();
            }
        }

        internal void InvokeBehaviourOnGainedOwnership()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].OnGainedOwnership();
            }
        }

        internal void InvokeBehaviourOnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].OnNetworkObjectParentChanged(parentNetworkObject);
            }
        }

        private bool m_IsReparented; // Did initial parent (came from the scene hierarchy) change at runtime?
        private ulong? m_LatestParent; // What is our last set parent NetworkObject's ID?
        private Transform m_CachedParent; // What is our last set parent Transform reference?

        internal void SetCachedParent(Transform parentTransform)
        {
            m_CachedParent = parentTransform;
        }

        internal static void WriteNetworkParenting(NetworkWriter writer, bool isReparented, ulong? latestParent)
        {
            writer.WriteBool(isReparented);
            if (isReparented)
            {
                var isLatestParentSet = latestParent != null && latestParent.HasValue;
                writer.WriteBool(isLatestParentSet);
                if (isLatestParentSet)
                {
                    writer.WriteUInt64Packed(latestParent.Value);
                }
            }
        }

        internal static (bool IsReparented, ulong? LatestParent) ReadNetworkParenting(NetworkReader reader)
        {
            ulong? latestParent = null;
            bool isReparented = reader.ReadBool();
            if (isReparented)
            {
                var isLatestParentSet = reader.ReadBool();
                if (isLatestParentSet)
                {
                    latestParent = reader.ReadUInt64Packed();
                }
            }

            return (isReparented, latestParent);
        }

        internal (bool IsReparented, ulong? LatestParent) GetNetworkParenting() => (m_IsReparented, m_LatestParent);

        internal void SetNetworkParenting(bool isReparented, ulong? latestParent)
        {
            m_IsReparented = isReparented;
            m_LatestParent = latestParent;
        }

        public bool TrySetParent(Transform parent, bool worldPositionStays = true)
        {
            return TrySetParent(parent.GetComponent<NetworkObject>(), worldPositionStays);
        }

        public bool TrySetParent(GameObject parent, bool worldPositionStays = true)
        {
            return TrySetParent(parent.GetComponent<NetworkObject>(), worldPositionStays);
        }

        public bool TrySetParent(NetworkObject parent, bool worldPositionStays = true)
        {
            if (!AutoObjectParentSync)
            {
                return false;
            }

            if (NetworkManager == null || !NetworkManager.IsListening)
            {
                return false;
            }

            if (!NetworkManager.IsServer)
            {
                return false;
            }

            if (!IsSpawned)
            {
                return false;
            }

            if (parent == null)
            {
                return false;
            }

            if (!parent.IsSpawned)
            {
                return false;
            }

            transform.SetParent(parent.transform, worldPositionStays);
            return true;
        }

        private void OnTransformParentChanged()
        {
            if (!AutoObjectParentSync)
            {
                return;
            }

            if (transform.parent == m_CachedParent)
            {
                return;
            }

            if (NetworkManager == null || !NetworkManager.IsListening)
            {
                transform.parent = m_CachedParent;
                Debug.LogException(new NotListeningException($"{nameof(NetworkManager)} is not listening, start a server or host before reparenting"));
                return;
            }

            if (!NetworkManager.IsServer)
            {
                transform.parent = m_CachedParent;
                Debug.LogException(new NotServerException($"Only the server can reparent {nameof(NetworkObject)}s"));
                return;
            }

            if (!IsSpawned)
            {
                transform.parent = m_CachedParent;
                Debug.LogException(new SpawnStateException($"{nameof(NetworkObject)} can only be reparented after being spawned"));
                return;
            }

            var parentTransform = transform.parent;
            if (parentTransform != null)
            {
                var parentObject = transform.parent.GetComponent<NetworkObject>();
                if (parentObject == null)
                {
                    transform.parent = m_CachedParent;
                    Debug.LogException(new InvalidParentException($"Invalid parenting, {nameof(NetworkObject)} moved under a non-{nameof(NetworkObject)} parent"));
                    return;
                }

                if (!parentObject.IsSpawned)
                {
                    transform.parent = m_CachedParent;
                    Debug.LogException(new SpawnStateException($"{nameof(NetworkObject)} can only be reparented under another spawned {nameof(NetworkObject)}"));
                    return;
                }

                m_LatestParent = parentObject.NetworkObjectId;
            }
            else
            {
                m_LatestParent = null;
            }

            m_IsReparented = true;
            ApplyNetworkParenting();

            var context = NetworkManager.MessageQueueContainer.EnterInternalCommandContext(
                MessageQueueContainer.MessageType.ParentSync, NetworkChannel.Internal,
                NetworkManager.ConnectedClientsIds.Where((id) => Observers.Contains(id)).ToArray(),
                NetworkUpdateLoop.UpdateStage);

            if (context != null)
            {
                using (var nonNullContext = (InternalCommandContext)context)
                {
                    nonNullContext.NetworkWriter.WriteUInt64Packed(NetworkObjectId);
                    WriteNetworkParenting(nonNullContext.NetworkWriter, m_IsReparented, m_LatestParent);
                }
            }
        }

        // We're keeping this set called OrphanChildren which contains NetworkObjects
        // because at the time we initialize/spawn NetworkObject locally, we might not have its parent replicated from the other side
        //
        // For instance, if we're spawning NetworkObject 5 and its parent is 10, what should happen if we do not have 10 yet?
        // let's say 10 is on the way to be replicated in a few frames and we could fix that parent-child relationship later.
        //
        // If you couldn't find your parent, we put you into OrphanChildren set and everytime we spawn another NetworkObject locally due to replication,
        // we call CheckOrphanChildren() method and quickly iterate over OrphanChildren set and see if we can reparent/adopt one.
        internal static HashSet<NetworkObject> OrphanChildren = new HashSet<NetworkObject>();

        internal bool ApplyNetworkParenting()
        {
            if (!AutoObjectParentSync)
            {
                return false;
            }

            if (!IsSpawned)
            {
                return false;
            }

            if (!m_IsReparented)
            {
                return true;
            }

            if (m_LatestParent == null || !m_LatestParent.HasValue)
            {
                m_CachedParent = null;
                transform.parent = null;

                InvokeBehaviourOnNetworkObjectParentChanged(null);
                return true;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(m_LatestParent.Value))
            {
                if (OrphanChildren.Add(this))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkObject)} ({name}) cannot find its parent, added to {nameof(OrphanChildren)} set");
                    }
                }
                return false;
            }

            var parentObject = NetworkManager.SpawnManager.SpawnedObjects[m_LatestParent.Value];

            m_CachedParent = parentObject.transform;
            transform.parent = parentObject.transform;

            InvokeBehaviourOnNetworkObjectParentChanged(parentObject);
            return true;
        }

        internal static void CheckOrphanChildren()
        {
            var objectsToRemove = new List<NetworkObject>();
            foreach (var orphanObject in OrphanChildren)
            {
                if (orphanObject.ApplyNetworkParenting())
                {
                    objectsToRemove.Add(orphanObject);
                }
            }
            foreach (var networkObject in objectsToRemove)
            {
                OrphanChildren.Remove(networkObject);
            }
        }

        internal void InvokeBehaviourNetworkSpawn()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InternalOnNetworkSpawn();
                ChildNetworkBehaviours[i].OnNetworkSpawn();
            }
        }

        internal void InvokeBehaviourNetworkDespawn()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InternalOnNetworkDespawn();
                ChildNetworkBehaviours[i].OnNetworkDespawn();
            }
        }

        private List<NetworkBehaviour> m_ChildNetworkBehaviours;

        internal List<NetworkBehaviour> ChildNetworkBehaviours
        {
            get
            {
                if (m_ChildNetworkBehaviours != null)
                {
                    return m_ChildNetworkBehaviours;
                }

                m_ChildNetworkBehaviours = new List<NetworkBehaviour>();
                var networkBehaviours = GetComponentsInChildren<NetworkBehaviour>(true);
                for (int i = 0; i < networkBehaviours.Length; i++)
                {
                    if (networkBehaviours[i].NetworkObject == this)
                    {
                        m_ChildNetworkBehaviours.Add(networkBehaviours[i]);
                    }
                }

                return m_ChildNetworkBehaviours;
            }
        }

        internal void WriteNetworkVariableData(Stream stream, ulong clientId)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InitializeVariables();
                NetworkBehaviour.WriteNetworkVariableData(ChildNetworkBehaviours[i].NetworkVariableFields, stream, clientId, NetworkManager);
            }
        }

        internal void SetNetworkVariableData(Stream stream)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InitializeVariables();
                NetworkBehaviour.SetNetworkVariableData(ChildNetworkBehaviours[i].NetworkVariableFields, stream, NetworkManager);
            }
        }

        internal ushort GetNetworkBehaviourOrderIndex(NetworkBehaviour instance)
        {
            // read the cached index, and verify it first
            if (instance.NetworkBehaviourIdCache < ChildNetworkBehaviours.Count)
            {
                if (ChildNetworkBehaviours[instance.NetworkBehaviourIdCache] == instance)
                {
                    return instance.NetworkBehaviourIdCache;
                }

                // invalid cached id reset
                instance.NetworkBehaviourIdCache = default;
            }

            for (ushort i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i] == instance)
                {
                    // cache the id, for next query
                    instance.NetworkBehaviourIdCache = i;
                    return i;
                }
            }

            return 0;
        }

        internal NetworkBehaviour GetNetworkBehaviourAtOrderIndex(ushort index)
        {
            if (index >= ChildNetworkBehaviours.Count)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError($"Behaviour index was out of bounds. Did you mess up the order of your {nameof(NetworkBehaviour)}s?");
                }

                return null;
            }

            return ChildNetworkBehaviours[index];
        }

        /// <summary>
        /// Used to serialize a NetworkObjects during scene synchronization that occurs
        /// upon a client being approved or a scene transition.
        /// </summary>
        /// <param name="writer">writer into the outbound stream</param>
        /// <param name="targetClientId">clientid we are targeting</param>
        internal void SerializeSceneObject(NetworkWriter writer, ulong targetClientId)
        {
            writer.WriteBool(IsPlayerObject);
            writer.WriteUInt64Packed(NetworkObjectId);
            writer.WriteUInt64Packed(OwnerClientId);

            NetworkObject parentNetworkObject = null;

            if (!AlwaysReplicateAsRoot && transform.parent != null)
            {
                parentNetworkObject = transform.parent.GetComponent<NetworkObject>();
            }

            if (parentNetworkObject == null)
            {
                // We don't have a parent
                writer.WriteBool(false);
            }
            else
            {
                // We do have a parent
                writer.WriteBool(true);
                // Write the parent's NetworkObjectId to be used for linking back to the child
                writer.WriteUInt64Packed(parentNetworkObject.NetworkObjectId);
            }

            // Write if we are a scene object or not
            writer.WriteBool(IsSceneObject ?? true);

            // Write the hash for this NetworkObject
            writer.WriteUInt32Packed(HostCheckForGlobalObjectIdHashOverride());

            if (IncludeTransformWhenSpawning == null || IncludeTransformWhenSpawning(OwnerClientId))
            {
                // Set the position and rotation data marker to true (i.e. flag to know, when reading from the stream, that position and rotation data follows).
                writer.WriteBool(true);

                // Write position
                writer.WriteSinglePacked(transform.position.x);
                writer.WriteSinglePacked(transform.position.y);
                writer.WriteSinglePacked(transform.position.z);

                // Write rotation
                writer.WriteSinglePacked(transform.rotation.eulerAngles.x);
                writer.WriteSinglePacked(transform.rotation.eulerAngles.y);
                writer.WriteSinglePacked(transform.rotation.eulerAngles.z);
            }
            else
            {
                // Set the position and rotation data marker to false (i.e. flag to know, when reading from the stream, that position and rotation data *was not included*)
                writer.WriteBool(false);
            }

            {
                var (isReparented, latestParent) = GetNetworkParenting();
                WriteNetworkParenting(writer, isReparented, latestParent);
            }

            // Write whether we are including network variable data
            writer.WriteBool(NetworkManager.NetworkConfig.EnableNetworkVariable);

            //If we are including NetworkVariable data
            if (NetworkManager.NetworkConfig.EnableNetworkVariable)
            {
                var buffer = writer.GetStream() as NetworkBuffer;

                // Write placeholder size, NOT as a packed value, initially as zero (i.e. we do not know how much NetworkVariable data will be written yet)
                writer.WriteUInt32(0);

                // Mark our current position before we potentially write any NetworkVariable data
                var positionBeforeNetworkVariableData = buffer.Position;

                // Write network variable data
                WriteNetworkVariableData(buffer, targetClientId);

                // If our current buffer position is greater than our positionBeforeNetworkVariableData then we wrote NetworkVariable data
                // Part 1: This will include the total NetworkVariable data size, if there was NetworkVariable data written, to the stream
                // in order to be able to skip past this entry on the deserialization side in the event this NetworkObject fails to be
                // constructed (See Part 2 below in the DeserializeSceneObject method)
                if (buffer.Position > positionBeforeNetworkVariableData)
                {
                    // Store our current stream buffer position
                    var endOfNetworkVariableData = buffer.Position;

                    // Calculate the total NetworkVariable data size written
                    var networkVariableDataSize = endOfNetworkVariableData - positionBeforeNetworkVariableData;

                    // Move the stream position back to just before we wrote our size (we include the unpacked UInt32 data size placeholder)
                    buffer.Position = positionBeforeNetworkVariableData - sizeof(uint);

                    // Now write the actual data size written into our unpacked UInt32 placeholder position
                    writer.WriteUInt32((uint)(networkVariableDataSize));

                    // Finally, revert the buffer position back to the end of the network variable data written
                    buffer.Position = endOfNetworkVariableData;
                }
            }
        }

        /// <summary>
        /// Used to deserialize a serialized scene object which occurs
        /// when the client is approved or during a scene transition
        /// </summary>
        /// <param name="objectStream">inbound stream</param>
        /// <param name="reader">reader for the stream</param>
        /// <param name="networkManager">NetworkManager instance</param>
        /// <returns>optional to use NetworkObject deserialized</returns>
        internal static NetworkObject DeserializeSceneObject(NetworkBuffer objectStream, NetworkReader reader, NetworkManager networkManager)
        {
            var isPlayerObject = reader.ReadBool();
            var networkId = reader.ReadUInt64Packed();
            var ownerClientId = reader.ReadUInt64Packed();
            var hasParent = reader.ReadBool();
            ulong? parentNetworkId = null;

            if (hasParent)
            {
                parentNetworkId = reader.ReadUInt32Packed();
            }

            bool isSceneObject = reader.ReadBool();

            uint globalObjectIdHash = reader.ReadUInt32Packed();
            Vector3? position = null;
            Quaternion? rotation = null;

            // Check to see if we have position and rotation values that follows
            if (reader.ReadBool())
            {
                position = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                rotation = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
            }

            var (isReparented, latestParent) = ReadNetworkParenting(reader);

            //Attempt to create a local NetworkObject
            var networkObject = networkManager.SpawnManager.CreateLocalNetworkObject(isSceneObject, globalObjectIdHash, ownerClientId, parentNetworkId, position, rotation, isReparented);

            networkObject?.SetNetworkParenting(isReparented, latestParent);

            // Determine if this NetworkObject has NetworkVariable data to read
            var networkVariableDataIsIncluded = reader.ReadBool();

            if (networkVariableDataIsIncluded)
            {
                // (See Part 1 above in the NetworkObject.SerializeSceneObject method to better understand this)
                // Part 2: This makes sure that if one NetworkObject fails to construct (for whatever reason) then we can "skip past"
                // that specific NetworkObject but continue processing any remaining serialized NetworkObjects as opposed to just
                // throwing an exception and skipping the remaining (if any) NetworkObjects.  This will prevent one misconfigured
                // issue (or more) from breaking the entire loading process.
                var networkVariableDataSize = reader.ReadUInt32();
                if (networkObject == null)
                {
                    // Log the error that the NetworkObject failed to construct
                    Debug.LogError($"Failed to spawn {nameof(NetworkObject)} for Hash {globalObjectIdHash}.");

                    // If we failed to load this NetworkObject, then skip past the network variable data
                    objectStream.Position += networkVariableDataSize;

                    // We have nothing left to do here.
                    return null;
                }
            }

            // Spawn the NetworkObject
            networkManager.SpawnManager.SpawnNetworkObjectLocally(networkObject, networkId, isSceneObject, isPlayerObject, ownerClientId, objectStream, true, false);

            return networkObject;
        }

        /// <summary>
        /// Only applies to Host mode.
        /// Will return the registered source NetworkPrefab's GlobalObjectIdHash if one exists.
        /// Server and Clients will always return the NetworkObject's GlobalObjectIdHash.
        /// </summary>
        /// <returns></returns>
        internal uint HostCheckForGlobalObjectIdHashOverride()
        {
            if (NetworkManager.IsHost)
            {
                if (NetworkManager.PrefabHandler.ContainsHandler(this))
                {
                    var globalObjectIdHash = NetworkManager.PrefabHandler.GetSourceGlobalObjectIdHash(GlobalObjectIdHash);
                    return globalObjectIdHash == 0 ? GlobalObjectIdHash : globalObjectIdHash;
                }
                else
                if (NetworkManager.NetworkConfig.OverrideToNetworkPrefab.ContainsKey(GlobalObjectIdHash))
                {
                    return NetworkManager.NetworkConfig.OverrideToNetworkPrefab[GlobalObjectIdHash];
                }
            }

            return GlobalObjectIdHash;
        }

        // Trigger the Interest system to do an update sweep on any Interest nodes
        //  I am associated with
        public void UpdateInterest()
        {
            foreach (var node in InterestNodes)
            {
                node?.UpdateObject(this);
            }
        }
    }
}
