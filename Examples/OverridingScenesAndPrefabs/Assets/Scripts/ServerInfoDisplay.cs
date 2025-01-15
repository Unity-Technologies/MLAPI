#if !DEDICATED_SERVER
using Unity.Netcode;
#endif
using UnityEngine;
using UnityEngine.UI;

public class ServerInfoDisplay : MonoBehaviour
{
    public Text ServerTime;
    public Text PlayerCount;
#if !DEDICATED_SERVER
    private void OnGUI()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (ServerTime)
        {
            ServerTime.text = $"NetworkTick: {NetworkManager.Singleton.ServerTime.Tick}";
        }

        if (PlayerCount)
        {
            PlayerCount.text = $"Player Count: {NetworkManager.Singleton.ConnectedClients.Count}";
        }
    }
#endif
}
