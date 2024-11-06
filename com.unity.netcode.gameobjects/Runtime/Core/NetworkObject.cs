using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Netcode.Components;
#if UNITY_EDITOR
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
#endif
using UnityEngine;
using UnityEngine.SceneManagement;



namespace Unity.Netcode
{
    /// <summary>
    /// A component used to identify that a GameObject in the network
    /// </summary>
    [AddComponentMenu("Netcode/Network Object", -99)]
    [DisallowMultipleComponent]
    public sealed class NetworkObject : MonoBehaviour
    {
        [HideInInspector]
        [SerializeField]
        internal uint GlobalObjectIdHash;

        /// <summary>
        /// Used to track the source GlobalObjectIdHash value of the associated network prefab.
        /// When an override exists or it is in-scene placed, GlobalObjectIdHash and PrefabGlobalObjectIdHash
        /// will be different. The PrefabGlobalObjectIdHash value is what is used when sending a <see cref="CreateObjectMessage"/>.
        /// </summary>
        internal uint PrefabGlobalObjectIdHash;

        /// <summary>
        /// This is the source prefab of an in-scene placed NetworkObject. This is not set for in-scene
        /// placd NetworkObjects that are not prefab instances, dynamically spawned prefab instances,
        /// or for network prefab assets.
        /// </summary>
        [HideInInspector]
        [SerializeField]
        internal uint InScenePlacedSourceGlobalObjectIdHash;

        /// <summary>
        /// Gets the Prefab Hash Id of this object if the object is registerd as a prefab otherwise it returns 0
        /// </summary>
        [HideInInspector]
        public uint PrefabIdHash
        {
            get
            {
                return GlobalObjectIdHash;
            }
        }

        /// <summary>
        /// All <see cref="NetworkTransform"></see> component instances associated with a <see cref="NetworkObject"/> component instance.
        /// </summary>
        /// <remarks>
        /// When parented, all child <see cref="NetworkTransform"/> component instances under a <see cref="NetworkObject"/> component instance that do not have
        /// another <see cref="NetworkObject"/> component instance will be associated with the initial component instance. This list does not contain any parented
        /// children <see cref="NetworkObject"/> instances with one or more <see cref="NetworkTransform"/> component instance(s).
        /// </remarks>
        public List<NetworkTransform> NetworkTransforms { get; private set; }


#if COM_UNITY_MODULES_PHYSICS
        /// <summary>
        /// All <see cref="NetworkRigidbodyBase"></see> component instances associated with a <see cref="NetworkObject"/> component instance.
        /// NOTE: This is only available if a physics package is included. If not, then this will not be available!
        /// </summary>
        /// <remarks>
        /// When parented, all child <see cref="NetworkRigidbodyBase"/> component instances under a <see cref="NetworkObject"/> component instance that do not have
        /// another <see cref="NetworkObject"/> component instance will be associated with the initial component instance. This list does not contain any parented
        /// child <see cref="NetworkObject"/> instances with one or more <see cref="NetworkTransform"/> component instance(s).
        /// </remarks>
        public List<NetworkRigidbodyBase> NetworkRigidbodies { get; private set; }
#endif
        /// <summary>
        /// The current parent <see cref="NetworkObject"/> component instance to this <see cref="NetworkObject"/> component instance. When there is no parent then
        /// this will be <see cref="null"/>.
        /// </summary>
        public NetworkObject CurrentParent { get; private set; }

#if UNITY_EDITOR
        private const string k_GlobalIdTemplate = "GlobalObjectId_V1-{0}-{1}-{2}-{3}";

        /// <summary>
        /// Object Types <see href="https://docs.unity3d.com/ScriptReference/GlobalObjectId.html"/>
        /// Parameter 0 of <see cref="k_GlobalIdTemplate"/>
        /// </summary>
        // 0 = Null (when considered a null object type we can ignore)
        // 1 = Imported Asset
        // 2 = Scene Object
        // 3 = Source Asset.
        private const int k_NullObjectType = 0;
        private const int k_ImportedAssetObjectType = 1;
        private const int k_SceneObjectType = 2;
        private const int k_SourceAssetObjectType = 3;

        [ContextMenu("Refresh In-Scene Prefab Instances")]
        internal void RefreshAllPrefabInstances()
        {
            var instanceGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(this);
            if (!PrefabUtility.IsPartOfAnyPrefab(this) || instanceGlobalId.identifierType != k_ImportedAssetObjectType)
            {
                EditorUtility.DisplayDialog("Network Prefab Assets Only", "This action can only be performed on a network prefab asset.", "Ok");
                return;
            }

            // Handle updating the currently active scene
            NetworkObjectRefreshTool.ProcessActiveScene();

            // Refresh all build settings scenes
            var activeScene = SceneManager.GetActiveScene();
            foreach (var editorScene in EditorBuildSettings.scenes)
            {
                // skip disabled scenes and the currently active scene
                if (!editorScene.enabled || activeScene.path == editorScene.path)
                {
                    continue;
                }
                // Add the scene to be processed
                NetworkObjectRefreshTool.ProcessScene(editorScene.path, true);
            }

            // Process all added scenes
            NetworkObjectRefreshTool.ProcessScenes();
        }

        internal void OnValidate()
        {
            // do NOT regenerate GlobalObjectIdHash for NetworkPrefabs while Editor is in PlayMode
            if (EditorApplication.isPlaying && !string.IsNullOrEmpty(gameObject.scene.name))
            {
                return;
            }

            // do NOT regenerate GlobalObjectIdHash if Editor is transitioning into or out of PlayMode
            if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Get a global object identifier for this network prefab
            var globalId = GetGlobalId();


            // if the identifier type is 0, then don't update the GlobalObjectIdHash
            if (globalId.identifierType == k_NullObjectType)
            {
                return;
            }

            var oldValue = GlobalObjectIdHash;
            GlobalObjectIdHash = globalId.ToString().Hash32();

            // If the GlobalObjectIdHash value changed, then mark the asset dirty
            if (GlobalObjectIdHash != oldValue)
            {
                // Check if this is an in-scnee placed NetworkObject (Special Case for In-Scene Placed)
                if (!IsEditingPrefab() && gameObject.scene.name != null && gameObject.scene.name != gameObject.name)
                {
                    // Sanity check to make sure this is a scene placed object
                    if (globalId.identifierType != k_SceneObjectType)
                    {
                        // This should never happen, but in the event it does throw and error
                        Debug.LogError($"[{gameObject.name}] is detected as an in-scene placed object but its identifier is of type {globalId.identifierType}! **Report this error**");
                    }

                    // If this is a prefab instance
                    if (PrefabUtility.IsPartOfAnyPrefab(this))
                    {
                        // We must invoke this in order for the modifications to get saved with the scene (does not mark scene as dirty)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                    }
                }
                else // Otherwise, this is a standard network prefab asset so we just mark it dirty for the AssetDatabase to update it
                {
                    EditorUtility.SetDirty(this);
                }
            }

            // Always check for in-scene placed to assure any previous version scene assets with in-scene place NetworkObjects gets updated
            CheckForInScenePlaced();
        }

        private bool IsEditingPrefab()
        {
            // Check if we are directly editing the prefab
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);

            // if we are not editing the prefab directly (or a sub-prefab), then return the object identifier
            if (stage == null || stage.assetPath == null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// This checks to see if this NetworkObject is an in-scene placed prefab instance. If so it will
        /// automatically find the source prefab asset's GlobalObjectIdHash value, assign it to
        /// InScenePlacedSourceGlobalObjectIdHash and mark this as being in-scene placed.
        /// </summary>
        /// <remarks>
        /// This NetworkObject is considered an in-scene placed prefab asset instance if it is:
        /// - Part of a prefab
        /// - Not being directly edited
        /// - Within a valid scene that is part of the scenes in build list
        /// (In-scene defined NetworkObjects that are not part of a prefab instance are excluded.)
        /// </remarks>
        private void CheckForInScenePlaced()
        {
            if (PrefabUtility.IsPartOfAnyPrefab(this) && !IsEditingPrefab() && gameObject.scene.IsValid() && gameObject.scene.isLoaded && gameObject.scene.buildIndex >= 0)
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                var assetPath = AssetDatabase.GetAssetPath(prefab);
                var sourceAsset = AssetDatabase.LoadAssetAtPath<NetworkObject>(assetPath);
                if (sourceAsset != null && sourceAsset.GlobalObjectIdHash != 0 && InScenePlacedSourceGlobalObjectIdHash != sourceAsset.GlobalObjectIdHash)
                {
                    InScenePlacedSourceGlobalObjectIdHash = sourceAsset.GlobalObjectIdHash;
                    EditorUtility.SetDirty(this);
                }
                IsSceneObject = true;
            }
        }

        private GlobalObjectId GetGlobalId()
        {
            var instanceGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(this);

            // If not editing a prefab, then just use the generated id
            if (!IsEditingPrefab())
            {
                return instanceGlobalId;
            }

            // If the asset doesn't exist at the given path, then return the object identifier
            var prefabStageAssetPath = PrefabStageUtility.GetPrefabStage(gameObject).assetPath;
            // If (for some reason) the asset path is null return the generated id
            if (prefabStageAssetPath == null)
            {
                return instanceGlobalId;
            }

            var theAsset = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabStageAssetPath);
            // If there is no asset at that path (for some odd/edge case reason), return the generated id
            if (theAsset == null)
            {
                return instanceGlobalId;
            }

            // If we can't get the asset GUID and/or the file identifier, then return the object identifier
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(theAsset, out var guid, out long localFileId))
            {
                return instanceGlobalId;
            }

            // Note: If we reached this point, then we are most likely opening a prefab to edit.
            // The instanceGlobalId will be constructed as if it is a scene object, however when it
            // is serialized its value will be treated as a file asset (the "why" to the below code).

            // Construct an imported asset identifier with the type being a source asset object type
            var prefabGlobalIdText = string.Format(k_GlobalIdTemplate, k_SourceAssetObjectType, guid, (ulong)localFileId, 0);

            // If we can't parse the result log an error and return the instanceGlobalId
            if (!GlobalObjectId.TryParse(prefabGlobalIdText, out var prefabGlobalId))
            {
                Debug.LogError($"[GlobalObjectId Gen] Failed to parse ({prefabGlobalIdText}) returning default ({instanceGlobalId})! ** Please Report This Error **");
                return instanceGlobalId;
            }

            // Otherwise, return the constructed identifier for the source prefab asset
            return prefabGlobalId;
        }
#endif // UNITY_EDITOR

        /// <summary>
        /// Gets the NetworkManager that owns this NetworkObject instance
        /// </summary>
        public NetworkManager NetworkManager => NetworkManagerOwner ? NetworkManagerOwner : NetworkManager.Singleton;

        /// <summary>
        /// Useful to know if we should or should not send a message
        /// </summary>
        internal bool HasRemoteObservers => !(Observers.Count() == 0 || (Observers.Contains(NetworkManager.LocalClientId) && Observers.Count() == 1));

        /// <summary>
        /// Distributed Authority Mode Only
        /// When set, NetworkObjects despawned remotely will be delayed until the tick count specified is reached on all non-owner instances.
        /// It will still despawn immediately on the owner-local side.
        /// </summary>
        [HideInInspector]
        public int DeferredDespawnTick;

        /// <summary>
        /// Distributed Authority Mode Only
        /// The delegate handler declaration for <see cref="OnDeferedDespawnComplete"/>.
        /// </summary>
        /// <returns>true (despawn) or false (do not despawn)</returns>
        public delegate bool OnDeferedDespawnCompleteDelegateHandler();

        /// <summary>
        /// If assigned, this callback will be invoked each frame update to determine if a <see cref="NetworkObject"/> that has had its despawn deferred
        /// should despawn. Use this callback to handle scenarios where you might have additional changes in state that could vindicate despawning earlier
        /// than the deferred despawn targeted future network tick.
        /// </summary>
        public OnDeferedDespawnCompleteDelegateHandler OnDeferredDespawnComplete;

        /// <summary>
        /// Distributed Authority Mode Only
        /// When invoked by the authority of the <see cref="NetworkObject"/>, this will locally despawn the <see cref="NetworkObject"/> while
        /// sending a delayed despawn to all non-authority instances. The tick offset + the authority's current known network tick (ServerTime.Tick)
        /// is when non-authority instances will despawn this <see cref="NetworkObject"/> instance.
        /// </summary>
        /// <param name="tickOffset">The number of ticks from the authority's currently known <see cref="NetworkManager.ServerTime.Tick"/> to delay the despawn.</param>
        /// <param name="destroy">Defaults to true, determines whether the <see cref="NetworkObject"/> will be destroyed.</param>
        public void DeferDespawn(int tickOffset, bool destroy = true)
        {
            if (!NetworkManager.DistributedAuthorityMode)
            {
                NetworkLog.LogError($"This method is only available in distributed authority mode.");
                return;
            }

            if (!IsSpawned)
            {
                NetworkLog.LogError($"Cannot defer despawning {name} because it is not spawned!");
                return;
            }

            if (!HasAuthority)
            {
                NetworkLog.LogError($"Only the authority can invoke {nameof(DeferDespawn)} and local Client-{NetworkManager.LocalClientId} is not the authority of {name}!");
                return;
            }

            // Apply the relative tick offset for when this NetworkObject should be despawned on
            // non-authoritative instances.
            DeferredDespawnTick = NetworkManager.ServerTime.Tick + tickOffset;

            var connectionManager = NetworkManager.ConnectionManager;

            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].PreVariableUpdate();
                // Notify all NetworkBehaviours that the authority is performing a deferred despawn.
                // This is when user script would update NetworkVariable states that might be needed
                // for the deferred despawn sequence on non-authoritative instances.
                ChildNetworkBehaviours[i].OnDeferringDespawn(DeferredDespawnTick);
            }

            // DAHost handles sending updates to all clients
            if (NetworkManager.DAHost)
            {
                for (int i = 0; i < connectionManager.ConnectedClientsList.Count; i++)
                {
                    var client = connectionManager.ConnectedClientsList[i];
                    if (IsNetworkVisibleTo(client.ClientId))
                    {
                        // Sync just the variables for just the objects this client sees
                        for (int k = 0; k < ChildNetworkBehaviours.Count; k++)
                        {
                            ChildNetworkBehaviours[k].NetworkVariableUpdate(client.ClientId);
                        }
                    }
                }
            }
            else // Clients just send their deltas to the service or DAHost
            {
                for (int k = 0; k < ChildNetworkBehaviours.Count; k++)
                {
                    ChildNetworkBehaviours[k].NetworkVariableUpdate(NetworkManager.ServerClientId);
                }
            }

            // Now despawn the local authority instance
            Despawn(destroy);
        }

        /// <summary>
        /// When enabled, NetworkObject ownership is distributed amongst clients.
        /// To set <see cref="OwnershipStatus.Distributable"/> during runtime, use <see cref="SetOwnershipStatus(OwnershipStatus, bool, OwnershipLockActions)"/>
        /// </summary>
        /// <remarks>
        /// Scenarios of interest:
        /// - If the <see cref="NetworkObject"/> is locked and the current owner is still connected, then it will not be redistributed upon a new client joining.
        /// - If the <see cref="NetworkObject"/> has an ownership request in progress, then it will not be redistributed upon a new client joining.
        /// - If the <see cref="NetworkObject"/> is locked but the owner is not longer connected, then it will be redistributed.
        /// - If the <see cref="NetworkObject"/> has an ownership request in progress but the target client is no longer connected, then it will be redistributed.
        /// </remarks>
        public bool IsOwnershipDistributable => Ownership.HasFlag(OwnershipStatus.Distributable);

        /// <summary>
        /// Returns true if the <see cref="NetworkObject"/> is has ownership locked.
        /// When locked, the <see cref="NetworkObject"/> cannot be redistributed nor can it be transferred by another client.
        /// To toggle the ownership loked status during runtime, use <see cref="SetOwnershipLock(bool)"/>.
        /// </summary>
        public bool IsOwnershipLocked => ((OwnershipStatusExtended)Ownership).HasFlag(OwnershipStatusExtended.Locked);

        /// <summary>
        /// When true, the <see cref="NetworkObject"/>'s ownership can be acquired by any non-owner client.
        /// To set <see cref="OwnershipStatus.Transferable"/> during runtime, use <see cref="SetOwnershipStatus(OwnershipStatus, bool, OwnershipLockActions)"/>.
        /// </summary>
        public bool IsOwnershipTransferable => Ownership.HasFlag(OwnershipStatus.Transferable);

        /// <summary>
        /// When true, the <see cref="NetworkObject"/>'s ownership can be acquired through non-owner client requesting ownership.
        /// To set <see cref="OwnershipStatus.Transferable"/> during runtime, use <see cref="SetOwnershipStatus(OwnershipStatus, bool, OwnershipLockActions)"/>
        /// To request ownership, use <see cref="RequestOwnership"/>.
        /// </summary>
        public bool IsOwnershipRequestRequired => Ownership.HasFlag(OwnershipStatus.RequestRequired);

        /// <summary>
        /// When true, the <see cref="NetworkObject"/>'s ownership cannot be acquired because an ownership request is underway.
        /// In order for this status to be applied, the the <see cref="NetworkObject"/> must have the <see cref="OwnershipStatus.RequestRequired"/>
        /// flag set and a non-owner client must have sent a request via <see cref="RequestOwnership"/>.
        /// </summary>
        public bool IsRequestInProgress => ((OwnershipStatusExtended)Ownership).HasFlag(OwnershipStatusExtended.Requested);

        /// <summary>
        /// Determines whether a NetworkObject can be distributed to other clients during
        /// a <see cref="NetworkTopologyTypes.DistributedAuthority"/> session.
        /// </summary>
