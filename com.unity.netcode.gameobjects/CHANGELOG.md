# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Additional documentation and release notes are available at [Multiplayer Documentation](https://docs-multiplayer.unity3d.com).

## [2.1.1] - 2024-10-18

### Added

- Added ability to edit the `NetworkConfig.AutoSpawnPlayerPrefabClientSide` within the inspector view. (#3097)
- Added `IContactEventHandlerWithInfo` that derives from `IContactEventHandler` that can be updated per frame to provide `ContactEventHandlerInfo` information to the `RigidbodyContactEventManager` when processing collisions. (#3094)
  - `ContactEventHandlerInfo.ProvideNonRigidBodyContactEvents`: When set to true, non-`Rigidbody` collisions with the registered `Rigidbody` will generate contact event notifications. (#3094)
  - `ContactEventHandlerInfo.HasContactEventPriority`: When set to true, the `Rigidbody` will be prioritized as the instance that generates the event if the `Rigidbody` colliding does not have priority. (#3094)
- Added a static `NetworkManager.OnInstantiated` event notification to be able to track when a new `NetworkManager` instance has been instantiated. (#3088)
- Added a static `NetworkManager.OnDestroying` event notification to be able to track when an existing `NetworkManager` instance is being destroyed. (#3088)

### Fixed

- Fixed issue where `NetworkPrefabProcessor` would not mark the prefab list as dirty and prevent saving the `DefaultNetworkPrefabs` asset when only imports or only deletes were detected.(#3103)
- Fixed an issue where nested `NetworkTransform` components in owner authoritative mode cleared their initial settings on the server, causing improper synchronization. (#3099)
- Fixed issue with service not getting synchronized with in-scene placed `NetworkObject` instances when a session owner starts a `SceneEventType.Load` event. (#3096)
- Fixed issue with the in-scene network prefab instance update menu tool where it was not properly updating scenes when invoked on the root prefab instance. (#3092)
- Fixed an issue where newly synchronizing clients would always receive current `NetworkVariable` values, potentially causing issues with collections if there were pending updates. Now, pending state updates serialize previous values to avoid duplicates on new clients. (#3081)
- Fixed issue where changing ownership would mark every `NetworkVariable` dirty. Now, it will only mark any `NetworkVariable` with owner read permissions as dirty and will send/flush any pending updates to all clients prior to sending the change in ownership message. (#3081)
- Fixed an issue where transferring ownership of `NetworkVariable` collections didn't update the new owner’s previous value, causing the last added value to be detected as a change during additions or removals. (#3081)
- Fixed issue where a client (or server) with no write permissions for a `NetworkVariable` using a standard .NET collection type could still modify the collection which could cause various issues depending upon the modification and collection type. (#3081)
- Fixed issue where applying the position and/or rotation to the `NetworkManager.ConnectionApprovalResponse` when connection approval and auto-spawn player prefab were enabled would not apply the position and/or rotation when the player prefab was instantiated. (#3078)
- Fixed issue where `NetworkObject.SpawnWithObservers` was not being honored when spawning the player prefab. (#3077)
- Fixed issue with the client count not being correct on the host or server side when a client disconnects itself from a session. (#3075)

### Changed

- Changed `NetworkConfig.AutoSpawnPlayerPrefabClientSide` is no longer automatically set when starting `NetworkManager`. (#3097)
- Updated `NetworkVariableDeltaMessage` so the server now forwards delta state updates from clients immediately, instead of waiting until the end of the frame or the next network tick. (#3081)

## [2.0.0] - 2024-09-12

### Added

- Added tooltips for all of the `NetworkObject` component's properties. (#3052)
- Added message size validation to named and unnamed message sending functions for better error messages. (#3049)
- Added "Check for NetworkObject Component" property to the Multiplayer->Netcode for GameObjects project settings. When disabled, this will bypass the in-editor `NetworkObject` check on `NetworkBehaviour` components. (#3031)
- Added `NetworkTransform.SwitchTransformSpaceWhenParented` property that, when enabled, will handle the world to local, local to world, and local to local transform space transitions when interpolation is enabled. (#3013)
- Added `NetworkTransform.TickSyncChildren` that, when enabled, will tick synchronize nested and/or child `NetworkTransform` components to eliminate any potential visual jittering that could occur if the `NetworkTransform` instances get into a state where their state updates are landing on different network ticks. (#3013)
- Added `NetworkObject.AllowOwnerToParent` property to provide the ability to allow clients to parent owned objects when running in a client-server network topology. (#3013)
- Added `NetworkObject.SyncOwnerTransformWhenParented` property to provide a way to disable applying the server's transform information in the parenting message on the client owner instance which can be useful for owner authoritative motion models. (#3013)
- Added `NetcodeEditorBase` editor helper class to provide easier modification and extension of the SDK's components. (#3013)

### Fixed

- Fixed issue where `NetworkAnimator` would send updates to non-observer clients. (#3057)
- Fixed issue where an exception could occur when receiving a universal RPC for a `NetworkObject` that has been despawned. (#3052)
- Fixed issue where a NetworkObject hidden from a client that is then promoted to be session owner was not being synchronized with newly joining clients.(#3051)
- Fixed issue where clients could have a wrong time delta on `NetworkVariableBase` which could prevent from sending delta state updates. (#3045)
- Fixed issue where setting a prefab hash value during connection approval but not having a player prefab assigned could cause an exception when spawning a player. (#3042)
- Fixed issue where the `NetworkSpawnManager.HandleNetworkObjectShow` could throw an exception if one of the `NetworkObject` components to show was destroyed during the same frame. (#3030)
- Fixed issue where the `NetworkManagerHelper` was continuing to check for hierarchy changes when in play mode. (#3026)
- Fixed issue with newly/late joined clients and `NetworkTransform` synchronization of parented `NetworkObject` instances. (#3013)
- Fixed issue with smooth transitions between transform spaces when interpolation is enabled (requires `NetworkTransform.SwitchTransformSpaceWhenParented` to be enabled). (#3013)

### Changed

- Changed `NetworkTransformEditor` now uses `NetworkTransform` as the base type class to assure it doesn't display a foldout group when using the base `NetworkTransform` component class. (#3052)
- Changed `NetworkAnimator.Awake` is now a protected virtual method. (#3052)
- Changed  when invoking `NetworkManager.ConnectionManager.DisconnectClient` during a distributed authority session a more appropriate message is logged. (#3052)
- Changed `NetworkTransformEditor` so it now derives from `NetcodeEditorBase`. (#3013)
- Changed `NetworkRigidbodyBaseEditor` so it now derives from `NetcodeEditorBase`. (#3013)
- Changed `NetworkManagerEditor` so it now derives from `NetcodeEditorBase`. (#3013)


## [2.0.0-pre.4] - 2024-08-21

### Added

- Added `NetworkVariable.CheckDirtyState` that is to be used in tandem with collections in order to detect whether the collection or an item within the collection has changed. (#3004)

### Fixed

- Fixed issue where nested `NetworkTransform` components were not getting updated. (#3016)
- Fixed issue by adding null checks in `NetworkVariableBase.CanClientRead` and `NetworkVariableBase.CanClientWrite` methods to ensure safe access to `NetworkBehaviour`. (#3012)
- Fixed issue where `FixedStringSerializer<T>` was using `NetworkVariableSerialization<byte>.AreEqual` to determine if two bytes were equal causes an exception to be thrown due to no byte serializer having been defined. (#3009)
- Fixed Issue where a state with dual triggers, inbound and outbound, could cause a false layer to layer state transition message to be sent to non-authority `NetworkAnimator` instances and cause a warning message to be logged. (#3008)
- Fixed issue using collections within `NetworkVariable` where the collection would not detect changes to items or nested items. (#3004)
- Fixed issue where `List`, `Dictionary`, and `HashSet` collections would not uniquely duplicate nested collections. (#3004)
- Fixed issue where `NotAuthorityTarget` would include the service observer in the list of targets to send the RPC to as opposed to excluding the service observer as it should. (#3000)
- Fixed issue where `ProxyRpcTargetGroup` could attempt to send a message if there were no targets to send to. (#3000)

### Changed

- Changed `NetworkAnimator` to automatically switch to owner authoritative mode when using a distributed authority network topology. (#3021)
- Changed permissions exception thrown in `NetworkList` to exiting early with a logged error that is now a unified permissions message within `NetworkVariableBase`. (#3004)
- Changed permissions exception thrown in `NetworkVariable.Value` to exiting early with a logged error that is now a unified permissions message within `NetworkVariableBase`. (#3004)


## [2.0.0-pre.3] - 2024-07-23

### Added
- Added: `UnityTransport.GetNetworkDriver` and `UnityTransport.GetLocalEndpoint` methods to expose the driver and local endpoint being used. (#2978)

### Fixed

- Fixed issue where deferred despawn was causing GC allocations when converting an `IEnumerable` to a list. (#2983)
- Fixed issue where the realtime network stats monitor was not able to display RPC traffic in release builds due to those stats being only available in development builds or the editor. (#2979)
- Fixed issue where `NetworkManager.ScenesLoaded` was not being updated if `PostSynchronizationSceneUnloading` was set and any loaded scenes not used during synchronization were unloaded. (#2971)
- Fixed issue where `Rigidbody2d` under Unity 6000.0.11f1 has breaking changes where `velocity` is now `linearVelocity` and `isKinematic` is replaced by `bodyType`. (#2971)
- Fixed issue where `NetworkSpawnManager.InstantiateAndSpawn` and `NetworkObject.InstantiateAndSpawn` were not honoring the ownerClientId parameter when using a client-server network topology. (#2968)
- Fixed issue where internal delta serialization could not have a byte serializer defined when serializing deltas for other types. Added `[GenerateSerializationForType(typeof(byte))]` to both the `NetworkVariable` and `AnticipatedNetworkVariable` classes to assure a byte serializer is defined.(#2962)
- Fixed issue when scene management was disabled and the session owner would still try to synchronize a late joining client. (#2962)
- Fixed issue when using a distributed authority network topology where it would allow a session owner to spawn a `NetworkObject` prior to being approved. Now, an error message is logged and the `NetworkObject` will not be spawned prior to the client being approved.  (#2962)
- Fixed issue where attempting to spawn during `NetworkBehaviour.OnInSceneObjectsSpawned` and `NetworkBehaviour.OnNetworkSessionSynchronized` notifications would throw a collection modified exception.  (#2962)

### Changed

- Changed logic where clients can now set the `NetworkSceneManager` client synchronization mode when using a distributed authority network topology. (#2985)


## [2.0.0-pre.2] - 2024-06-17

### Added

- Added `AnticipatedNetworkVariable<T>`, which adds support for client anticipation of `NetworkVariable` values, allowing for more responsive gameplay. (#2957)
- Added `AnticipatedNetworkTransform`, which adds support for client anticipation of NetworkTransforms. (#2957)
- Added `NetworkVariableBase.ExceedsDirtinessThreshold` to allow network variables to throttle updates by only sending updates when the difference between the current and previous values exceeds a threshold. (This is exposed in `NetworkVariable<T>` with the callback `NetworkVariable<T>.CheckExceedsDirtinessThreshold`). (#2957)
- Added `NetworkVariableUpdateTraits`, which add additional throttling support: `MinSecondsBetweenUpdates` will prevent the `NetworkVariable` from sending updates more often than the specified time period (even if it exceeds the dirtiness threshold), while `MaxSecondsBetweenUpdates` will force a dirty `NetworkVariable` to send an update after the specified time period even if it has not yet exceeded the dirtiness threshold. (#2957)
- Added virtual method `NetworkVariableBase.OnInitialize` which can be used by `NetworkVariable` subclasses to add initialization code. (#2957)
- Added `NetworkTime.TickWithPartial`, which represents the current tick as a double that includes the fractional/partial tick value. (#2957)
- Added `NetworkTickSystem.AnticipationTick`, which can be helpful with implementation of client anticipation. This value represents the tick the current local client was at at the beginning of the most recent network round trip, which enables it to correlate server update ticks with the client tick that may have triggered them. (#2957)
- Added event `NetworkManager.OnSessionOwnerPromoted` that is invoked when a new session owner promotion occurs. (#2948)
- Added `NetworkRigidBodyBase.GetLinearVelocity` and `NetworkRigidBodyBase.SetLinearVelocity` convenience/helper methods. (#2948)
- Added `NetworkRigidBodyBase.GetAngularVelocity` and `NetworkRigidBodyBase.SetAngularVelocity` convenience/helper methods. (#2948)

### Fixed

- Fixed issue when `NetworkTransform` half float precision is enabled and ownership changes the current base position was not being synchronized. (#2948)
- Fixed issue where `OnClientConnected` not being invoked on the session owner when connecting to a new distributed authority session. (#2948)
- Fixed issue where Rigidbody micro-motion (i.e. relatively small velocities) would result in non-authority instances slightly stuttering as the body would come to a rest (i.e. no motion). Now, the threshold value can increase at higher velocities and can decrease slightly below the provided threshold to account for this. (#2948)

### Changed

- Changed `NetworkAnimator` no longer requires the `Animator` component to exist on the same `GameObject`. (#2957)
- Changed `NetworkObjectReference` and `NetworkBehaviourReference` to allow null references when constructing and serializing. (#2957)
- Changed the client's owned objects is now returned (`NetworkClient` and `NetworkSpawnManager`) as an array as opposed to a list for performance purposes. (#2948)
- Changed `NetworkTransfrom.TryCommitTransformToServer` to be internal as it will be removed by the final 2.0.0 release. (#2948)
- Changed `NetworkTransformEditor.OnEnable` to a virtual method to be able to customize a `NetworkTransform` derived class by creating a derived editor control from `NetworkTransformEditor`. (#2948)


## [2.0.0-exp.5] - 2024-06-03

### Added


### Fixed

- Fixed issue where SessionOwner message was being treated as a new entry for the new message indexing when it should have been ordinally sorted with the legacy message indices. (#2942)

### Changed
- Changed `FastBufferReader` and `FastBufferWriter` so that they always ensure the length of items serialized is always serialized as an `uint` and added a check before casting for safe reading and writing.(#2946)


## [2.0.0-exp.4] - 2024-05-31

### Added

- Added `NetworkRigidbodyBase.AttachToFixedJoint` and `NetworkRigidbodyBase.DetachFromFixedJoint` to replace parenting for rigid bodies that have `NetworkRigidbodyBase.UseRigidBodyForMotion` enabled. (#2933)
- Added `NetworkBehaviour.OnNetworkPreSpawn` and `NetworkBehaviour.OnNetworkPostSpawn` methods that provide the ability to handle pre and post spawning actions during the `NetworkObject` spawn sequence. (#2912)
- Added a client-side only `NetworkBehaviour.OnNetworkSessionSynchronized` convenience method that is invoked on all `NetworkBehaviour`s after a newly joined client has finished synchronizing with the network session in progress. (#2912)
- Added `NetworkBehaviour.OnInSceneObjectsSpawned` convenience method that is invoked when all in-scene `NetworkObject`s have been spawned after a scene has been loaded or upon a host or server starting. (#2912)

### Fixed

- Fixed issue where non-authoritative rigid bodies with `NetworkRigidbodyBase.UseRigidBodyForMotion` enabled would constantly log errors about the renderTime being before `StartTimeConsumed`. (#2933)
- Fixed issue where in-scene placed NetworkObjects could be destroyed if a client disconnects early and/or before approval. (#2924)
- Fixed issue where a `NetworkObject` component's associated `NetworkBehaviour` components would not be detected if scene loading is disabled in the editor and the currently loaded scene has in-scene placed `NetworkObject`s. (#2912)
- Fixed issue where an in-scene placed `NetworkObject` with `NetworkTransform` that is also parented under a `GameObject` would not properly synchronize when the parent `GameObject` had a world space position other than 0,0,0. (#2898)

### Changed

- Change all the access modifiers of test class from Public to Internal (#2930)
- Changed messages are now sorted by enum values as opposed to ordinally sorting the messages by their type name. (#2929)
- Changed `NetworkClient.SessionModeTypes` to `NetworkClient.NetworkTopologyTypes`. (#2875)
- Changed `NetworkClient.SessionModeType` to `NetworkClient.NetworkTopologyType`. (#2875)
- Changed `NetworkConfig.SessionMode` to `NeworkConfig.NetworkTopology`. (#2875)

## [2.0.0-exp.2] - 2024-04-02

### Added
- Added updates to all internal messages to account for a distributed authority network session connection.  (#2863)
- Added `NetworkRigidbodyBase` that provides users with a more customizable network rigidbody, handles both `Rigidbody` and `Rigidbody2D`, and provides an option to make `NetworkTransform` use the rigid body for motion.  (#2863)
  - For a customized `NetworkRigidbodyBase` class:
    - `NetworkRigidbodyBase.AutoUpdateKinematicState` provides control on whether the kinematic setting will be automatically set or not when ownership changes.
    - `NetworkRigidbodyBase.AutoSetKinematicOnDespawn` provides control on whether isKinematic will automatically be set to true when the associated `NetworkObject` is despawned.
    - `NetworkRigidbodyBase.Initialize` is a protected method that, when invoked, will initialize the instance. This includes options to:
      - Set whether using a `RigidbodyTypes.Rigidbody` or `RigidbodyTypes.Rigidbody2D`.
      - Includes additional optional parameters to set the `NetworkTransform`, `Rigidbody`, and `Rigidbody2d` to use.
  - Provides additional public methods:
    - `NetworkRigidbodyBase.GetPosition` to return the position of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.GetRotation` to return the rotation of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.MovePosition` to move to the position of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.MoveRotation` to move to the rotation of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.Move` to move to the position and rotation of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.Move` to move to the position and rotation of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.SetPosition` to set the position of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.SetRotation` to set the rotation of the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting).
    - `NetworkRigidbodyBase.ApplyCurrentTransform` to set the position and rotation of the `Rigidbody` or `Rigidbody2d` based on the associated `GameObject` transform (depending upon its initialized setting).
    - `NetworkRigidbodyBase.WakeIfSleeping` to wake up the rigid body if sleeping.
    - `NetworkRigidbodyBase.SleepRigidbody` to put the rigid body to sleep.
    - `NetworkRigidbodyBase.IsKinematic` to determine if the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting) is currently kinematic.
    - `NetworkRigidbodyBase.SetIsKinematic` to set the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting) current kinematic state.
    - `NetworkRigidbodyBase.ResetInterpolation` to reset the `Rigidbody` or `Rigidbody2d` (depending upon its initialized setting) back to its original interpolation value when initialized.
  - Now includes a `MonoBehaviour.FixedUpdate` implementation that will update the assigned `NetworkTransform` when `NetworkRigidbodyBase.UseRigidBodyForMotion` is true. (#2863)
- Added `RigidbodyContactEventManager` that provides a more optimized way to process collision enter and collision stay events as opposed to the `Monobehaviour` approach. (#2863)
  - Can be used in client-server and distributed authority modes, but is particularly useful in distributed authority.
- Added rigid body motion updates to `NetworkTransform` which allows users to set interolation on rigid bodies. (#2863)
  - Extrapolation is only allowed on authoritative instances, but custom class derived from `NetworkRigidbodyBase` or `NetworkRigidbody` or `NetworkRigidbody2D` automatically switches non-authoritative instances to interpolation if set to extrapolation.
- Added distributed authority mode support to `NetworkAnimator`. (#2863)
- Added session mode selection to `NetworkManager` inspector view. (#2863)
- Added distributed authority permissions feature. (#2863)
- Added distributed authority mode specific `NetworkObject` permissions flags (Distributable, Transferable, and RequestRequired). (#2863)
- Added distributed authority mode specific `NetworkObject.SetOwnershipStatus` method that applies one or more `NetworkObject` instance's ownership flags. If updated when spawned, the ownership permission changes are synchronized with the other connected clients. (#2863)
- Added distributed authority mode specific `NetworkObject.RemoveOwnershipStatus` method that removes one or more `NetworkObject` instance's ownership flags. If updated when spawned, the ownership permission changes are synchronized with the other connected clients. (#2863)
- Added distributed authority mode specific `NetworkObject.HasOwnershipStatus` method that will return (true or false) whether one or more ownership flags is set. (#2863)
- Added distributed authority mode specific `NetworkObject.SetOwnershipLock` method that locks ownership of a `NetworkObject` to prevent ownership from changing until the current owner releases the lock. (#2863)
- Added distributed authority mode specific `NetworkObject.RequestOwnership` method that sends an ownership request to the current owner of a spawned `NetworkObject` instance. (#2863)
- Added distributed authority mode specific `NetworkObject.OnOwnershipRequested` callback handler that is invoked on the owner/authoritative side when a non-owner requests ownership. Depending upon the boolean returned value depends upon whether the request is approved or denied. (#2863)
- Added distributed authority mode specific `NetworkObject.OnOwnershipRequestResponse` callback handler that is invoked when a non-owner's request has been processed. This callback includes a `NetworkObjet.OwnershipRequestResponseStatus` response parameter that describes whether the request was approved or the reason why it was not approved. (#2863)
- Added distributed authority mode specific `NetworkObject.DeferDespawn` method that defers the despawning of `NetworkObject` instances on non-authoritative clients based on the tick offset parameter. (#2863)
- Added distributed authority mode specific `NetworkObject.OnDeferredDespawnComplete` callback handler that can be used to further control when deferring the despawning of a `NetworkObject` on non-authoritative instances. (#2863)
- Added `NetworkClient.SessionModeType` as one way to determine the current session mode of the network session a client is connected to. (#2863)
- Added distributed authority mode specific `NetworkClient.IsSessionOwner` property to determine if the current local client is the current session owner of a distributed authority session. (#2863)
- Added distributed authority mode specific client side spawning capabilities. When running in distributed authority mode, clients can instantiate and spawn `NetworkObject` instances (the local client is automatically the owner of the spawned object). (#2863)
  - This is useful to better visually synchronize owner authoritative motion models and newly spawned `NetworkObject` instances (i.e. projectiles for example).
- Added distributed authority mode specific client side player spawning capabilities. Clients will automatically spawn their associated player object locally. (#2863)
- Added distributed authority mode specific `NetworkConfig.AutoSpawnPlayerPrefabClientSide` property (default is true) to provide control over the automatic spawning of player prefabs on the local client side. (#2863)
- Added distributed authority mode specific `NetworkManager.OnFetchLocalPlayerPrefabToSpawn` callback that, when assigned, will allow the local client to provide the player prefab to be spawned for the local client. (#2863)
  - This is only invoked if the `NetworkConfig.AutoSpawnPlayerPrefabClientSide` property is set to true.
- Added distributed authority mode specific `NetworkBehaviour.HasAuthority` property that determines if the local client has authority over the associated `NetworkObject` instance (typical use case is within a `NetworkBehaviour` script much like that of `IsServer` or `IsClient`). (#2863)
- Added distributed authority mode specific `NetworkBehaviour.IsSessionOwner` property that determines if the local client is the session owner (typical use case would be to determine if the local client can has scene management authority within a `NetworkBehaviour` script). (#2863)
- Added support for distributed authority mode scene management where the currently assigned session owner can start scene events (i.e. scene loading and scene unloading). (#2863)

### Fixed

- Fixed issue where the host was not invoking `OnClientDisconnectCallback` for its own local client when internally shutting down. (#2822)
- Fixed issue where NetworkTransform could potentially attempt to "unregister" a named message prior to it being registered. (#2807)
- Fixed issue where in-scene placed `NetworkObject`s with complex nested children `NetworkObject`s (more than one child in depth) would not synchronize properly if WorldPositionStays was set to true. (#2796)

### Changed
- Changed client side awareness of other clients is now the same as a server or host. (#2863)
- Changed `NetworkManager.ConnectedClients` can now be accessed by both server and clients. (#2863)
- Changed `NetworkManager.ConnectedClientsList` can now be accessed by both server and clients. (#2863)
- Changed `NetworkTransform` defaults to owner authoritative when connected to a distributed authority session. (#2863)
- Changed `NetworkVariable` defaults to owner write and everyone read permissions when connected to a distributed authority session (even if declared with server read or write permissions).  (#2863)
- Changed `NetworkObject` no longer implements the `MonoBehaviour.Update` method in order to determine whether a `NetworkObject` instance has been migrated to a different scene. Instead, only `NetworkObjects` with the `SceneMigrationSynchronization` property set will be updated internally during the `NetworkUpdateStage.PostLateUpdate` by `NetworkManager`. (#2863)
- Changed `NetworkManager` inspector view layout where properties are now organized by category. (#2863)
- Changed `NetworkTransform` to now use `NetworkTransformMessage` as opposed to named messages for NetworkTransformState updates. (#2810)
- Changed `CustomMessageManager` so it no longer attempts to register or "unregister" a null or empty string and will log an error if this condition occurs. (#2807)

## [1.9.1] - 2024-04-18

### Added
- Added `AnticipatedNetworkVariable<T>`, which adds support for client anticipation of NetworkVariable values, allowing for more responsive game play (#2820)
- Added `AnticipatedNetworkTransform`, which adds support for client anticipation of `NetworkTransform`s (#2820)
- Added `NetworkVariableBase.ExceedsDirtinessThreshold` to allow network variables to throttle updates by only sending updates when the difference between the current and previous values exceeds a threshold. (This is exposed in NetworkVariable<T> with the callback NetworkVariable<T>.CheckExceedsDirtinessThreshold) (#2820)
- Added `NetworkVariableUpdateTraits`, which add additional throttling support: `MinSecondsBetweenUpdates` will prevent the `NetworkVariable` from sending updates more often than the specified time period (even if it exceeds the dirtiness threshold), while `MaxSecondsBetweenUpdates` will force a dirty `NetworkVariable` to send an update after the specified time period even if it has not yet exceeded the dirtiness threshold. (#2820)
- Added virtual method `NetworkVariableBase.OnInitialize()` which can be used by `NetworkVariable` subclasses to add initialization code (#2820)
- Added virtual method `NetworkVariableBase.Update()`, which is called once per frame to support behaviors such as interpolation between an anticipated value and an authoritative one. (#2820)
- Added `NetworkTime.TickWithPartial`, which represents the current tick as a double that includes the fractional/partial tick value. (#2820)
- `NetworkVariable` now includes built-in support for `NativeHashSet`, `NativeHashMap`, `List`, `HashSet`, and `Dictionary` (#2813)
- `NetworkVariable` now includes delta compression for collection values (`NativeList`, `NativeArray`, `NativeHashSet`, `NativeHashMap`, `List`, `HashSet`, `Dictionary`, and `FixedString` types) to save bandwidth by only sending the values that changed. (Note: For `NativeList`, `NativeArray`, and `List`, this algorithm works differently than that used in `NetworkList`. This algorithm will use less bandwidth for "set" and "add" operations, but `NetworkList` is more bandwidth-efficient if you are performing frequent "insert" operations.) (#2813)
- `UserNetworkVariableSerialization` now has optional callbacks for `WriteDelta` and `ReadDelta`. If both are provided, they will be used for all serialization operations on NetworkVariables of that type except for the first one for each client. If either is missing, the existing `Write` and `Read` will always be used. (#2813)
- Network variables wrapping `INetworkSerializable` types can perform delta serialization by setting `UserNetworkVariableSerialization<T>.WriteDelta` and `UserNetworkVariableSerialization<T>.ReadDelta` for those types. The built-in `INetworkSerializable` serializer will continue to be used for all other serialization operations, but if those callbacks are set, it will call into them on all but the initial serialization to perform delta serialization. (This could be useful if you have a large struct where most values do not change regularly and you want to send only the fields that did change.) (#2813)

### Fixed

- Fixed issue where `NetworkTransformEditor` would throw and exception if you excluded the physics package. (#2871)
- Fixed issue where `NetworkTransform` could not properly synchronize its base position when using half float precision. (#2845)
- Fixed issue where the host was not invoking `OnClientDisconnectCallback` for its own local client when internally shutting down. (#2822)
- Fixed issue where NetworkTransform could potentially attempt to "unregister" a named message prior to it being registered. (#2807)
- Fixed issue where in-scene placed `NetworkObject`s with complex nested children `NetworkObject`s (more than one child in depth) would not synchronize properly if WorldPositionStays was set to true. (#2796)

### Changed

- Changed `NetworkObjectReference` and `NetworkBehaviourReference` to allow null references when constructing and serializing. (#2874)
- Changed `NetworkAnimator` no longer requires the `Animator` component to exist on the same `GameObject`. (#2872)
- Changed `NetworkTransform` to now use `NetworkTransformMessage` as opposed to named messages for NetworkTransformState updates. (#2810)
- Changed `CustomMessageManager` so it no longer attempts to register or "unregister" a null or empty string and will log an error if this condition occurs. (#2807)

## [1.8.1] - 2024-02-05

### Fixed

- Fixed a compile error when compiling for IL2CPP targets when using the new `[Rpc]` attribute. (#2824)

## [1.8.0] - 2023-12-12

### Added

- Added a new RPC attribute, which is simply `Rpc`. (#2762)
  - This is a generic attribute that can perform the functions of both Server and Client RPCs, as well as enabling client-to-client RPCs. Includes several default targets: `Server`, `NotServer`, `Owner`, `NotOwner`, `Me`, `NotMe`, `ClientsAndHost`, and `Everyone`. Runtime overrides are available for any of these targets, as well as for sending to a specific ID or groups of IDs.
  - This attribute also includes the ability to defer RPCs that are sent to the local process to the start of the next frame instead of executing them immediately, treating them as if they had gone across the network. The default behavior is to execute immediately.
  - This attribute effectively replaces `ServerRpc` and `ClientRpc`. `ServerRpc` and `ClientRpc` remain in their existing forms for backward compatibility, but `Rpc` will be the recommended and most supported option.
- Added `NetworkManager.OnConnectionEvent` as a unified connection event callback to notify clients and servers of all client connections and disconnections within the session (#2762)
- Added `NetworkManager.ServerIsHost` and `NetworkBehaviour.ServerIsHost` to allow a client to tell if it is connected to a host or to a dedicated server (#2762)
- Added `SceneEventProgress.SceneManagementNotEnabled` return status to be returned when a `NetworkSceneManager` method is invoked and scene management is not enabled. (#2735)
- Added `SceneEventProgress.ServerOnlyAction` return status to be returned when a `NetworkSceneManager` method is invoked by a client. (#2735)
- Added `NetworkObject.InstantiateAndSpawn` and `NetworkSpawnManager.InstantiateAndSpawn` methods to simplify prefab spawning by assuring that the prefab is valid and applies any override prior to instantiating the `GameObject` and spawning the `NetworkObject` instance. (#2710)

### Fixed

- Fixed issue where a client disconnected by a server-host would not receive a local notification. (#2789)
- Fixed issue where a server-host could shutdown during a relay connection but periodically the transport disconnect message sent to any connected clients could be dropped. (#2789)
- Fixed issue where a host could disconnect its local client but remain running as a server. (#2789)
- Fixed issue where `OnClientDisconnectedCallback` was not being invoked under certain conditions. (#2789)
- Fixed issue where `OnClientDisconnectedCallback` was always returning 0 as the client identifier. (#2789)
- Fixed issue where if a host or server shutdown while a client owned NetworkObjects (other than the player) it would throw an exception. (#2789)
- Fixed issue where setting values on a `NetworkVariable` or `NetworkList` within `OnNetworkDespawn` during a shutdown sequence would throw an exception. (#2789)
- Fixed issue where a teleport state could potentially be overridden by a previous unreliable delta state. (#2777)
- Fixed issue where `NetworkTransform` was using the `NetworkManager.ServerTime.Tick` as opposed to `NetworkManager.NetworkTickSystem.ServerTime.Tick` during the authoritative side's tick update where it performed a delta state check. (#2777)
- Fixed issue where a parented in-scene placed NetworkObject would be destroyed upon a client or server exiting a network session but not unloading the original scene in which the NetworkObject was placed. (#2737)
- Fixed issue where during client synchronization and scene loading, when client synchronization or the scene loading mode are set to `LoadSceneMode.Single`, a `CreateObjectMessage` could be received, processed, and the resultant spawned `NetworkObject` could be instantiated in the client's currently active scene that could, towards the end of the client synchronization or loading process, be unloaded and cause the newly created `NetworkObject` to be destroyed (and throw and exception). (#2735)
- Fixed issue where a `NetworkTransform` instance with interpolation enabled would result in wide visual motion gaps (stuttering) under above normal latency conditions and a 1-5% or higher packet are drop rate. (#2713)
- Fixed issue where  you could not have multiple source network prefab overrides targeting the same network prefab as their override. (#2710)

### Changed
- Changed the server or host shutdown so it will now perform a "soft shutdown" when `NetworkManager.Shutdown` is invoked. This will send a disconnect notification to all connected clients and the server-host will wait for all connected clients to disconnect or timeout after a 5 second period before completing the shutdown process. (#2789)
- Changed `OnClientDisconnectedCallback` will now return the assigned client identifier on the local client side if the client was approved and assigned one prior to being disconnected. (#2789)
- Changed `NetworkTransform.SetState` (and related methods) now are cumulative during a fractional tick period and sent on the next pending tick. (#2777)
- `NetworkManager.ConnectedClientsIds` is now accessible on the client side and will contain the list of all clients in the session, including the host client if the server is operating in host mode (#2762)
- Changed `NetworkSceneManager` to return a `SceneEventProgress` status and not throw exceptions for methods invoked when scene management is disabled and when a client attempts to access a `NetworkSceneManager` method by a client. (#2735)
- Changed `NetworkTransform` authoritative instance tick registration so a single `NetworkTransform` specific tick event update will update all authoritative instances to improve perofmance. (#2713)
- Changed `NetworkPrefabs.OverrideToNetworkPrefab` dictionary is no longer used/populated due to it ending up being related to a regression bug and not allowing more than one override to be assigned to a network prefab asset. (#2710)
- Changed in-scene placed `NetworkObject`s now store their source network prefab asset's `GlobalObjectIdHash` internally that is used, when scene management is disabled, by clients to spawn the correct prefab even if the `NetworkPrefab` entry has an override. This does not impact dynamically spawning the same prefab which will yield the override on both host and client. (#2710)
- Changed in-scene placed `NetworkObject`s no longer require a `NetworkPrefab` entry with `GlobalObjectIdHash` override in order for clients to properly synchronize. (#2710)
- Changed in-scene placed `NetworkObject`s now set their `IsSceneObject` value when generating their `GlobalObjectIdHash` value. (#2710)
- Changed the default `NetworkConfig.SpawnTimeout` value from 1.0s to 10.0s. (#2710)

## [1.7.1] - 2023-11-15

### Added

### Fixed

- Fixed a bug where having a class with Rpcs that inherits from a class without Rpcs that inherits from NetworkVariable would cause a compile error. (#2751)
- Fixed issue where `NetworkBehaviour.Synchronize` was not truncating the write buffer if nothing was serialized during `NetworkBehaviour.OnSynchronize` causing an additional 6 bytes to be written per `NetworkBehaviour` component instance. (#2749)

## [1.7.0] - 2023-10-11

### Added

- exposed NetworkObject.GetNetworkBehaviourAtOrderIndex as a public API (#2724)
- Added context menu tool that provides users with the ability to quickly update the GlobalObjectIdHash value for all in-scene placed prefab instances that were created prior to adding a NetworkObject component to it. (#2707)
- Added methods NetworkManager.SetPeerMTU and NetworkManager.GetPeerMTU to be able to set MTU sizes per-peer (#2676)
- Added `GenerateSerializationForGenericParameterAttribute`, which can be applied to user-created Network Variable types to ensure the codegen generates serialization for the generic types they wrap. (#2694)
- Added `GenerateSerializationForTypeAttribute`, which can be applied to any class or method to ensure the codegen generates serialization for the specific provided type. (#2694)
- Exposed `NetworkVariableSerialization<T>.Read`, `NetworkVariableSerialization<T>.Write`, `NetworkVariableSerialization<T>.AreEqual`, and `NetworkVariableSerialization<T>.Duplicate` to further support the creation of user-created network variables by allowing users to access the generated serialization methods and serialize generic types efficiently without boxing. (#2694)
- Added `NetworkVariableBase.MarkNetworkBehaviourDirty` so that user-created network variable types can mark their containing `NetworkBehaviour` to be processed by the update loop. (#2694)

### Fixed

- Fixed issue where the server side `NetworkSceneManager` instance was not adding the currently active scene to its list of scenes loaded. (#2723)
- Generic NetworkBehaviour types no longer result in compile errors or runtime errors (#2720)
- Rpcs within Generic NetworkBehaviour types can now serialize parameters of the class's generic types (but may not have generic types of their own) (#2720)
- Errors are no longer thrown when entering play mode with domain reload disabled (#2720)
- NetworkSpawn is now correctly called each time when entering play mode with scene reload disabled (#2720)
- NetworkVariables of non-integer types will no longer break the inspector (#2714)
- NetworkVariables with NonSerializedAttribute will not appear in the inspector (#2714)
- Fixed issue where `UnityTransport` would attempt to establish WebSocket connections even if using UDP/DTLS Relay allocations when the build target was WebGL. This only applied to working in the editor since UDP/DTLS can't work in the browser. (#2695)
- Fixed issue where a `NetworkBehaviour` component's `OnNetworkDespawn` was not being invoked on the host-server side for an in-scene placed `NetworkObject` when a scene was unloaded (during a scene transition) and the `NetworkBehaviour` component was positioned/ordered before the `NetworkObject` component. (#2685)
- Fixed issue where `SpawnWithObservers` was not being honored when `NetworkConfig.EnableSceneManagement` was disabled. (#2682)
- Fixed issue where `NetworkAnimator` was not internally tracking changes to layer weights which prevented proper layer weight synchronization back to the original layer weight value. (#2674)
- Fixed "writing past the end of the buffer" error when calling ResetDirty() on managed network variables that are larger than 256 bytes when serialized. (#2670)
- Fixed issue where generation of the `DefaultNetworkPrefabs` asset was not enabled by default. (#2662)
- Fixed issue where the `GlobalObjectIdHash` value could be updated but the asset not marked as dirty. (#2662)
- Fixed issue where the `GlobalObjectIdHash` value of a (network) prefab asset could be assigned an incorrect value when editing the prefab in a temporary scene. (#2662)
- Fixed issue where the `GlobalObjectIdHash` value generated after creating a (network) prefab from an object constructed within the scene would not be the correct final value in a stand alone build. (#2662)

### Changed

- Updated dependency on `com.unity.transport` to version 1.4.0. (#2716)

## [1.6.0] - 2023-08-09

### Added

- Added a protected virtual method `NetworkTransform.OnInitialize(ref NetworkTransformState replicatedState)` that just returns the replicated state reference.
  
### Fixed

- Fixed issue where invoking `NetworkManager.Shutdown` within `NetworkManager.OnClientStopped` or `NetworkManager.OnServerStopped` would force `NetworkManager.ShutdownInProgress` to remain true after completing the shutdown process. (#2661)
- Fixed issue where ARMv7 Android builds would crash when trying to validate the batch header. (#2654)
- Fixed issue with client synchronization of position when using half precision and the delta position reaches the maximum value and is collapsed on the host prior to being forwarded to the non-owner clients. (#2636)
- Fixed issue with scale not synchronizing properly depending upon the spawn order of NetworkObjects. (#2636)
- Fixed issue position was not properly transitioning between ownership changes with an owner authoritative NetworkTransform. (#2636)
- Fixed issue where a late joining non-owner client could update an owner authoritative NetworkTransform if ownership changed without any updates to position prior to the non-owner client joining. (#2636)

### Changed

## [1.5.2] - 2023-07-24

### Added

### Fixed
- Bumped minimum Unity version supported to 2021.3 LTS
- Fixed issue where `NetworkClient.OwnedObjects` was not returning any owned objects due to the `NetworkClient.IsConnected` not being properly set. (#2631)
- Fixed a crash when calling TrySetParent with a null Transform (#2625)
- Fixed issue where a `NetworkTransform` using full precision state updates was losing transform state updates when interpolation was enabled. (#2624)
- Fixed issue where `NetworkObject.SpawnWithObservers` was not being honored for late joining clients. (#2623)
- Fixed issue where invoking `NetworkManager.Shutdown` multiple times, depending upon the timing, could cause an exception. (#2622)
- Fixed issue where removing ownership would not notify the server that it gained ownership. This also resolves the issue where an owner authoritative NetworkTransform would not properly initialize upon removing ownership from a remote client. (#2618)
- Fixed ILPP issues when using CoreCLR and for certain dedicated server builds. (#2614)
- Fixed an ILPP compile error when creating a generic NetworkBehaviour singleton with a static T instance. (#2603)

### Changed

## [1.5.1] - 2023-06-07

### Added

- Added support for serializing `NativeArray<>` and `NativeList<>` in `FastBufferReader`/`FastBufferWriter`, `BufferSerializer`, `NetworkVariable`, and RPCs. (To use `NativeList<>`, add `UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT` to your Scripting Define Symbols in `Project Settings > Player`) (#2375)
- The location of the automatically-created default network prefab list can now be configured (#2544)
- Added: Message size limits (max single message and max fragmented message) can now be set using NetworkManager.MaximumTransmissionUnitSize and NetworkManager.MaximumFragmentedMessageSize for transports that don't work with the default values (#2530)
- Added `NetworkObject.SpawnWithObservers` property (default is true) that when set to false will spawn a `NetworkObject` with no observers and will not be spawned on any client until `NetworkObject.NetworkShow` is invoked. (#2568)

### Fixed

- Fixed: Fixed a null reference in codegen in some projects (#2581)
- Fixed issue where the `OnClientDisconnected` client identifier was incorrect after a pending client connection was denied. (#2569)
- Fixed warning "Runtime Network Prefabs was not empty at initialization time." being erroneously logged when no runtime network prefabs had been added (#2565)
- Fixed issue where some temporary debug console logging was left in a merged PR. (#2562)
- Fixed the "Generate Default Network Prefabs List" setting not loading correctly and always reverting to being checked. (#2545)
- Fixed issue where users could not use NetworkSceneManager.VerifySceneBeforeLoading to exclude runtime generated scenes from client synchronization. (#2550)
- Fixed missing value on `NetworkListEvent` for `EventType.RemoveAt` events.  (#2542,#2543)
- Fixed issue where parenting a NetworkTransform under a transform with a scale other than Vector3.one would result in incorrect values on non-authoritative instances. (#2538)
- Fixed issue where a server would include scene migrated and then despawned NetworkObjects to a client that was being synchronized. (#2532)
- Fixed the inspector throwing exceptions when attempting to render `NetworkVariable`s of enum types. (#2529)
- Making a `NetworkVariable` with an `INetworkSerializable` type that doesn't meet the `new()` constraint will now create a compile-time error instead of an editor crash (#2528)
- Fixed Multiplayer Tools package installation docs page link on the NetworkManager popup. (#2526)
- Fixed an exception and error logging when two different objects are shown and hidden on the same frame (#2524)
- Fixed a memory leak in `UnityTransport` that occurred if `StartClient` failed. (#2518)
- Fixed issue where a client could throw an exception if abruptly disconnected from a network session with one or more spawned `NetworkObject`(s). (#2510)
- Fixed issue where invalid endpoint addresses were not being detected and returning false from NGO UnityTransport. (#2496)
- Fixed some errors that could occur if a connection is lost and the loss is detected when attempting to write to the socket. (#2495)

## Changed

- Adding network prefabs before NetworkManager initialization is now supported. (#2565)
- Connecting clients being synchronized now switch to the server's active scene before spawning and synchronizing NetworkObjects. (#2532)
- Updated `UnityTransport` dependency on `com.unity.transport` to 1.3.4. (#2533)
- Improved performance of NetworkBehaviour initialization by replacing reflection when initializing NetworkVariables with compile-time code generation, which should help reduce hitching during additive scene loads. (#2522)

## [1.4.0] - 2023-04-10

### Added

- Added a way to access the GlobalObjectIdHash via PrefabIdHash for use in the Connection Approval Callback. (#2437)
- Added `OnServerStarted` and `OnServerStopped` events that will trigger only on the server (or host player) to notify that the server just started or is no longer active (#2420)
- Added `OnClientStarted` and `OnClientStopped` events that will trigger only on the client (or host player) to notify that the client just started or is no longer active (#2420)
- Added `NetworkTransform.UseHalfFloatPrecision` property that, when enabled, will use half float values for position, rotation, and scale. This yields a 50% bandwidth savings a the cost of precision. (#2388)
- Added `NetworkTransform.UseQuaternionSynchronization` property that, when enabled, will synchronize the entire quaternion. (#2388)
- Added `NetworkTransform.UseQuaternionCompression` property that, when enabled, will use a smallest three implementation reducing a full quaternion synchronization update to the size of an unsigned integer. (#2388)
- Added `NetworkTransform.SlerpPosition` property that, when enabled along with interpolation being enabled, will interpolate using `Vector3.Slerp`. (#2388)
- Added `BufferedLinearInterpolatorVector3` that replaces the float version, is now used by `NetworkTransform`, and provides the ability to enable or disable `Slerp`. (#2388)
- Added `HalfVector3` used for scale when half float precision is enabled. (#2388)
- Added `HalfVector4` used for rotation when half float precision and quaternion synchronization is enabled. (#2388)
- Added `HalfVector3DeltaPosition` used for position when half float precision is enabled. This handles loss in position precision by updating only the delta position as opposed to the full position. (#2388)
- Added `NetworkTransform.GetSpaceRelativePosition` and `NetworkTransform.GetSpaceRelativeRotation` helper methods to return the proper values depending upon whether local or world space. (#2388)
- Added `NetworkTransform.OnAuthorityPushTransformState` virtual method that is invoked just prior to sending the `NetworkTransformState` to non-authoritative instances. This provides users with the ability to obtain more precise delta values for prediction related calculations. (#2388)
- Added `NetworkTransform.OnNetworkTransformStateUpdated` virtual method that is invoked just after the authoritative `NetworkTransformState` is applied. This provides users with the ability to obtain more precise delta values for prediction related calculations. (#2388)
- Added `NetworkTransform.OnInitialize`virtual method that is invoked after the `NetworkTransform` has been initialized or re-initialized when ownership changes. This provides for a way to make adjustments when `NetworkTransform` is initialized (i.e. resetting client prediction etc) (#2388)
- Added `NetworkObject.SynchronizeTransform` property (default is true) that provides users with another way to help with bandwidth optimizations where, when set to false, the `NetworkObject`'s associated transform will not be included when spawning and/or synchronizing late joining players. (#2388)
- Added `NetworkSceneManager.ActiveSceneSynchronizationEnabled` property, disabled by default, that enables client synchronization of server-side active scene changes. (#2383)
- Added `NetworkObject.ActiveSceneSynchronization`, disabled by default, that will automatically migrate a `NetworkObject` to a newly assigned active scene. (#2383)
- Added `NetworkObject.SceneMigrationSynchronization`, enabled by default, that will synchronize client(s) when a `NetworkObject` is migrated into a new scene on the server side via `SceneManager.MoveGameObjectToScene`. (#2383)

### Changed

- Made sure the `CheckObjectVisibility` delegate is checked and applied, upon `NetworkShow` attempt. Found while supporting (#2454), although this is not a fix for this (already fixed) issue. (#2463)
- Changed `NetworkTransform` authority handles delta checks on each new network tick and no longer consumes processing cycles checking for deltas for all frames in-between ticks. (#2388)
- Changed the `NetworkTransformState` structure is now public and now has public methods that provide access to key properties of the `NetworkTransformState` structure. (#2388)
- Changed `NetworkTransform` interpolation adjusts its interpolation "ticks ago" to be 2 ticks latent if it is owner authoritative and the instance is not the server or 1 tick latent if the instance is the server and/or is server authoritative. (#2388)
- Updated `NetworkSceneManager` to migrate dynamically spawned `NetworkObject`s with `DestroyWithScene` set to false into the active scene if their current scene is unloaded. (#2383)
- Updated the server to synchronize its local `NetworkSceneManager.ClientSynchronizationMode` during the initial client synchronization. (#2383)

### Fixed

- Fixed issue where during client synchronization the synchronizing client could receive a ObjectSceneChanged message before the client-side NetworkObject instance had been instantiated and spawned. (#2502)
- Fixed issue where `NetworkAnimator` was building client RPC parameters to exclude the host from sending itself messages but was not including it in the ClientRpc parameters. (#2492)
- Fixed issue where `NetworkAnimator` was not properly detecting and synchronizing cross fade initiated transitions. (#2481)
- Fixed issue where `NetworkAnimator` was not properly synchronizing animation state updates. (#2481)
- Fixed float NetworkVariables not being rendered properly in the inspector of NetworkObjects. (#2441)
- Fixed an issue where Named Message Handlers could remove themselves causing an exception when the metrics tried to access the name of the message.(#2426)
- Fixed registry of public `NetworkVariable`s in derived `NetworkBehaviour`s (#2423)
- Fixed issue where runtime association of `Animator` properties to `AnimationCurve`s would cause `NetworkAnimator` to attempt to update those changes. (#2416)
- Fixed issue where `NetworkAnimator` would not check if its associated `Animator` was valid during serialization and would spam exceptions in the editor console. (#2416)
- Fixed issue with a child's rotation rolling over when interpolation is enabled on a `NetworkTransform`. Now using half precision or full quaternion synchronization will always update all axis. (#2388)
- Fixed issue where `NetworkTransform` was not setting the teleport flag when the `NetworkTransform.InLocalSpace` value changed. This issue only impacted `NetworkTransform` when interpolation was enabled. (#2388)
- Fixed issue when the `NetworkSceneManager.ClientSynchronizationMode` is `LoadSceneMode.Additive` and the server changes the currently active scene prior to a client connecting then upon a client connecting and being synchronized the NetworkSceneManager would clear its internal ScenePlacedObjects list that could already be populated. (#2383)
- Fixed issue where a client would load duplicate scenes of already preloaded scenes during the initial client synchronization and `NetworkSceneManager.ClientSynchronizationMode` was set to `LoadSceneMode.Additive`. (#2383)

## [1.3.1] - 2023-03-27

### Added

- Added detection and graceful handling of corrupt packets for additional safety. (#2419)

### Changed

- The UTP component UI has been updated to be more user-friendly for new users by adding a simple toggle to switch between local-only (127.0.0.1) and remote (0.0.0.0) binding modes, using the toggle "Allow Remote Connections" (#2408)
- Updated `UnityTransport` dependency on `com.unity.transport` to 1.3.3. (#2450)
- `NetworkShow()` of `NetworkObject`s are delayed until the end of the frame to ensure consistency of delta-driven variables like `NetworkList`.
- Dirty `NetworkObject` are reset at end-of-frame and not at serialization time.
- `NetworkHide()` of an object that was just `NetworkShow()`n produces a warning, as remote clients will _not_ get a spawn/despawn pair.
- Renamed the NetworkTransform.SetState parameter `shouldGhostsInterpolate` to `teleportDisabled` for better clarity of what that parameter does. (#2228)
- Network prefabs are now stored in a ScriptableObject that can be shared between NetworkManagers, and have been exposed for public access. By default, a Default Prefabs List is created that contains all NetworkObject prefabs in the project, and new NetworkManagers will default to using that unless that option is turned off in the Netcode for GameObjects settings. Existing NetworkManagers will maintain their existing lists, which can be migrated to the new format via a button in their inspector. (#2322)

### Fixed

- Fixed issue where changes to a layer's weight would not synchronize unless a state transition was occurring.(#2399)
- Fixed issue where `NetworkManager.LocalClientId` was returning the `NetworkTransport.ServerClientId` as opposed to the `NetworkManager.m_LocalClientId`. (#2398)
- Fixed issue where a dynamically spawned `NetworkObject` parented under an in-scene placed `NetworkObject` would have its `InScenePlaced` value changed to `true`. This would result in a soft synchronization error for late joining clients. (#2396)
- Fixed a UTP test that was failing when you install Unity Transport package 2.0.0 or newer. (#2347)
- Fixed issue where `NetcodeSettingsProvider` would throw an exception in Unity 2020.3.x versions. (#2345)
- Fixed server side issue where, depending upon component ordering, some NetworkBehaviour components might not have their OnNetworkDespawn method invoked if the client side disconnected. (#2323)
- Fixed a case where data corruption could occur when using UnityTransport when reaching a certain level of send throughput. (#2332)
- Fixed an issue in `UnityTransport` where an exception would be thrown if starting a Relay host/server on WebGL. This exception should only be thrown if using direct connections (where WebGL can't act as a host/server). (#2321)
- Fixed `NetworkAnimator` issue where it was not checking for `AnimatorStateTtansition.destinationStateMachine` and any possible sub-states defined within it. (#2309)
- Fixed `NetworkAnimator` issue where the host client was receiving the ClientRpc animation updates when the host was the owner.(#2309)
- Fixed `NetworkAnimator` issue with using pooled objects and when specific properties are cleaned during despawn and destroy.(#2309)
- Fixed issue where `NetworkAnimator` was checking for animation changes when the associated `NetworkObject` was not spawned.(#2309)
- Corrected an issue with the documentation for BufferSerializer (#2401)

## [1.2.0] - 2022-11-21

### Added

- Added protected method `NetworkBehaviour.OnSynchronize` which is invoked during the initial `NetworkObject` synchronization process. This provides users the ability to include custom serialization information that will be applied to the `NetworkBehaviour` prior to the `NetworkObject` being spawned. (#2298)
- Added support for different versions of the SDK to talk to each other in circumstances where changes permit it. Starting with this version and into future versions, patch versions should be compatible as long as the minor version is the same. (#2290)
- Added `NetworkObject` auto-add helper and Multiplayer Tools install reminder settings to Project Settings. (#2285)
- Added `public string DisconnectReason` getter to `NetworkManager` and `string Reason` to `ConnectionApprovalResponse`. Allows connection approval to communicate back a reason. Also added `public void DisconnectClient(ulong clientId, string reason)` allowing setting a disconnection reason, when explicitly disconnecting a client. (#2280)

### Changed

- Changed 3rd-party `XXHash` (32 & 64) implementation with an in-house reimplementation (#2310)
- When `NetworkConfig.EnsureNetworkVariableLengthSafety` is disabled `NetworkVariable` fields do not write the additional `ushort` size value (_which helps to reduce the total synchronization message size_), but when enabled it still writes the additional `ushort` value. (#2298)
- Optimized bandwidth usage by encoding most integer fields using variable-length encoding. (#2276)

### Fixed
- Fixed `IsSpawnedObjectsPendingInDontDestroyOnLoad` is only set to true when loading a scene using `LoadSceneMode.Singleonly`. (#2330)
- Fixed issue where `NetworkTransform` components nested under a parent with a `NetworkObject` component  (i.e. network prefab) would not have their associated `GameObject`'s transform synchronized. (#2298)
- Fixed issue where `NetworkObject`s that failed to instantiate could cause the entire synchronization pipeline to be disrupted/halted for a connecting client. (#2298)
- Fixed issue where in-scene placed `NetworkObject`s nested under a `GameObject` would be added to the orphaned children list causing continual console warning log messages. (#2298)
- Custom messages are now properly received by the local client when they're sent while running in host mode. (#2296)
- Fixed issue where the host would receive more than one event completed notification when loading or unloading a scene only when no clients were connected. (#2292)
- Fixed an issue in `UnityTransport` where an error would be logged if the 'Use Encryption' flag was enabled with a Relay configuration that used a secure protocol. (#2289)
- Fixed issue where in-scene placed `NetworkObjects` were not honoring the `AutoObjectParentSync` property. (#2281)
- Fixed the issue where `NetworkManager.OnClientConnectedCallback` was being invoked before in-scene placed `NetworkObject`s had been spawned when starting `NetworkManager` as a host. (#2277)
- Creating a `FastBufferReader` with `Allocator.None` will not result in extra memory being allocated for the buffer (since it's owned externally in that scenario). (#2265)

### Removed

- Removed the `NetworkObject` auto-add and Multiplayer Tools install reminder settings from the Menu interface. (#2285)

## [1.1.0] - 2022-10-21

### Added

- Added `NetworkManager.IsApproved` flag that is set to `true` a client has been approved.(#2261)
- `UnityTransport` now provides a way to set the Relay server data directly from the `RelayServerData` structure (provided by the Unity Transport package) throuh its `SetRelayServerData` method. This allows making use of the new APIs in UTP 1.3 that simplify integration of the Relay SDK. (#2235)
- IPv6 is now supported for direct connections when using `UnityTransport`. (#2232)
- Added WebSocket support when using UTP 2.0 with `UseWebSockets` property in the `UnityTransport` component of the `NetworkManager` allowing to pick WebSockets for communication. When building for WebGL, this selection happens automatically. (#2201)
- Added position, rotation, and scale to the `ParentSyncMessage` which provides users the ability to specify the final values on the server-side when `OnNetworkObjectParentChanged` is invoked just before the message is created (when the `Transform` values are applied to the message). (#2146)
- Added `NetworkObject.TryRemoveParent` method for convenience purposes opposed to having to cast null to either `GameObject` or `NetworkObject`. (#2146)

### Changed

- Updated `UnityTransport` dependency on `com.unity.transport` to 1.3.0. (#2231)
- The send queues of `UnityTransport` are now dynamically-sized. This means that there shouldn't be any need anymore to tweak the 'Max Send Queue Size' value. In fact, this field is now removed from the inspector and will not be serialized anymore. It is still possible to set it manually using the `MaxSendQueueSize` property, but it is not recommended to do so aside from some specific needs (e.g. limiting the amount of memory used by the send queues in very constrained environments). (#2212)
- As a consequence of the above change, the `UnityTransport.InitialMaxSendQueueSize` field is now deprecated. There is no default value anymore since send queues are dynamically-sized. (#2212)
- The debug simulator in `UnityTransport` is now non-deterministic. Its random number generator used to be seeded with a constant value, leading to the same pattern of packet drops, delays, and jitter in every run. (#2196)
- `NetworkVariable<>` now supports managed `INetworkSerializable` types, as well as other managed types with serialization/deserialization delegates registered to `UserNetworkVariableSerialization<T>.WriteValue` and `UserNetworkVariableSerialization<T>.ReadValue` (#2219)
- `NetworkVariable<>` and `BufferSerializer<BufferSerializerReader>` now deserialize `INetworkSerializable` types in-place, rather than constructing new ones. (#2219)

### Fixed

- Fixed `NetworkManager.ApprovalTimeout` will not timeout due to slower client synchronization times as it now uses the added `NetworkManager.IsApproved` flag to determined if the client has been approved or not.(#2261)
- Fixed issue caused when changing ownership of objects hidden to some clients (#2242)
- Fixed issue where an in-scene placed NetworkObject would not invoke NetworkBehaviour.OnNetworkSpawn if the GameObject was disabled when it was despawned. (#2239)
- Fixed issue where clients were not rebuilding the `NetworkConfig` hash value for each unique connection request. (#2226)
- Fixed the issue where player objects were not taking the `DontDestroyWithOwner` property into consideration when a client disconnected. (#2225)
- Fixed issue where `SceneEventProgress` would not complete if a client late joins while it is still in progress. (#2222)
- Fixed issue where `SceneEventProgress` would not complete if a client disconnects. (#2222)
- Fixed issues with detecting if a `SceneEventProgress` has timed out. (#2222)
- Fixed issue #1924 where `UnityTransport` would fail to restart after a first failure (even if what caused the initial failure was addressed). (#2220)
- Fixed issue where `NetworkTransform.SetStateServerRpc` and `NetworkTransform.SetStateClientRpc` were not honoring local vs world space settings when applying the position and rotation. (#2203)
- Fixed ILPP `TypeLoadException` on WebGL on MacOS Editor and potentially other platforms. (#2199)
- Implicit conversion of NetworkObjectReference to GameObject will now return null instead of throwing an exception if the referenced object could not be found (i.e., was already despawned) (#2158)
- Fixed warning resulting from a stray NetworkAnimator.meta file (#2153)
- Fixed Connection Approval Timeout not working client side. (#2164)
- Fixed issue where the `WorldPositionStays` parenting parameter was not being synchronized with clients. (#2146)
- Fixed issue where parented in-scene placed `NetworkObject`s would fail for late joining clients. (#2146)
- Fixed issue where scale was not being synchronized which caused issues with nested parenting and scale when `WorldPositionStays` was true. (#2146)
- Fixed issue with `NetworkTransform.ApplyTransformToNetworkStateWithInfo` where it was not honoring axis sync settings when `NetworkTransformState.IsTeleportingNextFrame` was true. (#2146)
- Fixed issue with `NetworkTransform.TryCommitTransformToServer` where it was not honoring the `InLocalSpace` setting. (#2146)
- Fixed ClientRpcs always reporting in the profiler view as going to all clients, even when limited to a subset of clients by `ClientRpcParams`. (#2144)
- Fixed RPC codegen failing to choose the correct extension methods for `FastBufferReader` and `FastBufferWriter` when the parameters were a generic type (i.e., List<int>) and extensions for multiple instantiations of that type have been defined (i.e., List<int> and List<string>) (#2142)
- Fixed the issue where running a server (i.e. not host) the second player would not receive updates (unless a third player joined). (#2127)
- Fixed issue where late-joining client transition synchronization could fail when more than one transition was occurring.(#2127)
- Fixed throwing an exception in `OnNetworkUpdate` causing other `OnNetworkUpdate` calls to not be executed. (#1739)
- Fixed synchronization when Time.timeScale is set to 0. This changes timing update to use unscaled deltatime. Now network updates rate are independent from the local time scale. (#2171)
- Fixed not sending all NetworkVariables to all clients when a client connects to a server. (#1987)
- Fixed IsOwner/IsOwnedByServer being wrong on the server after calling RemoveOwnership (#2211)

## [1.0.2] - 2022-09-12

### Fixed

- Fixed issue where `NetworkTransform` was not honoring the InLocalSpace property on the authority side during OnNetworkSpawn. (#2170)
- Fixed issue where `NetworkTransform` was not ending extrapolation for the previous state causing non-authoritative instances to become out of synch. (#2170)
- Fixed issue where `NetworkTransform` was not continuing to interpolate for the remainder of the associated tick period. (#2170)
- Fixed issue during `NetworkTransform.OnNetworkSpawn` for non-authoritative instances where it was initializing interpolators with the replicated network state which now only contains the transform deltas that occurred during a network tick and not the entire transform state. (#2170)

## [1.0.1] - 2022-08-23

### Changed

- Changed version to 1.0.1. (#2131)
- Updated dependency on `com.unity.transport` to 1.2.0. (#2129)
- When using `UnityTransport`, _reliable_ payloads are now allowed to exceed the configured 'Max Payload Size'. Unreliable payloads remain bounded by this setting. (#2081)
- Performance improvements for cases with large number of NetworkObjects, by not iterating over all unchanged NetworkObjects

### Fixed

- Fixed an issue where reading/writing more than 8 bits at a time with BitReader/BitWriter would write/read from the wrong place, returning and incorrect result. (#2130)
- Fixed issue with the internal `NetworkTransformState.m_Bitset` flag not getting cleared upon the next tick advancement. (#2110)
- Fixed interpolation issue with `NetworkTransform.Teleport`. (#2110)
- Fixed issue where the authoritative side was interpolating its transform. (#2110)
- Fixed Owner-written NetworkVariable infinitely write themselves (#2109)
- Fixed NetworkList issue that showed when inserting at the very end of a NetworkList (#2099)
- Fixed issue where a client owner of a `NetworkVariable` with both owner read and write permissions would not update the server side when changed. (#2097)
- Fixed issue when attempting to spawn a parent `GameObject`, with `NetworkObject` component attached, that has one or more child `GameObject`s, that are inactive in the hierarchy, with `NetworkBehaviour` components it will no longer attempt to spawn the associated `NetworkBehaviour`(s) or invoke ownership changed notifications but will log a warning message. (#2096)
- Fixed an issue where destroying a NetworkBehaviour would not deregister it from the parent NetworkObject, leading to exceptions when the parent was later destroyed. (#2091)
- Fixed issue where `NetworkObject.NetworkHide` was despawning and destroying, as opposed to only despawning, in-scene placed `NetworkObject`s. (#2086)
- Fixed `NetworkAnimator` synchronizing transitions twice due to it detecting the change in animation state once a transition is started by a trigger. (#2084)
- Fixed issue where `NetworkAnimator` would not synchronize a looping animation for late joining clients if it was at the very end of its loop. (#2076)
- Fixed issue where `NetworkAnimator` was not removing its subscription from `OnClientConnectedCallback` when despawned during the shutdown sequence. (#2074)
- Fixed IsServer and IsClient being set to false before object despawn during the shutdown sequence. (#2074)
- Fixed NetworkList Value event on the server. PreviousValue is now set correctly when a new value is set through property setter. (#2067)
- Fixed NetworkLists not populating on client. NetworkList now uses the most recent list as opposed to the list at the end of previous frame, when sending full updates to dynamically spawned NetworkObject. The difference in behaviour is required as scene management spawns those objects at a different time in the frame, relative to updates. (#2062)

## [1.0.0] - 2022-06-27

### Changed

- Changed version to 1.0.0. (#2046)

## [1.0.0-pre.10] - 2022-06-21

### Added

- Added a new `OnTransportFailure` callback to `NetworkManager`. This callback is invoked when the manager's `NetworkTransport` encounters an unrecoverable error. Transport failures also cause the `NetworkManager` to shut down. Currently, this is only used by `UnityTransport` to signal a timeout of its connection to the Unity Relay servers. (#1994)
- Added `NetworkEvent.TransportFailure`, which can be used by implementations of `NetworkTransport` to signal to `NetworkManager` that an unrecoverable error was encountered. (#1994)
- Added test to ensure a warning occurs when nesting NetworkObjects in a NetworkPrefab (#1969)
- Added `NetworkManager.RemoveNetworkPrefab(...)` to remove a prefab from the prefabs list (#1950)

### Changed

- Updated `UnityTransport` dependency on `com.unity.transport` to 1.1.0. (#2025)
- (API Breaking) `ConnectionApprovalCallback` is no longer an `event` and will not allow more than 1 handler registered at a time. Also, `ConnectionApprovalCallback` is now an `Action<>` taking a `ConnectionApprovalRequest` and a `ConnectionApprovalResponse` that the client code must fill (#1972) (#2002)

### Removed

### Fixed
- Fixed issue where dynamically spawned `NetworkObject`s could throw an exception if the scene of origin handle was zero (0) and the `NetworkObject` was already spawned. (#2017)
- Fixed issue where `NetworkObject.Observers` was not being cleared when despawned. (#2009)
- Fixed `NetworkAnimator` could not run in the server authoritative mode. (#2003)
- Fixed issue where late joining clients would get a soft synchronization error if any in-scene placed NetworkObjects were parented under another `NetworkObject`. (#1985)
- Fixed issue where `NetworkBehaviourReference` would throw a type cast exception if using `NetworkBehaviourReference.TryGet` and the component type was not found. (#1984)
- Fixed `NetworkSceneManager` was not sending scene event notifications for the currently active scene and any additively loaded scenes when loading a new scene in `LoadSceneMode.Single` mode. (#1975)
- Fixed issue where one or more clients disconnecting during a scene event would cause `LoadEventCompleted` or `UnloadEventCompleted` to wait until the `NetworkConfig.LoadSceneTimeOut` period before being triggered. (#1973)
- Fixed issues when multiple `ConnectionApprovalCallback`s were registered (#1972)
- Fixed a regression in serialization support: `FixedString`, `Vector2Int`, and `Vector3Int` types can now be used in NetworkVariables and RPCs again without requiring a `ForceNetworkSerializeByMemcpy<>` wrapper. (#1961)
- Fixed generic types that inherit from NetworkBehaviour causing crashes at compile time. (#1976)
- Fixed endless dialog boxes when adding a `NetworkBehaviour` to a `NetworkManager` or vice-versa. (#1947)
- Fixed `NetworkAnimator` issue where it was only synchronizing parameters if the layer or state changed or was transitioning between states. (#1946)
- Fixed `NetworkAnimator` issue where when it did detect a parameter had changed it would send all parameters as opposed to only the parameters that changed. (#1946)
- Fixed `NetworkAnimator` issue where it was not always disposing the `NativeArray` that is allocated when spawned. (#1946)
- Fixed `NetworkAnimator` issue where it was not taking the animation speed or state speed multiplier into consideration. (#1946)
- Fixed `NetworkAnimator` issue where it was not properly synchronizing late joining clients if they joined while `Animator` was transitioning between states. (#1946)
- Fixed `NetworkAnimator` issue where the server was not relaying changes to non-owner clients when a client was the owner. (#1946)
- Fixed issue where the `PacketLoss` metric for tools would return the packet loss over a connection lifetime instead of a single frame. (#2004)

## [1.0.0-pre.9] - 2022-05-10

### Fixed

- Fixed Hosting again after failing to host now works correctly (#1938)
- Fixed NetworkManager to cleanup connected client lists after stopping (#1945)
- Fixed NetworkHide followed by NetworkShow on the same frame works correctly (#1940)

## [1.0.0-pre.8] - 2022-04-27

### Changed

- `unmanaged` structs are no longer universally accepted as RPC parameters because some structs (i.e., structs with pointers in them, such as `NativeList<T>`) can't be supported by the default memcpy struct serializer. Structs that are intended to be serialized across the network must add `INetworkSerializeByMemcpy` to the interface list (i.e., `struct Foo : INetworkSerializeByMemcpy`). This interface is empty and just serves to mark the struct as compatible with memcpy serialization. For external structs you can't edit, you can pass them to RPCs by wrapping them in `ForceNetworkSerializeByMemcpy<T>`. (#1901)
- Changed requirement to register in-scene placed NetworkObjects with `NetworkManager` in order to respawn them.  In-scene placed NetworkObjects are now automatically tracked during runtime and no longer need to be registered as a NetworkPrefab.  (#1898)

### Removed

- Removed `SIPTransport` (#1870)
- Removed `ClientNetworkTransform` from the package samples and moved to Boss Room's Utilities package which can be found [here](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/com.unity.multiplayer.samples.coop/Utilities/Net/ClientAuthority/ClientNetworkTransform.cs) (#1912)

### Fixed

- Fixed issue where `NetworkSceneManager` did not synchronize despawned in-scene placed NetworkObjects. (#1898)
- Fixed `NetworkTransform` generating false positive rotation delta checks when rolling over between 0 and 360 degrees. (#1890)
- Fixed client throwing an exception if it has messages in the outbound queue when processing the `NetworkEvent.Disconnect` event and is using UTP. (#1884)
- Fixed issue during client synchronization if 'ValidateSceneBeforeLoading' returned false it would halt the client synchronization process resulting in a client that was approved but not synchronized or fully connected with the server. (#1883)
- Fixed an issue where UNetTransport.StartServer would return success even if the underlying transport failed to start (#854)
- Passing generic types to RPCs no longer causes a native crash (#1901)
- Fixed a compile failure when compiling against com.unity.nuget.mono-cecil >= 1.11.4 (#1920)
- Fixed an issue where calling `Shutdown` on a `NetworkManager` that was already shut down would cause an immediate shutdown the next time it was started (basically the fix makes `Shutdown` idempotent). (#1877)

## [1.0.0-pre.7] - 2022-04-06

### Added

- Added editor only check prior to entering into play mode if the currently open and active scene is in the build list and if not displays a dialog box asking the user if they would like to automatically add it prior to entering into play mode. (#1828)
- Added `UnityTransport` implementation and `com.unity.transport` package dependency (#1823)
- Added `NetworkVariableWritePermission` to `NetworkVariableBase` and implemented `Owner` client writable netvars. (#1762)
- `UnityTransport` settings can now be set programmatically. (#1845)
- `FastBufferWriter` and Reader IsInitialized property. (#1859)
- Prefabs can now be added to the network at **runtime** (i.e., from an addressable asset). If `ForceSamePrefabs` is false, this can happen after a connection has been formed. (#1882)
- When `ForceSamePrefabs` is false, a configurable delay (default 1 second, configurable via `NetworkConfig.SpawnTimeout`) has been introduced to gracefully handle race conditions where a spawn call has been received for an object whose prefab is still being loaded. (#1882)

### Changed

- Changed `NetcodeIntegrationTestHelpers` to use `UnityTransport` (#1870)
- Updated `UnityTransport` dependency on `com.unity.transport` to 1.0.0 (#1849)

### Removed

- Removed `SnapshotSystem` (#1852)
- Removed `com.unity.modules.animation`, `com.unity.modules.physics` and `com.unity.modules.physics2d` dependencies from the package (#1812)
- Removed `com.unity.collections` dependency from the package (#1849)

### Fixed

- Fixed in-scene placed NetworkObjects not being found/ignored after a client disconnects and then reconnects. (#1850)
- Fixed issue where `UnityTransport` send queues were not flushed when calling `DisconnectLocalClient` or `DisconnectRemoteClient`. (#1847)
- Fixed NetworkBehaviour dependency verification check for an existing NetworkObject not searching from root parent transform relative GameObject. (#1841)
- Fixed issue where entries were not being removed from the NetworkSpawnManager.OwnershipToObjectsTable. (#1838)
- Fixed ClientRpcs would always send to all connected clients by default as opposed to only sending to the NetworkObject's Observers list by default. (#1836)
- Fixed clarity for NetworkSceneManager client side notification when it receives a scene hash value that does not exist in its local hash table. (#1828)
- Fixed client throws a key not found exception when it times out using UNet or UTP. (#1821)
- Fixed network variable updates are no longer limited to 32,768 bytes when NetworkConfig.EnsureNetworkVariableLengthSafety is enabled. The limits are now determined by what the transport can send in a message. (#1811)
- Fixed in-scene NetworkObjects get destroyed if a client fails to connect and shuts down the NetworkManager. (#1809)
- Fixed user never being notified in the editor that a NetworkBehaviour requires a NetworkObject to function properly. (#1808)
- Fixed PlayerObjects and dynamically spawned NetworkObjects not being added to the NetworkClient's OwnedObjects (#1801)
- Fixed issue where NetworkManager would continue starting even if the NetworkTransport selected failed. (#1780)
- Fixed issue when spawning new player if an already existing player exists it does not remove IsPlayer from the previous player (#1779)
- Fixed lack of notification that NetworkManager and NetworkObject cannot be added to the same GameObject with in-editor notifications (#1777)
- Fixed parenting warning printing for false positives (#1855)

## [1.0.0-pre.6] - 2022-03-02

### Added

- NetworkAnimator now properly synchrhonizes all animation layers as well as runtime-adjusted weighting between them (#1765)
- Added first set of tests for NetworkAnimator - parameter syncing, trigger set / reset, override network animator (#1735)

### Fixed

- Fixed an issue where sometimes the first client to connect to the server could see messages from the server as coming from itself. (#1683)
- Fixed an issue where clients seemed to be able to send messages to ClientId 1, but these messages would actually still go to the server (id 0) instead of that client. (#1683)
- Improved clarity of error messaging when a client attempts to send a message to a destination other than the server, which isn't allowed. (#1683)
- Disallowed async keyword in RPCs (#1681)
- Fixed an issue where Alpha release versions of Unity (version 2022.2.0a5 and later) will not compile due to the UNet Transport no longer existing (#1678)
- Fixed messages larger than 64k being written with incorrectly truncated message size in header (#1686) (credit: @kaen)
- Fixed overloading RPC methods causing collisions and failing on IL2CPP targets. (#1694)
- Fixed spawn flow to propagate `IsSceneObject` down to children NetworkObjects, decouple implicit relationship between object spawning & `IsSceneObject` flag (#1685)
- Fixed error when serializing ConnectionApprovalMessage with scene management disabled when one or more objects is hidden via the CheckObjectVisibility delegate (#1720)
- Fixed CheckObjectVisibility delegate not being properly invoked for connecting clients when Scene Management is enabled. (#1680)
- Fixed NetworkList to properly call INetworkSerializable's NetworkSerialize() method (#1682)
- Fixed NetworkVariables containing more than 1300 bytes of data (such as large NetworkLists) no longer cause an OverflowException (the limit on data size is now whatever limit the chosen transport imposes on fragmented NetworkDelivery mechanisms) (#1725)
- Fixed ServerRpcParams and ClientRpcParams must be the last parameter of an RPC in order to function properly. Added a compile-time check to ensure this is the case and trigger an error if they're placed elsewhere (#1721)
- Fixed FastBufferReader being created with a length of 1 if provided an input of length 0 (#1724)
- Fixed The NetworkConfig's checksum hash includes the NetworkTick so that clients with a different tickrate than the server are identified and not allowed to connect (#1728)
- Fixed OwnedObjects not being properly modified when using ChangeOwnership (#1731)
- Improved performance in NetworkAnimator (#1735)
- Removed the "always sync" network animator (aka "autosend") parameters (#1746)
- Fixed in-scene placed NetworkObjects not respawning after shutting down the NetworkManager and then starting it back up again (#1769)

## [1.0.0-pre.5] - 2022-01-26

### Added

- Added `PreviousValue` in `NetworkListEvent`, when `Value` has changed (#1528)

### Changed

- NetworkManager's GameObject is no longer allowed to be nested under one or more GameObject(s).(#1484)
- NetworkManager DontDestroy property was removed and now NetworkManager always is migrated into the DontDestroyOnLoad scene. (#1484)'

### Fixed

- Fixed network tick value sometimes being duplicated or skipped. (#1614)
- Fixed The ClientNetworkTransform sample script to allow for owner changes at runtime. (#1606)
- Fixed When the LogLevel is set to developer NetworkBehaviour generates warning messages when it should not (#1631)
- Fixed NetworkTransport Initialize now can receive the associated NetworkManager instance to avoid using NetworkManager.Singleton in transport layer (#1677)
- Fixed a bug where NetworkList.Contains value was inverted (#1363)

## [1.0.0-pre.4] - 2021-01-04

### Added

- Added `com.unity.modules.physics` and `com.unity.modules.physics2d` package dependencies (#1565)

### Removed

- Removed `com.unity.modules.ai` package dependency (#1565)
- Removed `FixedQueue`, `StreamExtensions`, `TypeExtensions` (#1398)

### Fixed

- Fixed in-scene NetworkObjects that are moved into the DDOL scene not getting restored to their original active state (enabled/disabled) after a full scene transition (#1354)
- Fixed invalid IL code being generated when using `this` instead of `this ref` for the FastBufferReader/FastBufferWriter parameter of an extension method. (#1393)
- Fixed an issue where if you are running as a server (not host) the LoadEventCompleted and UnloadEventCompleted events would fire early by the NetworkSceneManager (#1379)
- Fixed a runtime error when sending an array of an INetworkSerializable type that's implemented as a struct (#1402)
- NetworkConfig will no longer throw an OverflowException in GetConfig() when ForceSamePrefabs is enabled and the number of prefabs causes the config blob size to exceed 1300 bytes. (#1385)
- Fixed NetworkVariable not calling NetworkSerialize on INetworkSerializable types (#1383)
- Fixed NullReferenceException on ImportReferences call in NetworkBehaviourILPP (#1434)
- Fixed NetworkObjects not being despawned before they are destroyed during shutdown for client, host, and server instances. (#1390)
- Fixed KeyNotFound exception when removing ownership of a newly spawned NetworkObject that is already owned by the server. (#1500)
- Fixed NetworkManager.LocalClient not being set when starting as a host. (#1511)
- Fixed a few memory leak cases when shutting down NetworkManager during Incoming Message Queue processing. (#1323)
- Fixed network tick value sometimes being duplicated or skipped. (#1614)

### Changed

- The SDK no longer limits message size to 64k. (The transport may still impose its own limits, but the SDK no longer does.) (#1384)
- Updated com.unity.collections to 1.1.0 (#1451)
- NetworkManager's GameObject is no longer allowed to be nested under one or more GameObject(s).(#1484)
- NetworkManager DontDestroy property was removed and now NetworkManager always is migrated into the DontDestroyOnLoad scene. (#1484)

## [1.0.0-pre.3] - 2021-10-22

### Added

- ResetTrigger function to NetworkAnimator (#1327)

### Fixed

- Overflow exception when syncing Animator state. (#1327)
- Added `try`/`catch` around RPC calls, preventing exception from causing further RPC calls to fail (#1329)
- Fixed an issue where ServerClientId and LocalClientId could have the same value, causing potential confusion, and also fixed an issue with the UNet where the server could be identified with two different values, one of which might be the same as LocalClientId, and the other of which would not.(#1368)
- IL2CPP would not properly compile (#1359)

## [1.0.0-pre.2] - 2021-10-19


### Added

- Associated Known Issues for the 1.0.0-pre.1 release in the changelog

### Changed

- Updated label for `1.0.0-pre.1` changelog section

## [1.0.0-pre.1] - 2021-10-19

### Added

- Added `ClientNetworkTransform` sample to the SDK package (#1168)
- Added `Bootstrap` sample to the SDK package (#1140)
- Enhanced `NetworkSceneManager` implementation with additive scene loading capabilities (#1080, #955, #913)
  - `NetworkSceneManager.OnSceneEvent` provides improved scene event notificaitons
- Enhanced `NetworkTransform` implementation with per axis/component based and threshold based state replication (#1042, #1055, #1061, #1084, #1101)
- Added a jitter-resistent `BufferedLinearInterpolator<T>` for `NetworkTransform` (#1060)
- Implemented `NetworkPrefabHandler` that provides support for object pooling and `NetworkPrefab` overrides (#1073, #1004, #977, #905,#749, #727)
- Implemented auto `NetworkObject` transform parent synchronization at runtime over the network (#855)
- Adopted Unity C# Coding Standards in the codebase with `.editorconfig` ruleset (#666, #670)
- When a client tries to spawn a `NetworkObject` an exception is thrown to indicate unsupported behavior. (#981)
- Added a `NetworkTime` and `NetworkTickSystem` which allows for improved control over time and ticks. (#845)
- Added a `OnNetworkDespawn` function to `NetworkObject` which gets called when a `NetworkObject` gets despawned and can be overriden. (#865)
- Added `SnapshotSystem` that would allow variables and spawn/despawn messages to be sent in blocks (#805, #852, #862, #963, #1012, #1013, #1021, #1040, #1062, #1064, #1083, #1091, #1111, #1129, #1166, #1192)
  - Disabled by default for now, except spawn/despawn messages
  - Will leverage unreliable messages with eventual consistency
- `NetworkBehaviour` and `NetworkObject`'s `NetworkManager` instances can now be overriden (#762)
- Added metrics reporting for the new network profiler if the Multiplayer Tools package is present (#1104, #1089, #1096, #1086, #1072, #1058, #960, #897, #891, #878)
- `NetworkBehaviour.IsSpawned` a quick (and stable) way to determine if the associated NetworkObject is spawned (#1190)
- Added `NetworkRigidbody` and `NetworkRigidbody2D` components to support networking `Rigidbody` and `Rigidbody2D` components (#1202, #1175)
- Added `NetworkObjectReference` and `NetworkBehaviourReference` structs which allow to sending `NetworkObject/Behaviours` over RPCs/`NetworkVariable`s (#1173)
- Added `NetworkAnimator` component to support networking `Animator` component (#1281, #872)

### Changed

- Bumped minimum Unity version, renamed package as "Unity Netcode for GameObjects", replaced `MLAPI` namespace and its variants with `Unity.Netcode` namespace and per asm-def variants (#1007, #1009, #1015, #1017, #1019, #1025, #1026, #1065)
  - Minimum Unity version:
    - 2019.4 → 2020.3+
  - Package rename:
    - Display name: `MLAPI Networking Library` → `Netcode for GameObjects`
    - Name: `com.unity.multiplayer.mlapi` → `com.unity.netcode.gameobjects`
    - Updated package description
  - All `MLAPI.x` namespaces are replaced with `Unity.Netcode`
    - `MLAPI.Messaging` → `Unity.Netcode`
    - `MLAPI.Connection` → `Unity.Netcode`
    - `MLAPI.Logging` → `Unity.Netcode`
    - `MLAPI.SceneManagement` → `Unity.Netcode`
    - and other `MLAPI.x` variants to `Unity.Netcode`
  - All assembly definitions are renamed with `Unity.Netcode.x` variants
    - `Unity.Multiplayer.MLAPI.Runtime` → `Unity.Netcode.Runtime`
    - `Unity.Multiplayer.MLAPI.Editor` → `Unity.Netcode.Editor`
    - and other `Unity.Multiplayer.MLAPI.x` variants to `Unity.Netcode.x` variants
- Renamed `Prototyping` namespace and assembly definition to `Components` (#1145)
- Changed `NetworkObject.Despawn(bool destroy)` API to default to `destroy = true` for better usability (#1217)
- Scene registration in `NetworkManager` is now replaced by Build Setttings → Scenes in Build List (#1080)
- `NetworkSceneManager.SwitchScene` has been replaced by `NetworkSceneManager.LoadScene` (#955)
- `NetworkManager, NetworkConfig, and NetworkSceneManager` scene registration replaced with scenes in build list (#1080)
- `GlobalObjectIdHash` replaced `PrefabHash` and `PrefabHashGenerator` for stability and consistency (#698)
- `NetworkStart` has been renamed to `OnNetworkSpawn`. (#865)
- Network variable cleanup - eliminated shared mode, variables are server-authoritative (#1059, #1074)
- `NetworkManager` and other systems are no longer singletons/statics (#696, #705, #706, #737, #738, #739, #746, #747, #763, #765, #766, #783, #784, #785, #786, #787, #788)
- Changed `INetworkSerializable.NetworkSerialize` method signature to use `BufferSerializer<T>` instead of `NetworkSerializer` (#1187)
- Changed `CustomMessagingManager`'s methods to use `FastBufferWriter` and `FastBufferReader` instead of `Stream` (#1187)
- Reduced internal runtime allocations by removing LINQ calls and replacing managed lists/arrays with native collections (#1196)

### Removed

- Removed `NetworkNavMeshAgent` (#1150)
- Removed `NetworkDictionary`, `NetworkSet` (#1149)
- Removed `NetworkVariableSettings` (#1097)
- Removed predefined `NetworkVariable<T>` types (#1093)
  - Removed `NetworkVariableBool`, `NetworkVariableByte`, `NetworkVariableSByte`, `NetworkVariableUShort`, `NetworkVariableShort`, `NetworkVariableUInt`, `NetworkVariableInt`, `NetworkVariableULong`, `NetworkVariableLong`, `NetworkVariableFloat`, `NetworkVariableDouble`, `NetworkVariableVector2`, `NetworkVariableVector3`, `NetworkVariableVector4`, `NetworkVariableColor`, `NetworkVariableColor32`, `NetworkVariableRay`, `NetworkVariableQuaternion`
- Removed `NetworkChannel` and `MultiplexTransportAdapter` (#1133)
- Removed ILPP backend for 2019.4, minimum required version is 2020.3+ (#895)
- `NetworkManager.NetworkConfig` had the following properties removed: (#1080)
  - Scene Registrations no longer exists
  - Allow Runtime Scene Changes was no longer needed and was removed
- Removed the NetworkObject.Spawn payload parameter (#1005)
- Removed `ProfilerCounter`, the original MLAPI network profiler, and the built-in network profiler module (2020.3). A replacement can now be found in the Multiplayer Tools package. (#1048)
- Removed UNet RelayTransport and related relay functionality in UNetTransport (#1081)
- Removed `UpdateStage` parameter from `ServerRpcSendParams` and `ClientRpcSendParams` (#1187)
- Removed `NetworkBuffer`, `NetworkWriter`, `NetworkReader`, `NetworkSerializer`, `PooledNetworkBuffer`, `PooledNetworkWriter`, and `PooledNetworkReader` (#1187)
- Removed `EnableNetworkVariable` in `NetworkConfig`, it is always enabled now (#1179)
- Removed `NetworkTransform`'s FixedSendsPerSecond, AssumeSyncedSends, InterpolateServer, ExtrapolatePosition, MaxSendsToExtrapolate, Channel, EnableNonProvokedResendChecks, DistanceSendrate (#1060) (#826) (#1042, #1055, #1061, #1084, #1101)
- Removed `NetworkManager`'s `StopServer()`, `StopClient()` and `StopHost()` methods and replaced with single `NetworkManager.Shutdown()` method for all (#1108)

### Fixed

- Fixed ServerRpc ownership check to `Debug.LogError` instead of `Debug.LogWarning` (#1126)
- Fixed `NetworkObject.OwnerClientId` property changing before `NetworkBehaviour.OnGainedOwnership()` callback (#1092)
- Fixed `NetworkBehaviourILPP` to iterate over all types in an assembly (#803)
- Fixed cross-asmdef RPC ILPP by importing types into external assemblies (#678)
- Fixed `NetworkManager` shutdown when quitting the application or switching scenes (#1011)
  - Now `NetworkManager` shutdowns correctly and despawns existing `NetworkObject`s
- Fixed Only one `PlayerPrefab` can be selected on `NetworkManager` inspector UI in the editor (#676)
- Fixed connection approval not being triggered for host (#675)
- Fixed various situations where messages could be processed in an invalid order, resulting in errors (#948, #1187, #1218)
- Fixed `NetworkVariable`s being default-initialized on the client instead of being initialized with the desired value (#1266)
- Improved runtime performance and reduced GC pressure (#1187)
- Fixed #915 - clients are receiving data from objects not visible to them (#1099)
- Fixed `NetworkTransform`'s "late join" issues, `NetworkTransform` now uses `NetworkVariable`s instead of RPCs (#826)
- Throw an exception for silent failure when a client tries to get another player's `PlayerObject`, it is now only allowed on the server-side (#844)

### Known Issues

- `NetworkVariable` does not serialize `INetworkSerializable` types through their `NetworkSerialize` implementation
- `NetworkObjects` marked as `DontDestroyOnLoad` are disabled during some network scene transitions
- `NetworkTransform` interpolates from the origin when switching Local Space synchronization
- Exceptions thrown in `OnNetworkSpawn` user code for an object will prevent the callback in other objects
- Cannot send an array of `INetworkSerializable` in RPCs
- ILPP generation fails with special characters in project path

## [0.2.0] - 2021-06-03

WIP version increment to pass package validation checks. Changelog & final version number TBD.

## [0.1.1] - 2021-06-01

This is hotfix v0.1.1 for the initial experimental Unity MLAPI Package.

### Changed

- Fixed issue with the Unity Registry package version missing some fixes from the v0.1.0 release.

## [0.1.0] - 2021-03-23

This is the initial experimental Unity MLAPI Package, v0.1.0.

### Added

- Refactored a new standard for Remote Procedure Call (RPC) in MLAPI which provides increased performance, significantly reduced boilerplate code, and extensibility for future-proofed code. MLAPI RPC includes `ServerRpc` and `ClientRpc` to execute logic on the server and client-side. This provides a single performant unified RPC solution, replacing MLAPI Convenience and Performance RPC (see [here](#removed-features)).
- Added standarized serialization types, including built-in and custom serialization flows. See [RFC #2](https://github.com/Unity-Technologies/com.unity.multiplayer.rfcs/blob/master/text/0002-serializable-types.md) for details.
- `INetworkSerializable` interface replaces `IBitWritable`.
- Added `NetworkSerializer`..., which is the main aggregator that implements serialization code for built-in supported types and holds `NetworkReader` and `NetworkWriter` instances internally.
- Added a Network Update Loop infrastructure that aids Netcode systems to update (such as RPC queue and transport) outside of the standard `MonoBehaviour` event cycle. See [RFC #8](https://github.com/Unity-Technologies/com.unity.multiplayer.rfcs/blob/master/text/0008-network-update-loop.md) and the following details:
  - It uses Unity's [low-level Player Loop API](https://docs.unity3d.com/ScriptReference/LowLevel.PlayerLoop.html) and allows for registering `INetworkUpdateSystem`s with `NetworkUpdate` methods to be executed at specific `NetworkUpdateStage`s, which may also be before or after `MonoBehaviour`-driven game logic execution.
  - You will typically interact with `NetworkUpdateLoop` for registration and `INetworkUpdateSystem` for implementation.
  - `NetworkVariable`s are now tick-based using the `NetworkTickSystem`, tracking time through network interactions and syncs.
- Added message batching to handle consecutive RPC requests sent to the same client. `RpcBatcher` sends batches based on requests from the `RpcQueueProcessing`, by batch size threshold or immediately.
- [GitHub 494](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/494): Added a constraint to allow one `NetworkObject` per `GameObject`, set through the `DisallowMultipleComponent` attribute.
- Integrated MLAPI with the Unity Profiler for versions 2020.2 and later:
  - Added new profiler modules for MLAPI that report important network data.
  - Attached the profiler to a remote player to view network data over the wire.
- A test project is available for building and experimenting with MLAPI features. This project is available in the MLAPI GitHub [testproject folder](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/tree/release/0.1.0/testproject).
- Added a [MLAPI Community Contributions](https://github.com/Unity-Technologies/mlapi-community-contributions/tree/master/com.mlapi.contrib.extensions) new GitHub repository to accept extensions from the MLAPI community. Current extensions include moved MLAPI features for lag compensation (useful for Server Authoritative actions) and `TrackedObject`.

### Changed

- [GitHub 520](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/520): MLAPI now uses the Unity Package Manager for installation management.
- Added functionality and usability to `NetworkVariable`, previously called `NetworkVar`. Updates enhance options and fully replace the need for `SyncedVar`s.
- [GitHub 507](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/507): Reimplemented `NetworkAnimator`, which synchronizes animation states for networked objects.
- GitHub [444](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/444) and [455](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/455): Channels are now represented as bytes instead of strings.

For users of previous versions of MLAPI, this release renames APIs due to refactoring. All obsolete marked APIs have been removed as per [GitHub 513](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/513) and [GitHub 514](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/514).

| Previous MLAPI Versions | V 0.1.0 Name |
| -- | -- |
| `NetworkingManager` | `NetworkManager` |
| `NetworkedObject` | `NetworkObject` |
| `NetworkedBehaviour` | `NetworkBehaviour` |
| `NetworkedClient` | `NetworkClient` |
| `NetworkedPrefab` | `NetworkPrefab` |
| `NetworkedVar` | `NetworkVariable` |
| `NetworkedTransform` | `NetworkTransform` |
| `NetworkedAnimator` | `NetworkAnimator` |
| `NetworkedAnimatorEditor` | `NetworkAnimatorEditor` |
| `NetworkedNavMeshAgent` | `NetworkNavMeshAgent` |
| `SpawnManager` | `NetworkSpawnManager` |
| `BitStream` | `NetworkBuffer` |
| `BitReader` | `NetworkReader` |
| `BitWriter` | `NetworkWriter` |
| `NetEventType` | `NetworkEventType` |
| `ChannelType` | `NetworkDelivery` |
| `Channel` | `NetworkChannel` |
| `Transport` | `NetworkTransport` |
| `NetworkedDictionary` | `NetworkDictionary` |
| `NetworkedList` | `NetworkList` |
| `NetworkedSet` | `NetworkSet` |
| `MLAPIConstants` | `NetworkConstants` |
| `UnetTransport` | `UNetTransport` |

### Fixed

- [GitHub 460](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/460): Fixed an issue for RPC where the host-server was not receiving RPCs from the host-client and vice versa without the loopback flag set in `NetworkingManager`.
- Fixed an issue where data in the Profiler was incorrectly aggregated and drawn, which caused the profiler data to increment indefinitely instead of resetting each frame.
- Fixed an issue the client soft-synced causing PlayMode client-only scene transition issues, caused when running the client in the editor and the host as a release build. Users may have encountered a soft sync of `NetworkedInstanceId` issues in the `SpawnManager.ClientCollectSoftSyncSceneObjectSweep` method.
- [GitHub 458](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/458): Fixed serialization issues in `NetworkList` and `NetworkDictionary` when running in Server mode.
- [GitHub 498](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/498): Fixed numerical precision issues to prevent not a number (NaN) quaternions.
- [GitHub 438](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/438): Fixed booleans by reaching or writing bytes instead of bits.
- [GitHub 519](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/519): Fixed an issue where calling `Shutdown()` before making `NetworkManager.Singleton = null` is null on `NetworkManager.OnDestroy()`.

### Removed

With a new release of MLAPI in Unity, some features have been removed:

- SyncVars have been removed from MLAPI. Use `NetworkVariable`s in place of this functionality. <!-- MTT54 -->
- [GitHub 527](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/527): Lag compensation systems and `TrackedObject` have moved to the new [MLAPI Community Contributions](https://github.com/Unity-Technologies/mlapi-community-contributions/tree/master/com.mlapi.contrib.extensions) repo.
- [GitHub 509](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/509): Encryption has been removed from MLAPI. The `Encryption` option in `NetworkConfig` on the `NetworkingManager` is not available in this release. This change will not block game creation or running. A current replacement for this functionality is not available, and may be developed in future releases. See the following changes:
  - Removed `SecuritySendFlags` from all APIs.
  - Removed encryption, cryptography, and certificate configurations from APIs including `NetworkManager` and `NetworkConfig`.
  - Removed "hail handshake", including `NetworkManager` implementation and `NetworkConstants` entries.
  - Modified `RpcQueue` and `RpcBatcher` internals to remove encryption and authentication from reading and writing.
- Removed the previous MLAPI Profiler editor window from Unity versions 2020.2 and later.
- Removed previous MLAPI Convenience and Performance RPC APIs with the new standard RPC API. See [RFC #1](https://github.com/Unity-Technologies/com.unity.multiplayer.rfcs/blob/master/text/0001-std-rpc-api.md) for details.
- [GitHub 520](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/pull/520): Removed the MLAPI Installer.

### Known Issues

- `NetworkNavMeshAgent` does not synchronize mesh data, Agent Size, Steering, Obstacle Avoidance, or Path Finding settings. It only synchronizes the destination and velocity, not the path to the destination.
- For `RPC`, methods with a `ClientRpc` or `ServerRpc` suffix which are not marked with [ServerRpc] or [ClientRpc] will cause a compiler error.
- For `NetworkAnimator`, Animator Overrides are not supported. Triggers do not work.
- For `NetworkVariable`, the `NetworkDictionary` `List` and `Set` must use the `reliableSequenced` channel.
- `NetworkObjects`s are supported but when spawning a prefab with nested child network objects you have to manually call spawn on them
- `NetworkTransform` have the following issues:
  - Replicated objects may have jitter.
  - The owner is always authoritative about the object's position.
  - Scale is not synchronized.
- Connection Approval is not called on the host client.
- For `NamedMessages`, always use `NetworkBuffer` as the underlying stream for sending named and unnamed messages.
- For `NetworkManager`, connection management is limited. Use `IsServer`, `IsClient`, `IsConnectedClient`, or other code to check if MLAPI connected correctly.

## [0.0.1-preview.1] - 2020-12-20

This was an internally-only-used version of the Unity MLAPI Package
