using UnityEngine;

namespace Unity.Netcode.Samples
{
    /// <summary>
    /// Component attached to the "Player Prefab" on the `NetworkManager`.
    /// </summary>
    public class BootstrapPlayer : NetworkBehaviour
    {
        /// <summary>
        /// If this method is invoked on the client instance of this player, it will invoke a `ServerRpc` on the server-side.
        /// If this method is invoked on the server instance of this player, it will teleport player to a random position.
        /// </summary>
        /// <remarks>
        /// Since a `NetworkTransform` component is attached to this player, and the authority on that component is set to "Server",
        /// this transform's position modification can only be performed on the server, where it will then be replicated down to all clients through `NetworkTransform`.
        /// </remarks>
        [ServerRpc]
        public void RandomTeleportServerRpc()
        {
            var oldPosition = transform.position;
            transform.position = GetRandomPositionOnXYPlane();
            var newPosition = transform.position;
            print($"{nameof(RandomTeleportServerRpc)}() -> {nameof(OwnerClientId)}: {OwnerClientId} --- {nameof(oldPosition)}: {oldPosition} --- {nameof(newPosition)}: {newPosition}");
        }

        private static Vector3 GetRandomPositionOnXYPlane()
        {
            return new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0f);
        }
    }
}
