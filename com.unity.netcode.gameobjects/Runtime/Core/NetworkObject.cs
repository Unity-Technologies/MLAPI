using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            NetworkObjectRefreshTool.PrefabNetworkObject = this;
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
        public ulong OwnerClientId { get; internal set; }

        internal ulong PreviousOwnerId;

        /// <summary>
        /// If true, the object will always be replicated as root on clients and the parent will be ignored.
        /// </summary>
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
        [Tooltip("When false, the NetworkObject will spawn with no observers initially. (default is true)")]
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
        public bool DontDestroyWithOwner;

        /// <summary>
        /// Whether or not to enable automatic NetworkObject parent synchronization.
        /// </summary>
        public bool AutoObjectParentSync = true;

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

        private void Awake()
        {
            m_ChildNetworkBehaviours = null;
            SetCachedParent(transform.parent);
            SceneOrigin = gameObject.scene;
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

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (Observers.Contains(clientId))
            {
                throw new VisibilityChangeException("The object is already visible");
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

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (clientId == NetworkManager.ServerClientId)
            {
                throw new VisibilityChangeException("Cannot hide an object from the server");
            }

            if (!NetworkManager.SpawnManager.RemoveObjectFromShowingTo(this, clientId))
            {
                if (!Observers.Contains(clientId))
                {
                    throw new VisibilityChangeException("The object is already hidden");
                }
                Observers.Remove(clientId);

                var message = new DestroyObjectMessage
                {
                    NetworkObjectId = NetworkObjectId,
                    DestroyGameObject = !IsSceneObject.Value
                };
                // Send destroy call
                var size = NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
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
                throw new ArgumentNullException("At least one " + nameof(NetworkObject) + " has to be provided");
            }

            var networkManager = networkObjects[0].NetworkManager;

            if (!networkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (clientId == NetworkManager.ServerClientId)
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
            // If no NetworkManager is assigned, then just exit early
            if (!NetworkManager)
            {
                return;
            }

            if (NetworkManager.IsListening && !NetworkManager.IsServer && IsSpawned &&
                (IsSceneObject == null || (IsSceneObject.Value != true)))
            {
                // Clients should not despawn NetworkObjects while connected to a session, but we don't want to destroy the current call stack
                // if this happens. Instead, we should just generate a network log error and exit early (as long as we are not shutting down).
                if (!NetworkManager.ShutdownInProgress)
                {
                    // Since we still have a session connection, log locally and on the server to inform user of this issue.
                    if (NetworkManager.LogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogErrorServer($"[Invalid Destroy][{gameObject.name}][NetworkObjectId:{NetworkObjectId}] Destroy a spawned {nameof(NetworkObject)} on a non-host client is not valid. Call {nameof(Destroy)} or {nameof(Despawn)} on the server/host instead.");
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
            if (!NetworkManager.IsListening)
            {
                throw new NotListeningException($"{nameof(NetworkManager)} is not listening, start a server or host before spawning objects");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException($"Only server can spawn {nameof(NetworkObject)}s");
            }

            NetworkManager.SpawnManager.SpawnNetworkObjectLocally(this, NetworkManager.SpawnManager.GetNetworkObjectId(), IsSceneObject.HasValue && IsSceneObject.Value, playerObject, ownerClientId, destroyWithScene);

            for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
            {
                if (Observers.Contains(NetworkManager.ConnectedClientsList[i].ClientId))
                {
                    NetworkManager.SpawnManager.SendSpawnCallForObject(NetworkManager.ConnectedClientsList[i].ClientId, this);
                }
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

            if (!networkManager.IsServer)
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotAuthority]);
                return null;
            }

            if (NetworkManager.ShutdownInProgress)
            {
                Debug.LogWarning(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.InvokedWhenShuttingDown]);
                return null;
            }

            // Verify it is actually a valid prefab
            if (!NetworkManager.NetworkConfig.Prefabs.Contains(gameObject))
            {
                Debug.LogError(NetworkSpawnManager.InstantiateAndSpawnErrors[NetworkSpawnManager.InstantiateAndSpawnErrorTypes.NotRegisteredNetworkPrefab]);
                return null;
            }

            return NetworkManager.SpawnManager.InstantiateAndSpawnNoParameterChecks(this, ownerClientId, destroyWithScene, isPlayerObject, forceOverride, position, rotation);
        }

        /// <summary>
        /// Spawns this <see cref="NetworkObject"/> across the network. Can only be called from the Server
        /// </summary>
        /// <param name="destroyWithScene">Should the object be destroyed when the scene is changed</param>
        public void Spawn(bool destroyWithScene = false)
        {
            SpawnInternal(destroyWithScene, NetworkManager.ServerClientId, false);
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
            NetworkManager.SpawnManager.ChangeOwnership(this, newOwnerClientId);
        }

        internal void InvokeBehaviourOnLostOwnership()
        {
            // Server already handles this earlier, hosts should ignore, all clients should update
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
            // Server already handles this earlier, hosts should ignore and only client owners should update
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
                ChildNetworkBehaviours[i].OnNetworkObjectParentChanged(parentNetworkObject);
            }
        }

        private ulong? m_LatestParent; // What is our last set parent NetworkObject's ID?
        private Transform m_CachedParent; // What is our last set parent Transform reference?
        private bool m_CachedWorldPositionStays = true; // Used to preserve the world position stays parameter passed in TrySetParent

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
            return TrySetParent((NetworkObject)null, m_CachedWorldPositionStays);
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

            if (!NetworkManager.IsServer && !NetworkManager.ShutdownInProgress)
            {
                return false;
            }

            // If the parent is not null fail only if either of the two is true:
            // - This instance is spawned and the parent is not.
            // - This instance is not spawned and the parent is.
            // Basically, don't allow parenting when either the child or parent is not spawned.
            // Caveat: if the parent is null then we can allow parenting whether the instance is or is not spawned.
            if (parent != null && (IsSpawned ^ parent.IsSpawned))
            {
                return false;
            }

            m_CachedWorldPositionStays = worldPositionStays;

            if (parent == null)
            {
                transform.SetParent(null, worldPositionStays);
            }
            else
            {
                transform.SetParent(parent.transform, worldPositionStays);
            }

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
                // Log exception if we are a client and not shutting down.
                if (!NetworkManager.ShutdownInProgress)
                {
                    transform.parent = m_CachedParent;
                    Debug.LogException(new NotServerException($"Only the server can reparent {nameof(NetworkObject)}s"));
                }
                else // Otherwise, if we are removing a parent then go ahead and allow parenting to occur
                if (transform.parent == null)
                {
                    m_LatestParent = null;
                    m_CachedParent = null;
                    InvokeBehaviourOnNetworkObjectParentChanged(null);
                }
                return;
            }
            else // Otherwise, on the serer side if this instance is not spawned...
            if (!IsSpawned)
            {
                // ,,,and we are removing the parent, then go ahead and allow parenting to occur
                if (transform.parent == null)
                {
                    m_LatestParent = null;
                    m_CachedParent = null;
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
                removeParent = m_CachedParent != null;
            }

            ApplyNetworkParenting(removeParent);

            var message = new ParentSyncMessage
            {
                NetworkObjectId = NetworkObjectId,
                IsLatestParentSet = m_LatestParent != null && m_LatestParent.HasValue,
                LatestParent = m_LatestParent,
                RemoveParent = removeParent,
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

            unsafe
            {
                var maxCount = NetworkManager.ConnectedClientsIds.Count;
                ulong* clientIds = stackalloc ulong[maxCount];
                int idx = 0;
                foreach (var clientId in NetworkManager.ConnectedClientsIds)
                {
                    if (Observers.Contains(clientId))
                    {
                        clientIds[idx++] = clientId;
                    }
                }

                NetworkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientIds, idx);
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
                    m_CachedParent = parentNetworkObject.transform;

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
                m_CachedParent = null;
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

            m_CachedParent = parentObject.transform;
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

        internal void WriteNetworkVariableData(FastBufferWriter writer, ulong targetClientId)
        {
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
        internal void SetNetworkVariableData(FastBufferReader reader, ulong clientId)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                var behaviour = ChildNetworkBehaviours[i];
                behaviour.InitializeVariables();
                behaviour.SetNetworkVariableData(reader, clientId);
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

        public NetworkBehaviour GetNetworkBehaviourAtOrderIndex(ushort index)
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
            private byte m_BitField;
            public uint Hash;
            public ulong NetworkObjectId;
            public ulong OwnerClientId;

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
                writer.WriteValue(OwnerObject.GetSceneOriginHandle());

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

        internal void PostNetworkVariableWrite(bool forceSend)
        {
            for (int k = 0; k < ChildNetworkBehaviours.Count; k++)
            {
                ChildNetworkBehaviours[k].PostNetworkVariableWrite(forceSend);
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
                var reader = serializer.GetFastBufferReader();

                reader.ReadValueSafe(out ushort sizeOfSynchronizationData);
                var seekToEndOfSynchData = reader.Position + sizeOfSynchronizationData;
                // Apply the network variable synchronization data
                SetNetworkVariableData(reader, targetClientId);
                // Read the number of NetworkBehaviours to synchronize
                reader.ReadValueSafe(out byte numberSynchronized);
                var networkBehaviourId = (ushort)0;

                // If a NetworkBehaviour writes synchronization data, it will first
                // write its NetworkBehaviourId so when deserializing the client-side
                // can find the right NetworkBehaviour to deserialize the synchronization data.
                for (int i = 0; i < numberSynchronized; i++)
                {
                    serializer.SerializeValue(ref networkBehaviourId);
                    var networkBehaviour = GetNetworkBehaviourAtOrderIndex(networkBehaviourId);
                    networkBehaviour.Synchronize(ref serializer, targetClientId);
                }
            }
        }

        internal SceneObject GetMessageSceneObject(ulong targetClientId)
        {
            var obj = new SceneObject
            {
                NetworkObjectId = NetworkObjectId,
                OwnerClientId = OwnerClientId,
                IsPlayerObject = IsPlayerObject,
                IsSceneObject = IsSceneObject ?? true,
                DestroyWithScene = DestroyWithScene,
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
        /// <returns>optional to use NetworkObject deserialized</returns>
        internal static NetworkObject AddSceneObject(in SceneObject sceneObject, FastBufferReader reader, NetworkManager networkManager)
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
            if (NetworkManager.SceneManager == null)
            {
                return;
            }

            SceneOriginHandle = scene.handle;
            // Clients need to update the NetworkSceneHandle
            if (!NetworkManager.IsServer && NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle.ContainsKey(SceneOriginHandle))
            {
                NetworkSceneHandle = NetworkManager.SceneManager.ClientSceneHandleToServerSceneHandle[SceneOriginHandle];
            }
            else if (NetworkManager.IsServer)
            {
                // Since the server is the source of truth for the NetworkSceneHandle,
                // the NetworkSceneHandle is the same as the SceneOriginHandle.
                NetworkSceneHandle = SceneOriginHandle;
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

            // Only the server side will notify clients of non-parented NetworkObject scene changes
            if (NetworkManager.IsServer && notify && transform.parent == null)
            {
                NetworkManager.SceneManager.NotifyNetworkObjectSceneChanged(this);
            }
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
        private void Update()
        {
            // Early exit if SceneMigrationSynchronization is disabled, there is no NetworkManager assigned,
            // the NetworkManager is shutting down, the NetworkObject is not spawned, it is an in-scene placed
            // NetworkObject, or the GameObject's current scene handle is the same as the SceneOriginHandle
            if (!SceneMigrationSynchronization || NetworkManager == null || NetworkManager.ShutdownInProgress || !IsSpawned
                || IsSceneObject != false || gameObject.scene.handle == SceneOriginHandle)
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
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkBehaviour)}-{networkBehaviour.name} is being destroyed while {nameof(NetworkObject)}-{name} is still spawned! (could break state synchronization)");
                }
                ChildNetworkBehaviours.Remove(networkBehaviour);
            }
        }
    }
}
