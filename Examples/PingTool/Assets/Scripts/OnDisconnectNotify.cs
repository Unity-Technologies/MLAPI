using Unity.Netcode;
using UnityEngine;

public class OnDisconnectNotify : MonoBehaviour
{
    public NetworkManager NetworkManager;
    private void Start()
    {
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
        NetworkManager.OnConnectionEvent += OnConnectionEvent;
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        NetworkManagerHelper.Instance.LogMessage($"[{Time.realtimeSinceStartup}] Connection event {eventData.EventType} for Client-{eventData.ClientId}.");
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        NetworkManagerHelper.Instance.LogMessage($"[{Time.realtimeSinceStartup}] Connected event invoked for Client-{clientId}.");
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        NetworkManagerHelper.Instance.LogMessage($"[{Time.realtimeSinceStartup}] Disconnected event invoked for Client-{clientId}.");
    }

    private void OnDestroy()
    {
        NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        NetworkManager.OnConnectionEvent -= OnConnectionEvent;
    }
}
