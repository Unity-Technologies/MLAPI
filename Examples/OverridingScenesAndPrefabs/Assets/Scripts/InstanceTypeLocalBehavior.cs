using Unity.Netcode;
using UnityEngine;


/// <summary>
/// An example of how to get server or client specific behaviors without
/// directly using a <see cref="NetworkBehaviour"/>.
/// The comments below explain a bit further.
/// </summary>
public class InstanceTypeLocalBehavior : MonoBehaviour, INetworkUpdateSystem
{
    public string UniqueLocalInstanceContent;
    private MoverScriptNoRigidbody m_MoverScriptNoRigidbody;

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
            NetworkUpdateLoop.RegisterNetworkUpdate(this, NetworkUpdateStage.Update);
        }
        else
        {
            NetworkUpdateLoop.UnregisterAllNetworkUpdates(this);
        }
    }

    public void NetworkUpdate(NetworkUpdateStage updateStage)
    {
        if (updateStage == NetworkUpdateStage.Update)
        {
            OnUpdate();
        }
    }

    private float m_NextTimeToLog;
    private void OnUpdate()
    {
        if (m_NextTimeToLog < Time.realtimeSinceStartup)
        {
            var serverClient = m_MoverScriptNoRigidbody.IsServer ? "Server" : "Client";
            NetworkManagerBootstrapper.Instance.LogMessage($"[{Time.realtimeSinceStartup}][{serverClient}-{m_MoverScriptNoRigidbody.name}] {UniqueLocalInstanceContent}");
            m_NextTimeToLog = Time.realtimeSinceStartup + 5.0f;
        }
    }
}
