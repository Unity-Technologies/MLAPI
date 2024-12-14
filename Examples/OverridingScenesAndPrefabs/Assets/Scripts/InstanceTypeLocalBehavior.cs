using Unity.Netcode;
using UnityEngine;


/// <summary>
/// An example of how to get server or client specific behaviors without
/// directly using a <see cref="NetworkBehaviour"/> but still associating
/// with a <see cref="NetworkBehaviour"/>.
/// </summary>
public class InstanceTypeLocalBehavior : MonoBehaviour, INetworkUpdateSystem
{
    [Tooltip("When enabled, this will run only on a server or host. When disabled, this will only run on the owner of the local client player (including host).")]
    public bool ServerOnly;

    [Tooltip("This is the unique message example text displayed when running locally.")]
    public string UniqueLocalInstanceContent;

    private MoverScriptNoRigidbody m_MoverScriptNoRigidbody;
    private NetworkManager m_NetworkManager;
    private float m_NextTimeToLogMessage;

    private void Awake()
    {
        m_MoverScriptNoRigidbody = GetComponent<MoverScriptNoRigidbody>();
        m_MoverScriptNoRigidbody.NotifySpawnStatusChanged += OnSpawnStatusChanged;
    }

    /// <summary>
    /// Adjust this logic to fit your needs.
    /// This example makes the InstanceTypeLocalBehavior only update if:
    /// - It is a server (including host) and is marked for ServerOnly
    /// - It is a client (including host), is not marked for ServerOnly, and the local client is the owner of MoverScriptNoRigidbody.
    /// - It is in distributed authority mode, is not marked for ServerOnly, and the local client has authority of the MoverScriptNoRigidbody.
    /// </summary>
    private bool HasAuthority()
    {
        if (m_NetworkManager == null)
        {
            return false;
        }

        if (!ServerOnly && m_NetworkManager.DistributedAuthorityMode && m_MoverScriptNoRigidbody.HasAuthority)
        {
            return true;
        }
        else
        {
            if (ServerOnly && m_NetworkManager.IsServer)
            {
                return true;
            }
            else if (!ServerOnly && m_MoverScriptNoRigidbody.IsOwner)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///  <see cref="MoverScriptNoRigidbody.NotifySpawnStatusChanged"/>
    ///  Isolate the spawning status to the <see cref="NetworkBehaviour"/> and just
    ///  use actions, ecents, or delegates to notify non-shared behaviors that are
    ///  only on a server or client version of a network prefab that has a <see cref="NetworkPrefabOverrideHandler"/>.
    /// </summary>
    /// <param name="spawned"></param>
    private void OnSpawnStatusChanged(bool spawned)
    {
        if (spawned)
        {
            m_NetworkManager = m_MoverScriptNoRigidbody.NetworkManager;
            if (HasAuthority())
            {
                NetworkUpdateLoop.RegisterNetworkUpdate(this, NetworkUpdateStage.Update);
            }
        }
        else
        {
            // Whether registered or not, it is easier to just unregister always.
            NetworkUpdateLoop.UnregisterAllNetworkUpdates(this);
            m_NetworkManager = null;
        }
    }

    /// <summary>
    /// Invoked only on the instance(s) that have authority to update.
    /// <see cref="HasAuthority"/>
    /// </summary>
    /// <param name="updateStage"></param>
    public void NetworkUpdate(NetworkUpdateStage updateStage)
    {
        if (updateStage == NetworkUpdateStage.Update)
        {
            OnUpdate();
        }
    }


    private void OnUpdate()
    {
        if (m_NextTimeToLogMessage < Time.realtimeSinceStartup)
        {
            var serverClient = m_MoverScriptNoRigidbody.IsServer ? "Server" : "Client";
            NetworkManagerBootstrapper.Instance.LogMessage($"[{Time.realtimeSinceStartup}][{serverClient}-{m_MoverScriptNoRigidbody.name}] {UniqueLocalInstanceContent}");
            m_NextTimeToLogMessage = Time.realtimeSinceStartup + 5.0f;
        }
    }
}
