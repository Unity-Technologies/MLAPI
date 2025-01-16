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
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(NetworkPrefabOverrideHandler))]
public class NetworkPrefabOverrideHandler : MonoBehaviour, INetworkPrefabInstanceHandler
{
    public GameObject NetworkPrefab;
    public GameObject NetworkPrefabOverride;

    private NetworkManagerBootstrapper m_NetworkManager;

    private void Start()
    {
        m_NetworkManager = GetComponent<NetworkManagerBootstrapper>();
        m_NetworkManager.PrefabHandler.AddHandler(NetworkPrefab, this);
        NetworkManager.OnDestroying += NetworkManager_OnDestroying;
    }

    private void NetworkManager_OnDestroying(NetworkManager obj)
    {
        m_NetworkManager.PrefabHandler.RemoveHandler(NetworkPrefab);
    }

    /// <summary>
    /// Invoked on both server and clients when the prefab is spawned.
    /// Server-side will spawn the default network prefab.
    /// Client-side will spawn the network prefab override version.
    /// </summary>
    /// <param name="ownerClientId">the client identifier that will own this network prefab instance</param>
    /// <param name="position">optional to use the position passed in</param>
    /// <param name="rotation">optional to use the rotation passed in</param>
    /// <returns></returns>
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var gameObject = m_NetworkManager.IsClient ? Instantiate(NetworkPrefabOverride) : Instantiate(NetworkPrefab);
        // You could integrate spawn locations here and on the server side apply the spawn position at
        // this stage of the spawn process.
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        return gameObject.GetComponent<NetworkObject>();
    }

    public void Destroy(NetworkObject networkObject)
    {
#if !DEDICATED_SERVER
        // Another useful thing about handling this instantiation and destruction of a NetworkObject is that you can do house cleaning
        // prior to the object being destroyed. This handles the scenario where the server is following a player and the player disconnects.
        // Before destroying the player object, we want to unparent the camera and reset the player being followed.
        if (m_NetworkManager.IsServer && !m_NetworkManager.IsHost && Camera.main != null && Camera.main.transform.parent == networkObject.transform)
        {
            m_NetworkManager.ClearFollowPlayer();
        }
#endif
        Destroy(networkObject.gameObject);
    }
}

