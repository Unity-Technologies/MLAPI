using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles spawning different prefab versions based on whether it is a server or client.
/// !!! CAUTION !!!
/// Both network prefabs **MUST** have the same <see cref="NetworkBehaviour"/> components
/// and any server or client specific components that are not netcode related but are
/// dependencies of a <see cref="NetworkBehaviour"/> component on only the server or client
/// needs to have code within the <see cref="NetworkBehaviour"/> component to account for
/// any missing dependencies.
/// </summary>
public class NetworkPrefabOverrideHandler : NetworkBehaviour, INetworkPrefabInstanceHandler
{
    public GameObject ClientNetworkPrefab;

    public GameObject ServerNetworkPrefab;

    public override void OnNetworkSpawn()
    {
        // Register the server network prefab since server is handling spawning
        if (NetworkManager && NetworkManager.PrefabHandler != null)
        {
            NetworkManager.PrefabHandler.AddHandler(ServerNetworkPrefab, this);
        }
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.PrefabHandler.RemoveHandler(ServerNetworkPrefab);
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Invoked on both server and clients when the prefab is spawned.
    /// Server-side will spawn the server version.
    /// Client-side will spawn the client version.
    /// </summary>
    /// <param name="ownerClientId">the client identifier that will own this network prefab instance</param>
    /// <param name="position">optional to use the position passed in</param>
    /// <param name="rotation">optional to use the rotation passed in</param>
    /// <returns></returns>
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var gameObject = IsServer ? Instantiate(ServerNetworkPrefab) : Instantiate(ClientNetworkPrefab);
        // You could integrate spawn locations here and on the server side apply the spawn position at
        // this stage of the spawn process.
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        return gameObject.GetComponent<NetworkObject>();
    }

    public void Destroy(NetworkObject networkObject)
    {
        Destroy(networkObject.gameObject);
    }
}

