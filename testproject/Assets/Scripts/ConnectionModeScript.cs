#if ENABLE_RELAY_SERVICE
using System;
#endif
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if ENABLE_RELAY_SERVICE
using Unity.Services.Core;
using Unity.Services.Authentication;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;


/// <summary>
/// Used in tandem with the ConnectModeButtons prefab asset in test project
/// </summary>
public class ConnectionModeScript : MonoBehaviour
{
    [SerializeField]
    private GameObject m_ConnectionModeButtons;

    [SerializeField]
    private GameObject m_AuthenticationButtons;

    [SerializeField]
    private GameObject m_JoinCodeInput;

#if ENABLE_RELAY_SERVICE
    [SerializeField]
    private int m_MaxConnections = 10;
#endif

    [SerializeField]
    private LoadSceneMode m_ClientSynchronizationMode;

    [SerializeField]
    private GameObject m_DisconnectClientButton;


    private CommandLineProcessor m_CommandLineProcessor;

    [HideInInspector]
    public string RelayJoinCode { get; set; }

    internal void SetCommandLineHandler(CommandLineProcessor commandLineProcessor)
    {
        m_CommandLineProcessor = commandLineProcessor;
        if (m_CommandLineProcessor.AutoConnectEnabled())
        {
            StartCoroutine(WaitForNetworkManager());
        }
    }

    public delegate void OnNotifyConnectionEventDelegateHandler();

    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventServer;
    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventHost;
    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventClient;

    private IEnumerator WaitForNetworkManager()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            try
            {
                if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening)
                {
                    m_ConnectionModeButtons?.SetActive(false);
                    m_CommandLineProcessor.ProcessCommandLine();
                    break;
                }
            }
            catch { }
        }
        yield return null;
    }

    /// <summary>
    /// Check whether we are even using UnityTransport and
    /// if so whether it is using the RelayUnityTransport
    /// </summary>
    private bool HasRelaySupport()
    {
        var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (unityTransport != null && unityTransport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport)
        {
            return true;
        }
        return false;
    }

    // Start is called before the first frame update
    private void Start()
    {
        //If we have a NetworkManager instance and we are not listening and m_ConnectionModeButtons is not null then show the connection mode buttons
        if (m_ConnectionModeButtons && m_AuthenticationButtons)
        {

            if (HasRelaySupport())
            {
                m_JoinCodeInput.SetActive(true);
                //If Start() is called on the first frame update, it's not likely that the AuthenticationService is going to be instantiated yet
                //Moved old logic for this out to OnServicesInitialized
                m_ConnectionModeButtons.SetActive(false);
                m_AuthenticationButtons.SetActive(true);
            }
            else
            {
                m_JoinCodeInput.SetActive(false);
                m_AuthenticationButtons.SetActive(false);
                m_ConnectionModeButtons.SetActive(NetworkManager.Singleton && !NetworkManager.Singleton.IsListening);
            }
            if (m_DisconnectClientButton != null)
            {
                if (!NetworkManager.Singleton.IsListening)
                {
                    m_DisconnectClientButton.SetActive(false);
                }
                else
                {
                    m_DisconnectClientButton.SetActive(!NetworkManager.Singleton.IsServer);
                }
            }
        }
    }

    private void OnServicesInitialized()
    {
        if (HasRelaySupport())
        {
            m_JoinCodeInput.SetActive(true);
#if ENABLE_RELAY_SERVICE
            m_ConnectionModeButtons?.SetActive(false || AuthenticationService.Instance.IsSignedIn);
            m_AuthenticationButtons?.SetActive(NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && !AuthenticationService.Instance.IsSignedIn);
#endif
        }
    }

    /// <summary>
    /// Handles starting netcode in server mode
    /// </summary>
    public void OnStartServerButton()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
#if ENABLE_RELAY_SERVICE
            if (HasRelaySupport())
            {
                StartCoroutine(StartRelayServer(StartServer));
            }
            else
