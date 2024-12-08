using Unity.Netcode;
using UnityEngine;


/// <summary>
/// An example of how to get server or client specific behaviors without
/// directly using a <see cref="NetworkBehaviour"/> but still associating
/// with a <see cref="NetworkBehaviour"/>.
/// </summary>
public class InstanceTypeLocalBehavior : MonoBehaviour, INetworkUpdateSystem
{
    [Tooltip("When enabled, this will run only on a server or host.")]
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
            if (ServerOnly && m_NetworkManager.IsServer)
            {
                NetworkUpdateLoop.RegisterNetworkUpdate(this, NetworkUpdateStage.Update);
            }
        }
        else
        {
            NetworkUpdateLoop.UnregisterAllNetworkUpdates(this);
            m_NetworkManager = null;
        }
    }

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
