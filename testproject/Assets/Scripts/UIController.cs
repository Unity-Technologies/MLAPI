using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if ENABLE_RELAY_SERVICE
using Unity.Services.Core;
using Unity.Services.Authentication;
#endif

public class UIController : MonoBehaviour
{
    public NetworkManager NetworkManager;
    public GameObject ButtonsRoot;
    public GameObject AuthButton;
    public GameObject JoinCode;

    public UnityTransport Transport;

    private void Awake()
    {
#if ENABLE_RELAY_SERVICE
        if (Transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport)
        {
            HideButtons();
            JoinCode.SetActive(false);
        }
#endif
    }

    public void StartServer()
    {
        NetworkManager.StartServer();
        HideButtons();
    }

    public void StartHost()
    {
        NetworkManager.StartHost();
        HideButtons();
    }

    public void StartClient()
    {
        NetworkManager.StartClient();
        HideButtons();
    }

    private void HideButtons()
    {
        ButtonsRoot.SetActive(false);
    }

#if ENABLE_RELAY_SERVICE
    public async void OnSignIn()
    {

        await UnityServices.InitializeAsync();
        Debug.Log("OnSignIn");
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Logging in with PlayerID {AuthenticationService.Instance.PlayerId}");

        if (AuthenticationService.Instance.IsSignedIn)
        {
            ButtonsRoot.SetActive(true);
            JoinCode.SetActive(true);
            AuthButton.SetActive(false);
        }

    }
#endif
}