#endif
            {
                StartServer();
            }
        }
    }

    private void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(m_ClientSynchronizationMode);
        OnNotifyConnectionEventServer?.Invoke();
        if (m_ConnectionModeButtons != null)
        {
            m_ConnectionModeButtons.SetActive(false);
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    private void OnServerStopped(bool obj)
    {
        if (m_ConnectionModeButtons != null)
        {
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
            m_ConnectionModeButtons.SetActive(true);
        }
    }

#if ENABLE_RELAY_SERVICE
    /// <summary>
    /// Coroutine that handles starting MLAPI in server mode if Relay is enabled
    /// </summary>
    private IEnumerator StartRelayServer(Action postAllocationAction)
    {

        m_ConnectionModeButtons?.SetActive(false);

        var serverRelayUtilityTask = RelayUtility.AllocateRelayServerAndGetJoinCode(m_MaxConnections);
        while (!serverRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }
        if (serverRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to start Relay Server. Server not started. Exception: " + serverRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, key, joinCode) = serverRelayUtilityTask.Result;

        RelayJoinCode = joinCode;

        //When starting a relay server, both instances of connection data are identical.
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(ipv4address, port, allocationIdBytes, key, connectionData);

        postAllocationAction();
    }
#endif

    /// <summary>
    /// Handles starting netcode in host mode
    /// </summary>
    public void OnStartHostButton()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
#if ENABLE_RELAY_SERVICE
            if (HasRelaySupport())
            {
                StartCoroutine(StartRelayServer(StartHost));
            }
            else
#endif
            {
                StartHost();
            }
        }
    }

    private void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(m_ClientSynchronizationMode);
        OnNotifyConnectionEventHost?.Invoke();
        if (m_ConnectionModeButtons != null)
        {
            m_ConnectionModeButtons.SetActive(false);
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    /// <summary>
    /// Handles starting netcode in client mode
    /// </summary>
    public void OnStartClientButton()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
            if (HasRelaySupport())
            {
                StartCoroutine(StartRelayClient());
            }
            else
            {
                StartClient();
            }
        }
    }

    private void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        OnNotifyConnectionEventClient?.Invoke();
        if (m_ConnectionModeButtons)
        {
            m_ConnectionModeButtons.SetActive(false);
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        }

        if (m_DisconnectClientButton)
        {
            m_DisconnectClientButton.SetActive(true);
        }
    }

    private void OnClientStopped(bool obj)
    {
        if (m_ConnectionModeButtons)
        {
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
            m_ConnectionModeButtons.SetActive(true);
        }
    }

    public void DisconnectClient()
    {
        if (NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            m_ConnectionModeButtons.SetActive(true);
        }
    }


    /// <summary>
    /// Coroutine that kicks off Relay SDK calls to join a Relay Server instance with a join code
    /// </summary>
    /// <returns></returns>
    private IEnumerator StartRelayClient()
    {
#if ENABLE_RELAY_SERVICE
        m_ConnectionModeButtons.SetActive(false);

        //assumes that RelayJoinCodeInput populated RelayJoinCode prior to this
        var clientRelayUtilityTask = RelayUtility.JoinRelayServerFromJoinCode(RelayJoinCode);

        while (!clientRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }

        if (clientRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to connect to Relay Server. Exception: " + clientRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, hostConnectionData, key) = clientRelayUtilityTask.Result;

        //When connecting as a client to a relay server, connectionData and hostConnectionData are different.
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(ipv4address, port, allocationIdBytes, key, connectionData, hostConnectionData);

        NetworkManager.Singleton.StartClient();
        OnNotifyConnectionEventClient?.Invoke();
#else
        yield return null;
#endif
    }

    // Will be used for Relay support when it becomes available.
    // TODO: Remove this comment once relay support is available.
#if ENABLE_RELAY_SERVICE
    /// <summary>
    /// Handles authenticating UnityServices, needed for Relay
    /// </summary>
    public async void OnSignIn()
    {
        await UnityServices.InitializeAsync();
        OnServicesInitialized();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"Logging in with PlayerID {AuthenticationService.Instance.PlayerId}");

        if (AuthenticationService.Instance.IsSignedIn)
        {
            m_ConnectionModeButtons.SetActive(true);
            m_AuthenticationButtons.SetActive(false);
        }
    }
#endif

    public void Reset()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
            m_ConnectionModeButtons.SetActive(true);
        }
    }
}
