# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Additional documentation and release notes are available at [Multiplayer Documentation](https://docs-multiplayer.unity3d.com).

## [Unreleased]

### Added

- Enhanced `NetworkSceneManager` implementation with additive scene loading capabilities (#1080, #955, #913)
  - `NetworkSceneManager.OnSceneEvent` provides improved scene event notificaitons  
- Enhanced `NetworkTransform` implementation with per axis/component based and threshold based state replication (#1042, #1055, #1061, #1084, #1101)
- Implemented `NetworkPrefabHandler` that provides support for object pooling and `NetworkPrefab` overrides (#1073, #1004, #977, #905,#749, #727)
- Implemented auto `NetworkObject` transform parent synchronization at runtime over the network (#855)
- Adopted Unity C# Coding Standards in the codebase with `.editorconfig` ruleset (#666, #670)
- When a client tries to spawn a `NetworkObject` an exception is thrown to indicate unsupported behavior. (#981)
- Added a `NetworkTime` and `NetworkTickSystem` which allows for improved control over time and ticks. (#845)
- Added a `OnNetworkDespawn` function to `NetworkObject` which gets called when a `NetworkObject` gets despawned and can be overriden. (#865)
- Added `SnapshotSystem` that would allow variables and spawn/despawn messages to be sent in blocks (#805, #852, #862, #963, #1012, #1013, #1021, #1040, #1062, #1064, #1083, #1111, #1129, #1166, #1192)
  - Disabled by default for now, except spawn/despawn messages
  - Will leverage unreliable messages with eventual consistency
- `NetworkBehaviour` and `NetworkObject`'s `NetworkManager` instances can now be overriden (#762)
- Added metrics reporting for the new network profiler if the Multiplayer Tools package is present (#1104, #1089, #1096, #1086, #1072, #1058, #960, #897, #891, #878)

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
- Scene registration in `NetworkManager` is now replaced by Build Setttings → Scenes in Build List (#1080)
- `NetworkSceneManager.SwitchScene` has been replaced by `NetworkSceneManager.LoadScene` (#955)
- `GlobalObjectIdHash` replaced `PrefabHash` and `PrefabHashGenerator` for stability and consistency (#698)
- `NetworkStart` has been renamed to `OnNetworkSpawn`. (#865)
- Network variable cleanup - eliminated shared mode, variables are server-authoritative (#1059, #1074)
- `NetworkManager` and other systems are no longer singletons/statics (#696, #705, #706, #737, #738, #739, #746, #747, #763, #765, #766, #783, #784, #785, #786, #787, #788)

### Deprecated

- something

### Removed

- Removed ILPP backend for 2019.4, minimum required version is 2020.3+ (#895)
- `NetworkManager.NetworkConfig` had the following properties removed: (#1080)
  - Scene Registrations no longer exists
  - Allow Runtime Scene Changes was no longer needed and was removed
- Removed the NetworkObject.Spawn payload parameter (#1005)
- Removed `ProfilerCounter`, the original MLAPI network profiler, and the built-in network profiler module (2020.3). A replacement can now be found in the Multiplayer Tools package. (#1048)
- Removed NetworkSet, NetworkDictionary (#1149)
- Removed UNet RelayTransport and related relay functionality in UNetTransport (#1081)

### Fixed

- Fixed `NetworkObject.OwnerClientId` property changing before `NetworkBehaviour.OnGainedOwnership()` callback (#1092)
- Fixed `NetworkBehaviourILPP` to iterate over all types in an assembly (#803)
- Fixed cross-asmdef RPC ILPP by importing types into external assemblies (#678)
- Fixed `NetworkManager` shutdown when quitting the application or switching scenes (#1011)
  - Now `NetworkManager` shutdowns correctly and despawns existing `NetworkObject`s
- Fixed Only one `PlayerPrefab` can be selected on `NetworkManager` inspector UI in the editor (#676)
- Fixed connection approval not being triggered for host (#675)

### Security

- something

### TODO

Jaedyn:

- [6181e7e0] (2021-09-17) Jaedyn Draper / feat: INetworkMessage (#1187)
- [dc708a56] (2021-09-15) Jaedyn Draper / feat: Fast buffer reader and fast buffer writer (#1082)
- [5deae108] (2021-08-12) Jaedyn Draper / fix: Disabling fixedupdate portion of SpawnRpcDespawn test because it's failing for known reasons that will be fixed in the IMessage refactor. (#1049)
- [40a6aec0] (2021-08-09) Jaedyn Draper / fix: corrected NetworkVariable WriteField/WriteDelta/ReadField/ReadDelta dropping the last byte if unaligned. (#1008)
- [c25821d2] (2021-07-27) Jaedyn Draper / fix: Fixes for a few things discovered from the message ordering refactor: (#985)
- [b9ffc1f1] (2021-07-23) Jaedyn Draper / feat: Message Ordering (#948)

Samuel:

- [5513c906] (2021-09-16) Sam Bellomo / feat: interpolation for network transform (#1060)
- [cc7a7d5c] (2021-08-19) Sam Bellomo / test: adding more details to multiprocess readme (#1050)
- [13e2b7f1] (2021-07-13) Sam Bellomo / test: multiprocess tests part 6: fixing issues runnings all tests together (#957)
- [bf296660] (2021-07-13) Sam Bellomo / docs: Perf tests part 5. Adding documentation and instructions (#952)
- [6c8efd66] (2021-07-12) Sam Bellomo / test: Perf tests part 4. Adding example of performance test with spawning x network objects at once (#925)
- [725a77a9] (2021-07-12) Sam Bellomo / test: Perf tests part 3. Adding ExecuteStepInContext for better test readability (#924)
- [833f1faf] (2021-07-09) Sam Bellomo / test: Perf tests part 2. Adding Test Coordinator and base test class (#923)
- [d08b84ac] (2021-07-08) Sam Bellomo / test: Perf tests part 1. Basis for multiprocess tests process orchestration.  (#922)
- [7ed627c6] (2021-06-30) Sam Bellomo / fix: reducing log level for noisy log and adding details for developer log (#926)
- [4679474b] (2021-06-30) Sam Bellomo / feat: users can set authority on network transform programmatically (#868)
- [e122376f] (2021-06-29) Sam Bellomo / refactor: move NetworkBehaviour update to a separate non-static class (#917)
- [0855557e] (2021-06-29) Sam Bellomo / test: add utils for multi instance tests (#914)
- [9a47c661] (2021-06-29) Sam Bellomo / test: downgrading testproject to 2020.3.12f1 (#927)
- [b4a3f663] (2021-06-08) Sam Bellomo / docs: adding more info to help debug on network transform error message (#892)
- [4b15869f] (2021-05-21) Sam Bellomo / fix: Adding exception for silent failure for clients getting other player's object #844Merge pull request #844 from Unity-Technologies/feature/adding-exception-for-client-side-player-object-get
- [63436440] (2021-05-21) Samuel Bellomo / Merge branch 'develop' into feature/adding-exception-for-client-side-player-object-get
- [7561c341] (2021-05-21) Samuel Bellomo / adding null check and spacing fix
- [e2b17b10] (2021-05-21) Samuel Bellomo / some cleanup
- [3566ea04] (2021-05-20) Samuel Bellomo / fixing a few issues when connecting and disconnecting additional clients Adding separate tests in SpawnManagerTests. Added Teardown
- [b3c155b5] (2021-05-20) Samuel Bellomo / Merge branch 'develop' into feature/adding-exception-for-client-side-player-object-get
- [d783a4e0] (2021-05-20) Samuel Bellomo / adding more tests
- [e2fd839c] (2021-05-19) Samuel Bellomo / Adding tests for that exception Adding the possibility to have multiple clients in MultiInstanceHelpers Updating exception check to make sure to use local networkmanager (so it works with tests)
- [d11e22be] (2021-05-19) Sam Bellomo / feat: NetworkTransform now uses NetworkVariables instead of RPCs (#826)
- [ad8ae404] (2021-05-18) Samuel Bellomo / Adding proper exception for invalid case. This is so users don't have silent failures calling this client side expecting to see other player objects. This solves issue https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/issues/581

Philipp:

- [e47b73fa] (2021-09-17) Philipp Deschain / feat: NetworkAnimator and ClientNetworkAnimator (#1191)
- [0ea502b0] (2021-08-03) Philipp Deschain / Replacing community NetworkManagerHUD with a simpler implementation (#993)
- [4fad5915] (2021-07-27) Phil Deschain / fix: Network animator server authority fixes (#972)
- [be0ca068] (2021-07-06) Phil Deschain / feat: network animator Trigger parameter support (#872)
- [e864e8eb] (2021-05-03) Phil Deschain / feat: OnAllClientsReady (#755)

Others (2nd iteration):

- [268c7ec3] (2021-09-17) Simon Lemay / fix: Increase timeout for UTP adapter tests (#1199)
- [d2db3940] (2021-09-17) Jesse Olmer / docs: Update repo and both package readme files (#1180)
- [3d5b29a0] (2021-09-17) Andrew Spiering / chore: updating UTP package to pull in DTLS fixes (#1197)
- [7b361c64] (2021-09-17) Luke Stampfli / fix: network time arguments (#1194)
- [4fe7a30c] (2021-09-17) Luke Stampfli / feat: network physics (#1175)
- [2091e2ae] (2021-09-16) zain-mecklai / test: build multiprocesstestplayer and add it to project tests job (#1174)
- [16d98507] (2021-09-16) kvassall-unity / fix: Report messages properly that are locally executed as 0 bytes (#1162)
- [0654eaf8] (2021-09-16) Luke Stampfli / feat: add `NetworkObject` and `NetworkBehaviour` reference types (#1173)
- [80913c10] (2021-09-16) Jeffrey Rainy / feat: snapshot spawn pre-requisite 2 (#1192)
- [5114ca80] (2021-09-15) Noel Stephens / feat: NetworkBehaviour.IsSpawned  (#1190)
- [d04560fa] (2021-09-15) becksebenius-unity / feat: added tip to the network manager inspector that directs to install tools (MTT-1211) (#1182)
- [db6de508] (2021-09-14) Matt Walsh / refactor!: remove network dictionary & set, use native container in List, add tests (#1149)
- [4e3880f0] (2021-09-14) Albin Corén / fix: Fixed remote disconnects not properly cleaning up (#1184)
- [0941fd7f] (2021-09-14) Noel Stephens / test: base changes to PR-1114 (#1165)
- [d2669aec] (2021-09-14) Noel Stephens / test: verify do not destroy networkobjects on networkmanager shutdown (#1183)
- [eaa2f196] (2021-09-14) Jeffrey Rainy / chore: removal of EnableNetworkVariable in NetworkConfig. It's always True now (#1179)
- [22810067] (2021-09-14) Albin Corén / fix: Fix DontDestroyWithOwner not returning ownership (#1181)
- [9590a056] (2021-09-14) Andrew Spiering / test: Giving Android some more room as the connection tests are timing sensitive (#1178)
- [5ef9f418] (2021-09-14) Noel Stephens / fix: unitytransport connectionmode buttons (#1176)
- [46dfe4a2] (2021-09-13) Matt Walsh / test: added min frames to multi-instance helper (#1170)
- [b7c5d803] (2021-09-13) zain-mecklai / chore: Add mobile tests to nightly trigger (#1161)
- [fbd893dc] (2021-09-13) Jeffrey Rainy / feat: snapshot spawn pre-requisite (#1166)
- [a02dfee5] (2021-09-13) Cristian Mazo / feat: Unity Transport + Relay (#887)
- [9d0f50e9] (2021-09-13) Noel Stephens / feat: client scene synchronization mode (#1171)
- [6a032b19] (2021-09-13) M. Fatih MAR / fix: add `link.xml` to prevent IL2CPP stripping `Unity.PerformanceTesting` (#1172)
- [03264b05] (2021-09-11) M. Fatih MAR / chore: add boilerplate for `ClientNetworkTransform` sample (#1168)
- [1fe6a0f2] (2021-09-10) M. Fatih MAR / chore: remove `ClientNetworkVariable` (#1167)
- [a38029a3] (2021-09-10) kvassall-unity / chore: Disable test while we reevaluate the assumption that INetworkM… (#1163)
- [8d2dcf99] (2021-09-10) Briancoughlin / docs: rename Manual.md to Index.md
- [ae16e8c6] (2021-09-10) Josie Messa / Only track one metric for scene sync and do not report scene name (#1159)
- [97abc439] (2021-09-09) zain-mecklai / test: create job definitions for mobile build and test (#1152)
- [599ed687] (2021-09-09) Noel Stephens / test: make test runner scene ignored by default for BaseMultiInstanceTest (#1154)
- [52293db0] (2021-09-08) Jeffrey Rainy / fix: remove left-over reference to SyncTransform (#1155)
- [ace9d895] (2021-09-08) Jeffrey Rainy / chore: remove unused SyncTransform.cs (#1153)
- [d76d2815] (2021-09-07) Matt Walsh / chore!: remove NetworkNavMeshAgent (#1150)
- [b6937e8b] (2021-09-07) Noel Stephens / fix: NetworkObject parenting support in scene transitioning (#1148)
- [825bf192] (2021-09-07) M. Fatih MAR / chore!: rename Prototyping asmdef to Components (#1145)
- [b55baa19] (2021-09-07) M. Fatih MAR / feat: add bootstrap sample to package (#1140)
- [be05d1aa] (2021-09-07) M. Fatih MAR / chore: remove `--yamato` param from `standards.py` (#1144)
- [847068bf] (2021-09-03) Noel Stephens / fix: MTT-504 connection approval messages and comparing networkconfig (#1138)
- [92e53618] (2021-09-03) M. Fatih MAR / refactor!: remove NetworkChannel and MultiplexTransportAdapter (#1133)
- [8a74421f] (2021-09-02) Noel Stephens / fix: networkscenemanager not releasing buffers from pool (#1132)
- [f1a07069] (2021-09-02) Matt Walsh / test: fixed-length strings in netvars (#1119)
- [b5b40dec] (2021-09-02) Jeffrey Rainy / fix: snapshot system. last fixes for release (#1129)
- [1bbe95f8] (2021-09-02) Albin Corén / refactor!: Unified Shutdown (#1108)
- [5ed41b95] (2021-09-02) Josie Messa / chore: Fill out unity project for integration test project (#1128)
- [5338ca2b] (2021-09-01) M. Fatih MAR / feat: make ServerRpc ownership check an error log instead of warning log (#1126)
- [f703ba57] (2021-09-01) Noel Stephens / fix: client connected InvokeOnClientConnectedCallback with scene management disabled (#1123)
- [370d9ef1] (2021-09-01) becksebenius-unity / fix: removed `public` class `NetcodeObserver` (MTT-1157) (#1122)
- [ed110d05] (2021-08-31) becksebenius-unity / feat: add NetworkMessageSent/Received metrics (#1112)
- [9c759d6f] (2021-08-31) Jeffrey Rainy / feat: snapshot. MTU sizing option for Snapshot. MTT-1087 (#1111)
- [96309fb7] (2021-08-31) Benoit Doyon / Add metrics for transport bytes sent and received (#1104)
- [49997da9] (2021-08-31) Valere Plantevin / fix: Missing end profiling sample (#1118)
- [af1ce68d] (2021-08-31) Jesse Olmer / chore: support standalone mode for netcode runtimetests (#1115)
- [75609cd1] (2021-08-31) Benoit Doyon / feat: Change MetricNames for a more complex value type (#1109)
- [7fbc65cb] (2021-08-31) Josie Messa / feat: Track scene event metrics (#1089)
- [ff185d6a] (2021-08-31) Jeffrey Rainy / style: whitespace fixes (#1117)
- [c1ee3b62] (2021-08-30) Noel Stephens / feat: replace scene registration with scenes in build list (#1080)
- [b5f761cf] (2021-08-27) Jeffrey Rainy / fix: mtt-857 GitHub issue 915 (#1099)
- [ced41388] (2021-08-27) Noel Stephens / fix: NetworkSceneManager exception when DontDestroyOnLoad NetworkObjects are being synchronized (#1090)
- [f3851d6a] (2021-08-27) M. Fatih MAR / feat: NetworkTransform Custom Editor Inspector UI (#1101)
- [f8f53f3e] (2021-08-27) M. Fatih MAR / refactor: remove TempGlobalObjectIdHashOverride (#1105)
- [bef00ff6] (2021-08-27) JS Fauteux / fix: MTT-1124 Counters are now reported in sync with other metrics (#1096)
- [00164832] (2021-08-27) M. Fatih MAR / refactor: convert using var statements to using var declarations (#1100)
- [4dfc7601] (2021-08-27) becksebenius-unity / chore: updated all of the namespaces to match the tools package change (#1095)
- [15d5bef0] (2021-08-26) Matt Walsh / refactor!: remove network variable settings, network behaviour cleanup (#1097)
- [3796565a] (2021-08-26) Jeffrey Rainy / fix: mtt-1088 review. Safer handling of out-of-order or old messages (#1091)
- [90e4bbe9] (2021-08-26) M. Fatih MAR / refactor: assign auto-incremented `GlobalObjectIdHash` as a fallback in `MultiInstanceHelpers.MakeNetworkObjectTestPrefab()` + fix flaky tests exposed by this fix (#1094)
- [f733bec4] (2021-08-25) becksebenius-unity / feat: fulfilling interface for tools to find network objects from an id (#1086)
- [2017e0fd] (2021-08-25) Matt Walsh / chore!: remove netvar predefined types (#1093)
- [a7ffde6a] (2021-08-25) M. Fatih MAR / fix: change OwnerClientId before firing OnGainedOwnership() callback (#1092)
- [611678a2] (2021-08-25) Matt Walsh / feat!: network variables - client auth, permission cleanup, containers (#1074)
- [fbfcc94e] (2021-08-25) M. Fatih MAR / chore: expose `--verbosity` through `standards.py` (#1085)
- [4c166a64] (2021-08-24) M. Fatih MAR / test: NetworkTransformStateTests no longer uses ReplNetworkState (#1084)

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
