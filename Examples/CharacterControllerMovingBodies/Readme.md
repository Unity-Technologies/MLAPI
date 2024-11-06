# Netcode for GameObjects Smooth Transform Space Transitions
## Non-Rigidbody CharacterController Parenting with Moving Bodies
![image](https://github.com/user-attachments/assets/096953ca-5d7d-40d5-916b-72212575d258)
This example provides you with the fundamental building blocks for smooth synchronized transitions between two non-rigidbody based objects. This includes transitioning from world to local, local to world, and local to local transform spaces. 

### The `CharacterController`
![image](https://github.com/user-attachments/assets/13c627bd-920d-40c8-8947-69aa37b44ebf)
The `CharacterController` component is assigned to the `PlayerNoRigidbody` player prefab. It includes a `MoverScriptNoRigidbody` that handles all of the player's motion and includes some additional "non-rigidbody to non-rigidbody" collision handling logic that is applied when a player bumps into a rotation and/or moving body. The player prefab includes a child "PlayerBallPrime" that rotates around the player in local space (nested `NetworkTransform`), and the "PlayerBallPrime" has 3 children ("PlayerBallChild1-3") that each rotates around a different axis of the "PlayerBallPrime". While the end resulting effect is kind of cool looking, they provide a point of reference as to whether there is any deviation of each child's given axial path relative to each parent level. Additionally, it shows how tick synchronized nested `NetworkTransform` components keep synchronized with their parent and how that persists when the parent is parented or has its parent removed.

### Rotating Bodies
#### StationaryBodyA&B
The stationary bodies are representative of changing the parenting of a `NetworkObject` between two `NetworkObject` instances while remaining in local space, interpolating, and smoothly transitioning between the two parents.
![image](https://github.com/user-attachments/assets/1b234fdc-efc9-4053-a947-531c4fe5dd96)

#### RotatingBody
The RotatingBody are just provide an example of a rotating non-Rigidbody object and one way to handle a rotation based collision. This also tests world to local and local to world transform space updates.
![image](https://github.com/user-attachments/assets/f5da2374-08b6-4eeb-9a4a-cddc67ecb33b)

#### MovingRotatingBody & ElevatorBody
Both of these bodies use a simple path to follow.
The MovingRotatingBody moves between two points on the world Z-axis and rotates while moving. The ElevatorBody moves between 4 points (forming a rectangular path) to show motion on more than one axis.
![image](https://github.com/user-attachments/assets/146912eb-0dcc-4089-a6ba-3e0dbb51fd4e)


### Example Limitations
This example is primarily to provide a starting point for anyone interested in exploring a non-Rigidbody motion based project and is not to be considered a full working solution but more of a place to start. As an example. you might notice there is no consideration of Y-axis motion when on the elevator so it impacts the `MoverScriptNoRigidbody` jumping. This could be extended to take into consideration the bodies within the `RotatingBodyLogic` script that would increase the upward velocity amount when on a moving body that does move in the Y Axis. This could be further extended by adding a `MonoBehaviour` component to the path points that define its direction of movement or by a normalized direction vector between points to determine the movement of the body. These kind of details are left up to you to determine what style best fits your project.