#if !MULTIPLAYER_SERVICES_SDK_INSTALLED
        [HideInInspector]
#endif
        [SerializeField]
        internal OwnershipStatus Ownership = OwnershipStatus.Distributable;

        /// <summary>
        /// Ownership status flags:
        /// <see cref="None"/>: If nothing is set, then ownership is considered "static" and cannot be redistributed, requested, or transferred (i.e. a Player would have this).
        /// <see cref="Distributable"/>: When set, this instance will be automatically redistributed when a client joins (if not locked or no request is pending) or leaves.
        /// <see cref="Transferable"/>: When set, a non-owner can obtain ownership immediately (without requesting and as long as it is not locked).
        /// <see cref="RequestRequired"/>: When set, When set, a non-owner must request ownership from the owner (will always get locked once ownership is transferred).
        /// </summary>
        // Ranges from 1 to 8 bits
        [Flags]
        public enum OwnershipStatus
        {
            None = 0,
            Distributable = 1 << 0,
            Transferable = 1 << 1,
            RequestRequired = 1 << 2,
        }

        /// <summary>
        /// Intentionally internal
        /// </summary>
        // Ranges from 9 to 16 bits
        [Flags]
        internal enum OwnershipStatusExtended
        {
            // When locked and CanRequest is set, a non-owner can request ownership. If the owner responds by removing the Locked status, then ownership is transferred.
            // If the owner responds by removing the Requested status only, then ownership is denied.
            Requested = (1 << 8),
            Locked = (1 << 9),
        }

        internal bool HasExtendedOwnershipStatus(OwnershipStatusExtended extended)
        {
            var extendedOwnership = (OwnershipStatusExtended)Ownership;
            return extendedOwnership.HasFlag(extended);
        }

        internal void AddOwnershipExtended(OwnershipStatusExtended extended)
        {
            var extendedOwnership = (OwnershipStatusExtended)Ownership;
            extendedOwnership |= extended;
            Ownership = (OwnershipStatus)extendedOwnership;
        }

        internal void RemoveOwnershipExtended(OwnershipStatusExtended extended)
        {
            var extendedOwnership = (OwnershipStatusExtended)Ownership;
            extendedOwnership &= ~extended;
            Ownership = (OwnershipStatus)extendedOwnership;
        }

        /// <summary>
        /// Distributed Authority Only
        /// Locks ownership of a NetworkObject by the current owner.
        /// </summary>
        /// <param name="lockOwnership">defaults to lock (true) or unlock (false)</param>
        /// <returns>true or false depending upon lock operation's success</returns>
        public bool SetOwnershipLock(bool lockOwnership = true)
        {
            // If we are not in distributed autority mode, then exit early
            if (!NetworkManager.DistributedAuthorityMode)
            {
                Debug.LogError($"[Feature Not Allowed In Client-Server Mode] Ownership flags are a distributed authority feature only!");
                return false;
            }

            // If we don't have authority exit early
            if (!HasAuthority)
            {
                NetworkLog.LogWarningServer($"[Attempted Lock Without Authority] Client-{NetworkManager.LocalClientId} is trying to lock ownership but does not have authority!");
                return false;
            }

            // If we don't have the Transferable flag set and it is not a player object, then it is the same as having a static lock on ownership
            if (!IsOwnershipTransferable && !IsPlayerObject)
            {
                NetworkLog.LogWarning($"Trying to add or remove ownership lock on [{name}] which does not have the {nameof(OwnershipStatus.Transferable)} flag set!");
                return false;
            }

            // If we are locking and are already locked or we are unlocking and are already unlocked exit early and return true
            if (!(IsOwnershipLocked ^ lockOwnership))
            {
                return true;
            }

            if (lockOwnership)
            {
                AddOwnershipExtended(OwnershipStatusExtended.Locked);
            }
            else
            {
                RemoveOwnershipExtended(OwnershipStatusExtended.Locked);
            }

            SendOwnershipStatusUpdate();

            return true;
        }

        /// <summary>
        /// In the event of an immediate (local instance) failure to change ownership, the following ownership
        /// permission failure status codes will be returned via <see cref="OnOwnershipPermissionsFailure"/>.
        /// <see cref="Locked"/>: The <see cref="NetworkObject"/> is locked and ownership cannot be acquired.
        /// <see cref="RequestRequired"/>: The <see cref="NetworkObject"/> requires an ownership request via <see cref="RequestOwnership"/>.
        /// <see cref="RequestInProgress"/>: The <see cref="NetworkObject"/> is already processing an ownership request and ownership cannot be acquired at this time.
        /// <see cref="NotTransferrable"/>: The <see cref="NetworkObject"/> does not have the <see cref="OwnershipStatus.Transferable"/> flag set and ownership cannot be acquired.
        /// </summary>
        public enum OwnershipPermissionsFailureStatus
        {
            Locked,
            RequestRequired,
            RequestInProgress,
            NotTransferrable
        }

        /// <summary>
        /// <see cref="OnOwnershipPermissionsFailure"/>
        /// </summary>
        /// <param name="changeOwnershipFailure"></param>
        public delegate void OnOwnershipPermissionsFailureDelegateHandler(OwnershipPermissionsFailureStatus changeOwnershipFailure);

        /// <summary>
        /// If there is any callback assigned or subscriptions to this handler, then upon any ownership permissions failure that occurs during
        /// the invocation of <see cref="ChangeOwnership(ulong)"/> will trigger this notification containing an <see cref="OwnershipPermissionsFailureStatus"/>.
        /// </summary>
        public OnOwnershipPermissionsFailureDelegateHandler OnOwnershipPermissionsFailure;

        /// <summary>
        /// Returned by <see cref="RequestOwnership"/> to signify w
        /// <see cref="RequestSent"/>: The request for ownership was sent (does not mean it will be granted, but the request was sent).
        /// <see cref="AlreadyOwner"/>: The current client is already the owner (no need to request ownership).
        /// <see cref="RequestRequiredNotSet"/>: The <see cref="OwnershipStatus.RequestRequired"/> flag is not set on this <see cref="NetworkObject"/>
        /// <see cref="Locked"/>: The current owner has locked ownership which means requests are not available at this time.
        /// <see cref="RequestInProgress"/>: There is already a known request in progress. You can scan for ownership changes and try upon
        /// a change in ownership or just try again after a specific period of time or no longer attempt to request ownership.
        /// </summary>
        public enum OwnershipRequestStatus
        {
            RequestSent,
            AlreadyOwner,
            RequestRequiredNotSet,
            Locked,
            RequestInProgress,
        }

        /// <summary>
        /// Invoke this from a non-authority client to request ownership.
        /// </summary>
        /// <remarks>
        /// The <see cref="OwnershipRequestStatus"/> results of requesting ownership:
        /// <see cref="OwnershipRequestStatus.RequestSent"/>: The request for ownership was sent (does not mean it will be granted, but the request was sent).
        /// <see cref="OwnershipRequestStatus.AlreadyOwner"/>: The current client is already the owner (no need to request ownership).
        /// <see cref="OwnershipRequestStatus.RequestRequiredNotSet"/>: The <see cref="OwnershipStatus.RequestRequired"/> flag is not set on this <see cref="NetworkObject"/>
        /// <see cref="OwnershipRequestStatus.Locked"/>: The current owner has locked ownership which means requests are not available at this time.
        /// <see cref="OwnershipRequestStatus.RequestInProgress"/>: There is already a known request in progress. You can scan for ownership changes and try upon
        /// a change in ownership or just try again after a specific period of time or no longer attempt to request ownership.
        /// </remarks>
        /// <returns><see cref="OwnershipRequestStatus"/></returns>
        public OwnershipRequestStatus RequestOwnership()
        {
            // Exit early the local client is already the owner
            if (OwnerClientId == NetworkManager.LocalClientId)
            {
                return OwnershipRequestStatus.AlreadyOwner;
            }

            // Exit early if it doesn't have the RequestRequired flag
            if (!IsOwnershipRequestRequired)
            {
                return OwnershipRequestStatus.RequestRequiredNotSet;
            }

            // Exit early if it is locked
            if (IsOwnershipLocked)
            {
                return OwnershipRequestStatus.Locked;
            }

            // Exit early if there is already a request in progress
            if (IsRequestInProgress)
            {
                return OwnershipRequestStatus.RequestInProgress;
            }

            // Otherwise, send the request ownership message
            var changeOwnership = new ChangeOwnershipMessage
            {
                NetworkObjectId = NetworkObjectId,
                OwnerClientId = OwnerClientId,
                ClientIdCount = 1,
                RequestClientId = NetworkManager.LocalClientId,
                ClientIds = new ulong[1] { OwnerClientId },
                DistributedAuthorityMode = true,
                RequestOwnership = true,
                OwnershipFlags = (ushort)Ownership,
            };

            var sendTarget = NetworkManager.DAHost ? OwnerClientId : NetworkManager.ServerClientId;
            NetworkManager.ConnectionManager.SendMessage(ref changeOwnership, NetworkDelivery.Reliable, sendTarget);

            return OwnershipRequestStatus.RequestSent;
        }

        /// <summary>
        /// The delegate handler declaration used by <see cref="OnOwnershipRequested"/>.
        /// </summary>
        /// <param name="clientRequesting"></param>
        /// <returns></returns>
        public delegate bool OnOwnershipRequestedDelegateHandler(ulong clientRequesting);

        /// <summary>
        /// The <see cref="OnOwnershipRequestedDelegateHandler"/> callback that can be used
        /// to control when ownership can be transferred to a non-authority client.
        /// </summary>
        /// <remarks>
        /// Requesting ownership requires the <see cref="Ownership"/> flags to have the <see cref="OwnershipStatus.RequestRequired"/> flag set.
        /// </remarks>
        public OnOwnershipRequestedDelegateHandler OnOwnershipRequested;

        /// <summary>
        /// Invoked by ChangeOwnershipMessage
        /// </summary>
        /// <param name="clientRequestingOwnership">the client requesting ownership</param>
        /// <returns></returns>
        internal void OwnershipRequest(ulong clientRequestingOwnership)
        {
            var response = OwnershipRequestResponseStatus.Approved;

            // Do a last check to make sure this NetworkObject can be requested
            // CMB-DANGO-TODO: We could help optimize this process and check the below flags on the service side.
            // It wouldn't cover the scenario were an update was in-bound to the service from the owner, but it would
            // handle the case where something had already changed and the service was already "aware" of the change.
            if (IsOwnershipLocked)
            {
                response = OwnershipRequestResponseStatus.Locked;
            }
            else if (IsRequestInProgress)
            {
                response = OwnershipRequestResponseStatus.RequestInProgress;
            }
            else if (!IsOwnershipRequestRequired && !IsOwnershipTransferable)
            {
                response = OwnershipRequestResponseStatus.CannotRequest;
            }

            // Finally, check to see if OnOwnershipRequested is registered and if user script is allowing
            // this transfer of ownership
            if (OnOwnershipRequested != null && !OnOwnershipRequested.Invoke(clientRequestingOwnership))
            {
                response = OwnershipRequestResponseStatus.Denied;
            }

            // If we made it here and the response is still approved, then change ownership
            if (response == OwnershipRequestResponseStatus.Approved)
            {
                // When requested and approved, the owner immediately sets the Requested flag **prior to**
                // changing the ownership. This prevents race conditions from happening.
                // Until the ownership change has propagated out, requests can still flow through this owner,
                // but by that time this owner's instance will have the extended Requested flag and will
                // respond to any additional ownership request with OwnershipRequestResponseStatus.RequestInProgress.
                AddOwnershipExtended(OwnershipStatusExtended.Requested);

                // This action is always authorized as long as the client still has authority.
                // We need to pass in that this is a request approval ownership change.
                NetworkManager.SpawnManager.ChangeOwnership(this, clientRequestingOwnership, HasAuthority, true);
            }
            else
            {
                // Otherwise, send back the reason why the ownership request was denied for the clientRequestingOwnership
                /// Notes:
                /// We always apply the <see cref="NetworkManager.LocalClientId"/> as opposed to <see cref="OwnerClientId"/> to the
                /// <see cref="ChangeOwnershipMessage.OwnerClientId"/> value as ownership could have changed and the denied requests
                /// targeting this instance are because there is a request pending.
                /// DANGO-TODO: What happens if the client requesting disconnects prior to responding with the update in request pending?
                var changeOwnership = new ChangeOwnershipMessage
                {
                    NetworkObjectId = NetworkObjectId,
                    OwnerClientId = NetworkManager.LocalClientId, // Always use the local clientId (see above notes)
                    RequestClientId = clientRequestingOwnership,
                    DistributedAuthorityMode = true,
                    RequestDenied = true,
                    OwnershipRequestResponseStatus = (byte)response,
                    OwnershipFlags = (ushort)Ownership,
                };

                var sendTarget = NetworkManager.DAHost ? clientRequestingOwnership : NetworkManager.ServerClientId;
                NetworkManager.ConnectionManager.SendMessage(ref changeOwnership, NetworkDelivery.Reliable, sendTarget);
            }
        }

        /// <summary>
        /// What is returned via <see cref="OnOwnershipRequestResponse"/> after an ownership request has been sent via <see cref="RequestOwnership"/>
        /// </summary>
        /// <remarks>
        /// Approved: Granted ownership, and returned after the requesting client has gained ownership on the local instance.
        /// Locked: Was locked after request was sent.
        /// RequestInProgress: A request started before this request was received.
        /// CannotRequest: The RequestRequired status changed while the request was in flight.
        /// Denied: General denied message that is only set if <see cref="OnOwnershipRequested"/> returns false by the authority instance.
        /// </remarks>
        public enum OwnershipRequestResponseStatus
        {
            Approved,
            Locked,
            RequestInProgress,
            CannotRequest,
            Denied,
        }

        /// <summary>
        /// The delegate handler declaration used by <see cref="OnOwnershipRequestResponse"/>.
        /// </summary>
        /// <param name="ownershipRequestResponse"></param>
        public delegate void OnOwnershipRequestResponseDelegateHandler(OwnershipRequestResponseStatus ownershipRequestResponse);

        /// <summary>
        /// The <see cref="OnOwnershipRequestedDelegateHandler"/> callback that can be used
        /// to control when ownership can be transferred to a non-authority client.
        /// </summary>
        /// <remarks>
        /// Requesting ownership requires the <see cref="Ownership"/> flags to have the <see cref="OwnershipStatus.RequestRequired"/> flag set.
        /// </remarks>
        public OnOwnershipRequestResponseDelegateHandler OnOwnershipRequestResponse;

        /// <summary>
        /// Invoked when a request is denied
        /// </summary>
        internal void OwnershipRequestResponse(OwnershipRequestResponseStatus ownershipRequestResponse)
        {
            OnOwnershipRequestResponse?.Invoke(ownershipRequestResponse);
        }

        /// <summary>
        /// When passed as a parameter in <see cref="SetOwnershipStatus"/>, the following additional locking actions will occur:
        /// - <see cref="None"/>: (default) No locking action
        /// - <see cref="SetAndLock"/>: Will set the passed in flags and then lock the <see cref="NetworkObject"/>
        /// - <see cref="SetAndUnlock"/>: Will set the passed in flags and then unlock the <see cref="NetworkObject"/>
        /// </summary>
        public enum OwnershipLockActions
        {
            None,
            SetAndLock,
            SetAndUnlock
        }

        /// <summary>
        /// Adds an <see cref="OwnershipStatus"/> flag to the <see cref="Ownership"/> flags
        /// </summary>
        /// <param name="status">flag(s) to update</param>
        /// <param name="clearAndSet">defaults to false, but when true will clear the permissions and then set the permissions flags</param>
        /// <param name="lockAction">defaults to <see cref="OwnershipLockActions.None"/>, but when set it to anther action type it will either lock or unlock ownership after setting the flags</param>
        /// <returns>true (applied)/false (not applied)</returns>
        /// <remarks>
        /// If it returns false, then this means the flag(s) you are attempting to
        /// set were already set on the <see cref="NetworkObject"/> instance.
        /// If it returns true, then the flags were set and an ownership update message
        /// was sent to all observers of the <see cref="NetworkObject"/> instance.
        /// </remarks>
        public bool SetOwnershipStatus(OwnershipStatus status, bool clearAndSet = false, OwnershipLockActions lockAction = OwnershipLockActions.None)
        {
            // If it already has the flag do nothing
            if (!clearAndSet && Ownership.HasFlag(status))
            {
                return false;
            }

            if (clearAndSet || status == OwnershipStatus.None)
            {
                Ownership = OwnershipStatus.None;
            }

            // Faster to just OR a None status than to check
            // if it is !None before "OR'ing".
            Ownership |= status;

            if (lockAction != OwnershipLockActions.None)
            {
                SetOwnershipLock(lockAction == OwnershipLockActions.SetAndLock);
            }

            SendOwnershipStatusUpdate();

            return true;
        }

        /// <summary>
        /// Use this method to remove one or more ownership flags from the NetworkObject.
        /// If you want to clear and then set, use <see cref="SetOwnershipStatus"/>.
        /// </summary>
        /// <param name="status">the flag(s) to remove</param>
        /// <returns>true/false</returns>
        /// <remarks>
        /// If it returns false, then this means the flag(s) you are attempting to
        /// remove were not already set on the <see cref="NetworkObject"/> instance.
        /// If it returns true, then the flags were removed and an ownership update message
        /// was sent to all observers of the <see cref="NetworkObject"/> instance.
        /// </remarks>
        public bool RemoveOwnershipStatus(OwnershipStatus status)
        {
            // If it doesn't have the ownership flag or we are trying to remove the None permission, then return false
            if (!Ownership.HasFlag(status) || status == OwnershipStatus.None)
            {
                return false;
            }

            Ownership &= ~status;

            SendOwnershipStatusUpdate();

            return true;
        }

        /// <summary>
        /// Sends an update ownership status to all non-owner clients
        /// </summary>
        internal void SendOwnershipStatusUpdate()
        {
            // If there are no remote observers, then exit early
            if (!HasRemoteObservers)
            {
                return;
            }

            var changeOwnership = new ChangeOwnershipMessage
            {
                NetworkObjectId = NetworkObjectId,
                OwnerClientId = OwnerClientId,
                DistributedAuthorityMode = true,
                OwnershipFlagsUpdate = true,
                OwnershipFlags = (ushort)Ownership,
            };

            if (NetworkManager.DAHost)
            {
                foreach (var clientId in Observers)
                {
                    if (clientId == NetworkManager.LocalClientId)
                    {
                        continue;
                    }
                    NetworkManager.ConnectionManager.SendMessage(ref changeOwnership, NetworkDelivery.Reliable, clientId);
                }
            }
            else
            {
                changeOwnership.ClientIdCount = Observers.Count();
                changeOwnership.ClientIds = Observers.ToArray();
                NetworkManager.ConnectionManager.SendMessage(ref changeOwnership, NetworkDelivery.Reliable, NetworkManager.ServerClientId);
            }
        }

        /// <summary>
        /// Use this method to determine if a <see cref="NetworkObject"/> has one or more ownership flags set.
        /// </summary>
        /// <param name="status">one or more <see cref="OwnershipStatus"/> flags</param>
        /// <returns>true if the flag(s) are set and false if the flag or any one of the flags are not set</returns>
        public bool HasOwnershipStatus(OwnershipStatus status)
        {
            return Ownership.HasFlag(status);
        }

        /// <summary>
        /// This property can be used in client-server or distributed authority modes to determine if the local instance has authority.
        /// When in client-server mode, the server will always have authority over the NetworkObject and associated NetworkBehaviours.
        /// When in distributed authority mode, the owner is always the authority.
        /// </summary>
        /// <remarks>
        /// When in client-server mode, authority should is not considered the same as ownership.
        /// </remarks>
        public bool HasAuthority => InternalHasAuthority();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InternalHasAuthority()
        {
            var networkManager = NetworkManager;
            return networkManager.DistributedAuthorityMode ? OwnerClientId == networkManager.LocalClientId : networkManager.IsServer;
        }

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
        public ulong OwnerClientId { get; internal set; }

        internal ulong PreviousOwnerId;

        /// <summary>
        /// If true, the object will always be replicated as root on clients and the parent will be ignored.
        /// </summary>
        [Tooltip("If enabled (default disabled), instances of this NetworkObject will ignore any parent(s) it might have and replicate on clients as the root being its parent.")]
        public bool AlwaysReplicateAsRoot;

        /// <summary>
        /// Gets if this object is a player object
        /// </summary>
        public bool IsPlayerObject { get; internal set; }

        /// <summary>
        /// Determines if the associated NetworkObject's transform will get
        /// synchronized when spawned.
        /// </summary>
        /// <remarks>
        /// For things like in-scene placed NetworkObjects that have no visual
        /// components can help reduce the instance's initial synchronization
        /// bandwidth cost. This can also be useful for UI elements that have
        /// a predetermined fixed position.
        /// </remarks>
        [Tooltip("If enabled (default enabled), newly joining clients will be synchronized with the transform of the associated GameObject this component is attached to. Typical use case" +
            " scenario would be for managment objects or in-scene placed objects that don't move and already have their transform settings applied within the scene information.")]
        public bool SynchronizeTransform = true;

        /// <summary>
        /// Gets if the object is the personal clients player object
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

        //DANGOEXP TODO: Determine if we want to keep this
        public void SetSceneObjectStatus(bool isSceneObject = false)
        {
            IsSceneObject = isSceneObject;
        }

        /// <summary>
        /// Gets whether or not the object should be automatically removed when the scene is unloaded.
        /// </summary>
        public bool DestroyWithScene { get; set; }

        /// <summary>
        /// When set to true and the active scene is changed, this will automatically migrate the <see cref="NetworkObject"/>
        /// into the new active scene on both the server and client instances.
        /// </summary>
        /// <remarks>
        /// - This only applies to dynamically spawned <see cref="NetworkObject"/>s.
        /// - This only works when using integrated scene management (<see cref="NetworkSceneManager"/>).
        ///
        /// If there are more than one scenes loaded and the currently active scene is unloaded, then typically
        /// the <see cref="SceneManager"/> will automatically assign a new active scene. Similar to <see cref="DestroyWithScene"/>
        /// being set to <see cref="false"/>, this prevents any <see cref="NetworkObject"/> from being destroyed
        /// with the unloaded active scene by migrating it into the automatically assigned active scene.
        /// Additionally, this is can be useful in some seamless scene streaming implementations.
        /// Note:
        /// Only having <see cref="ActiveSceneSynchronization"/> set to true will *not* synchronize clients when
        /// changing a <see cref="NetworkObject"/>'s scene via <see cref="SceneManager.MoveGameObjectToScene(GameObject, Scene)"/>.
        /// To synchronize clients of a <see cref="NetworkObject"/>'s scene being changed via <see cref="SceneManager.MoveGameObjectToScene(GameObject, Scene)"/>,
        /// make sure <see cref="SceneMigrationSynchronization"/> is enabled (it is by default).
        /// </remarks>
        [Tooltip("When enabled (default disabled), spawned instances of this NetworkObject will automatically migrate to any newly assigned active scene.")]
        public bool ActiveSceneSynchronization;

        /// <summary>
        /// When enabled (the default), if a <see cref="NetworkObject"/> is migrated to a different scene (active or not)
        /// via <see cref="SceneManager.MoveGameObjectToScene(GameObject, Scene)"/> on the server side all client
        /// instances will be synchronized  and the <see cref="NetworkObject"/> migrated into the newly assigned scene.
        /// The updated scene migration will get synchronized with late joining clients as well.
        /// </summary>
        /// <remarks>
        /// - This only applies to dynamically spawned <see cref="NetworkObject"/>s.
        /// - This only works when using integrated scene management (<see cref="NetworkSceneManager"/>).
        /// Note:
        /// You can have both <see cref="ActiveSceneSynchronization"/> and <see cref="SceneMigrationSynchronization"/> enabled.
        /// The primary difference between the two is that <see cref="SceneMigrationSynchronization"/> only synchronizes clients
        /// when the server migrates a <see cref="NetworkObject"/> to a new scene. If the scene is unloaded and <see cref="DestroyWithScene"/>
        /// is <see cref="true"/> and <see cref="ActiveSceneSynchronization"/> is <see cref="false"/> and the scene is not the currently
        /// active scene, then the <see cref="NetworkObject"/> will be destroyed.
        /// </remarks>
        [Tooltip("When enabled (default enabled), dynamically spawned instances of this NetworkObject's migration to a different scene will automatically be synchonize amongst clients.")]
        public bool SceneMigrationSynchronization = true;

        /// <summary>
        /// Notifies when the NetworkObject is migrated into a new scene
        /// </summary>
        /// <remarks>
        /// - <see cref="ActiveSceneSynchronization"/> or <see cref="SceneMigrationSynchronization"/> (or both) need to be enabled
        /// - This only applies to dynamically spawned <see cref="NetworkObject"/>s.
        /// - This only works when using integrated scene management (<see cref="NetworkSceneManager"/>).
        /// </remarks>
        public Action OnMigratedToNewScene;

        /// <summary>
        /// When set to false, the NetworkObject will be spawned with no observers initially (other than the server)
        /// </summary>
        [Tooltip("When disabled (default enabled), the NetworkObject will spawn with no observers. You control object visibility using NetworkShow. This applies to newly joining clients as well.")]
        public bool SpawnWithObservers = true;

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
        /// If true, the objects ownership will be given to the server.
        /// </summary>
        [Tooltip("When enabled (default disabled), instances of this NetworkObject will not be destroyed if the owning client disconnects.")]
        public bool DontDestroyWithOwner;

        /// <summary>
        /// Whether or not to enable automatic NetworkObject parent synchronization.
        /// </summary>
        [Tooltip("When disabled (default enabled), NetworkObject parenting will not be automatically synchronized. This is typically used when you want to implement your own custom parenting solution.")]
        public bool AutoObjectParentSync = true;

        /// <summary>
        /// Determines if the owner will apply transform values sent by the parenting message.
        /// </summary>
        /// <remarks>
        /// When enabled, the resultant parenting transform changes sent by the authority will be applied on all instances. <br />
        /// When disabled, the resultant parenting transform changes sent by the authority will not be applied on the owner's instance. <br />
        /// When disabled, all non-owner instances will still be synchronized by the authority's transform values when parented.
        /// When using a <see cref="NetworkTopologyTypes.ClientServer"/> network topology and an owner authoritative motion model, disabling this can help smooth parenting transitions.
        /// When using a <see cref="NetworkTopologyTypes.DistributedAuthority"/> network topology this will have no impact on the owner's instance since only the authority/owner can parent.
        /// </remarks>
        [Tooltip("When disabled (default enabled), the owner will not apply a server or host's transform properties when parenting changes. Primarily useful for client-server network topology configurations.")]
        public bool SyncOwnerTransformWhenParented = true;

        /// <summary>
        /// Client-Server specific, when enabled an owner of a NetworkObject can parent locally as opposed to requiring the owner to notify the server it would like to be parented.
        /// This behavior is always true when using a distributed authority network topology and does not require it to be set.
        /// </summary>
        [Tooltip("When enabled (default disabled), owner's can parent a NetworkObject locally without having to send an RPC to the server or host. Only pertinent when using client-server network topology configurations.")]
        public bool AllowOwnerToParent;

        internal readonly HashSet<ulong> Observers = new HashSet<ulong>();

