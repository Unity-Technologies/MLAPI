# Netcode for GameObjects <br /> Overriding Scenes and NetworkPrefabs
This example, based on the [Netcode for GameObjects Smooth Transform Space Transitions](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/tree/example/server-client-unique-scenes-and-prefabs/Examples/CharacterControllerMovingBodies), provides examples of using:
- [`NetworkPrefabHandler`](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.0/api/Unity.Netcode.NetworkPrefabHandler.html) to be able to dynamically control prefab overrides.
  - For this example, the prefab handler is overriding the player prefab. You will only see the end result of this portion of the example by running a pure server instance as this is the only instance that will create instances of the ServerPlayer network prefab instead of the ClientPlayer prefab.
- [`NetworkSceneManager.SetClientSynchronizationMode`](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.0/api/Unity.Netcode.NetworkSceneManager.html#Unity_Netcode_NetworkSceneManager_SetClientSynchronizationMode_UnityEngine_SceneManagement_LoadSceneMode_) to use existing preloaded scenes for synchronization.
- [`NetworkSceneManager.VerifySceneBeforeLoading`](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.0/api/Unity.Netcode.NetworkSceneManager.html#Unity_Netcode_NetworkSceneManager_VerifySceneBeforeLoading) to control which scenes are used to synchronize with newly connected clients.
  - This includes the exclusion of scenes already loaded for both clients and/or server.

_This example supports using the client-server and distributed authority network topologies._

## Client Synchronization Mode
A server or session owner, depending upon network topology being used, sends the synchronization mode that a newly newly connected client will use when synchronizing.
[Read the documentation for more information about client synchronization mode.](https://docs-multiplayer.unity3d.com/netcode/current/basics/scenemanagement/client-synchronization-mode/)
This example uses an additive client synchronization mode in order to use already loaded scenes during client synchronization.

## The Bootstrap Loading Process
![image](https://github.com/user-attachments/assets/fe04e058-3c5f-42dd-b55f-b0caea2d7f84)

### BootstrapScene
The first scene loaded. Contains the `NetworkManagerBootstrapper` in-scene placed `GameObject`.
![image](https://github.com/user-attachments/assets/061d5c60-0fea-4209-a2d0-2e2ec425eb60)

#### Scene Bootstrap Loader (component)
![image](https://github.com/user-attachments/assets/24d37c38-75a7-42cb-a42f-13e5ce856a63)

This component handles preloading scenes for both the client(s) and server. The `NetworkManagerBootstrapper` is an extended `NetworkManager` that requires the `SceneBootstrapLoader` component which upon being started will invoke `SceneBootstrapLoader.LoadMainMenu`. 
- **Default Active Scene Asset:** There is always an active scene. For this example, the default active scene is the same on both the client and server relative properties.
  - This could represent a lobby or network session main menu (i.e. create or join session).
  - Both the client and the server preload this scene prior to starting a network session.
- **Local Scene Assets:** There could be times where you want to load scenes specific to the `NetworkManager` instance type (i.e. client, host, or server).
  - These scenes are not synchronized by a server (client-server) or session owner (distributed authority).
  - Having different locally loaded scenes is typically more common in a client-server network topology.
  - In a distributed authority network topology, it is more common to keep all scenes synchronized but you might want to load non-synchronized scenes (i.e. menu interface for settings etc).
- **Shared Scene Assets:** These scenes are synchronized by the server or session owner (depending upon network topology used).
  - This example only provides a server specific set of scene assets to load because you can always add those same scenes to the client-side locally loaded scenes.
    - If the server synchronizes any scenes from the share scene assets with a client that already has those scene loaded, then those locally loaded scenes on the client side will be used during synchronization.
    - Depending upon how many scenes you want to synchronize and/or how large one or more scenes are, preloading scenes can reduce synchronization time for clients.
The `NetworkManagerBootstrapper` uses the `SceneBootstrapLoader` component to start the creation or joining of a network session. The logical flow looks like:
- `NetworkManagerBootstrapper` invokes `SceneBootstrapLoader.StartSession` when you click one of the (very simple) main menu buttons and passes in the mode/type of `NetworkManager` to start.
- Based on the `NetworkManager` type being started, the `SceneBootstrapLoader` will then:
 - Load the default active scene using the `UnityEngine.SceneManagement.SceneManager`.
 - Load the local scenes using the `UnityEngine.SceneManagement.SceneManager`.
 - Then it will create or join a network session by either starting the `NetworkManager` or connecting to the sesssion via multiplayer services.
 - _Server or Session Owner only:_
   - If any, load the shared (i.e. synchronized) scene assets using the `NetworkSceneManager`

#### NetworkManager Bootstrapper (component)
![image](https://github.com/user-attachments/assets/54d0695f-87d2-4626-bdf6-9cf72b82d7f8)

Handles the pre-network session menu interface along with connect and disconnect events. Since it is derived from `NetworkManager`, it also defines the network session configuration (i.e. `NetworkConfig`).

#### Network Prefab Override Handler (component)
![image](https://github.com/user-attachments/assets/c382c3ff-bc72-4a6f-b2f2-04e0e70b1fa8)

This prefab handler determines at runtime where the local `NetworkManager` instance is a client/host or server and will spawn either the ClientPlayer or ServerPlayer prefab. The `NetworkPrefabOverrideHandler` does not need to be a `NetworkBehaviour` and sometimes (especially for overriding the player prefab) it is better to handle prefab handlers prior to starting the `NetworkManager`.





