using System.Collections;
using Unity.Netcode;
using UnityEngine;


public class ShutdownWhenNoClients : MonoBehaviour
{
    [Tooltip("When enabled, the DGS will wait for the defined ShutdownWaitTime period of time and if no new clients connected will shutdown")]
    public bool WaitBeforeShutdown = true;

    [Tooltip("The period of time a server will wait after the last client has disconnected before completely shutting itself down.")]
    public float ShutdownWaitTime = 4.0f;

    private NetworkManager m_NetworkManager;

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
        if (m_NetworkManager == null)
        {
            Debug.LogError($"No {nameof(NetworkManager)} found on {name}! This component should be placed on the same {nameof(GameObject)} as the {nameof(NetworkManager)}!");
            // Disable until resolved
            gameObject.SetActive(false);
        }
        else
        {
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.OnServerStopped += OnServerStopped;
        }
    }

    private void OnServerStarted()
    {
        m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }

    private void OnServerStopped(bool obj)
    {
        m_NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        if (m_NetworkManager.ConnectedClientsList.Count == 1 && m_NetworkManager.ConnectedClients.ContainsKey(clientId))
        {
            if (WaitBeforeShutdown)
            {
                StartCoroutine(WaitForShutdown());
            }
            else
            {
                m_NetworkManager.Shutdown();
            }
        }
    }

    private IEnumerator WaitForShutdown()
    {
        yield return new WaitForSeconds(ShutdownWaitTime);
        // Make sure no clients have connected while waiting to shutdown
        if (m_NetworkManager.ConnectedClients.Count == 0)
        {
            // If none then shut down
            m_NetworkManager.Shutdown();
        }
    }
}