#if MULTIPLAYER_TOOLS
        private string m_CachedNameForMetrics;
#endif
        internal string GetNameForMetrics()
        {
#if MULTIPLAYER_TOOLS
            return m_CachedNameForMetrics ??= name;
#else
            return null;
#endif
        }

        private readonly HashSet<ulong> m_EmptyULongHashSet = new HashSet<ulong>();
        /// <summary>
        /// Returns Observers enumerator
        /// </summary>
        /// <returns>Observers enumerator</returns>
        public HashSet<ulong>.Enumerator GetObservers()
        {
            if (!IsSpawned)
            {
                return m_EmptyULongHashSet.GetEnumerator();
            }

            return Observers.GetEnumerator();
        }

        /// <summary>
        /// Whether or not this object is visible to a specific client
        /// </summary>
        /// <param name="clientId">The clientId of the client</param>
        /// <returns>True if the client knows about the object</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNetworkVisibleTo(ulong clientId)
        {
            if (!IsSpawned)
            {
                return false;
            }
            return Observers.Contains(clientId);
        }

        /// <summary>
        ///  In the event the scene of origin gets unloaded, we keep
        ///  the most important part to uniquely identify in-scene
        ///  placed NetworkObjects
        /// </summary>
        internal int SceneOriginHandle = 0;

        /// <summary>
        /// The server-side scene origin handle
        /// </summary>
        internal int NetworkSceneHandle = 0;

        private Scene m_SceneOrigin;
        /// <summary>
        /// The scene where the NetworkObject was first instantiated
        /// Note: Primarily for in-scene placed NetworkObjects
        /// We need to keep track of the original scene of origin for
        /// the NetworkObject in order to be able to uniquely identify it
        /// using the scene of origin's handle.
        /// </summary>
        internal Scene SceneOrigin
        {
            get
            {
                return m_SceneOrigin;
            }

            set
            {
                // The scene origin should only be set once.
                // Once set, it should never change.
                if (SceneOriginHandle == 0 && value.IsValid() && value.isLoaded)
                {
                    m_SceneOrigin = value;
                    SceneOriginHandle = value.handle;
                }
            }
        }

        /// <summary>
        /// Helper method to return the correct scene handle
        /// Note: Do not use this within NetworkSpawnManager.SpawnNetworkObjectLocallyCommon
        /// </summary>
        internal int GetSceneOriginHandle()
        {
            if (SceneOriginHandle == 0 && IsSpawned && IsSceneObject != false)
            {
                throw new Exception($"{nameof(GetSceneOriginHandle)} called when {nameof(SceneOriginHandle)} is still zero but the {nameof(NetworkObject)} is already spawned!");
            }
            return SceneOriginHandle != 0 ? SceneOriginHandle : gameObject.scene.handle;
        }

        /// <summary>
        /// Makes the previously hidden <see cref="NetworkObject"/> "netcode visible" to the targeted client.
        /// </summary>
        /// <remarks>
        /// Usage: Use to start sending updates for a previously hidden <see cref="NetworkObject"/> to the targeted client.<br />
        /// <br />
        /// Dynamically Spawned: <see cref="NetworkObject"/>s will be instantiated and spawned on the targeted client side.<br />
        /// In-Scene Placed: The instantiated but despawned <see cref="NetworkObject"/>s will be spawned on the targeted client side.<br />
        /// <br />
        /// See Also:<br />
        /// <see cref="NetworkShow(ulong)"/><br />
        /// <see cref="NetworkHide(ulong)"/> or <see cref="NetworkHide(List{NetworkObject}, ulong)"/><br />
        /// </remarks>
        /// <param name="clientId">The targeted client</param>
        public void NetworkShow(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!HasAuthority)
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    throw new NotServerException($"Only the owner-authority can change visibility when distributed authority mode is enabled!");
                }
                else
                {
                    throw new NotServerException("Only the authority can change visibility");
                }
            }

            if (Observers.Contains(clientId))
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    Debug.LogError($"The object {name} is already visible to Client-{clientId}!");
                    return;
                }
                else
                {
                    throw new NotServerException("Only server can change visibility");
                }
            }

            if (CheckObjectVisibility != null && !CheckObjectVisibility(clientId))
            {
                if (NetworkManager.LogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"[NetworkShow] Trying to make {nameof(NetworkObject)} {gameObject.name} visible to client ({clientId}) but {nameof(CheckObjectVisibility)} returned false!");
                }
                return;
            }
            NetworkManager.SpawnManager.MarkObjectForShowingTo(this, clientId);
            Observers.Add(clientId);
        }


        /// <summary>
        /// Makes a list of previously hidden <see cref="NetworkObject"/>s "netcode visible" for the client specified.
        /// </summary>
        /// <remarks>
        /// Usage: Use to start sending updates for previously hidden <see cref="NetworkObject"/>s to the targeted client.<br />
        /// <br />
        /// Dynamically Spawned: <see cref="NetworkObject"/>s will be instantiated and spawned on the targeted client's side.<br />
        /// In-Scene Placed: Already instantiated but despawned <see cref="NetworkObject"/>s will be spawned on the targeted client's side.<br />
        /// <br />
        /// See Also:<br />
        /// <see cref="NetworkShow(ulong)"/><br />
        /// <see cref="NetworkHide(ulong)"/> or <see cref="NetworkHide(List{NetworkObject}, ulong)"/><br />
        /// </remarks>
        /// <param name="networkObjects">The objects to become "netcode visible" to the targeted client</param>
        /// <param name="clientId">The targeted client</param>
        public static void NetworkShow(List<NetworkObject> networkObjects, ulong clientId)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                NetworkLog.LogErrorServer($"At least one {nameof(NetworkObject)} has to be provided when showing a list of {nameof(NetworkObject)}s!");
                return;
            }

            // Do the safety loop first to prevent putting the netcode in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                var networkObject = networkObjects[i];
                var networkManager = networkObject.NetworkManager;

                if (networkManager.DistributedAuthorityMode && clientId == networkObject.OwnerClientId)
                {
                    NetworkLog.LogErrorServer($"Cannot hide an object from the owner when distributed authority mode is enabled! (Skipping {networkObject.gameObject.name})");
                }
                else if (!networkManager.DistributedAuthorityMode && clientId == NetworkManager.ServerClientId)
                {
                    NetworkLog.LogErrorServer("Cannot hide an object from the server!");
                    continue;
                }

                // Distributed authority mode adjustments to log a network error and continue when trying to show a NetworkObject
                // that the local instance does not own
                if (!networkObjects[i].HasAuthority)
                {
                    if (networkObjects[i].NetworkManager.DistributedAuthorityMode)
                    {
                        // It will log locally and to the "master-host".
                        NetworkLog.LogErrorServer("Only the owner-authority can change visibility when distributed authority mode is enabled!");
                        continue;
                    }
                    else
                    {
                        throw new NotServerException("Only server can change visibility");
                    }
                }

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
        /// Hides the <see cref="NetworkObject"/> from the targeted client.
        /// </summary>
        /// <remarks>
        /// Usage: Use to stop sending updates to the targeted client, "netcode invisible", for a currently visible <see cref="NetworkObject"/>.<br />
        /// <br />
        /// Dynamically Spawned: <see cref="NetworkObject"/>s will be despawned and destroyed on the targeted client's side.<br />
        /// In-Scene Placed: <see cref="NetworkObject"/>s will only be despawned on the targeted client's side.<br />
        /// <br />
        /// See Also:<br />
        /// <see cref="NetworkHide(List{NetworkObject}, ulong)"/><br />
        /// <see cref="NetworkShow(ulong)"/> or <see cref="NetworkShow(List{NetworkObject}, ulong)"/><br />
        /// </remarks>
        /// <param name="clientId">The targeted client</param>
        public void NetworkHide(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!HasAuthority && !NetworkManager.DAHost)
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    throw new NotServerException($"Only the owner-authority can change visibility when distributed authority mode is enabled!");
                }
                else
                {
                    throw new NotServerException("Only the authority can change visibility");
                }
            }

            if (!NetworkManager.SpawnManager.RemoveObjectFromShowingTo(this, clientId))
            {
                if (!Observers.Contains(clientId))
                {
                    if (NetworkManager.LogLevel <= LogLevel.Developer)
                    {
                        Debug.LogWarning($"{name} is already hidden from Client-{clientId}! (ignoring)");
                        return;
                    }
                }
                Observers.Remove(clientId);

                var message = new DestroyObjectMessage
                {
                    NetworkObjectId = NetworkObjectId,
                    DestroyGameObject = !IsSceneObject.Value,
                    IsDistributedAuthority = NetworkManager.DistributedAuthorityMode,
                    IsTargetedDestroy = NetworkManager.DistributedAuthorityMode,
                    TargetClientId = clientId, // Just always populate this value whether we write it or not
                    DeferredDespawnTick = DeferredDespawnTick,
                };

                var size = 0;
                if (NetworkManager.DistributedAuthorityMode)
                {
                    if (!NetworkManager.DAHost)
                    {
                        // Send destroy call to service or DAHost
                        size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, NetworkManager.ServerClientId);
                    }
                    else // DAHost mocking service
                    {
                        // Send destroy call
                        size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
                        // Broadcast the destroy to all clients so they can update their observers list
                        foreach (var client in NetworkManager.ConnectedClientsIds)
                        {
                            if (client == clientId || client == NetworkManager.LocalClientId)
                            {
                                continue;
                            }
                            size += NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, client);
                        }
                    }
                }
                else
                {
                    // Send destroy call
                    size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
                }
                NetworkManager.NetworkMetrics.TrackObjectDestroySent(clientId, this, size);
            }
        }

        /// <summary>
        /// Hides a list of <see cref="NetworkObject"/>s from the targeted client.
        /// </summary>
        /// <remarks>
        /// Usage: Use to stop sending updates to the targeted client, "netcode invisible", for the currently visible <see cref="NetworkObject"/>s.<br />
        /// <br />
        /// Dynamically Spawned: <see cref="NetworkObject"/>s will be despawned and destroyed on the targeted client's side.<br />
        /// In-Scene Placed: <see cref="NetworkObject"/>s will only be despawned on the targeted client's side.<br />
        /// <br />
        /// See Also:<br />
        /// <see cref="NetworkHide(ulong)"/><br />
        /// <see cref="NetworkShow(ulong)"/> or <see cref="NetworkShow(List{NetworkObject}, ulong)"/><br />
        /// </remarks>
        /// <param name="networkObjects">The <see cref="NetworkObject"/>s that will become "netcode invisible" to the targeted client</param>
        /// <param name="clientId">The targeted client</param>
        public static void NetworkHide(List<NetworkObject> networkObjects, ulong clientId)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                NetworkLog.LogErrorServer($"At least one {nameof(NetworkObject)} has to be provided when hiding a list of {nameof(NetworkObject)}s!");
                return;
            }

            // Do the safety loop first to prevent putting the netcode in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                var networkObject = networkObjects[i];
                var networkManager = networkObject.NetworkManager;

                if (networkManager.DistributedAuthorityMode && clientId == networkObject.OwnerClientId)
                {
                    NetworkLog.LogErrorServer($"Cannot hide an object from the owner when distributed authority mode is enabled! (Skipping {networkObject.gameObject.name})");
                }
                else if (!networkManager.DistributedAuthorityMode && clientId == NetworkManager.ServerClientId)
                {
                    NetworkLog.LogErrorServer("Cannot hide an object from the server!");
                    continue;
                }

                // Distributed authority mode adjustments to log a network error and continue when trying to show a NetworkObject
                // that the local instance does not own
                if (!networkObjects[i].HasAuthority)
                {
                    if (networkObjects[i].NetworkManager.DistributedAuthorityMode)
                    {
                        // It will log locally and to the "master-host".
                        NetworkLog.LogErrorServer($"Only the owner-authority can change hide a {nameof(NetworkObject)} when distributed authority mode is enabled!");
                        continue;
                    }
                    else
                    {
                        throw new NotServerException("Only server can change visibility!");
                    }
                }

                // CLIENT SPAWNING TODO: Log error and continue as opposed to throwing an exception
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
            // If no NetworkManager is assigned, then just exit early
            if (!NetworkManager)
            {
                return;
            }

            // Authority is the server (client-server) and the owner or DAHost (distributed authority) when destroying a NetworkObject
            var isAuthority = HasAuthority || NetworkManager.DAHost;

            if (NetworkManager.IsListening && !isAuthority && IsSpawned &&
                (IsSceneObject == null || (IsSceneObject.Value != true)))
            {
                // Clients should not despawn NetworkObjects while connected to a session, but we don't want to destroy the current call stack
                // if this happens. Instead, we should just generate a network log error and exit early (as long as we are not shutting down).
                if (!NetworkManager.ShutdownInProgress)
                {
                    // Since we still have a session connection, log locally and on the server to inform user of this issue.
                    if (NetworkManager.LogLevel <= LogLevel.Error)
                    {
                        if (NetworkManager.DistributedAuthorityMode)
                        {
                            NetworkLog.LogError($"[Invalid Destroy][{gameObject.name}][NetworkObjectId:{NetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-owner client is not valid during a distributed authority session. Call {nameof(Destroy)} or {nameof(Despawn)} on the client-owner instead.");
                        }
                        else
                        {
                            NetworkLog.LogErrorServer($"[Invalid Destroy][{gameObject.name}][NetworkObjectId:{NetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-host client is not valid. Call {nameof(Destroy)} or {nameof(Despawn)} on the server/host instead.");
                        }
                    }
                    return;
                }
                // Otherwise, clients can despawn NetworkObjects while shutting down and should not generate any messages when this happens
            }

            if (NetworkManager.SpawnManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out var networkObject))
            {
                if (this == networkObject)
                {
                    NetworkManager.SpawnManager.OnDespawnObject(networkObject, false);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SpawnInternal(bool destroyWithScene, ulong ownerClientId, bool playerObject)
        {
            if (NetworkManagerOwner == null)
            {
                NetworkManagerOwner = NetworkManager.Singleton;
            }
            if (!NetworkManager.IsListening)
            {
                throw new NotListeningException($"{nameof(NetworkManager)} is not listening, start a server or host before spawning objects");
            }

            if ((!NetworkManager.IsServer && !NetworkManager.DistributedAuthorityMode) || (NetworkManager.DistributedAuthorityMode && !NetworkManager.LocalClient.IsSessionOwner && NetworkManager.LocalClientId != ownerClientId))
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    throw new NotServerException($"When distributed authority mode is enabled, you can only spawn NetworkObjects that belong to the local instance! Local instance id {NetworkManager.LocalClientId} is not the same as the assigned owner id: {ownerClientId}!");
                }
                else
                {
                    throw new NotServerException($"Only server can spawn {nameof(NetworkObject)}s");
                }
            }

            if (NetworkManager.DistributedAuthorityMode)
            {
                if (NetworkManager.LocalClient == null || !NetworkManager.IsConnectedClient || !NetworkManager.ConnectionManager.LocalClient.IsApproved)
                {
                    Debug.LogError($"Cannot spawn {name} until the client is fully connected to the session!");
                    return;
                }
                if (NetworkManager.NetworkConfig.EnableSceneManagement)
                {
                    NetworkSceneHandle = NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle[gameObject.scene.handle];
                }
                if (DontDestroyWithOwner && !IsOwnershipDistributable)
                {
                    //Ownership |= OwnershipStatus.Distributable;
                    // DANGO-TODO: Review over don't destroy with owner being set but DistributeOwnership not being set
                    if (NetworkManager.LogLevel == LogLevel.Developer)
                    {
                        NetworkLog.LogWarning("DANGO-TODO: Review over don't destroy with owner being set but DistributeOwnership not being set. For now, if the NetworkObject does not destroy with the owner it will automatically set DistributeOwnership.");
                    }
                }
            }

            NetworkManager.SpawnManager.SpawnNetworkObjectLocally(this, NetworkManager.SpawnManager.GetNetworkObjectId(), IsSceneObject.HasValue && IsSceneObject.Value, playerObject, ownerClientId, destroyWithScene);

            if ((NetworkManager.DistributedAuthorityMode && NetworkManager.DAHost) || (!NetworkManager.DistributedAuthorityMode && NetworkManager.IsServer))
            {
                for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
                {
                    if (NetworkManager.ConnectedClientsList[i].ClientId == NetworkManager.ServerClientId)
                    {
                        continue;
                    }
                    if (Observers.Contains(NetworkManager.ConnectedClientsList[i].ClientId))
                    {
                        NetworkManager.SpawnManager.SendSpawnCallForObject(NetworkManager.ConnectedClientsList[i].ClientId, this);
                    }
                }
            }
            else if (NetworkManager.DistributedAuthorityMode && !NetworkManager.DAHost)
            {
                // If spawning with observers or if not spawning with observers but the observer count is greater than 1 (i.e. owner/authority creating),
                // then we want to send a spawn notification.
                if (SpawnWithObservers || !SpawnWithObservers && Observers.Count > 1)
                {
                    NetworkManager.SpawnManager.SendSpawnCallForObject(NetworkManager.ServerClientId, this);
                }
            }
            else
            {
                NetworkLog.LogWarningServer($"Ran into unknown conditional check during spawn when determining distributed authority mode or not");
            }
        }

        /// <summary>
        /// This invokes <see cref="NetworkSpawnManager.InstantiateAndSpawn(NetworkObject, ulong, bool, bool, bool, Vector3, Quaternion)"/>.
        /// </summary>
        /// <param name="networkPrefab">The NetworkPrefab to instantiate and spawn.</param>
        /// <param name="networkManager">The local instance of the NetworkManager connected to an session in progress.</param>
        /// <param name="ownerClientId">The owner of the <see cref="NetworkObject"/> instance (defaults to server).</param>
        /// <param name="destroyWithScene">Whether the <see cref="NetworkObject"/> instance will be destroyed when the scene it is located within is unloaded (default is false).</param>
        /// <param name="isPlayerObject">Whether the <see cref="NetworkObject"/> instance is a player object or not (default is false).</param>
        /// <param name="forceOverride">Whether you want to force spawning the override when running as a host or server or if you want it to spawn the override for host mode and
        /// the source prefab for server. If there is an override, clients always spawn that as opposed to the source prefab (defaults to false).  </param>
        /// <param name="position">The starting poisiton of the <see cref="NetworkObject"/> instance.</param>
        /// <param name="rotation">The starting rotation of the <see cref="NetworkObject"/> instance.</param>
        /// <returns>The newly instantiated and spawned <see cref="NetworkObject"/> prefab instance.</returns>
        public static NetworkObject InstantiateAndSpawn(GameObject networkPrefab, NetworkManager networkManager, ulong ownerClientId = NetworkManager.ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)
        {
            var networkObject = networkPrefab.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"The {nameof(NetworkPrefab)} {networkPrefab.name} does not have a {nameof(NetworkObject)} component!");
                return null;
            }
            return networkObject.InstantiateAndSpawn(networkManager, ownerClientId, destroyWithScene, isPlayerObject, forceOverride, position, rotation);
        }

        /// <summary>
        /// This invokes <see cref="NetworkSpawnManager.InstantiateAndSpawn(NetworkObject, ulong, bool, bool, bool, Vector3, Quaternion)"/>.
        /// </summary>
        /// <param name="networkManager">The local instance of the NetworkManager connected to an session in progress.</param>
        /// <param name="ownerClientId">The owner of the <see cref="NetworkObject"/> instance (defaults to server).</param>
        /// <param name="destroyWithScene">Whether the <see cref="NetworkObject"/> instance will be destroyed when the scene it is located within is unloaded (default is false).</param>
        /// <param name="isPlayerObject">Whether the <see cref="NetworkObject"/> instance is a player object or not (default is false).</param>
        /// <param name="forceOverride">Whether you want to force spawning the override when running as a host or server or if you want it to spawn the override for host mode and
        /// the source prefab for server. If there is an override, clients always spawn that as opposed to the source prefab (defaults to false).  </param>
        /// <param name="position">The starting poisiton of the <see cref="NetworkObject"/> instance.</param>
        /// <param name="rotation">The starting rotation of the <see cref="NetworkObject"/> instance.</param>
        /// <returns>The newly instantiated and spawned <see cref="NetworkObject"/> prefab instance.</returns>
        public NetworkObject InstantiateAndSpawn(NetworkManager networkManager, ulong ownerClientId = NetworkManager.ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)
        {
            if (networkManager == null)
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NetworkManagerNull]);
                return null;
            }

            if (!networkManager.IsListening)
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NoActiveSession]);
                return null;
            }

            ownerClientId = networkManager.DistributedAuthorityMode ? networkManager.LocalClientId : ownerClientId;
            // We only need to check for authority when running in client-server mode
            if (!networkManager.IsServer && !networkManager.DistributedAuthorityMode)
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotAuthority]);
                return null;
            }

            if (networkManager.ShutdownInProgress)
            {
                Debug.LogWarning(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.InvokedWhenShuttingDown]);
                return null;
            }

            // Verify it is actually a valid prefab
            if (!networkManager.NetworkConfig.Prefabs.Contains(gameObject))
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotRegisteredNetworkPrefab]);
                return null;
            }

            return networkManager.SpawnManager.InstantiateAndSpawnNoParameterChecks(this, ownerClientId, destroyWithScene, isPlayerObject, forceOverride, position, rotation);
        }

        /// <summary>
        /// Spawns this <see cref="NetworkObject"/> across the network. Can only be called from the Server
        /// </summary>
        /// <param name="destroyWithScene">Should the object be destroyed when the scene is changed</param>
        public void Spawn(bool destroyWithScene = false)
        {
            var clientId = NetworkManager.DistributedAuthorityMode ? NetworkManager.LocalClientId : NetworkManager.ServerClientId;
            SpawnInternal(destroyWithScene, clientId, false);
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
        /// <param name="clientId">The clientId who's player object this is</param>
        /// <param name="destroyWithScene">Should the object be destroyed when the scene is changed</param>
        public void SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false)
        {
            SpawnInternal(destroyWithScene, clientId, true);
        }

        /// <summary>
        /// Despawns the <see cref="GameObject"/> of this <see cref="NetworkObject"/> and sends a destroy message for it to all connected clients.
        /// </summary>
        /// <param name="destroy">(true) the <see cref="GameObject"/> will be destroyed (false) the <see cref="GameObject"/> will persist after being despawned</param>
        public void Despawn(bool destroy = true)
        {
            MarkVariablesDirty(false);
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
            NetworkManager.SpawnManager.ChangeOwnership(this, newOwnerClientId, HasAuthority);
        }

        internal void InvokeBehaviourOnLostOwnership()
        {
            // Always update the ownership table in distributed authority mode
            if (NetworkManager.DistributedAuthorityMode)
            {
                NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId, true);
            }
            else // Server already handles this earlier, hosts should ignore and only client owners should update
            if (!NetworkManager.IsServer)
            {
                NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId, true);
            }
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InternalOnLostOwnership();
            }
        }

        internal void InvokeBehaviourOnGainedOwnership()
        {
            // Always update the ownership table in distributed authority mode
            if (NetworkManager.DistributedAuthorityMode)
            {
                NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId);
            }
            else // Server already handles this earlier, hosts should ignore and only client owners should update
            if (!NetworkManager.IsServer && NetworkManager.LocalClientId == OwnerClientId)
            {
                NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId);
            }

            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].InternalOnGainedOwnership();
                }
                else
                {
                    Debug.LogWarning($"{ChildNetworkBehaviours[i].gameObject.name} is disabled! Netcode for GameObjects does not support disabled NetworkBehaviours! The {ChildNetworkBehaviours[i].GetType().Name} component was skipped during ownership assignment!");
                }
            }
        }

        internal void InvokeOwnershipChanged(ulong previous, ulong next)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].InternalOnOwnershipChanged(previous, next);
                }
                else
                {
                    Debug.LogWarning($"{ChildNetworkBehaviours[i].gameObject.name} is disabled! Netcode for GameObjects does not support disabled NetworkBehaviours! The {ChildNetworkBehaviours[i].GetType().Name} component was skipped during ownership assignment!");
                }
            }
        }

        internal void InvokeBehaviourOnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                // Invoke internal notification
                ChildNetworkBehaviours[i].InternalOnNetworkObjectParentChanged(parentNetworkObject);
                // Invoke public notification
                ChildNetworkBehaviours[i].OnNetworkObjectParentChanged(parentNetworkObject);
            }
        }

        private ulong? m_LatestParent; // What is our last set parent NetworkObject's ID?
        private Transform m_CachedParent; // What is our last set parent Transform reference?
        private bool m_CachedWorldPositionStays = true; // Used to preserve the world position stays parameter passed in TrySetParent

        /// <summary>
        /// With distributed authority, we need to have a way to determine if the parenting action is authorized.
        /// This is set when handling an incoming ParentSyncMessage and when running as a DAHost and a client has disconnected.
        /// </summary>
        internal bool AuthorityAppliedParenting = false;


        /// <summary>
        /// Returns the last known cached WorldPositionStays value for this NetworkObject
        /// </summary>
        /// <remarks>
        /// When parenting NetworkObjects, the optional WorldPositionStays value is cached and synchronized with clients.
        /// This method provides access to the instance relative cached value.
        /// <see cref="TrySetParent(GameObject, bool)"/>
        /// <see cref="TrySetParent(NetworkObject, bool)"/>
        /// <see cref="TrySetParent(Transform, bool)"/>
        /// </remarks>
        /// <returns><see cref="true"/> or <see cref="false"/></returns>
        public bool WorldPositionStays()
        {
            return m_CachedWorldPositionStays;
        }

        internal void SetCachedParent(Transform parentTransform)
        {
            AuthorityAppliedParenting = false;
            m_CachedParent = parentTransform;
        }

        internal Transform GetCachedParent()
        {
            return m_CachedParent;
        }

        internal ulong? GetNetworkParenting() => m_LatestParent;

        internal void SetNetworkParenting(ulong? latestParent, bool worldPositionStays)
        {
            m_LatestParent = latestParent;
            m_CachedWorldPositionStays = worldPositionStays;
        }

        /// <summary>
        /// Set the parent of the NetworkObject transform.
        /// </summary>
        /// <param name="parent">The new parent for this NetworkObject transform will be the child of.</param>
        /// <param name="worldPositionStays">If true, the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.</param>
        /// <returns>Whether or not reparenting was successful.</returns>
        public bool TrySetParent(Transform parent, bool worldPositionStays = true)
        {
            // If we are removing ourself from a parent
            if (parent == null)
            {
                return TrySetParent((NetworkObject)null, worldPositionStays);
            }

            var networkObject = parent.GetComponent<NetworkObject>();

            // If the parent doesn't have a NetworkObjet then return false, otherwise continue trying to parent
            return networkObject == null ? false : TrySetParent(networkObject, worldPositionStays);
        }

        /// <summary>
        /// Set the parent of the NetworkObject transform.
        /// </summary>
        /// <param name="parent">The new parent for this NetworkObject transform will be the child of.</param>
        /// <param name="worldPositionStays">If true, the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.</param>
        /// <returns>Whether or not reparenting was successful.</returns>
        public bool TrySetParent(GameObject parent, bool worldPositionStays = true)
        {
            // If we are removing ourself from a parent
            if (parent == null)
            {
                return TrySetParent((NetworkObject)null, worldPositionStays);
            }

            var networkObject = parent.GetComponent<NetworkObject>();

            // If the parent doesn't have a NetworkObjet then return false, otherwise continue trying to parent
            return networkObject == null ? false : TrySetParent(networkObject, worldPositionStays);
        }

        /// <summary>
        /// Used when despawning the parent, we want to preserve the cached WorldPositionStays value
        /// </summary>
        internal bool TryRemoveParentCachedWorldPositionStays()
        {
            return InternalTrySetParent(null, m_CachedWorldPositionStays);
        }

        /// <summary>
        /// Removes the parent of the NetworkObject's transform
        /// </summary>
        /// <remarks>
        /// This is a more convenient way to remove the parent without  having to cast the null value to either <see cref="GameObject"/> or <see cref="NetworkObject"/>
        /// </remarks>
        /// <param name="worldPositionStays">If true, the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.</param>
        /// <returns></returns>
        public bool TryRemoveParent(bool worldPositionStays = true)
        {
            return TrySetParent((NetworkObject)null, worldPositionStays);
        }

        /// <summary>
        /// Set the parent of the NetworkObject transform.
        /// </summary>
        /// <param name="parent">The new parent for this NetworkObject transform will be the child of.</param>
        /// <param name="worldPositionStays">If true, the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.</param>
        /// <returns>Whether or not reparenting was successful.</returns>
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

            // DANGO-TODO: Do we want to worry about ownership permissions here?
            // It wouldn't make sense to not allow parenting, but keeping this note here as a reminder.
            var isAuthority = HasAuthority || (AllowOwnerToParent && IsOwner);

            // If we don't have authority and we are not shutting down, then don't allow any parenting.
            // If we are shutting down and don't have authority then allow it.
            if (!isAuthority && !NetworkManager.ShutdownInProgress)
            {
                return false;
            }

            return InternalTrySetParent(parent, worldPositionStays);
        }

        internal bool InternalTrySetParent(NetworkObject parent, bool worldPositionStays = true)
        {
            if (parent != null && (IsSpawned ^ parent.IsSpawned))
            {
                if (NetworkManager != null && !NetworkManager.ShutdownInProgress)
                {
                    return false;
                }
            }

            m_CachedWorldPositionStays = worldPositionStays;

            if (parent == null)
            {
                CurrentParent = null;
                transform.SetParent(null, worldPositionStays);
            }
            else
            {
                CurrentParent = parent;
                transform.SetParent(parent.transform, worldPositionStays);
            }

            return true;
        }

        private void OnTransformParentChanged()
        {
            if (!AutoObjectParentSync || NetworkManager.ShutdownInProgress)
            {
                return;
            }

            if (transform.parent == m_CachedParent)
            {
                return;
            }

            if (NetworkManager == null || !NetworkManager.IsListening)
            {
                // DANGO-TODO: Review as to whether we want to provide a better way to handle changing parenting of objects when the
                // object is not spawned. Really, we shouldn't care about these types of changes.
                if (NetworkManager.DistributedAuthorityMode && m_CachedParent != null && transform.parent == null)
                {
                    m_CachedParent = null;
                    return;
                }
                transform.parent = m_CachedParent;
                Debug.LogException(new NotListeningException($"{nameof(NetworkManager)} is not listening, start a server or host before reparenting"));
                return;
            }
            var isAuthority = false;
            // With distributed authority, we need to track "valid authoritative" parenting changes.
            // So, either the authority or AuthorityAppliedParenting is considered a "valid parenting change".
            isAuthority = HasAuthority || AuthorityAppliedParenting || (AllowOwnerToParent && IsOwner);
            var distributedAuthority = NetworkManager.DistributedAuthorityMode;

            // If we do not have authority and we are spawned
            if (!isAuthority && IsSpawned)
            {

                // If the cached parent has not already been set and we are in distributed authority mode, then log an exception and exit early as a non-authority instance
                // is trying to set the parent.
                if (distributedAuthority)
                {
                    transform.parent = m_CachedParent;
                    NetworkLog.LogError($"[Not Owner] Only the owner-authority of child {gameObject.name}'s {nameof(NetworkObject)} component can reparent it!");
                }
                else
                {
                    transform.parent = m_CachedParent;
                    Debug.LogException(new NotServerException($"Only the server can reparent {nameof(NetworkObject)}s"));
                }
                return;
            }

            if (!IsSpawned)
            {
                AuthorityAppliedParenting = false;
                // and we are removing the parent, then go ahead and allow parenting to occur
                if (transform.parent == null)
                {
                    m_LatestParent = null;
                    SetCachedParent(null);
                    InvokeBehaviourOnNetworkObjectParentChanged(null);
                }
                else
                {
                    transform.parent = m_CachedParent;
                    Debug.LogException(new SpawnStateException($"{nameof(NetworkObject)} can only be reparented after being spawned"));
                }
                return;
            }
            var removeParent = false;
            var parentTransform = transform.parent;
            if (parentTransform != null)
            {
                if (!transform.parent.TryGetComponent<NetworkObject>(out var parentObject))
                {
                    transform.parent = m_CachedParent;
                    AuthorityAppliedParenting = false;
                    Debug.LogException(new InvalidParentException($"Invalid parenting, {nameof(NetworkObject)} moved under a non-{nameof(NetworkObject)} parent"));
                    return;
                }
                else if (!parentObject.IsSpawned)
                {
                    transform.parent = m_CachedParent;
                    AuthorityAppliedParenting = false;
                    Debug.LogException(new SpawnStateException($"{nameof(NetworkObject)} can only be reparented under another spawned {nameof(NetworkObject)}"));
                    return;
                }

                m_LatestParent = parentObject.NetworkObjectId;
            }
            else
            {
                m_LatestParent = null;
                removeParent = m_CachedParent != null;
            }

            // This can be reset within ApplyNetworkParenting
            var authorityApplied = AuthorityAppliedParenting;
            ApplyNetworkParenting(removeParent);

            var message = new ParentSyncMessage
            {
                NetworkObjectId = NetworkObjectId,
                IsLatestParentSet = m_LatestParent != null && m_LatestParent.HasValue,
                LatestParent = m_LatestParent,
                RemoveParent = removeParent,
                AuthorityApplied = authorityApplied,
                WorldPositionStays = m_CachedWorldPositionStays,
                Position = m_CachedWorldPositionStays ? transform.position : transform.localPosition,
                Rotation = m_CachedWorldPositionStays ? transform.rotation : transform.localRotation,
                Scale = transform.localScale,
            };

            // We need to preserve the m_CachedWorldPositionStays value until after we create the message
            // in order to assure any local space values changed/reset get applied properly. If our
            // parent is null then go ahead and reset the m_CachedWorldPositionStays the default value.
            if (parentTransform == null)
            {
                m_CachedWorldPositionStays = true;
            }

            // If we are connected to a CMB service or we are running a mock CMB service then send to the "server" identifier
            if (distributedAuthority || (!distributedAuthority && AllowOwnerToParent && IsOwner && !NetworkManager.IsServer))
            {
                if (!NetworkManager.DAHost)
                {
                    NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, 0);
                    return;
                }
                else
                {
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == NetworkManager.ServerClientId)
                        {
                            continue;
                        }
                        NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
                    }
                }
            }
            else
            {
                // Otherwise we are running in client-server =or= this has to be a DAHost instance.
                // Send to all connected clients.
                unsafe
                {
                    var maxCount = NetworkManager.ConnectedClientsIds.Count;
                    ulong* clientIds = stackalloc ulong[maxCount];
                    int idx = 0;
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == NetworkManager.ServerClientId)
                        {
                            continue;
                        }
                        if (Observers.Contains(clientId))
                        {
                            clientIds[idx++] = clientId;
                        }
                    }
                    NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientIds, idx);
                }
            }
        }

        // We're keeping this set called OrphanChildren which contains NetworkObjects
        // because at the time we initialize/spawn NetworkObject locally, we might not have its parent replicated from the other side
        //
        // For instance, if we're spawning NetworkObject 5 and its parent is 10, what should happen if we do not have 10 yet?
        // let's say 10 is on the way to be replicated in a few frames and we could fix that parent-child relationship later.
        //
        // If you couldn't find your parent, we put you into OrphanChildren set and every time we spawn another NetworkObject locally due to replication,
        // we call CheckOrphanChildren() method and quickly iterate over OrphanChildren set and see if we can reparent/adopt one.
        internal static HashSet<NetworkObject> OrphanChildren = new HashSet<NetworkObject>();

        internal bool ApplyNetworkParenting(bool removeParent = false, bool ignoreNotSpawned = false, bool orphanedChildPass = false)
        {
            if (!AutoObjectParentSync)
            {
                return false;
            }

            // SPECIAL CASE:
            // The ignoreNotSpawned is a special case scenario where a late joining client has joined
            // and loaded one or more scenes that contain nested in-scene placed NetworkObject children
            // yet the server's synchronization information does not indicate the NetworkObject in question
            // has a parent. Under this scenario, we want to remove the parent before spawning and setting
            // the transform values. This is the only scenario where the ignoreNotSpawned parameter is used.
            if (!IsSpawned && !ignoreNotSpawned)
            {
                return false;
            }

            // Handle the first in-scene placed NetworkObject parenting scenarios. Once the m_LatestParent
            // has been set, this will not be entered into again (i.e. the later code will be invoked and
            // users will get notifications when the parent changes).
            var isInScenePlaced = IsSceneObject.HasValue && IsSceneObject.Value;
            if (transform.parent != null && !removeParent && !m_LatestParent.HasValue && isInScenePlaced)
            {
                var parentNetworkObject = transform.parent.GetComponent<NetworkObject>();

                // If parentNetworkObject is null then the parent is a GameObject without a NetworkObject component
                // attached. Under this case, we preserve the hierarchy but we don't keep track of the parenting.
                // Note: We only start tracking parenting if the user removes the child from the standard GameObject
                // parent and then re-parents the child under a GameObject with a NetworkObject component attached.
                if (parentNetworkObject == null)
                {
                    // If we are parented under a GameObject, go ahead and mark the world position stays as false
                    // so clients synchronize their transform in local space. (only for in-scene placed NetworkObjects)
                    m_CachedWorldPositionStays = false;
                    return true;
                }
                else // If the parent still isn't spawned add this to the orphaned children and return false
                if (!parentNetworkObject.IsSpawned)
                {
                    OrphanChildren.Add(this);
                    return false;
                }
                else
                {
                    // If we made it this far, go ahead and set the network parenting values
                    // with the WorldPoisitonSays value set to false
                    // Note: Since in-scene placed NetworkObjects are parented in the scene
                    // the default "assumption" is that children are parenting local space
                    // relative.
                    SetNetworkParenting(parentNetworkObject.NetworkObjectId, false);

                    // Set the cached parent
                    SetCachedParent(parentNetworkObject.transform);

                    return true;
                }
            }

            // If we are removing the parent or our latest parent is not set, then remove the parent
            // removeParent is only set when:
            //  - The server-side NetworkObject.OnTransformParentChanged is invoked and the parent is being removed
            //  - The client-side when handling a ParentSyncMessage
            // When clients are synchronizing only the m_LatestParent.HasValue will not have a value if there is no parent
            // or a parent was removed prior to the client connecting (i.e. in-scene placed NetworkObjects)
            if (removeParent || !m_LatestParent.HasValue)
            {
                SetCachedParent(null);
                // We must use Transform.SetParent when taking WorldPositionStays into
                // consideration, otherwise just setting transform.parent = null defaults
                // to WorldPositionStays which can cause scaling issues if the parent's
                // scale is not the default (Vetctor3.one) value.
                transform.SetParent(null, m_CachedWorldPositionStays);
                InvokeBehaviourOnNetworkObjectParentChanged(null);
                return true;
            }

            // If we have a latest parent id but it hasn't been spawned yet, then add this instance to the orphanChildren
            // HashSet and return false (i.e. parenting not applied yet)
            if (m_LatestParent.HasValue && !NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(m_LatestParent.Value))
            {
                OrphanChildren.Add(this);
                return false;
            }

            // If we made it here, then parent this instance under the parentObject
            var parentObject = NetworkManager.SpawnManager.SpawnedObjects[m_LatestParent.Value];

            // If we are handling an orphaned child and its parent is orphaned too, then don't parent yet.
            if (orphanedChildPass)
            {
                if (OrphanChildren.Contains(parentObject))
                {
                    return false;
                }
            }
            SetCachedParent(parentObject.transform);
            transform.SetParent(parentObject.transform, m_CachedWorldPositionStays);
            InvokeBehaviourOnNetworkObjectParentChanged(parentObject);
            return true;
        }

        internal static void CheckOrphanChildren()
        {
            var objectsToRemove = new List<NetworkObject>();
            foreach (var orphanObject in OrphanChildren)
            {
                if (orphanObject.ApplyNetworkParenting(orphanedChildPass: true))
                {
                    objectsToRemove.Add(orphanObject);
                }
            }
            foreach (var networkObject in objectsToRemove)
            {
                OrphanChildren.Remove(networkObject);
            }
        }

        internal void InvokeBehaviourNetworkPreSpawn()
        {
            var networkManager = NetworkManager;
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].NetworkPreSpawn(ref networkManager);
                }
            }
        }

        internal void InvokeBehaviourNetworkSpawn()
        {
            NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId);

            if (SceneMigrationSynchronization && NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                AddNetworkObjectToSceneChangedUpdates(this);
            }

            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].InternalOnNetworkSpawn();
                }
                else
                {
                    Debug.LogWarning($"{ChildNetworkBehaviours[i].gameObject.name} is disabled! Netcode for GameObjects does not support spawning disabled NetworkBehaviours! The {ChildNetworkBehaviours[i].GetType().Name} component was skipped during spawn!");
                }
            }
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].VisibleOnNetworkSpawn();
                }
            }
        }

        internal void InvokeBehaviourNetworkPostSpawn()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].NetworkPostSpawn();
                }
            }
        }


        internal void InternalNetworkSessionSynchronized()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].NetworkSessionSynchronized();
                }
            }
        }

        internal void InternalInSceneNetworkObjectsSpawned()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i].gameObject.activeInHierarchy)
                {
                    ChildNetworkBehaviours[i].InSceneNetworkObjectsSpawned();
                }
            }
        }



        internal void InvokeBehaviourNetworkDespawn()
        {
            NetworkManager.SpawnManager.UpdateOwnershipTable(this, OwnerClientId, true);

            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InternalOnNetworkDespawn();
            }

            if (SceneMigrationSynchronization && NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                RemoveNetworkObjectFromSceneChangedUpdates(this);
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
                        var type = networkBehaviours[i].GetType();
                        if (type == typeof(NetworkTransform) || type.IsInstanceOfType(typeof(NetworkTransform)) || type.IsSubclassOf(typeof(NetworkTransform)))
                        {
                            if (NetworkTransforms == null)
                            {
                                NetworkTransforms = new List<NetworkTransform>();
                            }
                            var networkTransform = networkBehaviours[i] as NetworkTransform;
                            networkTransform.IsNested = i != 0 && networkTransform.gameObject != gameObject;
                            NetworkTransforms.Add(networkTransform);
                        }
#if COM_UNITY_MODULES_PHYSICS
                        else if (type.IsSubclassOf(typeof(NetworkRigidbodyBase)))
                        {
                            if (NetworkRigidbodies == null)
                            {
                                NetworkRigidbodies = new List<NetworkRigidbodyBase>();
                            }
                            NetworkRigidbodies.Add(networkBehaviours[i] as NetworkRigidbodyBase);
                        }
#endif
                    }
                }

                return m_ChildNetworkBehaviours;
            }
        }

        internal void WriteNetworkVariableData(FastBufferWriter writer, ulong targetClientId)
        {
            if (NetworkManager.DistributedAuthorityMode)
            {
                writer.WriteValueSafe((ushort)ChildNetworkBehaviours.Count);
                if (ChildNetworkBehaviours.Count == 0)
                {
                    return;
                }
            }
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                var behavior = ChildNetworkBehaviours[i];
                behavior.InitializeVariables();
                behavior.WriteNetworkVariableData(writer, targetClientId);
            }
        }

        internal void MarkVariablesDirty(bool dirty)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                var behavior = ChildNetworkBehaviours[i];
                behavior.MarkVariablesDirty(dirty);
            }
        }

        internal void MarkOwnerReadVariablesDirty()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].MarkOwnerReadVariablesDirty();
            }
        }

        // NGO currently guarantees that the client will receive spawn data for all objects in one network tick.
        //  Children may arrive before their parents; when they do they are stored in OrphanedChildren and then
        //  resolved when their parents arrived.  Because we don't send a partial list of spawns (yet), something
        //  has gone wrong if by the end of an update we still have unresolved orphans
        //

        // if and when we have different systems for where it is expected that orphans survive across ticks,
        //   then this warning will remind us that we need to revamp the system because then we can no longer simply
        //   spawn the orphan without its parent (at least, not when its transform is set to local coords mode)
        //   - because then you'll have children popping at the wrong location not having their parent's global position to root them
        //   - and then they'll pop to the correct location after they get the parent, and that would be not good
        internal static void VerifyParentingStatus()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
            {
                if (OrphanChildren.Count > 0)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkObject)} ({OrphanChildren.Count}) children not resolved to parents by the end of frame");
                }
            }
        }

        /// <summary>
        /// Only invoked during first synchronization of a NetworkObject (late join or newly spawned)
        /// </summary>
        internal bool SetNetworkVariableData(FastBufferReader reader, ulong clientId)
        {
            if (NetworkManager.DistributedAuthorityMode)
            {
                var readerPosition = reader.Position;
                reader.ReadValueSafe(out ushort behaviourCount);
                if (behaviourCount != ChildNetworkBehaviours.Count)
                {
                    Debug.LogError($"[{name}] Network Behavior Count Mismatch! [In: {behaviourCount} vs Local: {ChildNetworkBehaviours.Count}][StartReaderPos: {readerPosition}] CurrentReaderPos: {reader.Position}]");
                    return false;
                }
            }

            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                var behaviour = ChildNetworkBehaviours[i];
                behaviour.InitializeVariables();
                behaviour.SetNetworkVariableData(reader, clientId);
            }
            return true;
        }

        public ushort GetNetworkBehaviourOrderIndex(NetworkBehaviour instance)
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
                    NetworkLog.LogError($"{nameof(NetworkBehaviour)} index {index} was out of bounds for {name}. NetworkBehaviours must be the same, and in the same order, between server and client.");
                }
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    var currentKnownChildren = new System.Text.StringBuilder();
                    currentKnownChildren.Append($"Known child {nameof(NetworkBehaviour)}s:");
                    for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
                    {
                        var childNetworkBehaviour = ChildNetworkBehaviours[i];
                        currentKnownChildren.Append($" [{i}] {childNetworkBehaviour.__getTypeName()}");
                        currentKnownChildren.Append(i < ChildNetworkBehaviours.Count - 1 ? "," : ".");
                    }
                    NetworkLog.LogInfo(currentKnownChildren.ToString());
                }
                return null;
            }

            return ChildNetworkBehaviours[index];
        }

        internal struct SceneObject
        {
            private ushort m_BitField;
            public uint Hash;
            public ulong NetworkObjectId;
            public ulong OwnerClientId;
            public ushort OwnershipFlags;

            public bool IsPlayerObject
            {
                get => ByteUtility.GetBit(m_BitField, 0);
                set => ByteUtility.SetBit(ref m_BitField, 0, value);
            }
            public bool HasParent
            {
                get => ByteUtility.GetBit(m_BitField, 1);
                set => ByteUtility.SetBit(ref m_BitField, 1, value);
            }
            public bool IsSceneObject
            {
                get => ByteUtility.GetBit(m_BitField, 2);
                set => ByteUtility.SetBit(ref m_BitField, 2, value);
            }
            public bool HasTransform
            {
                get => ByteUtility.GetBit(m_BitField, 3);
                set => ByteUtility.SetBit(ref m_BitField, 3, value);
            }

            public bool IsLatestParentSet
            {
                get => ByteUtility.GetBit(m_BitField, 4);
                set => ByteUtility.SetBit(ref m_BitField, 4, value);
            }

            public bool WorldPositionStays
            {
                get => ByteUtility.GetBit(m_BitField, 5);
                set => ByteUtility.SetBit(ref m_BitField, 5, value);
            }

            /// <summary>
            /// Even though the server sends notifications for NetworkObjects that get
            /// destroyed when a scene is unloaded, we want to synchronize this so
            /// the client side can use it as part of a filter for automatically migrating
            /// to the current active scene when its scene is unloaded. (only for dynamically spawned)
            /// </summary>
            public bool DestroyWithScene
            {
                get => ByteUtility.GetBit(m_BitField, 6);
                set => ByteUtility.SetBit(ref m_BitField, 6, value);
            }

            public bool DontDestroyWithOwner
            {
                get => ByteUtility.GetBit(m_BitField, 7);
                set => ByteUtility.SetBit(ref m_BitField, 7, value);
            }

            public bool HasOwnershipFlags
            {
                get => ByteUtility.GetBit(m_BitField, 8);
                set => ByteUtility.SetBit(ref m_BitField, 8, value);
            }

            public bool SyncObservers
            {
                get => ByteUtility.GetBit(m_BitField, 9);
                set => ByteUtility.SetBit(ref m_BitField, 9, value);
            }

            public bool SpawnWithObservers
            {
                get => ByteUtility.GetBit(m_BitField, 10);
                set => ByteUtility.SetBit(ref m_BitField, 10, value);
            }

            // When handling the initial synchronization of NetworkObjects,
            // this will be populated with the known observers.
            public ulong[] Observers;

            //If(Metadata.HasParent)
            public ulong ParentObjectId;

            //If(Metadata.HasTransform)
            public struct TransformData : INetworkSerializeByMemcpy
            {
                public Vector3 Position;
                public Quaternion Rotation;
                public Vector3 Scale;
            }

            public TransformData Transform;

            //If(Metadata.IsReparented)

            //If(IsLatestParentSet)
            public ulong? LatestParent;

            public NetworkObject OwnerObject;
            public ulong TargetClientId;

            public int NetworkSceneHandle;


            public void Serialize(FastBufferWriter writer)
            {
                if (OwnerObject.NetworkManager.DistributedAuthorityMode)
                {
                    HasOwnershipFlags = true;
                    SpawnWithObservers = OwnerObject.SpawnWithObservers;
                }
                writer.WriteValueSafe(m_BitField);
                writer.WriteValueSafe(Hash);
                BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
                BytePacker.WriteValueBitPacked(writer, OwnerClientId);

                if (HasParent)
                {
                    BytePacker.WriteValueBitPacked(writer, ParentObjectId);
                    if (IsLatestParentSet)
                    {
                        BytePacker.WriteValueBitPacked(writer, LatestParent.Value);
                    }
                }

                if (HasOwnershipFlags)
                {
                    writer.WriteValueSafe(OwnershipFlags);
                }

                if (SyncObservers)
                {
                    BytePacker.WriteValuePacked(writer, Observers.Length);
                    foreach (var observer in Observers)
                    {
                        BytePacker.WriteValuePacked(writer, observer);
                    }
                }

                var writeSize = 0;
                writeSize += HasTransform ? FastBufferWriter.GetWriteSize<TransformData>() : 0;
                writeSize += FastBufferWriter.GetWriteSize<int>();

                if (!writer.TryBeginWrite(writeSize))
                {
                    throw new OverflowException("Could not serialize SceneObject: Out of buffer space.");
                }

                if (HasTransform)
                {
                    writer.WriteValue(Transform);
                }

                // The NetworkSceneHandle is the server-side relative
                // scene handle that the NetworkObject resides in.
                if (OwnerObject.NetworkManager.DistributedAuthorityMode)
                {
                    writer.WriteValue(OwnerObject.NetworkSceneHandle);
                }
                else
                {
                    writer.WriteValue(OwnerObject.GetSceneOriginHandle());
                }

                // Synchronize NetworkVariables and NetworkBehaviours
                var bufferSerializer = new BufferSerializer<BufferSerializerWriter>(new BufferSerializerWriter(writer));
                OwnerObject.SynchronizeNetworkBehaviours(ref bufferSerializer, TargetClientId);
            }

            public void Deserialize(FastBufferReader reader)
            {
                reader.ReadValueSafe(out m_BitField);
                reader.ReadValueSafe(out Hash);
                ByteUnpacker.ReadValueBitPacked(reader, out NetworkObjectId);
                ByteUnpacker.ReadValueBitPacked(reader, out OwnerClientId);

                if (HasParent)
                {
                    ByteUnpacker.ReadValueBitPacked(reader, out ParentObjectId);
                    if (IsLatestParentSet)
                    {
                        ByteUnpacker.ReadValueBitPacked(reader, out ulong latestParent);
                        LatestParent = latestParent;
                    }
                }

                if (HasOwnershipFlags)
                {
                    reader.ReadValueSafe(out OwnershipFlags);
                }

                if (SyncObservers)
                {
                    var observerCount = 0;
                    var observerId = (ulong)0;
                    ByteUnpacker.ReadValuePacked(reader, out observerCount);
                    Observers = new ulong[observerCount];
                    for (int i = 0; i < observerCount; i++)
                    {
                        ByteUnpacker.ReadValuePacked(reader, out observerId);
                        Observers[i] = observerId;
                    }
                }

                var readSize = 0;
                readSize += HasTransform ? FastBufferWriter.GetWriteSize<TransformData>() : 0;
                readSize += FastBufferWriter.GetWriteSize<int>();

                // Try to begin reading the remaining bytes
                if (!reader.TryBeginRead(readSize))
                {
                    throw new OverflowException("Could not deserialize SceneObject: Reading past the end of the buffer");
                }

                if (HasTransform)
                {
                    reader.ReadValue(out Transform);
                }

                // The NetworkSceneHandle is the server-side relative
                // scene handle that the NetworkObject resides in.
                reader.ReadValue(out NetworkSceneHandle);
            }
        }

        internal void PostNetworkVariableWrite(bool forced = false)
        {
            for (int k = 0; k < ChildNetworkBehaviours.Count; k++)
            {
                ChildNetworkBehaviours[k].PostNetworkVariableWrite(forced);
            }
        }

        /// <summary>
        /// Handles synchronizing NetworkVariables and custom synchronization data for NetworkBehaviours.
        /// </summary>
        /// <remarks>
        /// This is where we determine how much data is written after the associated NetworkObject in order to recover
        /// from a failed instantiated NetworkObject without completely disrupting client synchronization.
        /// </remarks>
        internal void SynchronizeNetworkBehaviours<T>(ref BufferSerializer<T> serializer, ulong targetClientId = 0) where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                var writer = serializer.GetFastBufferWriter();
                var positionBeforeSynchronizing = writer.Position;
                writer.WriteValueSafe((ushort)0);
                var sizeToSkipCalculationPosition = writer.Position;

                // Synchronize NetworkVariables
                WriteNetworkVariableData(writer, targetClientId);
                // Reserve the NetworkBehaviour synchronization count position
                var networkBehaviourCountPosition = writer.Position;
                writer.WriteValueSafe((byte)0);

                // Parse through all NetworkBehaviours and any that return true
                // had additional synchronization data written.
                // (See notes for reading/deserialization below)
                var synchronizationCount = (byte)0;
                foreach (var childBehaviour in ChildNetworkBehaviours)
                {
                    if (childBehaviour.Synchronize(ref serializer, targetClientId))
                    {
                        synchronizationCount++;
                    }
                }

                var currentPosition = writer.Position;
                // Write the total number of bytes written for NetworkVariable and NetworkBehaviour
                // synchronization.
                writer.Seek(positionBeforeSynchronizing);
                // We want the size of everything after our size to skip calculation position
                var size = (ushort)(currentPosition - sizeToSkipCalculationPosition);
                writer.WriteValueSafe(size);
                // Write the number of NetworkBehaviours synchronized
                writer.Seek(networkBehaviourCountPosition);
                writer.WriteValueSafe(synchronizationCount);
                // seek back to the position after writing NetworkVariable and NetworkBehaviour
                // synchronization data.
                writer.Seek(currentPosition);
            }
            else
            {
                var seekToEndOfSynchData = 0;
                var reader = serializer.GetFastBufferReader();
                try
                {
                    reader.ReadValueSafe(out ushort sizeOfSynchronizationData);
                    seekToEndOfSynchData = reader.Position + sizeOfSynchronizationData;
                    // Apply the network variable synchronization data
                    if (!SetNetworkVariableData(reader, targetClientId))
                    {
                        reader.Seek(seekToEndOfSynchData);
                        return;
                    }

                    // Read the number of NetworkBehaviours to synchronize
                    reader.ReadValueSafe(out byte numberSynchronized);

                    var networkBehaviourId = (ushort)0;

                    // If a NetworkBehaviour writes synchronization data, it will first
                    // write its NetworkBehaviourId so when deserializing the client-side
                    // can find the right NetworkBehaviour to deserialize the synchronization data.
                    for (int i = 0; i < numberSynchronized; i++)
                    {
                        reader.ReadValueSafe(out networkBehaviourId);
                        var networkBehaviour = GetNetworkBehaviourAtOrderIndex(networkBehaviourId);
                        networkBehaviour.Synchronize(ref serializer, targetClientId);
                    }

                    if (seekToEndOfSynchData != reader.Position)
                    {
                        Debug.LogWarning($"[Size mismatch] Expected: {seekToEndOfSynchData} Currently At: {reader.Position}!");
                    }
                }
                catch
                {
                    reader.Seek(seekToEndOfSynchData);
                }
            }
        }

        internal SceneObject GetMessageSceneObject(ulong targetClientId = NetworkManager.ServerClientId, bool syncObservers = false)
        {
            var obj = new SceneObject
            {
                NetworkObjectId = NetworkObjectId,
                OwnerClientId = OwnerClientId,
                IsPlayerObject = IsPlayerObject,
                IsSceneObject = IsSceneObject ?? true,
                DestroyWithScene = DestroyWithScene,
                DontDestroyWithOwner = DontDestroyWithOwner,
                HasOwnershipFlags = NetworkManager.DistributedAuthorityMode,
                OwnershipFlags = (ushort)Ownership,
                SyncObservers = syncObservers,
                Observers = syncObservers ? Observers.ToArray() : null,
                NetworkSceneHandle = NetworkSceneHandle,
                Hash = HostCheckForGlobalObjectIdHashOverride(),
                OwnerObject = this,
                TargetClientId = targetClientId
            };

            NetworkObject parentNetworkObject = null;

            if (!AlwaysReplicateAsRoot && transform.parent != null)
            {
                parentNetworkObject = transform.parent.GetComponent<NetworkObject>();
                // In-scene placed NetworkObjects parented under GameObjects with no NetworkObject
                // should set the has parent flag and preserve the world position stays value
                if (parentNetworkObject == null && obj.IsSceneObject)
                {
                    obj.HasParent = true;
                    obj.WorldPositionStays = m_CachedWorldPositionStays;
                }
            }

            if (parentNetworkObject != null)
            {
                obj.HasParent = true;
                obj.ParentObjectId = parentNetworkObject.NetworkObjectId;
                obj.WorldPositionStays = m_CachedWorldPositionStays;
                var latestParent = GetNetworkParenting();
                var isLatestParentSet = latestParent != null && latestParent.HasValue;
                obj.IsLatestParentSet = isLatestParentSet;
                if (isLatestParentSet)
                {
                    obj.LatestParent = latestParent.Value;
                }
            }

            if (IncludeTransformWhenSpawning == null || IncludeTransformWhenSpawning(OwnerClientId))
            {
                obj.HasTransform = SynchronizeTransform;

                // We start with the default AutoObjectParentSync values to determine which transform space we will
                // be synchronizing clients with.
                var syncRotationPositionLocalSpaceRelative = obj.HasParent && !m_CachedWorldPositionStays;
                var syncScaleLocalSpaceRelative = obj.HasParent && !m_CachedWorldPositionStays;

                // Always synchronize in-scene placed object's scale using local space
                if (obj.IsSceneObject)
                {
                    syncScaleLocalSpaceRelative = obj.HasParent;
                }

                // If auto object synchronization is turned off
                if (!AutoObjectParentSync)
                {
                    // We always synchronize position and rotation world space relative
                    syncRotationPositionLocalSpaceRelative = false;
                    // Scale is special, it synchronizes local space relative if it has a
                    // parent since applying the world space scale under a parent with scale
                    // will result in the improper scale for the child
                    syncScaleLocalSpaceRelative = obj.HasParent;
                }

                obj.Transform = new SceneObject.TransformData
                {
                    // If we are parented and we have the m_CachedWorldPositionStays disabled, then use local space
                    // values as opposed world space values.
                    Position = syncRotationPositionLocalSpaceRelative ? transform.localPosition : transform.position,
                    Rotation = syncRotationPositionLocalSpaceRelative ? transform.localRotation : transform.rotation,

                    // We only use the lossyScale if the NetworkObject has a parent. Multi-generation nested children scales can
                    // impact the final scale of the child NetworkObject in question. The solution is to use the lossy scale
                    // which can be thought of as "world space scale".
                    // More information:
                    // https://docs.unity3d.com/ScriptReference/Transform-lossyScale.html
                    Scale = syncScaleLocalSpaceRelative ? transform.localScale : transform.lossyScale,
                };
            }
            return obj;
        }

        /// <summary>
        /// Used to deserialize a serialized scene object which occurs
        /// when the client is approved or during a scene transition
        /// </summary>
        /// <param name="sceneObject">Deserialized scene object data</param>
        /// <param name="reader">FastBufferReader for the NetworkVariable data</param>
        /// <param name="networkManager">NetworkManager instance</param>
        /// <param name="invokedByMessage">will be true if invoked by CreateObjectMessage</param>
        /// <returns>The deserialized NetworkObject or null if deserialization failed</returns>
        internal static NetworkObject AddSceneObject(in SceneObject sceneObject, FastBufferReader reader, NetworkManager networkManager, bool invokedByMessage = false)
        {
            //Attempt to create a local NetworkObject
            var networkObject = networkManager.SpawnManager.CreateLocalNetworkObject(sceneObject);

            if (networkObject == null)
            {
                // Log the error that the NetworkObject failed to construct
                if (networkManager.LogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogError($"Failed to spawn {nameof(NetworkObject)} for Hash {sceneObject.Hash}.");
                }

                try
                {
                    // If we failed to load this NetworkObject, then skip past the Network Variable and (if any) synchronization data
                    reader.ReadValueSafe(out ushort networkBehaviourSynchronizationDataLength);
                    reader.Seek(reader.Position + networkBehaviourSynchronizationDataLength);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                // We have nothing left to do here.
                return null;
            }

            // This will get set again when the NetworkObject is spawned locally, but we set it here ahead of spawning
            // in order to be able to determine which NetworkVariables the client will be allowed to read.
            networkObject.OwnerClientId = sceneObject.OwnerClientId;

            // Special Case: Invoke NetworkBehaviour.OnPreSpawn methods here before SynchronizeNetworkBehaviours
            networkObject.InvokeBehaviourNetworkPreSpawn();

            // Synchronize NetworkBehaviours
            var bufferSerializer = new BufferSerializer<BufferSerializerReader>(new BufferSerializerReader(reader));
            networkObject.SynchronizeNetworkBehaviours(ref bufferSerializer, networkManager.LocalClientId);

            // Spawn the NetworkObject
            networkManager.SpawnManager.SpawnNetworkObjectLocally(networkObject, sceneObject, sceneObject.DestroyWithScene);

            if (sceneObject.SyncObservers)
            {
                foreach (var observer in sceneObject.Observers)
                {
                    networkObject.Observers.Add(observer);
                }
            }

            if (networkManager.DistributedAuthorityMode)
            {
                networkObject.SpawnWithObservers = sceneObject.SpawnWithObservers;
            }

            // If this was not invoked by a message handler, we are in distributed authority mode, and we are spawning with observers or
            // we are an observer (in case SpawnWithObservers is false)
            if (networkManager.DistributedAuthorityMode && (!invokedByMessage || networkObject.IsPlayerObject) &&
                (networkObject.SpawnWithObservers || networkObject.Observers.Contains(networkManager.LocalClientId)))
            {
                if (networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
                {
                    var playerObject = networkManager.LocalClient.PlayerObject;
                    if (networkObject.IsPlayerObject)
                    {
                        // If it is another player, then make sure the local player is aware of the player
                        playerObject.Observers.Add(networkObject.OwnerClientId);
                    }

                    // Assure the local player has observability
                    networkObject.Observers.Add(playerObject.OwnerClientId);

                    // If it is a player object, then add it to all known spawned NetworkObjects that spawn with observers
                    if (networkObject.IsPlayerObject)
                    {
                        foreach (var netObject in networkManager.SpawnManager.SpawnedObjects)
                        {
                            if (netObject.Value.SpawnWithObservers)
                            {
                                netObject.Value.Observers.Add(networkObject.OwnerClientId);
                            }
                        }
                    }

                    // Only add all other players as observers if we are spawning with observers,
                    // otherwise user controls via NetworkShow.
                    if (networkObject.SpawnWithObservers)
                    {
                        // Add all known players to the observers list if they don't already exist
                        foreach (var player in networkManager.SpawnManager.PlayerObjects)
                        {
                            networkObject.Observers.Add(player.OwnerClientId);
                        }
                    }
                }
            }
            return networkObject;
        }

        /// <summary>
        /// Subscribes to changes in the currently active scene
        /// </summary>
        /// <remarks>
        /// Only for dynamically spawned NetworkObjects
        /// </remarks>
        internal void SubscribeToActiveSceneForSynch()
        {
            if (ActiveSceneSynchronization)
            {
                if (IsSceneObject.HasValue && !IsSceneObject.Value)
                {
                    // Just in case it is a recycled NetworkObject, unsubscribe first
                    SceneManager.activeSceneChanged -= CurrentlyActiveSceneChanged;
                    SceneManager.activeSceneChanged += CurrentlyActiveSceneChanged;
                }
            }
        }

        /// <summary>
        /// If AutoSynchActiveScene is enabled, then this is the callback that handles updating
        /// a NetworkObject's scene information.
        /// </summary>
        private void CurrentlyActiveSceneChanged(Scene current, Scene next)
        {
            // Early exit if there is no NetworkManager assigned, the NetworkManager is shutting down, the NetworkObject
            // is not spawned, or an in-scene placed NetworkObject
            if (NetworkManager == null || NetworkManager.ShutdownInProgress || !IsSpawned || IsSceneObject != false)
            {
                return;
            }
            // This check is here in the event a user wants to disable this for some reason but also wants
            // the NetworkObject to synchronize to changes in the currently active scene at some later time.
            if (ActiveSceneSynchronization)
            {
                // Only dynamically spawned NetworkObjects that are not already in the newly assigned active scene will migrate
                // and update their scene handles
                if (IsSceneObject.HasValue && !IsSceneObject.Value && gameObject.scene != next && gameObject.transform.parent == null)
                {
                    SceneManager.MoveGameObjectToScene(gameObject, next);
                    SceneChangedUpdate(next);
                }
            }
        }

        /// <summary>
        /// Handles updating the NetworkObject's tracked scene handles
        /// </summary>
        internal void SceneChangedUpdate(Scene scene, bool notify = false)
        {
            // Avoiding edge case scenarios, if no NetworkSceneManager exit early
            if (NetworkManager.SceneManager == null || !IsSpawned)
            {
                return;
            }

            if (NetworkManager.SceneManager.IsSceneEventInProgress())
            {
                return;
            }

            var isAuthority = HasAuthority;
            SceneOriginHandle = scene.handle;

            // non-authority needs to update the NetworkSceneHandle
            if (!isAuthority && NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle.ContainsKey(SceneOriginHandle))
            {
                NetworkSceneHandle = NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle[SceneOriginHandle];
            }
            else if (isAuthority)
            {
                // Since the authority is the source of truth for the NetworkSceneHandle,
                // the NetworkSceneHandle is the same as the SceneOriginHandle.
                if (NetworkManager.DistributedAuthorityMode)
                {
                    NetworkSceneHandle = NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle[SceneOriginHandle];
                }
                else
                {
                    NetworkSceneHandle = SceneOriginHandle;
                }

            }
            else // Otherwise, the client did not find the client to server scene handle
            if (NetworkManager.LogLevel == LogLevel.Developer)
            {
                // There could be a scenario where a user has some client-local scene loaded that they migrate the NetworkObject
                // into, but that scenario seemed very edge case and under most instances a user should be notified that this
                // server - client scene handle mismatch has occurred. It also seemed pertinent to make the message replicate to
                // the server-side too.
                NetworkLog.LogWarningServer($"[Client-{NetworkManager.LocalClientId}][{gameObject.name}] Server - " +
                    $"client scene mismatch detected! Client-side scene handle ({SceneOriginHandle}) for scene ({gameObject.scene.name})" +
                    $"has no associated server side (network) scene handle!");
            }
            OnMigratedToNewScene?.Invoke();

            // Only the authority side will notify clients of non-parented NetworkObject scene changes
            if (isAuthority && notify && transform.parent == null)
            {
                NetworkManager.SceneManager.NotifyNetworkObjectSceneChanged(this);
            }
        }

        internal static Dictionary<ulong, NetworkObject> NetworkObjectsToSynchronizeSceneChanges = new Dictionary<ulong, NetworkObject>();

        internal static void AddNetworkObjectToSceneChangedUpdates(NetworkObject networkObject)
        {
            if (!NetworkObjectsToSynchronizeSceneChanges.ContainsKey(networkObject.NetworkObjectId))
            {
                NetworkObjectsToSynchronizeSceneChanges.Add(networkObject.NetworkObjectId, networkObject);
            }

            networkObject.UpdateForSceneChanges();
        }

        internal static void RemoveNetworkObjectFromSceneChangedUpdates(NetworkObject networkObject)
        {
            NetworkObjectsToSynchronizeSceneChanges.Remove(networkObject.NetworkObjectId);
        }

        internal static void UpdateNetworkObjectSceneChanges()
        {
            foreach (var entry in NetworkObjectsToSynchronizeSceneChanges)
            {
                entry.Value.UpdateForSceneChanges();
            }
        }

        private void Awake()
        {
            m_ChildNetworkBehaviours = null;
            NetworkTransforms?.Clear();
#if COM_UNITY_MODULES_PHYSICS
            NetworkRigidbodies?.Clear();
#endif
            SetCachedParent(transform.parent);
            SceneOrigin = gameObject.scene;
        }

        /// <summary>
        /// Update
        /// Detects if a NetworkObject's scene has changed for both server and client instances
        /// </summary>
        /// <remarks>
        /// About In-Scene Placed NetworkObjects:
        /// Since the same scene can be loaded more than once and in-scene placed NetworkObjects GlobalObjectIdHash
        /// values are only unique to the scene asset itself (and not per scene instance loaded), we will not be able
        /// to add this same functionality to in-scene placed NetworkObjects until we have a way to generate
        /// per-NetworkObject-instance unique GlobalObjectIdHash values for in-scene placed NetworkObjects.
        /// </remarks>
        internal void UpdateForSceneChanges()
        {
            // Early exit if SceneMigrationSynchronization is disabled, there is no NetworkManager assigned,
            // the NetworkManager is shutting down, the NetworkObject is not spawned, it is an in-scene placed
            // NetworkObject, or the GameObject's current scene handle is the same as the SceneOriginHandle
            if (!SceneMigrationSynchronization || !IsSpawned || NetworkManager == null || NetworkManager.ShutdownInProgress ||
                !NetworkManager.NetworkConfig.EnableSceneManagement || IsSceneObject != false || gameObject.scene.handle == SceneOriginHandle)
            {
                return;
            }

            // Otherwise, this has to be a dynamically spawned NetworkObject that has been
            // migrated to a new scene.
            SceneChangedUpdate(gameObject.scene, true);
        }

        /// <summary>
        /// Only applies to Host mode.
        /// Will return the registered source NetworkPrefab's GlobalObjectIdHash if one exists.
        /// Server and Clients will always return the NetworkObject's GlobalObjectIdHash.
        /// </summary>
        /// <returns></returns>
        internal uint HostCheckForGlobalObjectIdHashOverride()
        {
            if (NetworkManager.IsServer)
            {
                if (NetworkManager.PrefabHandler.ContainsHandler(this))
                {
                    var globalObjectIdHash = NetworkManager.PrefabHandler.GetSourceGlobalObjectIdHash(GlobalObjectIdHash);
                    return globalObjectIdHash == 0 ? GlobalObjectIdHash : globalObjectIdHash;
                }

                // If scene management is disabled and this is an in-scene placed NetworkObject then go ahead
                // and send the InScenePlacedSourcePrefab's GlobalObjectIdHash value (i.e. what to dynamically spawn)
                if (!NetworkManager.NetworkConfig.EnableSceneManagement && IsSceneObject.Value && InScenePlacedSourceGlobalObjectIdHash != 0)
                {
                    return InScenePlacedSourceGlobalObjectIdHash;
                }

                // If the PrefabGlobalObjectIdHash is a non-zero value and the GlobalObjectIdHash value is
                // different from the PrefabGlobalObjectIdHash value, then the NetworkObject instance is
                // an override for the original network prefab (i.e. PrefabGlobalObjectIdHash)
                if (!IsSceneObject.Value && GlobalObjectIdHash != PrefabGlobalObjectIdHash)
                {
                    // If the PrefabGlobalObjectIdHash is already populated (i.e. InstantiateAndSpawn used), then return this
                    if (PrefabGlobalObjectIdHash != 0)
                    {
                        return PrefabGlobalObjectIdHash;
                    }
                    else
                    {
                        // For legacy manual instantiation and spawning, check the OverrideToNetworkPrefab for a possible match
                        if (NetworkManager.NetworkConfig.Prefabs.OverrideToNetworkPrefab.ContainsKey(GlobalObjectIdHash))
                        {
                            return NetworkManager.NetworkConfig.Prefabs.OverrideToNetworkPrefab[GlobalObjectIdHash];
                        }
                    }
                }
            }

            return GlobalObjectIdHash;
        }

        /// <summary>
        /// Removes a NetworkBehaviour from the ChildNetworkBehaviours list when destroyed
        /// while the NetworkObject is still spawned.
        /// </summary>
        internal void OnNetworkBehaviourDestroyed(NetworkBehaviour networkBehaviour)
        {
            if (networkBehaviour.IsSpawned && IsSpawned)
            {
                if (NetworkManager?.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkBehaviour)}-{networkBehaviour.name} is being destroyed while {nameof(NetworkObject)}-{name} is still spawned! (could break state synchronization)");
                }
                ChildNetworkBehaviours.Remove(networkBehaviour);
            }
        }
    }
}
