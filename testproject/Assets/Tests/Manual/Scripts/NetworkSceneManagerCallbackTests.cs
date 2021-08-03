using UnityEngine;
using Unity.Multiplayer.Netcode;

namespace TestProject.ManualTests
{
    [AddComponentMenu("MLAPI/NetworkSceneManagerCallbackTests")]
    public class NetworkSceneManagerCallbackTests : NetworkBehaviour
    {
        public void StartHost()
        {
            NetworkManager.StartHost();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.SceneManager.OnNotifyServerClientLoadedScene += (progress, clientId) =>
                {
                    Debug.Log("OnNotifyServerClientLoadedScene invoked on the host - Passed");
                };

                NetworkManager.SceneManager.OnNotifyServerAllClientsLoadedScene += (progress, timedOut) =>
                {
                    Debug.Log("OnNotifyServerAllClientsLoadedScene invoked on the host - Passed");
                };

                NetworkManager.SceneManager.SwitchScene("SceneWeAreSwitchingTo");
            }
        }
    }
}
